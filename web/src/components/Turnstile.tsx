import { useEffect, useRef } from 'react';

declare global {
  interface Window {
    turnstile?: {
      render: (
        container: HTMLElement | string,
        options: {
          sitekey: string;
          callback?: (token: string) => void;
          'error-callback'?: () => void;
          'expired-callback'?: () => void;
          theme?: 'light' | 'dark' | 'auto';
        },
      ) => string;
      remove: (widgetId: string) => void;
    };
  }
}

interface TurnstileProps {
  /** Cloudflare Turnstile site key. When falsy, the component renders nothing. */
  siteKey?: string;
  /** Invoked with the verification token when the user passes the challenge. */
  onVerify: (token: string) => void;
  /** Invoked when the token expires or the challenge errors. */
  onExpire?: () => void;
  /** Optional widget theme. Defaults to <c>auto</c>. */
  theme?: 'light' | 'dark' | 'auto';
}

const SCRIPT_SRC = 'https://challenges.cloudflare.com/turnstile/v0/api.js';

/**
 * Lazy-loads the Cloudflare Turnstile script and renders an invisible challenge widget.
 * Renders nothing when no <c>siteKey</c> is supplied, so the component is safe to drop
 * into a form unconditionally.
 */
export const Turnstile = ({ siteKey, onVerify, onExpire, theme = 'auto' }: TurnstileProps) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const widgetIdRef = useRef<string | null>(null);

  useEffect(() => {
    if (!siteKey || !containerRef.current) return;

    let cancelled = false;
    const ensureScript = () =>
      new Promise<void>((resolve, reject) => {
        if (window.turnstile) return resolve();
        const existing = document.querySelector<HTMLScriptElement>(`script[src^="${SCRIPT_SRC}"]`);
        if (existing) {
          existing.addEventListener('load', () => resolve(), { once: true });
          existing.addEventListener('error', () => reject(new Error('script load failed')), { once: true });
          return;
        }
        const script = document.createElement('script');
        script.src = `${SCRIPT_SRC}?render=explicit`;
        script.async = true;
        script.defer = true;
        script.addEventListener('load', () => resolve(), { once: true });
        script.addEventListener('error', () => reject(new Error('script load failed')), { once: true });
        document.head.appendChild(script);
      });

    ensureScript()
      .then(() => {
        if (cancelled || !containerRef.current || !window.turnstile) return;
        widgetIdRef.current = window.turnstile.render(containerRef.current, {
          sitekey: siteKey,
          callback: onVerify,
          'expired-callback': onExpire,
          'error-callback': onExpire,
          theme,
        });
      })
      .catch(() => {
        // Network or script load failure; the form will surface a server-side captcha
        // failure on submit, which is the correct user-facing error.
      });

    return () => {
      cancelled = true;
      if (widgetIdRef.current && window.turnstile) {
        window.turnstile.remove(widgetIdRef.current);
        widgetIdRef.current = null;
      }
    };
  }, [siteKey, onVerify, onExpire, theme]);

  if (!siteKey) return null;
  return <div ref={containerRef} />;
};
