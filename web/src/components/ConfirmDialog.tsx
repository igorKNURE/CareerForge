import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/Button';
import { useConfirmStore } from '@/stores/confirm.store';

/**
 * Global confirmation modal driven by <code>useConfirmStore</code>. Renders nothing until
 * the store has an open prompt; Enter confirms, Escape / backdrop click cancels.
 */
export const ConfirmDialog = () => {
  const { t } = useTranslation();
  const { open, options, resolve } = useConfirmStore();
  const confirmBtnRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') resolve(false);
      if (e.key === 'Enter') resolve(true);
    };
    document.addEventListener('keydown', onKey);
    // Focus the confirm button so Enter/Space confirm and Tab cycles within the dialog.
    confirmBtnRef.current?.focus();
    return () => document.removeEventListener('keydown', onKey);
  }, [open, resolve]);

  if (!open || !options) return null;

  const destructive = options.destructive ?? false;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center px-4"
      aria-modal="true"
      role="dialog"
    >
      {/* Backdrop */}
      <button
        type="button"
        aria-label="Close"
        onClick={() => resolve(false)}
        className="absolute inset-0 bg-stone-900/30 backdrop-blur-sm dark:bg-black/50"
      />

      {/* Panel */}
      <div className="relative w-full max-w-md rounded-2xl bg-white p-6 shadow-[0_20px_60px_-15px_rgba(28,25,23,0.25)] ring-1 ring-stone-900/[0.08] dark:bg-stone-900 dark:ring-stone-100/[0.1]">
        <h2 className="font-display text-xl font-medium leading-snug text-stone-900 dark:text-stone-50">
          {options.title}
        </h2>
        {options.body && (
          <p className="mt-2 text-[14.5px] leading-relaxed text-stone-600 dark:text-stone-300">
            {options.body}
          </p>
        )}
        <div className="mt-6 flex justify-end gap-2">
          <Button variant="secondary" onClick={() => resolve(false)}>
            {options.cancelLabel ?? t('common.cancel')}
          </Button>
          <Button
            ref={confirmBtnRef}
            variant={destructive ? 'danger' : 'primary'}
            onClick={() => resolve(true)}
          >
            {options.confirmLabel ?? t('common.delete')}
          </Button>
        </div>
      </div>
    </div>
  );
};
