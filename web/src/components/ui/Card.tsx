import { forwardRef, type HTMLAttributes } from 'react';
import { cn } from '@/lib/utils';

/** Surface primitive used as the visual container for grouped content. */
export const Card = forwardRef<HTMLDivElement, HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div
      ref={ref}
      className={cn(
        'rounded-xl bg-white shadow-[0_1px_2px_rgba(28,25,23,0.04),0_4px_12px_-4px_rgba(28,25,23,0.04)] ring-1 ring-stone-900/[0.04]',
        'dark:bg-stone-900/60 dark:shadow-none dark:ring-stone-100/[0.06]',
        className,
      )}
      {...props}
    />
  ),
);
Card.displayName = 'Card';

export const CardHeader = ({ className, ...props }: HTMLAttributes<HTMLDivElement>) => (
  <div
    className={cn('border-b border-stone-100 px-6 py-5 dark:border-stone-800/60', className)}
    {...props}
  />
);

export const CardBody = ({ className, ...props }: HTMLAttributes<HTMLDivElement>) => (
  <div className={cn('p-6', className)} {...props} />
);

export const CardTitle = ({ className, ...props }: HTMLAttributes<HTMLHeadingElement>) => (
  <h3
    className={cn('font-display text-lg font-medium tracking-tight text-stone-900 dark:text-stone-100', className)}
    {...props}
  />
);

export const CardDescription = ({ className, ...props }: HTMLAttributes<HTMLParagraphElement>) => (
  <p className={cn('mt-1 text-sm text-stone-500 dark:text-stone-400', className)} {...props} />
);
