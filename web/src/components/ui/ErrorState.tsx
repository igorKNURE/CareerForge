import { CloudOff, RefreshCcw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from './Button';
import { useSystemStore } from '@/stores/system.store';

interface Props {
  /** Optional override of the title text. */
  title?: string;
  body?: string;
  onRetry?: () => void;
  retrying?: boolean;
}

/**
 * Editorial "couldn't load" state. Use when a fetch failed (network/5xx) so the
 * user knows it's a system issue, not an empty account.
 *
 * The retry button is suppressed when the global BackendStatusBanner is visible —
 * the banner is the single retry surface during outages, so we don't show two.
 */
export const ErrorState = ({ title, body, onRetry, retrying }: Props) => {
  const { t } = useTranslation();
  const backendHealthy = useSystemStore((s) => s.backendHealthy);
  const showRetry = !!onRetry && backendHealthy;
  return (
    <div className="flex flex-col items-center px-6 py-16 text-center">
      <div className="flex h-14 w-14 items-center justify-center rounded-full bg-amber-50 text-amber-700 dark:bg-amber-950/30 dark:text-amber-400">
        <CloudOff className="h-6 w-6" strokeWidth={1.5} />
      </div>
      <h3 className="mt-5 max-w-sm font-display text-xl font-medium leading-snug text-stone-900 dark:text-stone-50">
        {title ?? t('errors.cantLoadTitle')}
      </h3>
      <p className="mt-2 max-w-md text-[14.5px] leading-relaxed text-stone-500 dark:text-stone-400">
        {body ?? t('errors.cantLoadBody')}
      </p>
      {showRetry && (
        <div className="mt-6">
          <Button onClick={onRetry} disabled={retrying}>
            <RefreshCcw className={`h-4 w-4 ${retrying ? 'animate-spin' : ''}`} />
            {t('errors.retry')}
          </Button>
        </div>
      )}
    </div>
  );
};
