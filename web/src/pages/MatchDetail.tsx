import { useEffect, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertOctagon, AlertTriangle, Briefcase, Clipboard, FileText, Info, Printer, Quote, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';
import { matchesApi } from '@/api/matches';
import { resumesApi } from '@/api/resumes';
import { vacanciesApi } from '@/api/vacancies';
import { PageHeader } from '@/components/PageHeader';
import { Card } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { ScoreBar, ScoreCircle } from '@/components/ui/ScoreBar';
import { Spinner } from '@/components/ui/Spinner';
import { MatchDetailSkeleton } from '@/components/ui/PageSkeletons';
import { ErrorState } from '@/components/ui/ErrorState';
import { Prose } from '@/components/ui/Prose';
import { formatDate } from '@/lib/utils';
import { matchReportToMarkdown } from '@/lib/matchMarkdown';
import type { FindingSeverity, MatchFinding } from '@/types/api';

/** Match detail: scores, skill coverage, findings, improvement summary, and Markdown export. */
export const MatchDetailPage = () => {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  const qc = useQueryClient();
  const location = useLocation();
  const navigate = useNavigate();
  // Section-by-section reveal of report content following a re-score or a freshly
  // created match. The 'idle' and 'done' stages render content immediately.
  // The justScored flag is seeded from navigation state on entry from the create flow.
  type Stage = 'idle' | 'scores' | 'summary' | 'skills' | 'findings' | 'done';
  const navJustScored = (location.state as { justScored?: boolean } | null)?.justScored === true;
  const [justScored, setJustScored] = useState(navJustScored);
  const [stage, setStage] = useState<Stage>(navJustScored ? 'scores' : 'done');
  const [findingIndex, setFindingIndex] = useState(0);
  // Incremented per re-score so keyed children remount and replay the reveal sequence.
  const [rerunSeq, setRerunSeq] = useState(0);

  // Consume the navigation state after first render so back/forward navigation does
  // not replay the reveal animation.
  useEffect(() => {
    if (navJustScored) navigate(location.pathname, { replace: true, state: {} });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  const { data, isLoading, refetch } = useQuery({
    queryKey: ['match', id],
    queryFn: () => matchesApi.get(id!),
    enabled: !!id,
  });

  const resume = useQuery({
    queryKey: ['resume', data?.resumeId],
    queryFn: () => resumesApi.get(data!.resumeId),
    enabled: !!data?.resumeId,
  });
  const vacancy = useQuery({
    queryKey: ['vacancy', data?.jobDescriptionId],
    queryFn: () => vacanciesApi.get(data!.jobDescriptionId),
    enabled: !!data?.jobDescriptionId,
  });

  const rerun = useMutation({
    mutationFn: () => matchesApi.rerun(id!),
    onSuccess: (fresh) => {
      qc.setQueryData(['match', id], fresh);
      qc.invalidateQueries({ queryKey: ['matches'] });
      setJustScored(true);
      setStage('scores');
      setFindingIndex(0);
      setRerunSeq((s) => s + 1);
      // Restore the scroll position so the reveal sequence is visible. Re-score is
      // commonly invoked after scrolling into the findings list.
      window.scrollTo({ top: 0, behavior: 'smooth' });
      toast.success(t('matches.rescored'));
    },
    onError: () => toast.error(t('matches.couldNotRescore')),
  });

  // Stages without streaming text content (scores, skills) auto-advance after a short
  // delay. Empty stages are skipped so the reveal sequence cannot stall on missing data.
  const hasSummary = !!data?.improvementSummary;
  const findingsCount = data?.findings?.length ?? 0;
  useEffect(() => {
    // Defer the reveal sequence until query data is available, otherwise the timers
    // would elapse while the skeleton is still rendered.
    if (!justScored || !data) return;
    if (stage === 'scores') {
      const id = window.setTimeout(() => setStage('summary'), 200);
      return () => window.clearTimeout(id);
    }
    if (stage === 'summary' && !hasSummary) {
      setStage('skills');
      return;
    }
    if (stage === 'skills') {
      const id = window.setTimeout(() => setStage('findings'), 250);
      return () => window.clearTimeout(id);
    }
    if (stage === 'findings' && findingsCount === 0) {
      setStage('done');
      return;
    }
  }, [stage, justScored, data, hasSummary, findingsCount]);

  if (!data && isLoading) return <MatchDetailSkeleton />;
  if (!data) return <ErrorState onRetry={() => refetch()} />;

  const findings = [...(data.findings ?? [])].sort((a, b) => severityRank(a.severity) - severityRank(b.severity));

  const resumeLabel = resume.data?.fileName ?? t('matches.resume');
  const vacancyLabel = vacancy.data?.title
    ? vacancy.data.company
      ? `${vacancy.data.title} · ${vacancy.data.company}`
      : vacancy.data.title
    : t('matches.vacancy');

  return (
    <>
      <PageHeader
        title={t('matches.matchReport')}
        description={formatDate(data.createdAt)}
        backTo={{ to: '/matches', label: t('matches.title') }}
        actions={
          <div className="flex items-center gap-2 print:hidden">
            <Button
              variant="secondary"
              onClick={async () => {
                const md = matchReportToMarkdown(data, resume.data, vacancy.data, {
                  labels: {
                    matchReport: t('matches.matchReport'),
                    resume: t('matches.resume'),
                    vacancy: t('matches.vacancy'),
                    overall: t('matches.overall'),
                    skillCoverage: t('matches.skillCoverage'),
                    semanticSimilarity: t('matches.semanticSimilarity'),
                    experienceFit: t('matches.experienceFit'),
                    improvementSummary: t('matches.improvementSummary'),
                    matchedMustHaves: t('matches.matchedMustHaves'),
                    missingMustHaves: t('matches.missingMustHaves'),
                    matchedNiceToHaves: t('matches.matchedNiceToHaves'),
                    findings: t('matches.findings'),
                    recommendation: t('matches.recommendation'),
                    none: t('common.none'),
                  },
                });
                try {
                  await navigator.clipboard.writeText(md);
                  toast.success(t('matches.copiedMarkdown'));
                } catch {
                  toast.error(t('matches.copyFailed'));
                }
              }}
              title={t('matches.copyMarkdown')}
              aria-label={t('matches.copyMarkdown')}
            >
              <Clipboard className="h-4 w-4" /> {t('common.copy')}
            </Button>
            <Button
              variant="secondary"
              onClick={() => window.print()}
              title={t('matches.printPdf')}
              aria-label={t('matches.printPdf')}
            >
              <Printer className="h-4 w-4" /> {t('common.print')}
            </Button>
            <Button variant="secondary" onClick={() => rerun.mutate()} disabled={rerun.isPending}>
              {rerun.isPending ? <Spinner /> : <RefreshCw className="h-4 w-4" />}
              {t('common.rerun')}
            </Button>
          </div>
        }
      />

      {/* Source resume + vacancy — clickable */}
      <div className="mb-6 flex flex-wrap gap-2">
        <Link
          to={`/resumes#${data.resumeId}`}
          className="group inline-flex items-center gap-2 rounded-full bg-white px-3 py-1.5 text-[13px] text-stone-700 ring-1 ring-stone-200 transition-colors hover:bg-stone-50 hover:text-accent-700 hover:ring-accent-200 dark:bg-stone-900/60 dark:text-stone-300 dark:ring-stone-700 dark:hover:text-accent-400 dark:hover:ring-accent-800/60"
        >
          <FileText className="h-3.5 w-3.5 text-stone-400 group-hover:text-accent-600 dark:group-hover:text-accent-400" strokeWidth={1.75} />
          <span className="truncate max-w-[260px]">{resumeLabel}</span>
        </Link>
        <Link
          to={`/vacancies#${data.jobDescriptionId}`}
          className="group inline-flex items-center gap-2 rounded-full bg-white px-3 py-1.5 text-[13px] text-stone-700 ring-1 ring-stone-200 transition-colors hover:bg-stone-50 hover:text-accent-700 hover:ring-accent-200 dark:bg-stone-900/60 dark:text-stone-300 dark:ring-stone-700 dark:hover:text-accent-400 dark:hover:ring-accent-800/60"
        >
          <Briefcase className="h-3.5 w-3.5 text-stone-400 group-hover:text-accent-600 dark:group-hover:text-accent-400" strokeWidth={1.75} />
          <span className="truncate max-w-[320px]">{vacancyLabel}</span>
        </Link>
      </div>

      {(() => {
        // Visibility gates: when justScored is false, all sections render immediately;
        // during the reveal sequence each section waits for its stage.
        const showSummary = !justScored || ['summary', 'skills', 'findings', 'done'].includes(stage);
        const showSkills = !justScored || ['skills', 'findings', 'done'].includes(stage);
        const showFindings = !justScored || ['findings', 'done'].includes(stage);
        const visibleFindings = !justScored || stage === 'done'
          ? findings
          : stage === 'findings'
            ? findings.slice(0, findingIndex + 1)
            : [];
        return (
          <>
            <div
              key={`scores-${rerunSeq}`}
              className="grid animate-section-enter gap-6 md:grid-cols-[auto_1fr]"
            >
              <Card className="flex flex-col items-center justify-center p-8">
                <ScoreCircle value={data.overallScore} />
              </Card>
              <Card className="space-y-4 p-5">
                <ScoreBar label={t('matches.skillCoverage')} value={data.skillCoverageScore} />
                <ScoreBar label={t('matches.semanticSimilarity')} value={data.semanticSimilarityScore} />
                <ScoreBar label={t('matches.experienceFit')} value={data.experienceFitScore} />
              </Card>
            </div>

            {data.improvementSummary && showSummary && (
              <Card className="mt-6 animate-section-enter border-accent-200 bg-accent-50/50 p-5 dark:border-accent-900 dark:bg-accent-950/30">
                <div className="text-xs font-semibold uppercase tracking-wide text-accent-700 dark:text-accent-300">
                  {t('matches.improvementSummary')}
                </div>
                <div className="mt-2 text-sm leading-relaxed text-zinc-800 dark:text-zinc-200">
                  <Prose
                    key={`summary-${rerunSeq}`}
                    stream={justScored}
                    speed="fast"
                    onComplete={() => setStage((s) => (s === 'summary' ? 'skills' : s))}
                  >
                    {data.improvementSummary}
                  </Prose>
                </div>
              </Card>
            )}

            {showSkills && (
              <div
                key={`skills-${rerunSeq}`}
                className="mt-6 grid animate-section-enter gap-4 md:grid-cols-2"
              >
                <SkillSection title={t('matches.matchedMustHaves')} tone="success" skills={data.matchedMustHaveSkills ?? []} />
                <SkillSection title={t('matches.missingMustHaves')} tone="danger" skills={data.missingMustHaveSkills ?? []} />
                <SkillSection
                  title={t('matches.matchedNiceToHaves')}
                  tone="accent"
                  skills={data.matchedNiceToHaveSkills ?? []}
                  className="md:col-span-2"
                />
              </div>
            )}

            {findings.length > 0 && showFindings && (
              <section className="mt-8 animate-section-enter">
                <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                  {t('matches.findings')}
                </h2>
                <div className="space-y-3">
                  {visibleFindings.map((f, i) => (
                    <FindingCard
                      key={`${rerunSeq}-${i}`}
                      finding={f}
                      stream={justScored}
                      onComplete={() => {
                        if (i === findings.length - 1) setStage('done');
                        else setFindingIndex((idx) => Math.max(idx, i + 1));
                      }}
                    />
                  ))}
                </div>
              </section>
            )}
          </>
        );
      })()}
    </>
  );
};

const severityRank = (s: FindingSeverity) => (s === 'Critical' ? 0 : s === 'Warning' ? 1 : 2);

const SeverityIcon = ({ severity, className }: { severity: FindingSeverity; className: string }) => {
  if (severity === 'Critical') return <AlertOctagon className={className} />;
  if (severity === 'Warning') return <AlertTriangle className={className} />;
  return <Info className={className} />;
};

type SeverityStyle = {
  card: string;
  icon: string;
  badge: 'danger' | 'warning' | 'info';
};

const severityTone = (s: FindingSeverity): SeverityStyle =>
  s === 'Critical'
    ? { card: 'border-red-200 bg-red-50 dark:border-red-900/60 dark:bg-red-950/30', icon: 'text-red-600 dark:text-red-400', badge: 'danger' }
    : s === 'Warning'
    ? { card: 'border-amber-200 bg-amber-50 dark:border-amber-900/60 dark:bg-amber-950/30', icon: 'text-amber-600 dark:text-amber-400', badge: 'warning' }
    : { card: 'border-sky-200 bg-sky-50 dark:border-sky-900/60 dark:bg-sky-950/30', icon: 'text-sky-600 dark:text-sky-400', badge: 'info' };

const FindingCard = ({
  finding, stream = false, onComplete,
}: {
  finding: MatchFinding;
  stream?: boolean;
  onComplete?: () => void;
}) => {
  const { t } = useTranslation();
  const tone = severityTone(finding.severity);
  // While streaming, the description renders first, followed by the recommendation.
  // The parent keys this card on rerunSeq, ensuring fresh state per re-score.
  type FStage = 'desc' | 'rec' | 'done';
  const [fstage, setFStage] = useState<FStage>(stream ? 'desc' : 'done');
  const showRec = !stream || ['rec', 'done'].includes(fstage);
  return (
    <Card className={`${tone.card} animate-section-enter p-5`}>
      <div className="flex items-start gap-3">
        <SeverityIcon severity={finding.severity} className={`h-5 w-5 shrink-0 ${tone.icon}`} />
        <div className="min-w-0 flex-1 space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="text-sm font-semibold text-zinc-900 dark:text-zinc-50">{finding.title}</h3>
            <Badge tone={tone.badge}>{finding.severity}</Badge>
            <Badge>{finding.category}</Badge>
          </div>
          <div className="text-sm leading-relaxed text-zinc-700 dark:text-zinc-300">
            <Prose
              stream={stream}
              speed="fast"
              onComplete={() => {
                if (finding.recommendation) setFStage('rec');
                else { setFStage('done'); onComplete?.(); }
              }}
            >
              {finding.description}
            </Prose>
          </div>
          {finding.recommendation && showRec && (
            <div className="animate-section-enter rounded-md bg-white/70 px-3 py-2 text-sm text-zinc-800 dark:bg-zinc-900/70 dark:text-zinc-200">
              <span className="font-medium text-zinc-900 dark:text-zinc-100">{t('matches.recommendation')}: </span>
              <Prose
                inline
                stream={stream}
                speed="fast"
                onComplete={() => { setFStage('done'); onComplete?.(); }}
              >
                {finding.recommendation}
              </Prose>
            </div>
          )}
          {finding.resumeExcerpt && (!stream || fstage === 'done') && (
            <div className="flex animate-section-enter gap-2 border-l-2 border-zinc-300 pl-3 text-xs text-zinc-600 dark:border-zinc-600 dark:text-zinc-400">
              <Quote className="mt-0.5 h-3 w-3 shrink-0" />
              <span className="italic">{finding.resumeExcerpt}</span>
            </div>
          )}
        </div>
      </div>
    </Card>
  );
};

const SkillSection = ({
  title, tone, skills, className,
}: {
  title: string;
  tone: 'success' | 'danger' | 'accent';
  skills: string[];
  className?: string;
}) => {
  const { t } = useTranslation();
  return (
    <Card className={`p-5 ${className ?? ''}`}>
      <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
        {title}
      </div>
      {skills.length === 0 ? (
        <p className="text-sm text-zinc-400 dark:text-zinc-600">{t('common.none')}</p>
      ) : (
        <div className="flex flex-wrap gap-1.5">
          {skills.map((s) => <Badge key={s} tone={tone}>{s}</Badge>)}
        </div>
      )}
    </Card>
  );
};
