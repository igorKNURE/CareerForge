import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Link } from 'react-router-dom';
import { Copy, ArrowLeft } from 'lucide-react';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';
import { Input, Label, FieldError } from '@/components/ui/Input';
import { Spinner } from '@/components/ui/Spinner';
import { authApi } from '@/api/auth';

/** "Send password reset link" form. */
export const ForgotPasswordPage = () => {
  const { t } = useTranslation();
  const [submitted, setSubmitted] = useState(false);
  const [devToken, setDevToken] = useState<string | null>(null);

  const schema = z.object({ email: z.string().email(t('auth.validation.emailInvalid')) });
  type FormValues = z.infer<typeof schema>;

  const { register, handleSubmit, getValues, formState } = useForm<FormValues>({ resolver: zodResolver(schema) });

  const onSubmit = async (values: FormValues) => {
    try {
      const r = await authApi.forgotPassword(values.email);
      setDevToken(r.devToken);
      setSubmitted(true);
    } catch {
      toast.error(t('auth.couldNotRequestReset'));
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center px-4 py-10">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex justify-center">
          <span className="font-display text-[22px] font-medium tracking-tight text-stone-900 dark:text-stone-50">
            {t('app.name')}
          </span>
        </div>

        <Card className="p-8">
          <h1 className="font-display text-[28px] font-medium tracking-tight leading-snug text-stone-900 dark:text-stone-50">
            {t('auth.forgotPasswordTitle')}
          </h1>
          <p className="mt-2 text-[14.5px] leading-relaxed text-stone-500 dark:text-stone-400">
            {t('auth.forgotPasswordSub')}
          </p>

          {!submitted ? (
            <form onSubmit={handleSubmit(onSubmit)} className="mt-8 space-y-5">
              <div>
                <Label htmlFor="email">{t('auth.email')}</Label>
                <Input id="email" type="email" autoComplete="email" {...register('email')} />
                <FieldError message={formState.errors.email?.message} />
              </div>
              <Button type="submit" size="lg" className="w-full" disabled={formState.isSubmitting}>
                {formState.isSubmitting ? <Spinner /> : t('auth.sendResetLink')}
              </Button>
            </form>
          ) : (
            <div className="mt-8 space-y-4 text-[14.5px]">
              <p className="leading-relaxed text-stone-700 dark:text-stone-300">
                {t('auth.ifAccountExistsFor', { email: getValues('email') })}
              </p>
              {devToken && (
                <DevTokenBlock
                  email={getValues('email')}
                  token={devToken}
                  resetPath="/reset-password"
                  label={t('auth.devTokenLabel')}
                />
              )}
            </div>
          )}

          <div className="mt-8 border-t border-stone-100 pt-5 dark:border-stone-800/60">
            <Link
              to="/login"
              className="inline-flex items-center gap-1.5 text-[13px] text-stone-500 transition-colors hover:text-accent-700 dark:text-stone-400 dark:hover:text-accent-400"
            >
              <ArrowLeft className="h-3.5 w-3.5" /> {t('auth.backToSignIn')}
            </Link>
          </div>
        </Card>
      </div>
    </div>
  );
};

/** Renders the dev-only token + copy button shown when SMTP is not configured. */
export const DevTokenBlock = ({
  email, token, resetPath, label,
}: {
  email: string;
  token: string;
  resetPath: string;
  label: string;
}) => {
  const { t } = useTranslation();
  const url = `${window.location.origin}${resetPath}?email=${encodeURIComponent(email)}&token=${encodeURIComponent(token)}`;
  return (
    <div className="rounded-lg bg-amber-50/60 p-4 text-xs ring-1 ring-amber-200/60 dark:bg-amber-950/20 dark:ring-amber-900/40">
      <div className="mb-1.5 text-[10px] font-semibold uppercase tracking-[0.18em] text-amber-700 dark:text-amber-300">
        {label} {t('common.devOnlySuffix')}
      </div>
      <p className="leading-relaxed text-amber-800/90 dark:text-amber-200/90">
        {t('auth.devTokenHint')}
      </p>
      <a
        href={url}
        className="mt-2.5 block break-all font-mono text-[11px] text-amber-700 underline decoration-amber-300 underline-offset-2 hover:text-amber-900 dark:text-amber-300 dark:decoration-amber-700"
      >
        {url}
      </a>
      <Button
        variant="ghost"
        size="sm"
        className="mt-3 text-amber-800 hover:bg-amber-100/60 dark:text-amber-200 dark:hover:bg-amber-900/30"
        onClick={() => {
          navigator.clipboard.writeText(url);
          toast.success(t('auth.linkCopied'));
        }}
      >
        <Copy className="h-3 w-3" /> {t('auth.copyLink')}
      </Button>
    </div>
  );
};
