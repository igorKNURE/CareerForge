import { useEffect, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { cn } from '@/lib/utils';

type ProseSpeed = 'slow' | 'normal' | 'fast';

interface ProseProps {
  children: string;
  /** When true, the rendered output is collapsed into an inline span suitable for placement within a paragraph. */
  inline?: boolean;
  /** When true, content is revealed progressively (typewriter effect). The animation runs once per unique content string. */
  stream?: boolean;
  /** Invoked once the streaming reveal completes. Not invoked when streaming is disabled. */
  onComplete?: () => void;
  /** When true, the viewport tracks the tail of the streaming text. Only scrolls downward to avoid disrupting users who have scrolled up. */
  followScroll?: boolean;
  /** Reveal pace. When omitted, the speed is derived from content length. */
  speed?: ProseSpeed;
  className?: string;
}

// Reveal pace presets, expressed as characters per tick and tick interval (ms).
const SPEED_PRESETS: Record<ProseSpeed, { chars: number; ms: number }> = {
  slow:   { chars: 3, ms: 28 },
  normal: { chars: 5, ms: 20 },
  fast:   { chars: 8, ms: 16 },
};

const deriveSpeed = (length: number): ProseSpeed =>
  length > 300 ? 'fast' : length < 80 ? 'slow' : 'normal';

/**
 * Renders model-authored markdown using the application's editorial typography.
 * Renders block-level by default; use <c>inline</c> to embed within an existing line.
 */
export const Prose = ({ children, inline = false, stream = false, onComplete, followScroll = false, speed, className }: ProseProps) => {
  const Wrapper = inline ? 'span' : 'div';
  const effectiveSpeed = speed ?? deriveSpeed(children.length);
  const visible = useTypewriter(children, stream, effectiveSpeed, onComplete);
  const isStreaming = stream && visible.length < children.length;
  const tailRef = useRef<HTMLSpanElement>(null);

  // Keeps the tail of the streaming text in view while it is being revealed. Uses
  // requestAnimationFrame for smooth motion. Only scrolls downward.
  useEffect(() => {
    if (!followScroll || !isStreaming) return;
    let rafId = 0;
    const follow = () => {
      const el = tailRef.current;
      if (el) {
        const rect = el.getBoundingClientRect();
        const target = window.innerHeight * 0.7;
        if (rect.bottom > target) {
          window.scrollBy({ top: rect.bottom - target, behavior: 'auto' });
        }
      }
      rafId = requestAnimationFrame(follow);
    };
    rafId = requestAnimationFrame(follow);
    return () => cancelAnimationFrame(rafId);
  }, [followScroll, isStreaming]);

  // While streaming, plain whitespace-preserving text is rendered to avoid markdown
  // flicker caused by partial token sequences (e.g. an unclosed emphasis marker).
  if (isStreaming) {
    return (
      <Wrapper className={cn(inline ? 'inline' : 'space-y-3', className)}>
        <span className={cn(inline ? 'inline' : 'block', 'whitespace-pre-wrap leading-relaxed')}>
          {visible}
          <span
            ref={tailRef}
            className="ml-[2px] inline-block h-[1em] w-[3px] translate-y-[2px] animate-pulse rounded-[1px] bg-accent-500/80"
          />
        </span>
      </Wrapper>
    );
  }

  return (
    <Wrapper className={cn(inline ? 'inline' : 'space-y-3', className)}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        skipHtml
        components={{
          // Heading levels emitted by the model are downgraded to bold paragraphs to
          // preserve the surrounding visual hierarchy.
          h1: ({ children }) => <p className="font-semibold">{children}</p>,
          h2: ({ children }) => <p className="font-semibold">{children}</p>,
          h3: ({ children }) => <p className="font-semibold">{children}</p>,
          h4: ({ children }) => <p className="font-semibold">{children}</p>,
          p: ({ children }) =>
            inline ? <>{children}</> : <p className="leading-relaxed">{children}</p>,
          strong: ({ children }) => (
            <strong className="font-semibold text-stone-900 dark:text-stone-100">{children}</strong>
          ),
          em: ({ children }) => <em className="italic">{children}</em>,
          code: ({ children }) => (
            <code className="rounded bg-stone-900/[0.05] px-1 py-[1px] font-mono text-[0.92em] text-stone-800 dark:bg-stone-100/[0.08] dark:text-stone-200">
              {children}
            </code>
          ),
          a: ({ children, href }) => (
            <a
              href={href}
              target="_blank"
              rel="noreferrer"
              className="text-accent-700 underline-offset-2 hover:underline dark:text-accent-400"
            >
              {children}
            </a>
          ),
          ul: ({ children }) => <ul className="mt-1 list-disc space-y-1 pl-5">{children}</ul>,
          ol: ({ children }) => <ol className="mt-1 list-decimal space-y-1 pl-5">{children}</ol>,
          li: ({ children }) => <li className="leading-relaxed">{children}</li>,
          blockquote: ({ children }) => (
            <blockquote className="border-l-2 border-stone-300 pl-3 italic text-stone-600 dark:border-stone-600 dark:text-stone-400">
              {children}
            </blockquote>
          ),
          hr: () => null,
        }}
      >
        {children}
      </ReactMarkdown>
    </Wrapper>
  );
};

/**
 * Progressively reveals <c>full</c> over time. The reveal animation runs when
 * <c>enabled</c> is true at the moment a new content string arrives, and is recorded
 * as completed for that exact string so subsequent renders do not replay it.
 */
function useTypewriter(full: string, enabled: boolean, speed: ProseSpeed, onComplete?: () => void): string {
  const [revealed, setRevealed] = useState(() => (enabled ? 0 : full.length));
  const completedRef = useRef<string | null>(null);
  const onCompleteRef = useRef(onComplete);
  const enabledRef = useRef(enabled);
  const speedRef = useRef(speed);

  // The reveal effect depends only on the content string, but reads the latest values
  // of the other parameters via refs. Refs are kept current via dedicated effects to
  // satisfy the rule prohibiting ref mutation during render.
  useEffect(() => { onCompleteRef.current = onComplete; }, [onComplete]);
  useEffect(() => { enabledRef.current = enabled; }, [enabled]);
  useEffect(() => { speedRef.current = speed; }, [speed]);

  useEffect(() => {
    if (completedRef.current === full) {
      setRevealed(full.length);
      return;
    }
    if (!enabledRef.current) {
      // Streaming is disabled: render the content immediately and mark this exact
      // string as already revealed, so a subsequent flip to enabled does not animate it.
      setRevealed(full.length);
      completedRef.current = full;
      return;
    }
    const cfg = SPEED_PRESETS[speedRef.current];
    setRevealed(0);
    const id = window.setInterval(() => {
      setRevealed((r) => {
        const next = r + cfg.chars;
        if (next >= full.length) {
          completedRef.current = full;
          window.clearInterval(id);
          onCompleteRef.current?.();
          return full.length;
        }
        return next;
      });
    }, cfg.ms);
    return () => window.clearInterval(id);
  }, [full]);

  return full.slice(0, revealed);
}
