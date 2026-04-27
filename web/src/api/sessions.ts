import { api } from './client';
import type { SessionListItem, SessionResponse, TurnResponse } from '@/types/api';

/** Wrappers for the <code>/sessions</code> endpoints driving an interview turn-by-turn. */
export const sessionsApi = {
  list: () => api.get<SessionListItem[]>('/sessions').then((r) => r.data),
  get: (id: string) => api.get<SessionResponse>(`/sessions/${id}`).then((r) => r.data),
  create: (resumeId: string, jobDescriptionId: string, name?: string, language?: string) =>
    api
      .post<SessionResponse>('/sessions', { resumeId, jobDescriptionId, name, language })
      .then((r) => r.data),
  remove: (id: string) => api.delete(`/sessions/${id}`).then((r) => r.data),
  rename: (id: string, name: string) =>
    api.patch<SessionListItem>(`/sessions/${id}`, { name }).then((r) => r.data),
  generateQuestion: (sessionId: string) =>
    api.post<TurnResponse>(`/sessions/${sessionId}/turns`).then((r) => r.data),
  submitAnswer: (sessionId: string, turnId: string, answerText: string) =>
    api
      .post<TurnResponse>(`/sessions/${sessionId}/turns/${turnId}/answer`, { answerText })
      .then((r) => r.data),
};
