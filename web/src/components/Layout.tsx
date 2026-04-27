import { useEffect, useRef, useState } from 'react';
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { del as idbDel } from 'idb-keyval';
import {
  LayoutDashboard,
  FileText,
  Briefcase,
  Sparkles,
  MessagesSquare,
  Settings as SettingsIcon,
  LogOut,
  Sun,
  Moon,
  Languages,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuthStore } from '@/stores/auth.store';
import { useThemeStore } from '@/stores/theme.store';
import { authApi } from '@/api/auth';
import { cn } from '@/lib/utils';
import { SUPPORTED_LANGUAGES, type SupportedLanguage } from '@/i18n';
import { BackendStatusBanner } from './BackendStatusBanner';
import { RouteErrorBoundary } from './RouteErrorBoundary';

/** App shell: top navigation, mobile bottom tab bar, and the routed <code>Outlet</code>. */
export const Layout = () => {
  const { t } = useTranslation();
  const { email, refreshToken, clear, setProfile, accessToken } = useAuthStore();
  const navigate = useNavigate();
  const location = useLocation();
  const qc = useQueryClient();

  // The auth store persists tokens and email but not displayName; refresh the profile
  // on mount so the greeting is restored after a hard reload.
  useEffect(() => {
    if (!accessToken) return;
    let cancelled = false;
    authApi
      .me()
      .then((me) => {
        if (cancelled) return;
        setProfile({ email: me.email, displayName: me.displayName });
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [accessToken, setProfile]);

  const primary = [
    { to: '/', label: t('nav.dashboard'), icon: LayoutDashboard, end: true },
    { to: '/resumes', label: t('nav.resumes'), icon: FileText },
    { to: '/vacancies', label: t('nav.vacancies'), icon: Briefcase },
    { to: '/matches', label: t('nav.matches'), icon: Sparkles },
    { to: '/sessions', label: t('nav.interview'), icon: MessagesSquare },
  ];

  const handleLogout = async () => {
    if (refreshToken) await authApi.logout(refreshToken).catch(() => {});
    clear();
    // Clear in-memory query cache and persisted snapshot to prevent the next session
    // on this device from seeing the previous account's data.
    qc.clear();
    idbDel('cf-rq-cache-v1').catch(() => { /* best effort */ });
    navigate('/login');
  };

  return (
    <div className="flex min-h-screen flex-col">
      {/* Top header */}
      <header
        className="sticky top-0 z-30 border-b border-stone-200/70 bg-[color:var(--color-canvas)]/80 backdrop-blur-md dark:border-stone-800/60 dark:bg-[color:var(--color-canvas-dark)]/80"
        style={{ paddingTop: 'env(safe-area-inset-top)' }}
      >
        <div
          className="mx-auto flex h-16 w-full max-w-6xl items-center gap-6 px-5 sm:px-8"
          style={{
            paddingLeft: 'max(1.25rem, env(safe-area-inset-left))',
            paddingRight: 'max(1.25rem, env(safe-area-inset-right))',
          }}
        >
          <Link to="/" className="flex items-center" aria-label={t('app.name')}>
            <span className="font-display text-[19px] font-medium tracking-tight text-stone-900 dark:text-stone-50">
              {t('app.name')}
            </span>
          </Link>

          <nav className="hidden flex-1 items-center justify-center gap-1 md:flex">
            {primary.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) =>
                  cn(
                    'group rounded-md px-3 py-1.5 text-[13.5px] tracking-tight transition-colors',
                    isActive
                      ? 'text-stone-900 dark:text-stone-100'
                      : 'text-stone-500 hover:text-stone-900 dark:text-stone-400 dark:hover:text-stone-100',
                  )
                }
              >
                {({ isActive }) => (
                  <span className="relative inline-block">
                    {item.label}
                    {/* Active links display a wide underline; inactive links display a
                        narrower underline that becomes visible on hover. */}
                    <span
                      aria-hidden
                      className={cn(
                        'absolute -bottom-[18px] left-1/2 h-px -translate-x-1/2 bg-accent-600 transition-all duration-200 dark:bg-accent-400',
                        isActive
                          ? 'w-6 opacity-100'
                          : 'w-3 opacity-0 group-hover:opacity-70',
                      )}
                    />
                  </span>
                )}
              </NavLink>
            ))}
          </nav>

          <div className="ml-auto flex items-center gap-2 md:ml-0">
            <UserMenu email={email} onLogout={handleLogout} />
          </div>
        </div>
        <BackendStatusBanner />
      </header>

      {/* Keying the main wrapper on pathname re-mounts it on each route change so the
          page-enter animation replays. Cached query data is restored from React Query
          immediately, so the remount is visually instantaneous. */}
      <main className="flex-1">
        <div
          key={location.pathname}
          className="mx-auto w-full max-w-4xl animate-page-enter pb-24 pt-10 sm:pb-12 sm:pt-14"
          style={{
            paddingLeft: 'max(1.25rem, env(safe-area-inset-left))',
            paddingRight: 'max(1.25rem, env(safe-area-inset-right))',
            paddingBottom: 'calc(env(safe-area-inset-bottom) + 6rem)',
          }}
        >
          {/* Per-route boundary keeps navigation usable when a single page throws.
              The boundary remounts via the key above, clearing the error on navigation. */}
          <RouteErrorBoundary>
            <Outlet />
          </RouteErrorBoundary>
        </div>
      </main>

      {/* Mobile bottom tab bar */}
      <nav
        className="fixed inset-x-0 bottom-0 z-30 border-t border-stone-200/70 bg-[color:var(--color-canvas)]/95 backdrop-blur-md md:hidden print:hidden dark:border-stone-800/60 dark:bg-[color:var(--color-canvas-dark)]/95"
        style={{ paddingBottom: 'env(safe-area-inset-bottom)' }}
      >
        <div className="mx-auto flex max-w-md items-stretch justify-around px-2">
          {primary.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) =>
                cn(
                  'flex flex-1 flex-col items-center gap-1 px-2 py-2.5 text-[10px] font-medium tracking-wide transition-colors',
                  isActive
                    ? 'text-accent-700 dark:text-accent-400'
                    : 'text-stone-500 hover:text-stone-700 dark:text-stone-500 dark:hover:text-stone-300',
                )
              }
            >
              <item.icon className="h-5 w-5" strokeWidth={1.75} />
              <span className="leading-tight">{item.label}</span>
            </NavLink>
          ))}
        </div>
      </nav>
    </div>
  );
};

