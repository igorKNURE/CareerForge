import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import * as Sentry from '@sentry/react';
import './index.css';
import './i18n';
import { App } from './App';

// Sentry is opt-in via build-time configuration. When VITE_SENTRY_DSN is unset the SDK
// is not initialised.
const dsn = import.meta.env.VITE_SENTRY_DSN;
if (dsn) {
  Sentry.init({
    dsn,
    environment: import.meta.env.MODE,
    tracesSampleRate: 0,
    sendDefaultPii: false,
  });
}

// Service worker registration is restricted to production browser environments.
// Capacitor builds serve from `capacitor://` and bundle assets into the native binary,
// making a service worker redundant.
const isCapacitor = typeof window !== 'undefined'
  && (window.location.protocol === 'capacitor:' || 'Capacitor' in window);
if ('serviceWorker' in navigator && import.meta.env.PROD && !isCapacitor) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      console.warn('Service worker registration failed');
    });
  });
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
