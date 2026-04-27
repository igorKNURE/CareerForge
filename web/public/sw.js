/**
 * Service worker for the PWA installation. Implements three caching strategies:
 *
 *   - Static assets: cache-first. Hashed asset filenames cause stale entries to be
 *     evicted naturally as new builds are deployed.
 *   - Navigation requests: network-first with a cached index.html fallback, so the
 *     application shell remains available offline.
 *   - API requests: network-only. The data layer is cached by React Query.
 *
 * Capacitor builds bundle assets natively and skip service-worker registration.
 */

const CACHE_NAME = 'cf-shell-v1';
const APP_SHELL = ['/', '/index.html', '/manifest.webmanifest', '/favicon.svg'];

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE_NAME).then((c) => c.addAll(APP_SHELL)));
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k))),
    ),
  );
  self.clients.claim();
});

self.addEventListener('fetch', (event) => {
  const req = event.request;
  if (req.method !== 'GET') return;

  const url = new URL(req.url);

  // API traffic is excluded; the data layer is cached by React Query.
  if (url.pathname.startsWith('/api/') || /\/(matches|sessions|resumes|vacancies|auth)(\b|\/)/.test(url.pathname)) {
    return;
  }

  // Navigation: network-first ensures users receive fresh deployments; the cached
  // shell is served on network failure.
  if (req.mode === 'navigate') {
    event.respondWith(
      fetch(req)
        .then((res) => {
          const copy = res.clone();
          caches.open(CACHE_NAME).then((c) => c.put('/index.html', copy)).catch(() => {});
          return res;
        })
        .catch(() => caches.match('/index.html').then((m) => m || new Response('Offline', { status: 503 }))),
    );
    return;
  }

  // Same-origin static assets are cache-first.
  if (url.origin === self.location.origin) {
    event.respondWith(
      caches.match(req).then((hit) =>
        hit ||
        fetch(req).then((res) => {
          if (res.ok) {
            const copy = res.clone();
            caches.open(CACHE_NAME).then((c) => c.put(req, copy)).catch(() => {});
          }
          return res;
        }),
      ),
    );
  }
});
