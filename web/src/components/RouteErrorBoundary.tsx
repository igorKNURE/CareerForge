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

/**
 * Per-route error boundary. Renders an in-line error card within the layout shell so
 * navigation remains usable. The application-level boundary in <c>App.tsx</c> covers
 * exceptions thrown outside the routed view.
 */
class RouteErrorBoundaryInner extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Route-level UI error', error, info);
    Sentry.captureException(error, {
      tags: { boundary: 'route' },
      extra: { componentStack: info.componentStack },
    });
  }

  reset = () => this.setState({ error: null });

  render() {
    const { t } = this.props;
    if (!this.state.error) return this.props.children;
    return (
      <div className="mx-auto flex max-w-lg flex-col items-center py-16 text-center">
        <AlertTriangle className="h-8 w-8 text-red-500" strokeWidth={1.75} />
        <h1 className="mt-4 font-display text-xl font-medium text-stone-900 dark:text-stone-50">
          {t('errors.somethingBroke')}
        </h1>
        <p className="mt-2 max-w-sm text-sm text-stone-500 dark:text-stone-400">
          {this.state.error.message || t('errors.unexpected')}
        </p>
        <Button onClick={this.reset} variant="secondary" className="mt-6">
          <RefreshCw className="h-4 w-4" /> {t('errors.retry')}
        </Button>
      </div>
    );
  }
}

export const RouteErrorBoundary = withTranslation()(RouteErrorBoundaryInner);
