import clsx, { type ClassValue } from 'clsx';

/** Conditional Tailwind class composer; thin wrapper around clsx. */
export const cn = (...inputs: ClassValue[]) => clsx(inputs);

/** Locale-aware short date+time formatter for ISO strings returned by the API. */
export const formatDate = (iso: string) => {
  const d = new Date(iso);
  return d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
};

/** Trim a string to at most <code>n</code> characters, replacing the tail with an ellipsis. */
export const truncate = (s: string, n = 80) => (s.length <= n ? s : s.slice(0, n - 1) + '…');

/**
 * Tailwind text-color class for a 0–100 match score, bucketed for at-a-glance triage.
 * Shared across Matches list, Dashboard, and anywhere else we surface bare scores.
 */
export const scoreTone = (score: number) =>
  score >= 75
    ? 'text-emerald-600 dark:text-emerald-400'
    : score >= 50
      ? 'text-amber-600 dark:text-amber-400'
      : 'text-red-600 dark:text-red-400';
