import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Briefcase, ChevronRight, MessagesSquare, Plus, RefreshCw, Search, Sparkles, Trash2, X } from 'lucide-react';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';
import { vacanciesApi } from '@/api/vacancies';
import { PageHeader } from '@/components/PageHeader';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { Input, Textarea, Label } from '@/components/ui/Input';
import { Spinner } from '@/components/ui/Spinner';
import { ListPageSkeleton } from '@/components/ui/PageSkeletons';
import { EmptyState } from '@/components/ui/EmptyState';
import { ErrorState } from '@/components/ui/ErrorState';
import { confirm } from '@/stores/confirm.store';
import { formatDate, cn } from '@/lib/utils';
import type { VacancyListItem, VacancyResponse } from '@/types/api';

/** Vacancies list with paste-to-create form, expand-to-view, reparse, and delete. */
export const VacanciesPage = () => {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const { hash } = useLocation();
  const [showForm, setShowForm] = useState(false);
  const [openId, setOpenId] = useState<string | null>(null);
  const [titleHint, setTitleHint] = useState('');
  const [rawText, setRawText] = useState('');
  const [search, setSearch] = useState('');

  const { data: items, isLoading, isError, refetch } = useQuery({ queryKey: ['vacancies'], queryFn: vacanciesApi.list });

  useEffect(() => {
    const id = hash.startsWith('#') ? hash.slice(1) : '';
    if (!id || !items?.some((v) => v.id === id)) return;
    setOpenId(id);
    document.getElementById(`vacancy-${id}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, [hash, items]);

  const create = useMutation({
    mutationFn: () => vacanciesApi.create(rawText, titleHint || undefined),
    onSuccess: () => {
      toast.success(t('vacancies.parsed'));
      setShowForm(false);
      setTitleHint('');
      setRawText('');
      qc.invalidateQueries({ queryKey: ['vacancies'] });
    },
    onError: (e: unknown) => {
      const msg = (e as { response?: { data?: { title?: string } } }).response?.data?.title;
      toast.error(msg ?? t('vacancies.parseFailed'));
    },
  });

  const remove = useMutation({
    mutationFn: (id: string) => vacanciesApi.remove(id),
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: ['vacancies'] });
      const prev = qc.getQueryData<VacancyListItem[]>(['vacancies']);
      qc.setQueryData<VacancyListItem[]>(['vacancies'], (old) => (old ?? []).filter((v) => v.id !== id));
      return { prev };
    },
    onError: (_e, _id, ctx) => {
      if (ctx?.prev) qc.setQueryData(['vacancies'], ctx.prev);
      toast.error(t('common.deleteFailed'));
    },
    onSettled: () => qc.invalidateQueries({ queryKey: ['vacancies'] }),
  });

  // Only block the page when there's no cached data to fall back on. When the persister
  // has a snapshot, render it and let the global BackendStatusBanner indicate freshness.
  if (isLoading && !items) return <ListPageSkeleton />;
  if (isError && !items) return <ErrorState onRetry={() => refetch()} />;

  return (
    <>
      <PageHeader
        title={t('vacancies.title')}
        description={t('vacancies.subtitle')}
        actions={
          <Button
            variant={showForm ? 'secondary' : 'primary'}
            onClick={() => setShowForm((s) => !s)}
          >
            {showForm ? <X className="h-4 w-4" /> : <Plus className="h-4 w-4" />}
            {showForm ? t('common.cancel') : t('vacancies.newVacancy')}
          </Button>
        }
      />

      {showForm && (
        <Card className="mb-4 p-5">
          <div className="space-y-3">
            <div>
              <Label htmlFor="hint">{t('vacancies.titleHint')}</Label>
              <Input
                id="hint"
                placeholder={t('vacancies.titleHintPlaceholder')}
                value={titleHint}
                onChange={(e) => setTitleHint(e.target.value)}
              />
            </div>
            <div>
              <Label htmlFor="raw">{t('vacancies.rawTextLabel')}</Label>
              <Textarea
                id="raw"
                rows={10}
                placeholder={t('vacancies.rawTextPlaceholder')}
                value={rawText}
                onChange={(e) => setRawText(e.target.value)}
              />
            </div>
            <div className="flex justify-end">
              <Button onClick={() => create.mutate()} disabled={create.isPending || rawText.length < 80}>
                {create.isPending ? <Spinner /> : t('vacancies.parseVacancy')}
              </Button>
            </div>
          </div>
        </Card>
      )}

      {items?.length === 0 ? (
        <EmptyState
          icon={Briefcase}
          title={t('vacancies.empty.title')}
          body={t('vacancies.empty.body')}
        />
      ) : (() => {
        const q = search.trim().toLowerCase();
        const filtered = q
          ? (items ?? []).filter((v) =>
              v.title.toLowerCase().includes(q) ||
              (v.company ?? '').toLowerCase().includes(q),
            )
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
                  placeholder={t('vacancies.searchPlaceholder')}
                  className="h-8 flex-1 border-0 border-b border-stone-200 bg-transparent px-1 text-sm placeholder:text-stone-400 focus:border-accent-500 focus:outline-none focus:ring-0 dark:border-stone-700 dark:placeholder:text-stone-600"
                />
              </div>
            )}
            {filtered.length === 0 ? (
              <p className="py-8 text-center text-sm text-stone-400 dark:text-stone-500">
                {t('vacancies.noMatches')}
              </p>
            ) : (
              <div className="space-y-2">
                {filtered.map((item, i) => (
                  <VacancyRow
                    key={item.id}
                    item={item}
                    index={i}
                    expanded={openId === item.id}
                    onToggle={() => setOpenId(openId === item.id ? null : item.id)}
                    onRemove={async () => {
                      const ok = await confirm({
                        title: t('vacancies.confirmDeleteTitle'),
                        body: t('vacancies.confirmDeleteBody', {
                          name: item.title || t('common.untitled'),
                        }),
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
    </>
  );
};

const statusTone = (s: VacancyListItem['status']) =>
  s === 'Done' ? 'success' : s === 'Error' ? 'danger' : 'info';

const VacancyRow = ({
  item, index, expanded, onToggle, onRemove,
}: {
  item: VacancyListItem;
  index: number;
  expanded: boolean;
  onToggle: () => void;
  onRemove: () => void;
}) => {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const { data: detail, isLoading } = useQuery({
    queryKey: ['vacancy', item.id],
    queryFn: () => vacanciesApi.get(item.id),
    enabled: expanded,
  });

  const reparse = useMutation({
    mutationFn: () => vacanciesApi.reparse(item.id),
    onSuccess: (fresh) => {
      qc.setQueryData(['vacancy', item.id], fresh);
      qc.invalidateQueries({ queryKey: ['vacancies'] });
      toast.success(t('vacancies.reparsed'));
    },
    onError: () => toast.error(t('vacancies.reparseFailed')),
  });

  return (
    <Card
      id={`vacancy-${item.id}`}
      style={{ animationDelay: `${Math.min(index, 12) * 28}ms` }}
      className="animate-list-enter scroll-mt-24"
    >
      <button type="button" onClick={onToggle} className="flex w-full items-center gap-3 px-5 py-4 text-left">
        <ChevronRight
          className={cn('h-4 w-4 shrink-0 text-zinc-400 transition-transform dark:text-zinc-500', expanded && 'rotate-90')}
        />
        <Briefcase className="h-4 w-4 shrink-0 text-zinc-400 dark:text-zinc-500" />
        <div className="flex-1">
          <div className="text-sm font-medium text-zinc-900 dark:text-zinc-100">{item.title}</div>
          <div className="text-xs text-zinc-500 dark:text-zinc-400">
            {item.company ?? t('vacancies.unknownCompany')} · {formatDate(item.createdAt)}
          </div>
        </div>
        <Badge tone={statusTone(item.status)}>{item.status}</Badge>
      </button>
      {expanded && (
        <div className="border-t border-zinc-100 px-5 py-4 dark:border-zinc-800">
          {isLoading ? <Spinner /> : detail ? (
            <VacancyDetail
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

const VacancyDetail = ({
  detail, onRemove, onReparse, isReparsing,
}: {
  detail: VacancyResponse;
  onRemove: () => void;
  onReparse: () => void;
  isReparsing: boolean;
}) => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const p = detail.profile;
  return (
    <div className="space-y-4">
      {p ? (
        <>
          <div>
            <div className="text-base font-semibold text-zinc-900 dark:text-zinc-50">{p.title}</div>
            <div className="text-sm text-zinc-500 dark:text-zinc-400">
              {p.company} · {p.seniority ?? t('vacancies.levelUnspecified')}
              {p.yearsRequired ? ` · ${t('vacancies.yearsRequired', { years: p.yearsRequired })}` : ''}
            </div>
          </div>
          <p className="text-sm leading-relaxed text-zinc-700 dark:text-zinc-300">{p.summary}</p>
          <div className="grid gap-4 sm:grid-cols-2">
            <SkillBlock title={t('vacancies.mustHave')} skills={p.mustHaveSkills ?? []} tone="accent" />
            <SkillBlock title={t('vacancies.niceToHave')} skills={p.niceToHaveSkills ?? []} tone="neutral" />
          </div>
          {(p.responsibilities ?? []).length > 0 && (
            <ListBlock title={t('vacancies.responsibilities')} items={p.responsibilities ?? []} />
          )}
          {(p.qualifications ?? []).length > 0 && (
            <ListBlock title={t('vacancies.qualifications')} items={p.qualifications ?? []} />
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
            onClick={() => navigate(`/matches?vacancyId=${detail.id}`)}
            disabled={detail.status !== 'Done'}
          >
            <Sparkles className="h-4 w-4" /> {t('vacancies.scoreWithResume')}
          </Button>
          <Button
            variant="secondary"
            size="sm"
            onClick={() => navigate(`/sessions?vacancyId=${detail.id}`)}
            disabled={detail.status !== 'Done'}
          >
            <MessagesSquare className="h-4 w-4" /> {t('vacancies.practiceForRole')}
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

const SkillBlock = ({ title, skills, tone }: { title: string; skills: string[]; tone: 'accent' | 'neutral' }) => (
  <div>
    <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
      {title}
    </div>
    <div className="flex flex-wrap gap-1.5">
      {skills.map((s) => <Badge key={s} tone={tone}>{s}</Badge>)}
    </div>
  </div>
);

const ListBlock = ({ title, items }: { title: string; items: string[] }) => (
  <div>
    <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
      {title}
    </div>
    <ul className="list-disc space-y-1 pl-5 text-sm text-zinc-700 dark:text-zinc-300">
      {items.map((it, i) => <li key={i}>{it}</li>)}
    </ul>
  </div>
);
