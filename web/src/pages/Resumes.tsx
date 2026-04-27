import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FileText, MessagesSquare, RefreshCw, Search, Sparkles, Trash2, ChevronRight } from 'lucide-react';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';
import { resumesApi } from '@/api/resumes';
import { PageHeader } from '@/components/PageHeader';
import { Dropzone } from '@/components/Dropzone';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { Spinner } from '@/components/ui/Spinner';
import { ListPageSkeleton } from '@/components/ui/PageSkeletons';
import { EmptyState } from '@/components/ui/EmptyState';
import { ErrorState } from '@/components/ui/ErrorState';
import { confirm } from '@/stores/confirm.store';
import { formatDate, cn } from '@/lib/utils';
import type { ResumeListItem, ResumeResponse } from '@/types/api';

/** Resumes list with upload, expand-to-view, reparse, delete, and a deep-link to the score flow. */
export const ResumesPage = () => {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const { hash } = useLocation();
  const [openId, setOpenId] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  const { data: items, isLoading, isError, refetch } = useQuery({ queryKey: ['resumes'], queryFn: resumesApi.list });

  // When the URL hash references a known resume id, auto-open that row and scroll to it.
  useEffect(() => {
    const id = hash.startsWith('#') ? hash.slice(1) : '';
    if (!id || !items?.some((r) => r.id === id)) return;
    setOpenId(id);
    const el = document.getElementById(`resume-${id}`);
    el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, [hash, items]);

  const upload = useMutation({
    mutationFn: (file: File) => resumesApi.upload(file),
    onSuccess: () => {
      toast.success(t('resumes.parsed'));
      qc.invalidateQueries({ queryKey: ['resumes'] });
    },
    onError: (e: unknown) => {
      const msg = (e as { response?: { data?: { title?: string } } }).response?.data?.title;
      toast.error(msg ?? t('resumes.uploadFailed'));
    },
  });

  const remove = useMutation({
    mutationFn: (id: string) => resumesApi.remove(id),
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: ['resumes'] });
      const prev = qc.getQueryData<ResumeListItem[]>(['resumes']);
      qc.setQueryData<ResumeListItem[]>(['resumes'], (old) => (old ?? []).filter((r) => r.id !== id));
      return { prev };
    },
    onError: (_e, _id, ctx) => {
      if (ctx?.prev) qc.setQueryData(['resumes'], ctx.prev);
      toast.error(t('common.deleteFailed'));
    },
    onSettled: () => qc.invalidateQueries({ queryKey: ['resumes'] }),
  });

  if (isLoading && !items) return <ListPageSkeleton />;
  if (isError && !items) return <ErrorState onRetry={() => refetch()} />;

  return (
    <>
      <PageHeader
        title={t('resumes.title')}
        description={t('resumes.subtitle')}
      />

      <Dropzone
        accept=".pdf,.txt,application/pdf,text/plain"
        disabled={upload.isPending}
        onFile={(f) => upload.mutate(f)}
        hint={upload.isPending ? t('resumes.parsing') : t('resumes.uploadHint')}
      />

      <div className="mt-6">
        {items?.length === 0 ? (
          <EmptyState
            icon={FileText}
            title={t('resumes.empty.title')}
            body={t('resumes.empty.body')}
          />
        ) : (() => {
          const q = search.trim().toLowerCase();
          const filtered = q
            ? (items ?? []).filter((r) => r.fileName.toLowerCase().includes(q))
            : (items ?? []);
          return (
            <>
              {(items?.length ?? 0) > 3 && (
                <div className="mb-3 flex items-center gap-2">
                  <Search className="h-4 w-4 text-stone-400" strokeWidth={1.75} />
                  <input
                    type="search"
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    placeholder={t('resumes.searchPlaceholder')}
                    className="h-8 flex-1 border-0 border-b border-stone-200 bg-transparent px-1 text-sm placeholder:text-stone-400 focus:border-accent-500 focus:outline-none focus:ring-0 dark:border-stone-700 dark:placeholder:text-stone-600"
                  />
                </div>
              )}
              {filtered.length === 0 ? (
                <p className="py-8 text-center text-sm text-stone-400 dark:text-stone-500">
                  {t('resumes.noMatches')}
                </p>
              ) : (
                <div className="space-y-2">
                  {filtered.map((item, i) => (
                    <ResumeRow
                      key={item.id}
                      item={item}
                      index={i}
                      expanded={openId === item.id}
                      onToggle={() => setOpenId(openId === item.id ? null : item.id)}
                      onRemove={async () => {
                        const ok = await confirm({
                          title: t('resumes.confirmDeleteTitle'),
                          body: t('resumes.confirmDeleteBody', { name: item.fileName }),
                          confirmLabel: t('common.delete'),
                          destructive: true,
                        });
                        if (ok) remove.mutate(item.id);
                      }}
                    />
                  ))}
                </div>
              )}
            </>
          );
        })()}
      </div>
    </>
  );
};

const statusTone = (s: ResumeListItem['status']) =>
  s === 'Done' ? 'success' : s === 'Error' ? 'danger' : 'info';

