import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';

interface BackTo {
  to: string;
  label: string;
}

interface Props {
  title: string;
  description?: string;
  eyebrow?: string;
  actions?: ReactNode;
  backTo?: BackTo;
}

/** Shared editorial-style page header with eyebrow, title, description, optional actions, and a back link. */
export const PageHeader = ({ title, description, eyebrow, actions, backTo }: Props) => (
  <header className="mb-10">
    {backTo && (
      <Link
        to={backTo.to}
        className="mb-4 inline-flex items-center gap-1.5 text-[12.5px] font-medium text-stone-500 transition-colors hover:text-accent-700 dark:text-stone-400 dark:hover:text-accent-400"
      >
        <ArrowLeft className="h-3.5 w-3.5" strokeWidth={2} />
        {backTo.label}
      </Link>
    )}
    <div className="flex items-end justify-between gap-6">
      <div className="min-w-0">
        {eyebrow && (
          <div className="mb-3 text-[11px] font-medium uppercase tracking-[0.18em] text-accent-700 dark:text-accent-400">
            {eyebrow}
          </div>
        )}
        <h1 className="font-display text-4xl font-medium leading-[1.1] tracking-tight text-stone-900 dark:text-stone-50">
          {title}
        </h1>
        {description && (
          <p className="mt-3 max-w-xl text-[15px] leading-relaxed text-stone-500 dark:text-stone-400">
            {description}
          </p>
        )}
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </div>
  </header>
);
