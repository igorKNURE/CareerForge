import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { QueryClient } from '@tanstack/react-query';
import { PersistQueryClientProvider } from '@tanstack/react-query-persist-client';
import { createAsyncStoragePersister } from '@tanstack/query-async-storage-persister';
import { get as idbGet, set as idbSet, del as idbDel } from 'idb-keyval';
import { Toaster } from 'sonner';
import { Layout } from '@/components/Layout';
import { ProtectedRoute } from '@/components/ProtectedRoute';
import { ErrorBoundary } from '@/components/ErrorBoundary';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { LoginPage } from '@/pages/Login';
import { RegisterPage } from '@/pages/Register';
import { ForgotPasswordPage } from '@/pages/ForgotPassword';
import { ResetPasswordPage } from '@/pages/ResetPassword';
import { EmailConfirmPage } from '@/pages/EmailConfirm';
import { DashboardPage } from '@/pages/Dashboard';
import { ResumesPage } from '@/pages/Resumes';
import { VacanciesPage } from '@/pages/Vacancies';
import { MatchesPage } from '@/pages/Matches';
import { MatchDetailPage } from '@/pages/MatchDetail';
import { SessionsPage } from '@/pages/Sessions';
import { SessionDetailPage } from '@/pages/SessionDetail';
import { SettingsPage } from '@/pages/Settings';
import { useThemeStore } from '@/stores/theme.store';

// gcTime is set well above the React Query default to ensure entries remain in memory
// long enough for the persister to write them to IndexedDB and rehydrate them on
// subsequent launches.
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      retry: 1,
      gcTime: 1000 * 60 * 60 * 24 * 7,
    },
  },
});

// React Query cache is persisted to IndexedDB. Compared to localStorage, IndexedDB
// provides higher capacity (~50 MB+) and asynchronous I/O.
const IDB_KEY = 'cf-rq-cache-v1';
const persister = createAsyncStoragePersister({
  storage: {
    getItem: (key: string) => idbGet<string>(key).then((v) => v ?? null),
    setItem: (key: string, value: string) => idbSet(key, value),
    removeItem: (key: string) => idbDel(key),
  },
  key: IDB_KEY,
});

// Discard any legacy localStorage cache snapshot from the previous persistence backend.
if (typeof window !== 'undefined') {
  try { window.localStorage.removeItem('cf-rq-cache-v1'); } catch { /* no-op */ }
}

const persistOptions = {
  persister,
  maxAge: 1000 * 60 * 60 * 24 * 7,
};

/** Composition root: providers, router, global dialogs, and toaster. */
export const App = () => {
  const theme = useThemeStore((s) => s.theme);
  return (
    <ErrorBoundary>
      <PersistQueryClientProvider client={queryClient} persistOptions={persistOptions}>
        <BrowserRouter>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route path="/forgot-password" element={<ForgotPasswordPage />} />
            <Route path="/reset-password" element={<ResetPasswordPage />} />
            <Route path="/email/confirm" element={<EmailConfirmPage />} />

            <Route element={<ProtectedRoute><Layout /></ProtectedRoute>}>
              <Route index element={<DashboardPage />} />
              <Route path="resumes" element={<ResumesPage />} />
              <Route path="vacancies" element={<VacanciesPage />} />
              <Route path="matches" element={<MatchesPage />} />
              <Route path="matches/:id" element={<MatchDetailPage />} />
              <Route path="sessions" element={<SessionsPage />} />
              <Route path="sessions/:id" element={<SessionDetailPage />} />
              <Route path="settings" element={<SettingsPage />} />
            </Route>

            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </BrowserRouter>
        <ConfirmDialog />
        <Toaster position="bottom-right" richColors closeButton theme={theme} />
      </PersistQueryClientProvider>
    </ErrorBoundary>
  );
};
