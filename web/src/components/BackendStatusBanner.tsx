import { useQueryClient } from '@tanstack/react-query';
import { CloudOff, RefreshCcw } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSystemStore } from '@/stores/system.store';

/** Amber banner shown when the backend is unreachable, with a one-click retry that refetches all queries. */
export const BackendStatusBanner = () => {
  const { t } = useTranslation();
  const healthy = useSystemStore((s) => s.backendHealthy);
  const qc = useQueryClient();
  const [retrying, setRetrying] = useState(false);

  if (healthy) return null;

  const retry = async () => {
    setRetrying(true);
    try {
      await qc.refetchQueries();
    } finally {
      setRetrying(false);
    }
  };

  return (
    <div className="bg-amber-100/90 text-amber-900 backdrop-blur dark:bg-amber-950/70 dark:text-amber-200">
      <div className="mx-auto flex max-w-6xl items-center gap-3 px-5 py-2 text-[13px] sm:px-8">
        <CloudOff className="h-4 w-4 shrink-0" strokeWidth={1.75} />
        <span className="flex-1 truncate">{t('errors.backendDown')}</span>
        <button
          type="button"
          onClick={retry}
          disabled={retrying}
          className="inline-flex items-center gap-1.5 rounded-md bg-amber-200/70 px-2.5 py-1 text-[12px] font-medium text-amber-900 transition-colors hover:bg-amber-300/70 disabled:opacity-60 dark:bg-amber-900/60 dark:text-amber-100 dark:hover:bg-amber-900"
        >
          <RefreshCcw className={`h-3.5 w-3.5 ${retrying ? 'animate-spin' : ''}`} />
          {t('errors.retry')}
        </button>
      </div>
    </div>
  );
};
