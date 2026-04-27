import { forwardRef, type InputHTMLAttributes, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';
import { cn } from '@/lib/utils';

const baseClasses =
  'w-full rounded-md border border-zinc-200 bg-white px-3 py-2 text-sm text-zinc-900 placeholder:text-zinc-400 ' +
  'focus:border-accent-500 focus:ring-2 focus:ring-accent-500/30 disabled:bg-zinc-50 disabled:text-zinc-400 ' +
  'dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-100 dark:placeholder:text-zinc-600 ' +
  'dark:focus:border-accent-500 dark:disabled:bg-zinc-900/50 dark:disabled:text-zinc-600';

/** Themed text input matching the design system's focus / disabled states. */
export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(
  ({ className, ...props }, ref) => (
    <input ref={ref} className={cn(baseClasses, 'h-10', className)} {...props} />
  ),
);
Input.displayName = 'Input';

/** Themed multiline textarea sharing the <code>Input</code> styling. */
export const Textarea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement>>(
  ({ className, rows = 6, ...props }, ref) => (
    <textarea ref={ref} rows={rows} className={cn(baseClasses, 'resize-y', className)} {...props} />
  ),
);
Textarea.displayName = 'Textarea';

/** Themed native select matching the <code>Input</code> styling. */
export const Select = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement>>(
  ({ className, children, ...props }, ref) => (
    <select ref={ref} className={cn(baseClasses, 'h-10', className)} {...props}>
      {children}
    </select>
  ),
);
Select.displayName = 'Select';

/** Uppercase-tracked field label; pair with the matching field's <code>id</code> for accessibility. */
export const Label = ({ children, htmlFor }: { children: React.ReactNode; htmlFor?: string }) => (
  <label
    htmlFor={htmlFor}
    className="mb-1.5 block text-xs font-medium uppercase tracking-wide text-zinc-500 dark:text-zinc-400"
  >
    {children}
  </label>
);

/** Renders a small red validation message, or nothing if <code>message</code> is empty. */
export const FieldError = ({ message }: { message?: string }) =>
  message ? <p className="mt-1 text-xs text-red-600 dark:text-red-400">{message}</p> : null;
