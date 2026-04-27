import { api } from './client';
import type { MatchReportListItem, MatchReportResponse } from '@/types/api';

/** CRUD + rerun wrappers for the <code>/matches</code> endpoints. */
export const matchesApi = {
  list: () => api.get<MatchReportListItem[]>('/matches').then((r) => r.data),
  get: (id: string) => api.get<MatchReportResponse>(`/matches/${id}`).then((r) => r.data),
  create: (resumeId: string, jobDescriptionId: string) =>
    api.post<MatchReportResponse>('/matches', { resumeId, jobDescriptionId }).then((r) => r.data),
  rerun: (id: string) =>
    api.post<MatchReportResponse>(`/matches/${id}/rerun`).then((r) => r.data),
  remove: (id: string) => api.delete(`/matches/${id}`).then((r) => r.data),
};
