import type { CapacitorConfig } from '@capacitor/cli';

/**
 * Capacitor configuration for the iOS shell.
 *
 * The web bundle is loaded from the capacitor:// scheme, so backend URLs are absolute
 * and supplied at build time via VITE_API_BASE.
 */
const config: CapacitorConfig = {
  appId: 'com.careerforge.app',
  appName: 'CareerForge',
  webDir: 'dist',
  // Background colour matches the application canvas so the WebView never paints
  // white into the status-bar zone, home-indicator zone, or rubber-band overscroll.
  backgroundColor: '#1b1816',
  ios: {
    // Allows the WebView to extend behind the status bar and home indicator. CSS
    // padding via env(safe-area-inset-*) keeps content clear of those regions.
    contentInset: 'never',
    backgroundColor: '#1b1816',
    scrollEnabled: true,
  },
};

export default config;
