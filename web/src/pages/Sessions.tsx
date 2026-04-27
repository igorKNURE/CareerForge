import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRight, Check, FileText, MessagesSquare, Pencil, Plus, Trash2, X } from 'lucide-react';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';
import { sessionsApi } from '@/api/sessions';
import { confirm } from '@/stores/confirm.store';
import { resumesApi } from '@/api/resumes';
import { vacanciesApi } from '@/api/vacancies';
import { PageHeader } from '@/components/PageHeader';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Input, Label, Select } from '@/components/ui/Input';
import { Spinner } from '@/components/ui/Spinner';
import { ListPageSkeleton } from '@/components/ui/PageSkeletons';
import { EmptyState } from '@/components/ui/EmptyState';
import { ErrorState } from '@/components/ui/ErrorState';
import { formatDate } from '@/lib/utils';
import type { ResumeListItem, SessionListItem, VacancyListItem } from '@/types/api';

/** Lists past interview sessions and lets the user start a new one. */
export const SessionsPage = () => {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [showForm, setShowForm] = useState(false);
  const [resumeId, setResumeId] = useState('');
  const [vacancyId, setVacancyId] = useState('');
  const [name, setName] = useState('');
  const [interviewLang, setInterviewLang] = useState<'en' | 'uk'>('en');

  const sessions = useQuery({ queryKey: ['sessions'], queryFn: sessionsApi.list });
  const resumes = useQuery({ queryKey: ['resumes'], queryFn: resumesApi.list });
  const vacancies = useQuery({ queryKey: ['vacancies'], queryFn: vacanciesApi.list });

  useEffect(() => {
    const r = searchParams.get('resumeId');
    const v = searchParams.get('vacancyId');
    if (!r && !v) return;
    if (r) setResumeId(r);
    if (v) setVacancyId(v);
    setShowForm(true);
    setSearchParams({}, { replace: true });
  }, [searchParams, setSearchParams]);

  const create = useMutation({
    mutationFn: () => sessionsApi.create(resumeId, vacancyId, name || undefined, interviewLang),
    onSuccess: (s) => {
      toast.success(t('interview.sessionCreated'));
      qc.invalidateQueries({ queryKey: ['sessions'] });
      navigate(`/sessions/${s.id}`);
    },
    onError: () => toast.error(t('interview.couldNotCreateSession')),
  });

  const remove = useMutation({
    mutationFn: (id: string) => sessionsApi.remove(id),
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: ['sessions'] });
      const prev = qc.getQueryData<typeof items>(['sessions']);
      qc.setQueryData<typeof items>(['sessions'], (old) => (old ?? []).filter((s) => s.id !== id));
      return { prev };
    },
    onError: (_e, _id, ctx) => {
      if (ctx?.prev) qc.setQueryData(['sessions'], ctx.prev);
      toast.error(t('interview.couldNotDeleteSession'));
    },
    onSettled: () => qc.invalidateQueries({ queryKey: ['sessions'] }),
  });

  const rename = useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => sessionsApi.rename(id, name),
    onMutate: async ({ id, name }) => {
      await qc.cancelQueries({ queryKey: ['sessions'] });
      const prev = qc.getQueryData<typeof items>(['sessions']);
      qc.setQueryData<typeof items>(['sessions'], (old) =>
        (old ?? []).map((s) => (s.id === id ? { ...s, name } : s)),
      );
      return { prev };
    },
    onError: (_e, _vars, ctx) => {
      if (ctx?.prev) qc.setQueryData(['sessions'], ctx.prev);
      toast.error(t('interview.renameFailed'));
    },
    onSuccess: () => toast.success(t('interview.renamed')),
    onSettled: () => qc.invalidateQueries({ queryKey: ['sessions'] }),
  });

  const handleDelete = async (id: string, sessionName: string) => {
    const ok = await confirm({
      title: t('interview.confirmDeleteSessionTitle'),
      body: t('interview.confirmDeleteSessionBody', { name: sessionName }),
      confirmLabel: t('common.delete'),
      destructive: true,
    });
    if (ok) remove.mutate(id);
  };

  const items = sessions.data ?? [];
  const resumesById = useMemo(
    () => Object.fromEntries((resumes.data ?? []).map((r) => [r.id, r])) as Record<string, ResumeListItem>,
    [resumes.data],
  );
  const vacanciesById = useMemo(
    () => Object.fromEntries((vacancies.data ?? []).map((v) => [v.id, v])) as Record<string, VacancyListItem>,
    [vacancies.data],
  );

  // Sessions are clustered by vacancy and ordered by recency within each cluster.
  // Clusters are ordered by their most-recent session. Sessions whose vacancy has been
  // deleted are grouped into a trailing "Other" bucket.
  const groups = useMemo(() => {
    const map = new Map<string, { vacancy: VacancyListItem | undefined; sessions: SessionListItem[] }>();
    for (const s of items) {
      const key = s.jobDescriptionId;
      if (!map.has(key)) map.set(key, { vacancy: vacanciesById[key], sessions: [] });
      map.get(key)!.sessions.push(s);
    }
    for (const g of map.values()) {
      g.sessions.sort((a, b) => +new Date(b.updatedAt) - +new Date(a.updatedAt));
    }
    return Array.from(map.values()).sort((a, b) => {
      // Push unknown-vacancy buckets to the end, then sort by recency.
      if (!a.vacancy && b.vacancy) return 1;
      if (a.vacancy && !b.vacancy) return -1;
      return +new Date(b.sessions[0].updatedAt) - +new Date(a.sessions[0].updatedAt);
    });
  }, [items, vacanciesById]);

  if ((sessions.isLoading && !sessions.data) || (resumes.isLoading && !resumes.data) || (vacancies.isLoading && !vacancies.data)) {
    return <ListPageSkeleton />;
  }
  if (sessions.isError && !sessions.data) return <ErrorState onRetry={() => sessions.refetch()} />;

  return (
    <>
      <PageHeader
        title={t('interview.title')}
        description={t('interview.subtitle')}
        actions={
          items.length > 0 || showForm ? (
            <Button
              variant={showForm ? 'secondary' : 'primary'}
              onClick={() => setShowForm((s) => !s)}
            >
              {showForm ? <X className="h-4 w-4" /> : <Plus className="h-4 w-4" />}
              {showForm ? t('common.cancel') : t('interview.newSession')}
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
            <div>
              <Label htmlFor="n">{t('interview.sessionName')}</Label>
              <Input
                id="n"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder={t('interview.sessionNamePlaceholder')}
              />
            </div>
            <div>
              <Label htmlFor="lang">{t('interview.chatLanguage')}</Label>
              <Select
                id="lang"
                value={interviewLang}
                onChange={(e) => setInterviewLang(e.target.value as 'en' | 'uk')}
              >
                <option value="en">{t('settings.english')}</option>
                <option value="uk">{t('settings.ukrainian')}</option>
              </Select>
            </div>
          </div>
          <div className="mt-4 flex justify-end">
            <Button onClick={() => create.mutate()} disabled={!resumeId || !vacancyId || create.isPending}>
              {create.isPending ? <Spinner /> : t('interview.startSession')}
            </Button>
          </div>
        </Card>
      )}

      {items.length === 0 ? (
        <EmptyState
          icon={MessagesSquare}
          title={t('interview.empty.title')}
          body={t('interview.empty.body')}
          action={
            <Button onClick={() => setShowForm(true)}>
              <Plus className="h-4 w-4" /> {t('interview.newSession')}
            </Button>
          }
        />
      ) : (
        <div className="space-y-8">
          {groups.map((g, gi) => {
            const headerLabel = g.vacancy
              ? g.vacancy.company
                ? `${g.vacancy.title} · ${g.vacancy.company}`
                : g.vacancy.title
              : t('interview.uncategorised');
            return (
              <section key={g.vacancy?.id ?? `unknown-${gi}`} className="space-y-2">
                <h3 className="text-[11px] font-semibold uppercase tracking-[0.18em] text-zinc-500 dark:text-zinc-400">
                  {headerLabel}
                </h3>
                {g.sessions.map((s, i) => (
                  <SessionRow
                    key={s.id}
                    session={s}
                    resume={resumesById[s.resumeId]}
                    index={i}
                    onRename={(name) => rename.mutate({ id: s.id, name })}
                    onDelete={() => handleDelete(s.id, s.name)}
                  />
                ))}
              </section>
            );
          })}
        </div>
      )}
    </>
  );
};