const UserMenu = ({ email, onLogout }: { email: string | null; onLogout: () => void }) => {
  const { t, i18n } = useTranslation();
  const { theme, set } = useThemeStore();
  const displayName = useAuthStore((s) => s.displayName);
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onClick = (e: MouseEvent) => {
      if (!ref.current?.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false);
    document.addEventListener('mousedown', onClick);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onClick);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  const initial = ((displayName?.trim() || email || '?').charAt(0)).toUpperCase();
  const currentLang = (i18n.resolvedLanguage ?? i18n.language ?? 'en').split('-')[0] as SupportedLanguage;

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-label="Open user menu"
        aria-expanded={open}
        className="flex h-9 w-9 items-center justify-center rounded-full bg-stone-900/[0.04] text-sm font-medium tracking-tight text-stone-700 ring-1 ring-stone-900/[0.05] transition-colors hover:bg-stone-900/[0.08] dark:bg-stone-100/[0.06] dark:text-stone-200 dark:ring-stone-100/[0.08] dark:hover:bg-stone-100/[0.10]"
      >
        {initial}
      </button>

      {open && (
        <div className="absolute right-0 top-12 w-64 origin-top-right animate-menu-enter rounded-xl bg-white p-1.5 shadow-[0_4px_24px_-4px_rgba(28,25,23,0.16)] ring-1 ring-stone-900/[0.06] dark:bg-stone-900 dark:ring-stone-100/[0.08]">
          {email && (
            <div className="border-b border-stone-100 px-3 py-2.5 dark:border-stone-800/60">
              <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-stone-400 dark:text-stone-500">
                {t('settings.account')}
              </div>
              <div className="mt-0.5 truncate text-sm text-stone-900 dark:text-stone-100">{email}</div>
            </div>
          )}

          <div className="px-1 py-1.5">
            <button
              type="button"
              onClick={() => set(theme === 'dark' ? 'light' : 'dark')}
              className="flex w-full items-center gap-3 rounded-md px-2.5 py-2 text-sm text-stone-700 hover:bg-stone-100 dark:text-stone-200 dark:hover:bg-stone-800/60"
            >
              {theme === 'dark' ? <Sun className="h-4 w-4 text-stone-400" /> : <Moon className="h-4 w-4 text-stone-400" />}
              {theme === 'dark' ? t('nav.lightMode') : t('nav.darkMode')}
            </button>
          </div>

          <div className="border-t border-stone-100 px-1 py-1.5 dark:border-stone-800/60">
            <div className="flex items-center gap-2 px-2.5 py-1">
              <Languages className="h-4 w-4 text-stone-400" />
              <div className="flex gap-1">
                {SUPPORTED_LANGUAGES.map((lang) => (
                  <button
                    key={lang}
                    type="button"
                    onClick={() => i18n.changeLanguage(lang)}
                    className={cn(
                      'rounded px-2 py-0.5 text-[12px] font-medium uppercase tracking-wider transition-colors',
                      currentLang === lang
                        ? 'bg-stone-900/[0.06] text-stone-900 dark:bg-stone-100/[0.08] dark:text-stone-100'
                        : 'text-stone-500 hover:text-stone-900 dark:text-stone-400 dark:hover:text-stone-100',
                    )}
                  >
                    {lang}
                  </button>
                ))}
              </div>
            </div>
          </div>

          <div className="border-t border-stone-100 px-1 py-1.5 dark:border-stone-800/60">
            <Link
              to="/settings"
              onClick={() => setOpen(false)}
              className="flex items-center gap-3 rounded-md px-2.5 py-2 text-sm text-stone-700 hover:bg-stone-100 dark:text-stone-200 dark:hover:bg-stone-800/60"
            >
              <SettingsIcon className="h-4 w-4 text-stone-400" />
              {t('nav.settings')}
            </Link>
            <button
              type="button"
              onClick={onLogout}
              className="flex w-full items-center gap-3 rounded-md px-2.5 py-2 text-sm text-stone-700 hover:bg-stone-100 dark:text-stone-200 dark:hover:bg-stone-800/60"
            >
              <LogOut className="h-4 w-4 text-stone-400" />
              {t('nav.signOut')}
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
