import { api } from './client';
import type { ResumeListItem, ResumeResponse } from '@/types/api';

/** CRUD + reparse wrappers for the <code>/resumes</code> endpoints. */
export const resumesApi = {
  list: () => api.get<ResumeListItem[]>('/resumes').then((r) => r.data),
  get: (id: string) => api.get<ResumeResponse>(`/resumes/${id}`).then((r) => r.data),
  upload: (file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    return api
      .post<ResumeResponse>('/resumes', fd, { headers: { 'Content-Type': 'multipart/form-data' } })
      .then((r) => r.data);
  },
  reparse: (id: string) =>
    api.post<ResumeResponse>(`/resumes/${id}/reparse`).then((r) => r.data),
  remove: (id: string) => api.delete(`/resumes/${id}`).then((r) => r.data),
};
