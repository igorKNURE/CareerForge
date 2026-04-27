import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { CheckCircle2, XCircle, ArrowLeft } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';
import { Spinner } from '@/components/ui/Spinner';
import { authApi } from '@/api/auth';

type State = 'idle' | 'pending' | 'ok' | 'error';

/** Lands here from the email-confirmation link; shows a success or failure state. */
export const EmailConfirmPage = () => {
  const { t } = useTranslation();
  const [params] = useSearchParams();
  const email = params.get('email') ?? '';
  const token = params.get('token') ?? '';
  const [state, setState] = useState<State>('idle');
  const [message, setMessage] = useState<string>('');
  const calledRef = useRef(false);

  useEffect(() => {
    if (calledRef.current) return;
    calledRef.current = true;
    if (!email || !token) {
      setState('error');
      setMessage(t('auth.missingTokenInUrl'));
      return;
    }
    setState('pending');
    authApi
      .confirmEmail(email, token)
      .then(() => setState('ok'))
      .catch((e: unknown) => {
        const errs = (e as { response?: { data?: { errors?: Record<string, string[]> } } })
          .response?.data?.errors;
        const first = errs ? Object.values(errs)[0]?.[0] : null;
        setMessage(first ?? t('auth.couldNotConfirmEmail'));
        setState('error');
      });
  }, [email, token, t]);

  return (
    <div className="flex min-h-screen items-center justify-center px-4 py-10">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex justify-center">
          <span className="font-display text-[22px] font-medium tracking-tight text-stone-900 dark:text-stone-50">
            {t('app.name')}
          </span>
        </div>

        <Card className="p-8 text-center">
          {state === 'pending' && (
            <div className="py-4">
              <Spinner className="mx-auto h-8 w-8" />
              <p className="mt-4 text-[14.5px] leading-relaxed text-stone-500 dark:text-stone-400">
                {t('auth.confirmingEmail')}
              </p>
            </div>
          )}

          {state === 'ok' && (
            <>
              <CheckCircle2 className="mx-auto h-12 w-12 text-emerald-500" strokeWidth={1.5} />
              <h1 className="mt-4 font-display text-[28px] font-medium tracking-tight leading-snug text-stone-900 dark:text-stone-50">
                {t('auth.emailConfirmed')}
              </h1>
              <p className="mt-2 text-[14.5px] leading-relaxed text-stone-500 dark:text-stone-400">
                {t('auth.emailConfirmedSub')}
              </p>
              <Link to="/" className="mt-8 inline-block">
                <Button size="lg">{t('common.continue')}</Button>
              </Link>
            </>
          )}

          {state === 'error' && (
            <>
              <XCircle className="mx-auto h-12 w-12 text-red-500" strokeWidth={1.5} />
              <h1 className="mt-4 font-display text-[28px] font-medium tracking-tight leading-snug text-stone-900 dark:text-stone-50">
                {t('auth.emailConfirmFailed')}
              </h1>
              <p className="mt-2 text-[14.5px] leading-relaxed text-stone-500 dark:text-stone-400">{message}</p>
              <div className="mt-8 border-t border-stone-100 pt-5 dark:border-stone-800/60">
                <Link
                  to="/login"
                  className="inline-flex items-center gap-1.5 text-[13px] text-stone-500 transition-colors hover:text-accent-700 dark:text-stone-400 dark:hover:text-accent-400"
                >
                  <ArrowLeft className="h-3.5 w-3.5" /> {t('auth.backToSignIn')}
                </Link>
              </div>
            </>
          )}
        </Card>
      </div>
    </div>
  );
};
