import { useRef, useState, type DragEvent } from 'react';
import { Upload } from 'lucide-react';
import { cn } from '@/lib/utils';

interface DropzoneProps {
  accept?: string;
  disabled?: boolean;
  onFile: (file: File) => void;
  hint?: string;
}

/** Click / drag-and-drop file picker that emits a single <code>File</code> via <code>onFile</code>. */
export const Dropzone = ({ accept, disabled, onFile, hint }: DropzoneProps) => {
  const ref = useRef<HTMLInputElement>(null);
  const [hover, setHover] = useState(false);

  const handleDrop = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setHover(false);
    if (disabled) return;
    const file = e.dataTransfer.files?.[0];
    if (file) onFile(file);
  };

  return (
    <div
      onDragOver={(e) => { e.preventDefault(); if (!disabled) setHover(true); }}
      onDragLeave={() => setHover(false)}
      onDrop={handleDrop}
      onClick={() => !disabled && ref.current?.click()}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => { if ((e.key === 'Enter' || e.key === ' ') && !disabled) ref.current?.click(); }}
      className={cn(
        'flex cursor-pointer flex-col items-center justify-center gap-2 rounded-lg border-2 border-dashed px-6 py-10 text-center transition-colors',
        hover
          ? 'border-accent-500 bg-accent-50/40 dark:bg-accent-950/30'
          : 'border-zinc-300 bg-white hover:border-accent-400 dark:border-zinc-700 dark:bg-zinc-900 dark:hover:border-accent-500',
        disabled && 'cursor-not-allowed opacity-60',
      )}
    >
      <input
        ref={ref}
        type="file"
        accept={accept}
        hidden
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) onFile(file);
          e.target.value = '';
        }}
      />
      <Upload className="h-6 w-6 text-zinc-400 dark:text-zinc-500" />
      <div className="text-sm font-medium text-zinc-700 dark:text-zinc-200">
        Drop a file here or click to browse
      </div>
      {hint && <div className="text-xs text-zinc-500 dark:text-zinc-400">{hint}</div>}
    </div>
  );
};
