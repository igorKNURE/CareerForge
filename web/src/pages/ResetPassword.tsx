import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';
import { Input, Label, FieldError } from '@/components/ui/Input';
import { Spinner } from '@/components/ui/Spinner';
import { authApi } from '@/api/auth';

/** Completes a password reset using the token delivered by email. */
export const ResetPasswordPage = () => {
  const { t } = useTranslation();
  const [params] = useSearchParams();
  const navigate = useNavigate();

  const schema = z.object({
    email: z.string().email(t('auth.validation.emailInvalid')),
    token: z.string().min(1, t('auth.validation.tokenRequired')),
    newPassword: z
      .string()
      .min(8, t('auth.validation.passwordMinLength'))
      .regex(/[A-Z]/, t('auth.validation.passwordUppercase'))
      .regex(/[a-z]/, t('auth.validation.passwordLowercase'))
      .regex(/\d/, t('auth.validation.passwordDigit')),
  });
  type FormValues = z.infer<typeof schema>;

  const { register, handleSubmit, setValue, formState } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      email: params.get('email') ?? '',
      token: params.get('token') ?? '',
    },
  });

  useEffect(() => {
    const email = params.get('email');
    const token = params.get('token');
    if (email) setValue('email', email);
    if (token) setValue('token', token);
  }, [params, setValue]);

  const onSubmit = async (values: FormValues) => {
    try {
      await authApi.resetPassword(values.email, values.token, values.newPassword);
      toast.success(t('auth.passwordUpdated'));
      navigate('/login');
    } catch (e: unknown) {
      const errs = (e as { response?: { data?: { errors?: Record<string, string[]> } } }).response?.data?.errors;
      const first = errs ? Object.values(errs)[0]?.[0] : null;
      toast.error(first ?? t('auth.resetFailed'));
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
            {t('auth.resetPassword')}
          </h1>
          <p className="mt-2 text-[14.5px] leading-relaxed text-stone-500 dark:text-stone-400">
            {t('auth.resetPasswordSub')}
          </p>

          <form onSubmit={handleSubmit(onSubmit)} className="mt-8 space-y-5">
            <div>
              <Label htmlFor="email">{t('auth.email')}</Label>
              <Input id="email" type="email" autoComplete="email" {...register('email')} />
              <FieldError message={formState.errors.email?.message} />
            </div>
            <div>
              <Label htmlFor="token">{t('auth.resetTokenLabel')}</Label>
              <Input id="token" type="text" {...register('token')} className="font-mono text-xs" />
              <FieldError message={formState.errors.token?.message} />
            </div>
            <div>
              <Label htmlFor="newPassword">{t('auth.newPassword')}</Label>
              <Input id="newPassword" type="password" autoComplete="new-password" {...register('newPassword')} />
              <FieldError message={formState.errors.newPassword?.message} />
            </div>

            <Button type="submit" size="lg" className="w-full" disabled={formState.isSubmitting}>
              {formState.isSubmitting ? <Spinner /> : t('auth.updatePassword')}
            </Button>
          </form>

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
