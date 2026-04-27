import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AuthResponse } from '@/types/api';

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  expiresAt: string | null;
  refreshExpiresAt: string | null;
  email: string | null;
  displayName: string | null;
  setTokens: (auth: AuthResponse, email?: string) => void;
  setProfile: (profile: { email: string; displayName: string | null }) => void;
  clear: () => void;
  isAuthenticated: () => boolean;
}

/**
 * Persisted auth state — tokens, expiry, email, and display name. <code>isAuthenticated</code>
 * treats a token as expired 5 seconds before its real expiry so we trigger a refresh proactively.
 */
export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      accessToken: null,
      refreshToken: null,
      expiresAt: null,
      refreshExpiresAt: null,
      email: null,
      displayName: null,
      setTokens: (auth, email) =>
        set({
          accessToken: auth.accessToken,
          refreshToken: auth.refreshToken,
          expiresAt: auth.expiresAt,
          refreshExpiresAt: auth.refreshExpiresAt,
          email: email ?? get().email,
        }),
      setProfile: (profile) =>
        set({ email: profile.email, displayName: profile.displayName }),
      clear: () =>
        set({
          accessToken: null,
          refreshToken: null,
          expiresAt: null,
          refreshExpiresAt: null,
          email: null,
          displayName: null,
        }),
      isAuthenticated: () => {
        const { accessToken, expiresAt } = get();
        if (!accessToken || !expiresAt) return false;
        return new Date(expiresAt).getTime() > Date.now() - 5_000;
      },
    }),
    { name: 'careerforge.auth' },
  ),
);
