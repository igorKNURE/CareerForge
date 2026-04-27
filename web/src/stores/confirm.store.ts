import { create } from 'zustand';

/** Options for the global confirmation dialog (see <code>confirm</code>). */
export interface ConfirmOptions {
  title: string;
  body?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  /** Visual treatment for the confirm button. */
  destructive?: boolean;
}

interface ConfirmState {
  open: boolean;
  options: ConfirmOptions | null;
  resolver: ((confirmed: boolean) => void) | null;
  ask: (opts: ConfirmOptions) => Promise<boolean>;
  resolve: (confirmed: boolean) => void;
}

/** Backing store for the global confirmation dialog; you usually want the <code>confirm</code> helper instead. */
export const useConfirmStore = create<ConfirmState>((set, get) => ({
  open: false,
  options: null,
  resolver: null,
  ask: (opts) =>
    new Promise<boolean>((resolve) => {
      set({ open: true, options: opts, resolver: resolve });
    }),
  resolve: (confirmed) => {
    const { resolver } = get();
    resolver?.(confirmed);
    set({ open: false, options: null, resolver: null });
  },
}));

/**
 * Imperative wrapper — call from anywhere (event handlers, mutations) to ask the user.
 * Returns true if the user confirmed, false otherwise.
 */
export const confirm = (opts: ConfirmOptions): Promise<boolean> =>
  useConfirmStore.getState().ask(opts);
