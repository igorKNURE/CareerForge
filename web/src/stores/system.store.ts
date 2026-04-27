import { create } from 'zustand';

interface SystemState {
  backendHealthy: boolean;
  /** Last unhealthy timestamp; helps suppress flicker on transient blips. */
  lastUnhealthyAt: number | null;
  setHealthy: () => void;
  setUnhealthy: () => void;
}

/** Tracks backend reachability so the app can render an offline banner. Updated by the axios response interceptor. */
export const useSystemStore = create<SystemState>((set, get) => ({
  backendHealthy: true,
  lastUnhealthyAt: null,
  setHealthy: () => {
    if (get().backendHealthy) return;
    set({ backendHealthy: true });
  },
  setUnhealthy: () => {
    set({ backendHealthy: false, lastUnhealthyAt: Date.now() });
  },
}));
