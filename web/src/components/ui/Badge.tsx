import type { HTMLAttributes } from 'react';
import { cn } from '@/lib/utils';

type Tone = 'neutral' | 'accent' | 'success' | 'warning' | 'danger' | 'info';

// Tone presets combine text colour, ring colour, and a low-alpha fill.
const tones: Record<Tone, string> = {
  neutral: 'text-stone-700 ring-stone-200 bg-stone-100/70 dark:text-stone-200 dark:ring-stone-700 dark:bg-stone-800/60',
  accent: 'text-accent-700 ring-accent-200 bg-accent-50/80 dark:text-accent-300 dark:ring-accent-800/60 dark:bg-accent-950/50',
  success: 'text-emerald-700 ring-emerald-200 bg-emerald-50/80 dark:text-emerald-300 dark:ring-emerald-800/60 dark:bg-emerald-950/40',
  warning: 'text-amber-700 ring-amber-200 bg-amber-50/80 dark:text-amber-300 dark:ring-amber-800/60 dark:bg-amber-950/40',
  danger: 'text-red-700 ring-red-200 bg-red-50/80 dark:text-red-300 dark:ring-red-800/60 dark:bg-red-950/40',
  info: 'text-sky-700 ring-sky-200 bg-sky-50/80 dark:text-sky-300 dark:ring-sky-800/60 dark:bg-sky-950/40',
};

interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  tone?: Tone;
}

/** Small inline label with one of six semantic tones (status, severity, etc.). */
export const Badge = ({ className, tone = 'neutral', ...props }: BadgeProps) => (
  <span
    className={cn(
      'inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-[11px] font-medium uppercase tracking-[0.08em] ring-1 ring-inset',
      tones[tone],
      className,
    )}
    {...props}
  />
);
