import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Link, useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';
import { Input, Label, FieldError } from '@/components/ui/Input';
import { authApi } from '@/api/auth';
import { useAuthStore } from '@/stores/auth.store';
import { Spinner } from '@/components/ui/Spinner';

/** Account creation form; on success drops the user straight into the app via the auth store. */
export const RegisterPage = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const setTokens = useAuthStore((s) => s.setTokens);

  const schema = z.object({
    email: z.string().email(t('auth.validation.emailInvalid')),
    displayName: z.string().trim().max(64, t('auth.validation.displayNameTooLong')).optional(),
    password: z
      .string()
      .min(8, t('auth.validation.passwordMinLength'))
      .regex(/[A-Z]/, t('auth.validation.passwordUppercase'))
      .regex(/[a-z]/, t('auth.validation.passwordLowercase'))
      .regex(/\d/, t('auth.validation.passwordDigit')),
  });
  type FormValues = z.infer<typeof schema>;

  const { register, handleSubmit, formState } = useForm<FormValues>({
    resolver: zodResolver(schema),
  });

  const setProfile = useAuthStore((s) => s.setProfile);
  const onSubmit = async (values: FormValues) => {
    try {
      const trimmedName = values.displayName?.trim() || undefined;
      const auth = await authApi.register(values.email, values.password, trimmedName);
      setTokens(auth, values.email);
      setProfile({ email: values.email, displayName: trimmedName ?? null });
      toast.success(t('auth.accountCreated'));
      navigate('/');
    } catch (e: unknown) {
      const msg =
        (e as { response?: { data?: { errors?: Record<string, string[]> } } }).response?.data?.errors;
      const first = msg ? Object.values(msg)[0]?.[0] : null;
      toast.error(first ?? t('auth.registrationFailed'));
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
            {t('auth.createAccount')}
          </h1>
          <p className="mt-2 text-[14.5px] leading-relaxed text-stone-500 dark:text-stone-400">
            {t('auth.createAccountSub')}
          </p>

          <form onSubmit={handleSubmit(onSubmit)} className="mt-8 space-y-5">
            <div>
              <Label htmlFor="email">{t('auth.email')}</Label>
              <Input id="email" type="email" autoComplete="email" {...register('email')} />
              <FieldError message={formState.errors.email?.message} />
            </div>
            <div>
              <Label htmlFor="displayName">
                {t('auth.displayName')}
                <span className="ml-1 text-stone-400 dark:text-stone-500">{t('auth.optional')}</span>
              </Label>
              <Input
                id="displayName"
                autoComplete="given-name"
                placeholder={t('auth.displayNamePlaceholder')}
                {...register('displayName')}
              />
              <FieldError message={formState.errors.displayName?.message} />
            </div>
            <div>
              <Label htmlFor="password">{t('auth.password')}</Label>
              <Input id="password" type="password" autoComplete="new-password" {...register('password')} />
              <FieldError message={formState.errors.password?.message} />
            </div>

            <Button type="submit" size="lg" className="w-full" disabled={formState.isSubmitting}>
              {formState.isSubmitting ? <Spinner /> : t('auth.createAccount')}
            </Button>
          </form>

          <p className="mt-8 border-t border-stone-100 pt-5 text-center text-[13px] text-stone-500 dark:border-stone-800/60 dark:text-stone-400">
            {t('auth.alreadyHaveAccount')}{' '}
            <Link
              to="/login"
              className="font-medium text-stone-700 transition-colors hover:text-accent-700 dark:text-stone-200 dark:hover:text-accent-400"
            >
              {t('auth.signIn')}
            </Link>
          </p>
        </Card>
      </div>
    </div>
  );
};
