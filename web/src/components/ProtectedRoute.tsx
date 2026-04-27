import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth.store';

/** Redirects to <code>/login</code> if the user isn't authenticated, preserving the attempted path in router state. */
export const ProtectedRoute = ({ children }: { children: ReactNode }) => {
  const isAuth = useAuthStore((s) => s.isAuthenticated());
  const location = useLocation();
  if (!isAuth) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  return <>{children}</>;
};
