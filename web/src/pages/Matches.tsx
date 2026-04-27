import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Sparkles, Plus, ArrowRight, Trash2, X, FileText, Briefcase } from 'lucide-react';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';
import { matchesApi } from '@/api/matches';
import { confirm } from '@/stores/confirm.store';
import { resumesApi } from '@/api/resumes';
import { vacanciesApi } from '@/api/vacancies';
import { PageHeader } from '@/components/PageHeader';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Label, Select } from '@/components/ui/Input';
import { Spinner } from '@/components/ui/Spinner';
import { ListPageSkeleton } from '@/components/ui/PageSkeletons';
import { EmptyState } from '@/components/ui/EmptyState';
import { ErrorState } from '@/components/ui/ErrorState';
import { formatDate, scoreTone } from '@/lib/utils';

/** Match-reports list with a "create new match" form prefillable via query params. */
export const MatchesPage = () => {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [showForm, setShowForm] = useState(false);
  const [resumeId, setResumeId] = useState('');
  const [vacancyId, setVacancyId] = useState('');

  const matches = useQuery({ queryKey: ['matches'], queryFn: matchesApi.list });
  const resumes = useQuery({ queryKey: ['resumes'], queryFn: resumesApi.list });
  const vacancies = useQuery({ queryKey: ['vacancies'], queryFn: vacanciesApi.list });

  // Pre-fill the form when arriving from a Resume/Vacancy "Score …" shortcut.
  useEffect(() => {
    const r = searchParams.get('resumeId');
    const v = searchParams.get('vacancyId');
    if (!r && !v) return;
    if (r) setResumeId(r);
    if (v) setVacancyId(v);
    setShowForm(true);
    // Clear the params so refresh doesn't keep re-opening the form.
    setSearchParams({}, { replace: true });
  }, [searchParams, setSearchParams]);

  const create = useMutation({
    mutationFn: () => matchesApi.create(resumeId, vacancyId),
    onSuccess: (data) => {
      toast.success(t('matches.scored'));
      qc.invalidateQueries({ queryKey: ['matches'] });
      // Hand the cascade flag to MatchDetail so a brand-new match plays the same
      // section-by-section reveal that re-scoring does.
      navigate(`/matches/${data.id}`, { state: { justScored: true } });
    },
    onError: () => toast.error(t('matches.couldNotScore')),
  });

  const remove = useMutation({
    mutationFn: (id: string) => matchesApi.remove(id),
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: ['matches'] });
      const prev = qc.getQueryData<typeof items>(['matches']);
      qc.setQueryData<typeof items>(['matches'], (old) => (old ?? []).filter((m) => m.id !== id));
      return { prev };
    },
    onError: (_e, _id, ctx) => {
      if (ctx?.prev) qc.setQueryData(['matches'], ctx.prev);
      toast.error(t('matches.couldNotScore'));
    },
    onSettled: () => qc.invalidateQueries({ queryKey: ['matches'] }),
  });

  const handleDelete = async (id: string) => {
    const ok = await confirm({
      title: t('matches.confirmDeleteReportTitle'),
      body: t('matches.confirmDeleteReportBody'),
      confirmLabel: t('common.delete'),
      destructive: true,
    });
    if (ok) remove.mutate(id);
  };

  const resumesById = useMemo(
    () => Object.fromEntries((resumes.data ?? []).map((r) => [r.id, r])),
    [resumes.data],
  );
  const vacanciesById = useMemo(
    () => Object.fromEntries((vacancies.data ?? []).map((v) => [v.id, v])),
    [vacancies.data],
  );

  // Render the skeleton only while no cached snapshot is available for any of the three
  // queries. When at least one list is hydrated, render the page and tolerate missing
  // labels in the lookup maps.
  if ((matches.isLoading && !matches.data) || (resumes.isLoading && !resumes.data) || (vacancies.isLoading && !vacancies.data)) {
    return <ListPageSkeleton />;
  }
  if (matches.isError && !matches.data) return <ErrorState onRetry={() => matches.refetch()} />;

  const canScore = resumeId && vacancyId && !create.isPending;
  const items = matches.data ?? [];

  return (
    <>
      <PageHeader
        title={t('matches.title')}
        description={t('matches.subtitle')}
        actions={
          items.length > 0 || showForm ? (
            <Button
              variant={showForm ? 'secondary' : 'primary'}
              onClick={() => setShowForm((s) => !s)}
            >
              {showForm ? <X className="h-4 w-4" /> : <Plus className="h-4 w-4" />}
              {showForm ? t('common.cancel') : t('matches.newMatch')}
            </Button>
          ) : null
        }
      />

      {showForm && (
        <Card className="mb-4 p-5">
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <Label htmlFor="r">{t('matches.resume')}</Label>
              <Select id="r" value={resumeId} onChange={(e) => setResumeId(e.target.value)}>
                <option value="">{t('matches.selectResume')}</option>
                {resumes.data?.filter((r) => r.status === 'Done').map((r) => (
                  <option key={r.id} value={r.id}>{r.fileName}</option>
                ))}
              </Select>
            </div>
            <div>
              <Label htmlFor="v">{t('matches.vacancy')}</Label>
              <Select id="v" value={vacancyId} onChange={(e) => setVacancyId(e.target.value)}>
                <option value="">{t('matches.selectVacancy')}</option>
                {vacancies.data?.filter((v) => v.status === 'Done').map((v) => (
                  <option key={v.id} value={v.id}>{v.title}{v.company ? ` · ${v.company}` : ''}</option>
                ))}
              </Select>
            </div>
          </div>
          <div className="mt-4 flex justify-end">
            <Button onClick={() => create.mutate()} disabled={!canScore}>
              {create.isPending ? <Spinner /> : t('matches.scoreMatch')}
            </Button>
          </div>
        </Card>
      )}

      {items.length === 0 ? (
        <EmptyState
          icon={Sparkles}
          title={t('matches.empty.title')}
          body={t('matches.empty.body')}
          action={
            <Button onClick={() => setShowForm(true)}>
              <Plus className="h-4 w-4" /> {t('matches.newMatch')}
            </Button>
          }
        />
      ) : (
        <div className="space-y-2">
          {items.map((m, i) => {
            const resumeLabel = resumesById[m.resumeId]?.fileName ?? '—';
            const vacancy = vacanciesById[m.jobDescriptionId];
            const vacancyLabel = vacancy
              ? vacancy.company
                ? `${vacancy.title} · ${vacancy.company}`
                : vacancy.title
              : '—';
            return (
              <Card
                key={m.id}
                style={{ animationDelay: `${Math.min(i, 12) * 28}ms` }}
                className="group flex animate-list-enter items-center gap-4 p-4 transition-colors hover:border-accent-300 dark:hover:border-accent-700"
              >
                <Link to={`/matches/${m.id}`} className="flex min-w-0 flex-1 items-center gap-4">
                  <div className={`font-mono text-2xl font-semibold tabular-nums ${scoreTone(m.overallScore)}`}>
                    {m.overallScore.toFixed(0)}
                  </div>
                  <div className="min-w-0 flex-1 space-y-1.5">
                    <div className="flex flex-wrap items-center gap-1.5">
                      <RowChip icon={FileText} label={resumeLabel} max="max-w-[180px]" />
                      <RowChip icon={Briefcase} label={vacancyLabel} max="max-w-[260px]" />
                    </div>
                    <div className="flex items-center gap-2 text-[11px] text-zinc-500 dark:text-zinc-400">
                      <span>{formatDate(m.createdAt)}</span>
                      <SeverityCounts critical={m.criticalCount} warning={m.warningCount} info={m.infoCount} />
                    </div>
                  </div>
                </Link>
                <button
                  type="button"
                  onClick={() => handleDelete(m.id)}
                  className="rounded-md p-2 text-zinc-400 opacity-0 transition-opacity hover:bg-red-50 hover:text-red-600 group-hover:opacity-100 dark:text-zinc-500 dark:hover:bg-red-950/40 dark:hover:text-red-400"
                  aria-label={t('matches.deleteMatchAria')}
                >
                  <Trash2 className="h-4 w-4" />
                </button>
                <Link to={`/matches/${m.id}`}>
                  <ArrowRight className="h-4 w-4 text-zinc-300 dark:text-zinc-700" />
                </Link>
              </Card>
            );
          })}
        </div>
      )}
    </>
  );
};

