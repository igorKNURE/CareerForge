import type { HTMLAttributes } from 'react';
import { cn } from '@/lib/utils';

/**
 * Generic shimmer/pulse rectangle. Use multiple in a layout that mirrors the real
 * page so the visual shape is preserved while data loads.
 */
export const Skeleton = ({ className, ...props }: HTMLAttributes<HTMLDivElement>) => (
  <div
    className={cn('animate-pulse rounded-md bg-stone-200/70 dark:bg-stone-800/60', className)}
    {...props}
  />
);

/**
 * A horizontal text-line skeleton — defaults to one line.
 * Pass `lines` for a stacked block (e.g. paragraph).
 */
export const SkeletonText = ({
  lines = 1,
  className,
}: {
  lines?: number;
  className?: string;
}) => (
  <div className={cn('space-y-2', className)}>
    {Array.from({ length: lines }).map((_, i) => (
      <Skeleton
        key={i}
        className={cn(
          'h-3.5',
          // Vary widths so it doesn't look like a uniform brick.
          i === lines - 1 ? 'w-3/4' : 'w-full',
        )}
      />
    ))}
  </div>
);
