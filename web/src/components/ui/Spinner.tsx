import { cn } from '@/lib/utils';

/**
 * Inline 16px loading indicator. Renders a CSS-only ring with a transparent quadrant;
 * the symmetric geometry rotates without optical wobble.
 */
export const Spinner = ({ className }: { className?: string }) => (
  <span
    role="status"
    aria-label="Loading"
    className={cn(
      'inline-block h-4 w-4 shrink-0 animate-spin rounded-full border-2 border-current border-t-transparent align-[-0.125em]',
      className,
    )}
  />
);

/** Vertically-centered loading indicator that fills the available space. */
export const FullPageSpinner = () => (
  <div className="flex h-full min-h-[40vh] w-full items-center justify-center text-zinc-400">
    <Spinner className="h-6 w-6 border-[2.5px]" />
  </div>
);
