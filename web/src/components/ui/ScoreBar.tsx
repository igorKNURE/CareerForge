import { cn } from '@/lib/utils';

interface ScoreBarProps {
  label: string;
  value: number;
  max?: number;
  className?: string;
}

/** Horizontal labelled progress bar; tone shifts red → amber → emerald past 50% / 75%. */
export const ScoreBar = ({ label, value, max = 100, className }: ScoreBarProps) => {
  const pct = Math.min(100, Math.max(0, (value / max) * 100));
  const tone = pct >= 75 ? 'bg-emerald-600' : pct >= 50 ? 'bg-amber-600' : 'bg-red-600';
  return (
    <div className={cn('space-y-2', className)}>
      <div className="flex items-baseline justify-between">
        <span className="text-[11px] font-medium uppercase tracking-[0.12em] text-stone-500 dark:text-stone-400">{label}</span>
        <span className="font-mono text-sm tabular-nums text-stone-900 dark:text-stone-100">
          {Math.round(value)}
          <span className="text-stone-400 dark:text-stone-500">/{max}</span>
        </span>
      </div>
      <div className="h-[3px] w-full overflow-hidden rounded-full bg-stone-200/70 dark:bg-stone-800">
        <div className={cn('h-full transition-all duration-500', tone)} style={{ width: `${pct}%` }} />
      </div>
    </div>
  );
};

/** Donut-style overall-score indicator (0–100); colours match the <code>ScoreBar</code> thresholds. */
export const ScoreCircle = ({ value }: { value: number }) => {
  const pct = Math.min(100, Math.max(0, value));
  const colour =
    pct >= 75
      ? 'text-emerald-700 dark:text-emerald-400'
      : pct >= 50
      ? 'text-amber-700 dark:text-amber-400'
      : 'text-red-700 dark:text-red-400';
  const stroke =
    pct >= 75 ? 'stroke-emerald-600' : pct >= 50 ? 'stroke-amber-600' : 'stroke-red-600';
  const r = 38;
  const c = 2 * Math.PI * r;
  return (
    <div className="relative inline-flex h-28 w-28 items-center justify-center">
      <svg className="absolute inset-0 -rotate-90" viewBox="0 0 88 88">
        <circle cx={44} cy={44} r={r} className="stroke-stone-200/80 dark:stroke-stone-800" fill="none" strokeWidth={4} />
        <circle
          cx={44} cy={44} r={r}
          className={cn(stroke, 'transition-all duration-500')}
          fill="none"
          strokeWidth={4}
          strokeLinecap="round"
          strokeDasharray={c}
          strokeDashoffset={c - (pct / 100) * c}
        />
      </svg>
      <div className="text-center">
        <div className={cn('font-display text-3xl font-medium tabular-nums', colour)}>{Math.round(pct)}</div>
        <div className="mt-0.5 text-[9px] uppercase tracking-[0.18em] text-stone-400 dark:text-stone-500">overall</div>
      </div>
    </div>
  );
};
