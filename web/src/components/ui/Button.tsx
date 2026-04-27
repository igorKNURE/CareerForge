import { forwardRef, type ButtonHTMLAttributes } from 'react';
import { cn } from '@/lib/utils';

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger';
type Size = 'sm' | 'md' | 'lg';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
}

const variants: Record<Variant, string> = {
  primary:
    'bg-accent-700 text-white shadow-[inset_0_1px_0_rgba(255,255,255,0.08)] hover:bg-accent-800 disabled:bg-accent-900/50 disabled:text-accent-100/50',
  secondary:
    'bg-white text-stone-900 ring-1 ring-stone-200 hover:bg-stone-50 hover:ring-stone-300 disabled:bg-stone-50 disabled:text-stone-400 disabled:ring-stone-200 ' +
    'dark:bg-stone-800/60 dark:text-stone-100 dark:ring-stone-700 dark:hover:bg-stone-800 dark:hover:ring-stone-600 dark:disabled:bg-stone-900 dark:disabled:text-stone-600',
  ghost:
    'bg-transparent text-stone-700 hover:bg-stone-100 disabled:text-stone-300 ' +
    'dark:text-stone-300 dark:hover:bg-stone-800 dark:disabled:text-stone-600',
  danger:
    'bg-red-700 text-white hover:bg-red-800 disabled:bg-red-900/40 disabled:text-red-100/40',
};

const sizes: Record<Size, string> = {
  sm: 'h-8 px-3 text-[13px]',
  md: 'h-10 px-4 text-[13.5px]',
  lg: 'h-12 px-6 text-[15px]',
};

/** Standard button with four visual variants and three sizes. */
export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant = 'primary', size = 'md', ...props }, ref) => (
    <button
      ref={ref}
      className={cn(
        'inline-flex items-center justify-center gap-2 rounded-lg font-medium tracking-tight transition-all',
        'disabled:cursor-not-allowed',
        variants[variant],
        sizes[size],
        className,
      )}
      {...props}
    />
  ),
);
Button.displayName = 'Button';
