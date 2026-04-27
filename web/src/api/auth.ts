import { api } from './client';
import type { AuthResponse, MeResponse } from '@/types/api';

/** Thin wrappers around the <code>/auth/*</code> endpoints (register, login, refresh, password reset, email confirmation). */
export const authApi = {
  register: (email: string, password: string, displayName?: string, captchaToken?: string) =>
    api
      .post<AuthResponse>('/auth/register', { email, password, displayName, captchaToken })
      .then((r) => r.data),
  login: (email: string, password: string) =>
    api.post<AuthResponse>('/auth/login', { email, password }).then((r) => r.data),
  me: () => api.get<MeResponse>('/auth/me').then((r) => r.data),
  logout: (refreshToken: string) =>
    api.post('/auth/logout', { refreshToken }).then((r) => r.data),
  forgotPassword: (email: string) =>
    api
      .post<{ message: string; devToken: string | null }>('/auth/forgot-password', { email })
      .then((r) => r.data),
  resetPassword: (email: string, token: string, newPassword: string) =>
    api.post('/auth/reset-password', { email, token, newPassword }).then((r) => r.data),
  sendEmailConfirm: (email: string) =>
    api
      .post<{ message: string; devToken: string | null }>('/auth/email/send-confirm', { email })
      .then((r) => r.data),
  confirmEmail: (email: string, token: string) =>
    api.post('/auth/email/confirm', { email, token }).then((r) => r.data),
};
