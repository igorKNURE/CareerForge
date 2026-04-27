import { Component, type ErrorInfo, type ReactNode } from 'react';
import { AlertTriangle, RefreshCw } from 'lucide-react';
import { withTranslation, type WithTranslation } from 'react-i18next';
import * as Sentry from '@sentry/react';
import { Button } from '@/components/ui/Button';

interface Props extends WithTranslation {
  children: ReactNode;
}

interface State {
  error: Error | null;
}

class ErrorBoundaryInner extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unhandled UI error', error, info);
    Sentry.captureException(error, { extra: { componentStack: info.componentStack } });
  }

  reset = () => {
    this.setState({ error: null });
    window.location.reload();
  };

  render() {
    const { t } = this.props;
    if (!this.state.error) return this.props.children;
    return (
      <div className="flex min-h-screen items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-md rounded-lg border border-red-200 bg-white p-8 text-center dark:border-red-900/60 dark:bg-zinc-900">
          <AlertTriangle className="mx-auto h-10 w-10 text-red-500" />
          <h1 className="mt-3 text-xl font-semibold text-zinc-900 dark:text-zinc-50">
            {t('errors.somethingBroke')}
          </h1>
          <p className="mt-2 text-sm text-zinc-500 dark:text-zinc-400">
            {this.state.error.message ?? t('errors.unexpected')}
          </p>
          <Button onClick={this.reset} className="mt-6">
            <RefreshCw className="h-4 w-4" /> {t('common.reload')}
          </Button>
        </div>
      </div>
    );
  }
}

/** Top-level React error boundary; renders a localized fallback with a "reload" CTA. */
export const ErrorBoundary = withTranslation()(ErrorBoundaryInner);