const ResumeRow = ({
  item, index, expanded, onToggle, onRemove,
}: {
  item: ResumeListItem;
  index: number;
  expanded: boolean;
  onToggle: () => void;
  onRemove: () => void;
}) => {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const { data: detail, isLoading } = useQuery({
    queryKey: ['resume', item.id],
    queryFn: () => resumesApi.get(item.id),
    enabled: expanded,
  });

  const reparse = useMutation({
    mutationFn: () => resumesApi.reparse(item.id),
    onSuccess: (fresh) => {
      qc.setQueryData(['resume', item.id], fresh);
      qc.invalidateQueries({ queryKey: ['resumes'] });
      toast.success(t('resumes.reparsed'));
    },
    onError: () => toast.error(t('resumes.reparseFailed')),
  });

  return (
    <Card
      id={`resume-${item.id}`}
      style={{ animationDelay: `${Math.min(index, 12) * 28}ms` }}
      className="animate-list-enter scroll-mt-24"
    >
      <button
        type="button"
        onClick={onToggle}
        className="flex w-full items-center gap-3 px-5 py-4 text-left"
      >
        <ChevronRight
          className={cn(
            'h-4 w-4 shrink-0 text-zinc-400 transition-transform dark:text-zinc-500',
            expanded && 'rotate-90',
          )}
        />
        <FileText className="h-4 w-4 shrink-0 text-zinc-400 dark:text-zinc-500" />
        <div className="flex-1">
          <div className="text-sm font-medium text-zinc-900 dark:text-zinc-100">{item.fileName}</div>
          <div className="text-xs text-zinc-500 dark:text-zinc-400">{formatDate(item.createdAt)}</div>
        </div>
        <Badge tone={statusTone(item.status)}>{item.status}</Badge>
      </button>

      {expanded && (
        <div className="border-t border-zinc-100 px-5 py-4 dark:border-zinc-800">
          {isLoading ? <Spinner /> : detail ? (
            <ResumeDetail
              detail={detail}
              onRemove={onRemove}
              onReparse={() => reparse.mutate()}
              isReparsing={reparse.isPending}
            />
          ) : null}
        </div>
      )}
    </Card>
  );
};

const ResumeDetail = ({
  detail, onRemove, onReparse, isReparsing,
}: {
  detail: ResumeResponse;
  onRemove: () => void;
  onReparse: () => void;
  isReparsing: boolean;
}) => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const profile = detail.profile;
  return (
    <div className="space-y-4">
      {profile ? (
        <>
          <div>
            <div className="text-base font-semibold text-zinc-900 dark:text-zinc-50">{profile.fullName}</div>
            <div className="text-sm text-zinc-500 dark:text-zinc-400">{profile.headline}</div>
            {profile.yearsOfExperience !== undefined && (
              <div className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
                {t('resumes.yearsExperience', { years: profile.yearsOfExperience })}
              </div>
            )}
          </div>
          <p className="text-sm leading-relaxed text-zinc-700 dark:text-zinc-300">{profile.summary}</p>
          <SkillsBlock skills={profile.skills ?? []} />
          {(profile.experience ?? []).length > 0 && (
            <div>
              <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                {t('resumes.experience')}
              </div>
              <div className="space-y-3">
                {(profile.experience ?? []).map((e, i) => (
                  <div key={i} className="border-l-2 border-zinc-200 pl-3 dark:border-zinc-700">
                    <div className="text-sm font-medium text-zinc-900 dark:text-zinc-100">
                      {e.role} · {e.company}
                    </div>
                    <div className="text-xs text-zinc-500 dark:text-zinc-400">
                      {e.startDate} – {e.endDate ?? t('resumes.present')}
                    </div>
                    <ul className="mt-1.5 list-disc space-y-1 pl-4 text-sm text-zinc-700 dark:text-zinc-300">
                      {(e.highlights ?? []).map((h, hi) => <li key={hi}>{h}</li>)}
                    </ul>
                  </div>
                ))}
              </div>
            </div>
          )}
          {(profile.education ?? []).length > 0 && (
            <div>
              <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                {t('resumes.education')}
              </div>
              <div className="space-y-2">
                {(profile.education ?? []).map((e, i) => (
                  <div key={i} className="border-l-2 border-zinc-200 pl-3 dark:border-zinc-700">
                    <div className="text-sm font-medium text-zinc-900 dark:text-zinc-100">
                      {e.degree}
                    </div>
                    <div className="text-xs text-zinc-500 dark:text-zinc-400">
                      {e.institution}{e.year ? ` · ${e.year}` : ''}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      ) : (
        <p className="text-sm text-zinc-500 dark:text-zinc-400">
          {detail.errorMessage ?? t('common.profileNotAvailable')}
        </p>
      )}
      <div className="flex flex-wrap items-center justify-between gap-2 border-t border-stone-100 pt-4 dark:border-stone-800/60">
        <div className="flex flex-wrap gap-2">
          <Button
            variant="secondary"
            size="sm"
            onClick={() => navigate(`/matches?resumeId=${detail.id}`)}
            disabled={detail.status !== 'Done'}
          >
            <Sparkles className="h-4 w-4" /> {t('resumes.scoreAgainstVacancy')}
          </Button>
          <Button
            variant="secondary"
            size="sm"
            onClick={() => navigate(`/sessions?resumeId=${detail.id}`)}
            disabled={detail.status !== 'Done'}
          >
            <MessagesSquare className="h-4 w-4" /> {t('resumes.practiceInterview')}
          </Button>
        </div>
        <div className="flex gap-2">
          <Button variant="secondary" size="sm" onClick={onReparse} disabled={isReparsing}>
            {isReparsing ? <Spinner /> : <RefreshCw className="h-4 w-4" />} {t('common.reparse')}
          </Button>
          <Button variant="danger" size="sm" onClick={onRemove}>
            <Trash2 className="h-4 w-4" /> {t('common.delete')}
          </Button>
        </div>
      </div>

    </div>
  );
};


const SkillsBlock = ({ skills }: { skills: string[] }) => {
  const { t } = useTranslation();
  return (
    <div>
      <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
        {t('resumes.skills')}
      </div>
      <div className="flex flex-wrap gap-1.5">
        {skills.map((s) => <Badge key={s}>{s}</Badge>)}
      </div>
    </div>
  );
};
