import { api } from './client';
import type { VacancyListItem, VacancyResponse } from '@/types/api';

/** CRUD + reparse wrappers for the <code>/vacancies</code> endpoints. */
export const vacanciesApi = {
  list: () => api.get<VacancyListItem[]>('/vacancies').then((r) => r.data),
  get: (id: string) => api.get<VacancyResponse>(`/vacancies/${id}`).then((r) => r.data),
  create: (rawText: string, titleHint?: string) =>
    api.post<VacancyResponse>('/vacancies', { rawText, titleHint }).then((r) => r.data),
  reparse: (id: string) =>
    api.post<VacancyResponse>(`/vacancies/${id}/reparse`).then((r) => r.data),
  remove: (id: string) => api.delete(`/vacancies/${id}`).then((r) => r.data),
};
