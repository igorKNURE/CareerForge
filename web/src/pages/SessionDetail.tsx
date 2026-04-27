import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Briefcase, FileText, Send, Sparkles, Wand2 } from 'lucide-react';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';
import { sessionsApi } from '@/api/sessions';
import { resumesApi } from '@/api/resumes';
import { vacanciesApi } from '@/api/vacancies';
import { PageHeader } from '@/components/PageHeader';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Textarea } from '@/components/ui/Input';
import { ScoreBar, ScoreCircle } from '@/components/ui/ScoreBar';
import { Spinner } from '@/components/ui/Spinner';
import { SessionDetailSkeleton } from '@/components/ui/PageSkeletons';
import { ErrorState } from '@/components/ui/ErrorState';
import { Prose } from '@/components/ui/Prose';
import { formatDate, cn } from '@/lib/utils';
import type { TurnResponse } from '@/types/api';

const ROMAN = ['I', 'II', 'III', 'IV', 'V', 'VI', 'VII', 'VIII', 'IX', 'X', 'XI', 'XII'];
const toRoman = (n: number) => ROMAN[n - 1] ?? n.toString();

const isMacPlatform = () =>
  typeof navigator !== 'undefined' && /Mac|iPhone|iPad|iPod/.test(navigator.platform);

/** Live interview session: turn-by-turn question / answer / evaluation flow. */
export const SessionDetailPage = () => {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  const qc = useQueryClient();
  const [freshQuestionId, setFreshQuestionId] = useState<string | null>(null);
  const { data: session, isLoading, refetch } = useQuery({
    queryKey: ['session', id],
    queryFn: () => sessionsApi.get(id!),
    enabled: !!id,
  });

  const resume = useQuery({
    queryKey: ['resume', session?.resumeId],
    queryFn: () => resumesApi.get(session!.resumeId),
    enabled: !!session?.resumeId,
  });
  const vacancy = useQuery({
    queryKey: ['vacancy', session?.jobDescriptionId],
    queryFn: () => vacanciesApi.get(session!.jobDescriptionId),
    enabled: !!session?.jobDescriptionId,
  });

  const generate = useMutation({
    mutationFn: () => sessionsApi.generateQuestion(id!),
    onSuccess: (turn) => {
      setFreshQuestionId(turn.id);
      qc.invalidateQueries({ queryKey: ['session', id] });
    },
    onError: () => toast.error(t('interview.couldNotGenerate')),
  });

  if (!session && isLoading) return <SessionDetailSkeleton />;
  if (!session) return <ErrorState onRetry={() => refetch()} />;
  const lastTurn = session.turns.at(-1);
  const needsQuestion = !lastTurn || lastTurn.evaluation !== null;
  const langLabel = session.language === 'uk' ? t('settings.ukrainian') : t('settings.english');

  const resumeLabel = resume.data?.fileName ?? t('matches.resume');
  const vacancyLabel = vacancy.data?.title
    ? vacancy.data.company
      ? `${vacancy.data.title} · ${vacancy.data.company}`
      : vacancy.data.title
    : t('matches.vacancy');

  return (
    <>
      <PageHeader
        eyebrow={`${langLabel} · ${t('interview.sessionStarted', { date: formatDate(session.createdAt) })}`}
        title={session.name}
        description={t('dashboard.turns', { count: session.turnCount })}
        backTo={{ to: '/sessions', label: t('interview.title') }}
      />

      <div className="mb-8 flex flex-wrap gap-2">
        <Link
          to={`/resumes#${session.resumeId}`}
          className="group inline-flex items-center gap-2 rounded-full bg-white px-3 py-1.5 text-[13px] text-stone-700 ring-1 ring-stone-200 transition-colors hover:bg-stone-50 hover:text-accent-700 hover:ring-accent-200 dark:bg-stone-900/60 dark:text-stone-300 dark:ring-stone-700 dark:hover:text-accent-400 dark:hover:ring-accent-800/60"
        >
          <FileText className="h-3.5 w-3.5 text-stone-400 group-hover:text-accent-600 dark:group-hover:text-accent-400" strokeWidth={1.75} />
          <span className="truncate max-w-[260px]">{resumeLabel}</span>
        </Link>
        <Link
          to={`/vacancies#${session.jobDescriptionId}`}
          className="group inline-flex items-center gap-2 rounded-full bg-white px-3 py-1.5 text-[13px] text-stone-700 ring-1 ring-stone-200 transition-colors hover:bg-stone-50 hover:text-accent-700 hover:ring-accent-200 dark:bg-stone-900/60 dark:text-stone-300 dark:ring-stone-700 dark:hover:text-accent-400 dark:hover:ring-accent-800/60"
        >
          <Briefcase className="h-3.5 w-3.5 text-stone-400 group-hover:text-accent-600 dark:group-hover:text-accent-400" strokeWidth={1.75} />
          <span className="truncate max-w-[320px]">{vacancyLabel}</span>
        </Link>
      </div>

      <div className="space-y-12">
        {session.turns.map((turn, idx) => (
          <TurnBlock
            key={turn.id}
            sessionId={session.id}
            turn={turn}
            isLast={idx === session.turns.length - 1}
            streamQuestion={turn.id === freshQuestionId}
          />
        ))}

        {session.turns.length === 0 ? (
          <div className="flex flex-col items-center py-16 text-center">
            <Sparkles className="h-7 w-7 text-accent-500/70" strokeWidth={1.5} />
            <p className="mt-4 max-w-sm font-display text-lg leading-snug text-stone-600 dark:text-stone-400">
              {t('interview.generateFirst')}
            </p>
            <div className="mt-6">
              <Button onClick={() => generate.mutate()} disabled={generate.isPending}>
                {generate.isPending ? <Spinner /> : <Wand2 className="h-4 w-4" />}
                {t('interview.generateFirstQuestion')}
              </Button>
            </div>
          </div>
        ) : needsQuestion ? (
          <div className="flex justify-center pt-2">
            <Button onClick={() => generate.mutate()} disabled={generate.isPending}>
              {generate.isPending ? <Spinner /> : <Wand2 className="h-4 w-4" />}
              {t('interview.generateNextQuestion')}
            </Button>
          </div>
        ) : null}
      </div>
    </>
  );
};