const SessionRow = ({
  session, resume, index, onRename, onDelete,
}: {
  session: SessionListItem;
  resume: ResumeListItem | undefined;
  index: number;
  onRename: (name: string) => void;
  onDelete: () => void;
}) => {
  const { t } = useTranslation();
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(session.name);
  const inputRef = useRef<HTMLInputElement>(null);

  // Re-sync draft if the upstream name changes (e.g. after a rename optimistic update settles).
  useEffect(() => {
    if (!editing) setDraft(session.name);
  }, [session.name, editing]);

  useEffect(() => {
    if (editing) {
      inputRef.current?.focus();
      inputRef.current?.select();
    }
  }, [editing]);

  const commit = () => {
    const trimmed = draft.trim();
    setEditing(false);
    if (trimmed && trimmed !== session.name) onRename(trimmed);
    else setDraft(session.name);
  };
  const cancel = () => {
    setDraft(session.name);
    setEditing(false);
  };

  return (
    <Card
      style={{ animationDelay: `${Math.min(index, 12) * 28}ms` }}
      className="group flex animate-list-enter items-center gap-3 p-4 transition-colors hover:border-accent-300 dark:hover:border-accent-700"
    >
      <div className="min-w-0 flex-1">
        {editing ? (
          <div className="flex items-center gap-2">
            <Input
              ref={inputRef}
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') { e.preventDefault(); commit(); }
                if (e.key === 'Escape') { e.preventDefault(); cancel(); }
              }}
              onBlur={commit}
              maxLength={256}
              className="h-8 text-sm"
            />
            <button
              type="button"
              onMouseDown={(e) => e.preventDefault() /* keep input from blurring before click */}
              onClick={commit}
              className="rounded-md p-1.5 text-emerald-600 hover:bg-emerald-50 dark:text-emerald-400 dark:hover:bg-emerald-950/40"
              aria-label={t('interview.saveName')}
            >
              <Check className="h-4 w-4" />
            </button>
            <button
              type="button"
              onMouseDown={(e) => e.preventDefault()}
              onClick={cancel}
              className="rounded-md p-1.5 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700 dark:text-zinc-500 dark:hover:bg-zinc-800 dark:hover:text-zinc-300"
              aria-label={t('common.cancel')}
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        ) : (
          <Link to={`/sessions/${session.id}`} className="block">
            <div className="flex items-center gap-2 text-sm font-medium text-zinc-900 dark:text-zinc-100">
              <MessagesSquare className="h-3.5 w-3.5 shrink-0 text-stone-400 dark:text-stone-500" strokeWidth={1.75} />
              <span className="truncate">{session.name}</span>
            </div>
            <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-zinc-500 dark:text-zinc-400">
              {resume && (
                <span className="inline-flex items-center gap-1">
                  <FileText className="h-3 w-3 text-stone-400 dark:text-stone-500" strokeWidth={1.75} />
                  <span className="truncate max-w-[180px]">{resume.fileName}</span>
                </span>
              )}
              <span>{t('dashboard.turns', { count: session.turnCount })}</span>
              <span>{t('interview.lastActivity', { date: formatDate(session.updatedAt) })}</span>
            </div>
          </Link>
        )}
      </div>

      {!editing && (
        <>
          <button
            type="button"
            onClick={() => setEditing(true)}
            className="rounded-md p-2 text-zinc-400 opacity-0 transition-opacity hover:bg-stone-100 hover:text-stone-700 group-hover:opacity-100 dark:text-zinc-500 dark:hover:bg-stone-800 dark:hover:text-stone-300"
            aria-label={t('interview.renameSessionAria')}
            title={t('interview.rename')}
          >
            <Pencil className="h-4 w-4" />
          </button>
          <button
            type="button"
            onClick={onDelete}
            className="rounded-md p-2 text-zinc-400 opacity-0 transition-opacity hover:bg-red-50 hover:text-red-600 group-hover:opacity-100 dark:text-zinc-500 dark:hover:bg-red-950/40 dark:hover:text-red-400"
            aria-label={t('interview.deleteSessionAria')}
          >
            <Trash2 className="h-4 w-4" />
          </button>
          <Link to={`/sessions/${session.id}`}>
            <ArrowRight className="h-4 w-4 text-zinc-300 dark:text-zinc-700" />
          </Link>
        </>
      )}
    </Card>
  );
};

