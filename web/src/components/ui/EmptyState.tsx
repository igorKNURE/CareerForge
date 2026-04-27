import type { ComponentType, ReactNode, SVGProps } from 'react';
import { cn } from '@/lib/utils';

interface EmptyStateProps {
  icon: ComponentType<SVGProps<SVGSVGElement>>;
  title: string;
  body: string;
  action?: ReactNode;
  className?: string;
}

/**
 * Editorial empty state — generous spacing, serif title, optional CTA.
 * Reads like an invitation, not a "no items" placeholder.
 */
export const EmptyState = ({ icon: Icon, title, body, action, className }: EmptyStateProps) => (
  <div className={cn('flex flex-col items-center px-6 py-16 text-center', className)}>
    <div className="flex h-14 w-14 items-center justify-center rounded-full bg-accent-50 text-accent-700 dark:bg-accent-950/40 dark:text-accent-400">
      <Icon className="h-6 w-6" strokeWidth={1.5} />
    </div>
    <h3 className="mt-5 max-w-sm font-display text-xl font-medium leading-snug text-stone-900 dark:text-stone-50">
      {title}
    </h3>
    <p className="mt-2 max-w-md text-[14.5px] leading-relaxed text-stone-500 dark:text-stone-400">
      {body}
    </p>
    {action && <div className="mt-6">{action}</div>}
  </div>
);
