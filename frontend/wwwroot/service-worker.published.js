// Caches the published Blazor app so it opens offline (Games and PDF tools work without a connection).
// Based on the Blazor WebAssembly PWA template. service-worker-assets.js is generated at publish
// and lists every app file with its hash; a new deploy changes its version, which installs a new cache.
//
// Not cached: anything under /ng/ (the Angular My Documents app, added to the site after publish;
// it needs the API anyway) and cross-origin requests (API, Entra sign-in, blob storage).

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', onFetch);

// index.html asks the new version to take over when the user clicks "Reload" on the update banner
self.addEventListener('message', event => {
    if (event.data?.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff2?$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/];
// staticwebapp.config.json is read by Azure Static Web Apps but never served, and one failed
// request fails the whole install
const offlineAssetsExclude = [/^service-worker\.js$/, /^staticwebapp\.config\.json$/];

const base = '/';
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);
const notCachedPrefix = new URL('ng/', baseUrl).href;

async function onInstall() {
    console.info('Service worker: Install');

    // Fetch and cache all matching items from the assets manifest
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate() {
    console.info('Service worker: Activate');

    // Delete unused caches from earlier versions
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

function onFetch(event) {
    const request = event.request;
    if (request.method !== 'GET' || !request.url.startsWith(baseUrl.href) || request.url.startsWith(notCachedPrefix)) {
        return; // let the browser handle it normally
    }
    event.respondWith(fromCacheOrNetwork(request));
}

async function fromCacheOrNetwork(request) {
    // For all navigation requests, try to serve index.html from cache, unless that client-side URL
    // is itself a file in the manifest (then serve that file)
    const shouldServeIndexHtml = request.mode === 'navigate' && !manifestUrlList.some(url => url === request.url);

    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(shouldServeIndexHtml ? 'index.html' : request);
    return cachedResponse || fetch(request);
}