const RowChip = ({
  icon: Icon, label, max,
}: {
  icon: typeof FileText;
  label: string;
  max: string;
}) => (
  <span className="inline-flex items-center gap-1.5 rounded-full bg-stone-100/80 px-2 py-0.5 text-[11.5px] text-stone-600 dark:bg-stone-800/60 dark:text-stone-400">
    <Icon className="h-3 w-3 text-stone-400 dark:text-stone-500" strokeWidth={1.75} />
    <span className={`truncate ${max}`}>{label}</span>
  </span>
);

const SeverityCounts = ({
  critical, warning, info,
}: {
  critical: number;
  warning: number;
  info: number;
}) => {
  if (critical + warning + info === 0) return null;
  return (
    <span className="flex items-center gap-2">
      <span className="text-stone-300 dark:text-stone-600">·</span>
      {critical > 0 && (
        <span className="inline-flex items-center gap-1 font-medium text-red-700 dark:text-red-400">
          <span className="h-1.5 w-1.5 rounded-full bg-red-500" />
          {critical}
        </span>
      )}
      {warning > 0 && (
        <span className="inline-flex items-center gap-1 font-medium text-amber-700 dark:text-amber-400">
          <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
          {warning}
        </span>
      )}
      {info > 0 && (
        <span className="inline-flex items-center gap-1 font-medium text-sky-700 dark:text-sky-400">
          <span className="h-1.5 w-1.5 rounded-full bg-sky-500" />
          {info}
        </span>
      )}
    </span>
  );
};
