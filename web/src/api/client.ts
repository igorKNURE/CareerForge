import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import i18n from '@/i18n';
import { useAuthStore } from '@/stores/auth.store';
import { useSystemStore } from '@/stores/system.store';
import type { AuthResponse } from '@/types/api';

/**
 * API base URL. Defaults to <c>/api</c> for browser deployments behind a reverse proxy
 * or the Vite dev proxy. Override with <c>VITE_API_BASE</c> for builds where the
 * frontend cannot share an origin with the API (for example, the Capacitor iOS shell).
 */
export const API_BASE = import.meta.env.VITE_API_BASE || '/api';

/**
 * Shared axios client. Attaches the bearer token + UI language on every request,
 * tracks backend health, and transparently rotates a stale access token via the
 * refresh endpoint on a single 401 retry.
 */
export const api = axios.create({ baseURL: API_BASE });

/** Two-letter language code derived from i18n state (e.g. "en-US" → "en"). */
export const currentUiLanguage = (): string =>
  (i18n.resolvedLanguage ?? i18n.language ?? 'en').split('-')[0];

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = useAuthStore.getState().accessToken;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  config.headers['Accept-Language'] = currentUiLanguage();
  return config;
});

let refreshPromise: Promise<string | null> | null = null;

async function refreshAccessToken(): Promise<string | null> {
  if (refreshPromise) return refreshPromise;
  const { refreshToken } = useAuthStore.getState();
  if (!refreshToken) return null;
  refreshPromise = (async () => {
    try {
      const { data } = await axios.post<AuthResponse>(`${API_BASE}/auth/refresh`, { refreshToken });
      useAuthStore.getState().setTokens(data);
      return data.accessToken;
    } catch {
      useAuthStore.getState().clear();
      return null;
    } finally {
      refreshPromise = null;
    }
  })();
  return refreshPromise;
}

api.interceptors.response.use(
  (r) => {
    useSystemStore.getState().setHealthy();
    return r;
  },
  async (error: AxiosError) => {
    // A network failure (no response) or any 5xx response indicates the backend is
    // not currently serving. Any other response — including 4xx — means the server
    // replied successfully and is therefore healthy from the client's perspective.
    const status = error.response?.status;
    const isNetwork = !error.response;
    const isServerError = typeof status === 'number' && status >= 500 && status < 600;
    if (isNetwork || isServerError) {
      useSystemStore.getState().setUnhealthy();
    } else {
      useSystemStore.getState().setHealthy();
    }

    const original = error.config as InternalAxiosRequestConfig & { _retried?: boolean };
    if (error.response?.status === 401 && original && !original._retried && original.url !== '/auth/refresh') {
      original._retried = true;
      const newToken = await refreshAccessToken();
      if (newToken) {
        original.headers.Authorization = `Bearer ${newToken}`;
        return api.request(original);
      }
    }
    return Promise.reject(error);
  },
);