const TurnBlock = ({
  sessionId,
  turn,
  isLast,
  streamQuestion = false,
}: {
  sessionId: string;
  turn: TurnResponse;
  isLast: boolean;
  streamQuestion?: boolean;
}) => {
  const { t } = useTranslation();
  const qc = useQueryClient();
  // Unsent answer drafts are persisted to localStorage and rehydrated on mount.
  // Cleared on submission and when the turn becomes locked.
  const draftKey = `cf-draft-${sessionId}-${turn.id}`;
  const [draft, setDraft] = useState(() => {
    if (turn.answerText) return turn.answerText;
    if (typeof window === 'undefined') return '';
    return window.localStorage.getItem(draftKey) ?? '';
  });
  const [justSubmitted, setJustSubmitted] = useState(false);
  // Section-by-section reveal of evaluation content following submission.
  // Historical evaluations render fully at the 'done' stage on mount.
  type Stage = 'idle' | 'strengths' | 'weaknesses' | 'recs' | 'done';
  const [stage, setStage] = useState<Stage>('done');
  // Within the 'recs' stage, recommendations themselves are revealed sequentially.
  const [recIndex, setRecIndex] = useState(0);
  const evalCardRef = useRef<HTMLDivElement>(null);
  const questionRef = useRef<HTMLHeadingElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const isLocked = turn.evaluation !== null;

  // When a freshly-generated question appears, scroll it into view and place focus
  // in the answer textarea after the scroll completes.
  useEffect(() => {
    if (streamQuestion && questionRef.current) {
      questionRef.current.scrollIntoView({ behavior: 'smooth', block: 'start' });
      const id = window.setTimeout(() => textareaRef.current?.focus({ preventScroll: true }), 120);
      return () => window.clearTimeout(id);
    }
  }, [streamQuestion]);

  // Persist the in-progress draft so a reload or navigation does not discard work.
  useEffect(() => {
    if (isLocked) return;
    if (draft) window.localStorage.setItem(draftKey, draft);
    else window.localStorage.removeItem(draftKey);
  }, [draft, draftKey, isLocked]);

  const submit = useMutation({
    mutationFn: () => sessionsApi.submitAnswer(sessionId, turn.id, draft),
    onSuccess: () => {
      setJustSubmitted(true);
      setStage('strengths');
      setRecIndex(0);
      window.localStorage.removeItem(draftKey);
      toast.success(t('interview.answerEvaluated'));
      qc.invalidateQueries({ queryKey: ['session', sessionId] });
    },
    onError: () => toast.error(t('interview.evaluationFailed')),
  });

  const canSubmit = draft.length >= 20 && !submit.isPending;
  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if ((e.metaKey || e.ctrlKey) && e.key === 'Enter' && canSubmit) {
      e.preventDefault();
      submit.mutate();
    }
  };

  // Scroll to the evaluation area when it first appears — both when the loading
  // placeholder mounts and again when the evaluation result lands.
  useEffect(() => {
    if ((submit.isPending || (justSubmitted && turn.evaluation)) && evalCardRef.current) {
      evalCardRef.current.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }, [submit.isPending, justSubmitted, turn.evaluation]);

  return (
    <article className={cn('relative', !isLast && 'border-b border-stone-200/70 pb-12 dark:border-stone-800/60')}>
      {/* Eyebrow: round number + meta */}
      <div className="mb-4 flex items-center gap-3 text-[11px] font-medium uppercase tracking-[0.18em] text-stone-500 dark:text-stone-400">
        <span className="font-display text-base font-medium normal-case tracking-tight text-accent-700 dark:text-accent-400">
          {toRoman(turn.turnIndex + 1)}.
        </span>
        <span>{turn.category}</span>
        <span className="text-stone-300 dark:text-stone-600">·</span>
        <span>{turn.difficulty}</span>
        <span className="text-stone-300 dark:text-stone-600">·</span>
        <span>{turn.expectedFormat}</span>
      </div>

      {/* The question — editorial pull-quote */}
      <h2
        ref={questionRef}
        className="scroll-mt-24 font-display text-[26px] font-medium leading-snug tracking-tight text-stone-900 dark:text-stone-50"
      >
        <Prose inline stream={streamQuestion} followScroll={streamQuestion} speed="slow">{turn.questionText}</Prose>
      </h2>
      {turn.rationale && (
        <p className="mt-3 max-w-2xl text-sm italic leading-relaxed text-stone-500 dark:text-stone-400">
          {turn.rationale}
        </p>
      )}

      {/* Answer area */}
      <div className="mt-8">
        <Textarea
          ref={textareaRef}
          rows={6}
          placeholder={t('interview.typeAnswer')}
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={handleKeyDown}
          disabled={isLocked}
          className="min-h-[160px] resize-y bg-white/70 backdrop-blur field-sizing-content dark:bg-stone-900/40"
        />

        {/* While the answer is being graded, an in-place placeholder card renders in
            the position the evaluation will occupy. */}
        {submit.isPending && !turn.evaluation && (
          <Card ref={evalCardRef} className="mt-6 animate-section-enter p-7">
            <div className="flex items-center gap-3 text-[11px] font-medium uppercase tracking-[0.18em] text-accent-700 dark:text-accent-400">
              <span className="relative flex h-2 w-2">
                <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-accent-500/60" />
                <span className="relative inline-flex h-2 w-2 rounded-full bg-accent-600" />
              </span>
              {t('interview.analysing')}
            </div>
          </Card>
        )}

        {turn.evaluation &&
          (turn.evaluation.evaluationFailed ? (
            <Card ref={evalCardRef} className="mt-6 animate-section-enter border-l-2 border-l-amber-500 p-5 dark:border-l-amber-400">
              <div className="text-[10px] font-semibold uppercase tracking-[0.18em] text-amber-700 dark:text-amber-400">
                {t('interview.graderUnavailable')}
              </div>
              <p className="mt-2 text-sm leading-relaxed text-stone-700 dark:text-stone-300">
                {turn.evaluation.weaknesses}
              </p>
            </Card>
          ) : (
            <Card ref={evalCardRef} className="mt-6 animate-section-enter p-7">
              {/* Top: overall score + axis scores */}
              <div className="flex flex-col gap-7 sm:flex-row sm:items-start sm:gap-10">
                <div className="flex shrink-0 justify-center sm:justify-start">
                  <ScoreCircle value={turn.evaluation.overallScore} />
                </div>
                <div className="flex-1 space-y-4 sm:pt-1">
                  <ScoreBar label={t('interview.content')} value={turn.evaluation.contentScore} max={5} />
                  <ScoreBar label={t('interview.structure')} value={turn.evaluation.structureScore} max={5} />
                  <ScoreBar label={t('interview.relevance')} value={turn.evaluation.relevanceScore} max={5} />
                </div>
              </div>

              {/* Evaluation prose. Sections render sequentially after submission;
                  otherwise all sections render together. */}
              <div className="mt-7 space-y-5 border-t border-stone-100 pt-6 text-[14.5px] leading-relaxed dark:border-stone-800/60">
                {turn.evaluation.strengths && (justSubmitted ? stage !== 'idle' : true) && (
                  <div className="animate-section-enter">
                    <div className="mb-1 text-[10px] font-semibold uppercase tracking-[0.18em] text-emerald-700 dark:text-emerald-400">
                      {t('interview.strengths')}
                    </div>
                    <div className="text-stone-700 dark:text-stone-300">
                      <Prose
                        stream={justSubmitted}
                        followScroll={justSubmitted}
                        onComplete={() => setStage((s) => (s === 'strengths' ? 'weaknesses' : s))}
                      >
                        {turn.evaluation.strengths}
                      </Prose>
                    </div>
                  </div>
                )}
                {turn.evaluation.weaknesses && (justSubmitted ? ['weaknesses', 'recs', 'done'].includes(stage) : true) && (
                  <div className="animate-section-enter">
                    <div className="mb-1 text-[10px] font-semibold uppercase tracking-[0.18em] text-amber-700 dark:text-amber-400">
                      {t('interview.weaknesses')}
                    </div>
                    <div className="text-stone-700 dark:text-stone-300">
                      <Prose
                        stream={justSubmitted}
                        followScroll={justSubmitted}
                        onComplete={() => setStage((s) => (s === 'weaknesses' ? 'recs' : s))}
                      >
                        {turn.evaluation.weaknesses}
                      </Prose>
                    </div>
                  </div>
                )}
                {(turn.evaluation.recommendations ?? []).length > 0 && (justSubmitted ? ['recs', 'done'].includes(stage) : true) && (
                  <div className="animate-section-enter">
                    <div className="mb-2 text-[10px] font-semibold uppercase tracking-[0.18em] text-stone-500 dark:text-stone-400">
                      {t('interview.recommendations')}
                    </div>
                    <ul className="space-y-2">
                      {(() => {
                        const recs = turn.evaluation.recommendations ?? [];
                        const visible = justSubmitted && stage === 'recs' ? recs.slice(0, recIndex + 1) : recs;
                        return visible.map((r, i) => {
                          const isLast = i === recs.length - 1;
                          return (
                            <li key={i} className="flex animate-section-enter gap-3 text-stone-700 dark:text-stone-300">
                              <span className="mt-[7px] block h-1 w-1 shrink-0 rounded-full bg-accent-500" />
                              <Prose
                                inline
                                stream={justSubmitted}
                                followScroll={justSubmitted}
                                onComplete={() => {
                                  if (isLast) setStage('done');
                                  else setRecIndex((idx) => Math.max(idx, i + 1));
                                }}
                              >
                                {r}
                              </Prose>
                            </li>
                          );
                        });
                      })()}
                    </ul>
                  </div>
                )}
              </div>
            </Card>
          ))}

        {!isLocked && !submit.isPending && (
          <div className="mt-4 flex items-center justify-end gap-3">
            <span className="hidden text-[11px] text-stone-400 sm:inline-flex sm:items-center sm:gap-1.5 dark:text-stone-500">
              <kbd className="rounded border border-stone-200 bg-stone-50 px-1.5 py-0.5 font-sans text-[10px] tracking-wide text-stone-500 dark:border-stone-700 dark:bg-stone-800/60 dark:text-stone-400">
                {isMacPlatform() ? '⌘' : 'Ctrl'} ↵
              </kbd>
              {t('interview.submitHint')}
            </span>
            <Button onClick={() => submit.mutate()} disabled={!canSubmit}>
              <Send className="h-4 w-4" />
              {t('interview.submitForGrading')}
            </Button>
          </div>
        )}
      </div>
    </article>
  );
};
