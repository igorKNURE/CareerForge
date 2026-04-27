import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Sun, Moon, Mail, KeyRound, Languages } from 'lucide-react';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';
import { useAuthStore } from '@/stores/auth.store';
import { useThemeStore } from '@/stores/theme.store';
import { authApi } from '@/api/auth';
import { PageHeader } from '@/components/PageHeader';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Spinner } from '@/components/ui/Spinner';
import { DevTokenBlock } from '@/pages/ForgotPassword';
import { SUPPORTED_LANGUAGES, type SupportedLanguage } from '@/i18n';

/** Account preferences: display name, language, and theme. */
export const SettingsPage = () => {
  const { t, i18n } = useTranslation();
  const { email } = useAuthStore();
  const { theme, set } = useThemeStore();
  const [sending, setSending] = useState(false);
  const [confirmDevToken, setConfirmDevToken] = useState<string | null>(null);

  const currentLang = (i18n.resolvedLanguage ?? i18n.language ?? 'en').split('-')[0] as SupportedLanguage;

  const sendConfirm = async () => {
    if (!email) return;
    setSending(true);
    try {
      const r = await authApi.sendEmailConfirm(email);
      setConfirmDevToken(r.devToken);
      toast.success(t('auth.confirmationSent'));
    } catch {
      toast.error(t('auth.couldNotSendConfirmation'));
    } finally {
      setSending(false);
    }
  };

  return (
    <>
      <PageHeader title={t('settings.title')} description={t('settings.subtitle')} />

      <div className="space-y-6">
        <Card className="p-5">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
            {t('settings.account')}
          </h2>
          <div className="mt-3 flex items-center justify-between text-sm">
            <span className="text-zinc-500 dark:text-zinc-400">{t('auth.email')}</span>
            <span className="font-medium text-zinc-900 dark:text-zinc-100">{email}</span>
          </div>
        </Card>

        <Card className="p-5">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
            {t('settings.appearance')}
          </h2>
          <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">
            {t('settings.appearanceSub')}
          </p>
          <div className="mt-4 flex gap-2">
            <Button
              variant={theme === 'dark' ? 'primary' : 'secondary'}
              onClick={() => set('dark')}
            >
              <Moon className="h-4 w-4" /> {t('settings.dark')}
            </Button>
            <Button
              variant={theme === 'light' ? 'primary' : 'secondary'}
              onClick={() => set('light')}
            >
              <Sun className="h-4 w-4" /> {t('settings.light')}
            </Button>
          </div>
        </Card>

        <Card className="p-5">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
            {t('settings.language')}
          </h2>
          <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">
            {t('settings.languageSub')}
          </p>
          <div className="mt-4 flex gap-2">
            {SUPPORTED_LANGUAGES.map((lang) => (
              <Button
                key={lang}
                variant={currentLang === lang ? 'primary' : 'secondary'}
                onClick={() => i18n.changeLanguage(lang)}
              >
                <Languages className="h-4 w-4" />
                {lang === 'en' ? t('settings.english') : t('settings.ukrainian')}
              </Button>
            ))}
          </div>
        </Card>

        <Card className="p-5">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
            {t('auth.emailConfirm')}
          </h2>
          <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">
            {t('auth.emailConfirmHelp')}
          </p>
          <div className="mt-4">
            <Button variant="secondary" onClick={sendConfirm} disabled={sending}>
              {sending ? <Spinner /> : <Mail className="h-4 w-4" />}
              {t('auth.sendConfirmationEmail')}
            </Button>
          </div>
          {confirmDevToken && email && (
            <div className="mt-3">
              <DevTokenBlock
                email={email}
                token={confirmDevToken}
                resetPath="/email/confirm"
                label={t('auth.devTokenConfirmLabel')}
              />
            </div>
          )}
        </Card>

        <Card className="p-5">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
            {t('settings.passwordSection')}
          </h2>
          <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">
            {t('settings.passwordSub')}
          </p>
          <div className="mt-4">
            <Link to="/forgot-password">
              <Button variant="secondary">
                <KeyRound className="h-4 w-4" /> {t('auth.resetPassword')}
              </Button>
            </Link>
          </div>
        </Card>
      </div>
    </>
  );
};
