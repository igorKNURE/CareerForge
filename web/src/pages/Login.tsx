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

/** Email + password login form; redirects back to the originally requested route on success. */
export const LoginPage = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const setTokens = useAuthStore((s) => s.setTokens);

  const schema = z.object({
    email: z.string().email(t('auth.validation.emailInvalid')),
    password: z.string().min(1, t('auth.validation.passwordRequired')),
  });
  type FormValues = z.infer<typeof schema>;

  const { register, handleSubmit, formState } = useForm<FormValues>({
    resolver: zodResolver(schema),
  });

  const onSubmit = async (values: FormValues) => {
    try {
      const auth = await authApi.login(values.email, values.password);
      setTokens(auth, values.email);
      navigate('/');
    } catch {
      toast.error(t('auth.invalidCredentials'));
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
            {t('auth.signIn')}
          </h1>
          <p className="mt-2 text-[14.5px] leading-relaxed text-stone-500 dark:text-stone-400">
            {t('auth.signInWelcome')}
          </p>

          <form onSubmit={handleSubmit(onSubmit)} className="mt-8 space-y-5">
            <div>
              <Label htmlFor="email">{t('auth.email')}</Label>
              <Input id="email" type="email" autoComplete="email" {...register('email')} />
              <FieldError message={formState.errors.email?.message} />
            </div>
            <div>
              <Label htmlFor="password">{t('auth.password')}</Label>
              <Input id="password" type="password" autoComplete="current-password" {...register('password')} />
              <FieldError message={formState.errors.password?.message} />
            </div>

            <Button type="submit" size="lg" className="w-full" disabled={formState.isSubmitting}>
              {formState.isSubmitting ? <Spinner /> : t('auth.signIn')}
            </Button>
          </form>

          <div className="mt-8 flex items-center justify-between border-t border-stone-100 pt-5 text-[13px] dark:border-stone-800/60">
            <Link
              to="/forgot-password"
              className="text-stone-500 transition-colors hover:text-accent-700 dark:text-stone-400 dark:hover:text-accent-400"
            >
              {t('auth.forgotPassword')}
            </Link>
            <Link
              to="/register"
              className="text-stone-500 transition-colors hover:text-accent-700 dark:text-stone-400 dark:hover:text-accent-400"
            >
              {t('auth.createAccount')}
            </Link>
          </div>
        </Card>
      </div>
    </div>
  );
};
