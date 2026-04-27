import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { ArrowRight, Briefcase, FileText, MessagesSquare, Sparkles, Upload } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { resumesApi } from '@/api/resumes';
import { vacanciesApi } from '@/api/vacancies';
import { matchesApi } from '@/api/matches';
import { sessionsApi } from '@/api/sessions';
import { PageHeader } from '@/components/PageHeader';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { ScoreCircle } from '@/components/ui/ScoreBar';
import { DashboardSkeleton } from '@/components/ui/PageSkeletons';
import { ErrorState } from '@/components/ui/ErrorState';
import { useAuthStore } from '@/stores/auth.store';
import { formatDate, scoreTone } from '@/lib/utils';
import { useQueryClient } from '@tanstack/react-query';

/** Authenticated landing page: greeting, next-best-action card, and quick links. */
export const DashboardPage = () => {
  const { t } = useTranslation();
  const email = useAuthStore((s) => s.email);
  const displayName = useAuthStore((s) => s.displayName);
  const handle = displayName || (email ?? '').split('@')[0] || t('dashboard.friend');

  const qc = useQueryClient();
  const resumes = useQuery({ queryKey: ['resumes'], queryFn: resumesApi.list });
  const vacancies = useQuery({ queryKey: ['vacancies'], queryFn: vacanciesApi.list });
  const matches = useQuery({ queryKey: ['matches'], queryFn: matchesApi.list });
  const sessions = useQuery({ queryKey: ['sessions'], queryFn: sessionsApi.list });

  // Skeleton only when nothing has been hydrated yet — partial cache renders the page.
  const anyDataMissing = !resumes.data || !vacancies.data || !matches.data || !sessions.data;
  const anyLoading = resumes.isLoading || vacancies.isLoading || matches.isLoading || sessions.isLoading;
  if (anyLoading && anyDataMissing) {
    return <DashboardSkeleton />;
  }

  const allFailed = !resumes.data && !vacancies.data && !matches.data && !sessions.data;
  if (allFailed) {
    return (
      <ErrorState
        onRetry={() => {
          qc.refetchQueries({ queryKey: ['resumes'] });
          qc.refetchQueries({ queryKey: ['vacancies'] });
          qc.refetchQueries({ queryKey: ['matches'] });
          qc.refetchQueries({ queryKey: ['sessions'] });
        }}
      />
    );
  }

  const resumeCount = resumes.data?.length ?? 0;
  const vacancyCount = vacancies.data?.length ?? 0;
  const matchCount = matches.data?.length ?? 0;
  const sessionCount = sessions.data?.length ?? 0;

  const featuredMatch = (matches.data ?? [])[0];
  const recentMatches = (matches.data ?? []).slice(0, 4);
  const recentSessions = (sessions.data ?? []).slice(0, 4);

  return (
    <>
      <PageHeader
        eyebrow={t('dashboard.welcomeBack')}
        title={handle}
        description={t('dashboard.subtitleEditorial')}
      />

      <NextStep
        resumeCount={resumeCount}
        vacancyCount={vacancyCount}
        matchCount={matchCount}
        sessionCount={sessionCount}
      />

      {featuredMatch && (
        <section className="mt-8">
          <h2 className="mb-3 text-[11px] font-semibold uppercase tracking-[0.18em] text-stone-500 dark:text-stone-400">
            {t('dashboard.latestMatch')}
          </h2>
          <Link to={`/matches/${featuredMatch.id}`} className="block">
            <Card className="flex items-center gap-7 p-7 transition-all hover:ring-accent-200 dark:hover:ring-accent-800/60">
              <div className="shrink-0">
                <ScoreCircle value={featuredMatch.overallScore} />
              </div>
              <div className="flex-1 min-w-0">
                <div className="text-[11px] font-medium uppercase tracking-[0.14em] text-stone-400 dark:text-stone-500">
                  {formatDate(featuredMatch.createdAt)}
                </div>
                <div className="mt-1 font-display text-xl font-medium leading-snug text-stone-900 dark:text-stone-50">
                  {summariseScore(featuredMatch.overallScore, t)}
                </div>
                <div className="mt-2 flex items-center gap-3 text-sm text-stone-500 dark:text-stone-400">
                  <span>{t('dashboard.openMatch')} →</span>
                  <DashSeverityCounts
                    critical={featuredMatch.criticalCount}
                    warning={featuredMatch.warningCount}
                    info={featuredMatch.infoCount}
                  />
                </div>
              </div>
            </Card>
          </Link>
        </section>
      )}

      <div className="mt-10 grid gap-8 md:grid-cols-2">
        <section>
          <div className="mb-3 flex items-baseline justify-between">
            <h2 className="text-[11px] font-semibold uppercase tracking-[0.18em] text-stone-500 dark:text-stone-400">
              {t('dashboard.recentMatches')}
            </h2>
            {matchCount > 0 && (
              <Link to="/matches" className="text-[12px] font-medium text-stone-500 hover:text-accent-700 dark:text-stone-400 dark:hover:text-accent-400">
                {t('dashboard.viewAll')} →
              </Link>
            )}
          </div>
          {recentMatches.length === 0 ? (
            <EmptyHint icon={Sparkles} text={t('dashboard.noMatchesYet')} />
          ) : (
            <ul className="divide-y divide-stone-100 dark:divide-stone-800/60">
              {recentMatches.map((m) => (
                <li key={m.id}>
                  <Link
                    to={`/matches/${m.id}`}
                    className="flex items-baseline justify-between gap-3 py-3 text-sm transition-colors hover:text-accent-700 dark:hover:text-accent-400"
                  >
                    <div className="flex items-baseline gap-3 min-w-0">
                      <span className={`font-display text-lg font-medium tabular-nums ${scoreTone(m.overallScore)}`}>
                        {m.overallScore.toFixed(0)}
                      </span>
                      <span className="truncate text-stone-500 dark:text-stone-400">
                        {formatDate(m.createdAt)}
                      </span>
                      <DashSeverityCounts critical={m.criticalCount} warning={m.warningCount} info={m.infoCount} />
                    </div>
                    <ArrowRight className="h-3.5 w-3.5 shrink-0 text-stone-300 dark:text-stone-600" />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section>
          <div className="mb-3 flex items-baseline justify-between">
            <h2 className="text-[11px] font-semibold uppercase tracking-[0.18em] text-stone-500 dark:text-stone-400">
              {t('dashboard.recentInterviews')}
            </h2>
            {sessionCount > 0 && (
              <Link to="/sessions" className="text-[12px] font-medium text-stone-500 hover:text-accent-700 dark:text-stone-400 dark:hover:text-accent-400">
                {t('dashboard.viewAll')} →
              </Link>
            )}
          </div>
          {recentSessions.length === 0 ? (
            <EmptyHint icon={MessagesSquare} text={t('dashboard.noSessionsYet')} />
          ) : (
            <ul className="divide-y divide-stone-100 dark:divide-stone-800/60">
              {recentSessions.map((s) => (
                <li key={s.id}>
                  <Link
                    to={`/sessions/${s.id}`}
                    className="flex items-baseline justify-between gap-3 py-3 text-sm transition-colors hover:text-accent-700 dark:hover:text-accent-400"
                  >
                    <div className="min-w-0">
                      <div className="truncate font-medium text-stone-900 dark:text-stone-100">
                        {s.name}
                      </div>
                      <div className="text-[12px] text-stone-500 dark:text-stone-400">
                        {t('dashboard.turns', { count: s.turnCount })} · {formatDate(s.updatedAt)}
                      </div>
                    </div>
                    <ArrowRight className="h-3.5 w-3.5 shrink-0 text-stone-300 dark:text-stone-600" />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </>
  );
};

const summariseScore = (score: number, t: (k: string) => string) => {
  if (score >= 80) return t('dashboard.scoreSummary.strong');
  if (score >= 60) return t('dashboard.scoreSummary.solid');
  if (score >= 40) return t('dashboard.scoreSummary.partial');
  return t('dashboard.scoreSummary.weak');
};

interface NextStepProps {
  resumeCount: number;
  vacancyCount: number;
  matchCount: number;
  sessionCount: number;
}

const NextStep = ({ resumeCount, vacancyCount, matchCount, sessionCount }: NextStepProps) => {
  const { t } = useTranslation();

  // Pick the most actionable next step based on what's missing.
  if (resumeCount === 0) {
    return (
      <CtaCard
        icon={FileText}
        title={t('dashboard.cta.firstResume.title')}
        body={t('dashboard.cta.firstResume.body')}
        actionLabel={t('dashboard.cta.firstResume.action')}
        actionIcon={Upload}
        to="/resumes"
      />
    );
  }
  if (vacancyCount === 0) {
    return (
      <CtaCard
        icon={Briefcase}
        title={t('dashboard.cta.firstVacancy.title')}
        body={t('dashboard.cta.firstVacancy.body')}
        actionLabel={t('dashboard.cta.firstVacancy.action')}
        actionIcon={Briefcase}
        to="/vacancies"
      />
    );
  }
  if (matchCount === 0) {
    return (
      <CtaCard
        icon={Sparkles}
        title={t('dashboard.cta.firstMatch.title')}
        body={t('dashboard.cta.firstMatch.body')}
        actionLabel={t('dashboard.cta.firstMatch.action')}
        actionIcon={Sparkles}
        to="/matches"
      />
    );
  }
  if (sessionCount === 0) {
    return (
      <CtaCard
        icon={MessagesSquare}
        title={t('dashboard.cta.firstSession.title')}
        body={t('dashboard.cta.firstSession.body')}
        actionLabel={t('dashboard.cta.firstSession.action')}
        actionIcon={MessagesSquare}
        to="/sessions"
      />
    );
  }
  return (
    <CtaCard
      icon={Sparkles}
      title={t('dashboard.cta.continue.title')}
      body={t('dashboard.cta.continue.body')}
      actionLabel={t('dashboard.cta.continue.action')}
      actionIcon={ArrowRight}
      to="/sessions"
    />
  );
};

interface CtaCardProps {
  icon: typeof Sparkles;
  title: string;
  body: string;
  actionLabel: string;
  actionIcon: typeof Sparkles;
  to: string;
}

const CtaCard = ({ icon: Icon, title, body, actionLabel, actionIcon: ActionIcon, to }: CtaCardProps) => (
  <Card className="flex flex-col gap-5 p-7 sm:flex-row sm:items-center sm:justify-between">
    <div className="flex items-start gap-4 min-w-0">
      <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-accent-50 text-accent-700 dark:bg-accent-950/40 dark:text-accent-400">
        <Icon className="h-5 w-5" strokeWidth={1.75} />
      </div>
      <div className="min-w-0">
        <h2 className="font-display text-lg font-medium leading-snug text-stone-900 dark:text-stone-50">
          {title}
        </h2>
        <p className="mt-1 text-sm text-stone-500 dark:text-stone-400">{body}</p>
      </div>
    </div>
    <Link to={to} className="shrink-0">
      <Button>
        <ActionIcon className="h-4 w-4" /> {actionLabel}
      </Button>
    </Link>
  </Card>
);

const EmptyHint = ({ icon: Icon, text }: { icon: typeof Sparkles; text: string }) => (
  <div className="flex items-center gap-3 py-6 text-sm text-stone-400 dark:text-stone-500">
    <Icon className="h-4 w-4" strokeWidth={1.5} />
    {text}
  </div>
);

const DashSeverityCounts = ({
  critical, warning, info,
}: {
  critical: number;
  warning: number;
  info: number;
}) => {
  if (critical + warning + info === 0) return null;
  return (
    <span className="inline-flex items-center gap-2 text-[11.5px] font-medium">
      {critical > 0 && (
        <span className="inline-flex items-center gap-1 text-red-700 dark:text-red-400">
          <span className="h-1.5 w-1.5 rounded-full bg-red-500" />
          {critical}
        </span>
      )}
      {warning > 0 && (
        <span className="inline-flex items-center gap-1 text-amber-700 dark:text-amber-400">
          <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
          {warning}
        </span>
      )}
      {info > 0 && (
        <span className="inline-flex items-center gap-1 text-sky-700 dark:text-sky-400">
          <span className="h-1.5 w-1.5 rounded-full bg-sky-500" />
          {info}
        </span>
      )}
    </span>
  );
};
