// wwwroot/sw.js
const STATIC_CACHE = 'kiosk-static-v5';

const scopeUrl = new URL(self.registration.scope);
const BASE_ORIGIN = scopeUrl.origin;
const BASE_PATH = scopeUrl.pathname.endsWith('/') ? scopeUrl.pathname : scopeUrl.pathname + '/';

const toAbsolute = (path) => {
    if (!path) return BASE_ORIGIN + BASE_PATH;
    if (path.startsWith('http')) return path;
    if (path.startsWith('/')) path = path.slice(1);
    return BASE_ORIGIN + BASE_PATH + path;
};

const stripBase = (pathname) => {
    if (pathname.startsWith(BASE_PATH)) {
        return pathname.slice(BASE_PATH.length);
    }
    return pathname.replace(/^\//, '');
};

const STATIC_ASSETS = [
    '', 'kiosk.html', 'kiosk-admin.html', 'kiosk-dashboard.html',
    'manifest.json',
    'assets/liberty.svg',
    'assets/icons/angry.svg',
    'assets/icons/sad.svg',
    'assets/icons/meh.svg',
    'assets/icons/smile.svg',
    'assets/icons/laugh.svg'
];

self.addEventListener('install', (event) => {
    self.skipWaiting();
    event.waitUntil((async () => {
        const cache = await caches.open(STATIC_CACHE);
        await Promise.allSettled(
            STATIC_ASSETS.map(async (url) => {
                try {
                    await cache.add(new Request(toAbsolute(url), { cache: 'reload' }));
                } catch { /* ignore 404/opaque */ }
            })
        );
    })());
});

self.addEventListener('activate', (event) => {
    event.waitUntil((async () => {
        const keys = await caches.keys();
        await Promise.all(
            keys.map(k => (k !== STATIC_CACHE ? caches.delete(k) : Promise.resolve()))
        );
        // Optional: enable navigation preload for quicker HTML fetches
        if (self.registration.navigationPreload) {
            await self.registration.navigationPreload.enable();
        }
        self.clients.claim();
    })());
});

self.addEventListener('fetch', (event) => {
    const req = event.request;
    const url = new URL(req.url);
    const accept = req.headers.get('accept') || '';

    // 1) API + JSON: always network, never cached
    const relativePath = stripBase(url.pathname);
    const looksLikeApi = relativePath.startsWith('api/');
    const looksLikeJson = accept.includes('application/json') || relativePath.endsWith('.json');

    if (looksLikeApi || looksLikeJson) {
        event.respondWith(fetch(req, { cache: 'no-store' }));
        return;
    }

    // Only cache GETs
    if (req.method !== 'GET') return;

    // 2) HTML navigations: network-first, fall back to cache then /kiosk.html
    if (accept.includes('text/html') || req.mode === 'navigate') {
        event.respondWith((async () => {
            try {
                // Prefer a preloaded response if available (faster on slow nets)
                const preload = event.preloadResponse ? await event.preloadResponse : null;
                const fresh = preload || await fetch(req, { cache: 'no-store' });
                const cache = await caches.open(STATIC_CACHE);
                cache.put(req, fresh.clone());
                return fresh;
            } catch {
                const cache = await caches.open(STATIC_CACHE);
                return (await cache.match(req)) || (await cache.match(toAbsolute('kiosk.html'))) || Response.error();
            }
        })());
        return;
    }

    // 3) Other static assets: cache-first, update cache in background
    event.respondWith((async () => {
        const cache = await caches.open(STATIC_CACHE);
        const cached = await cache.match(req, { ignoreSearch: true });
        if (cached) return cached;
        try {
            const fresh = await fetch(req);
            cache.put(req, fresh.clone());
            return fresh;
        } catch {
            return Response.error();
        }
    })());
});
