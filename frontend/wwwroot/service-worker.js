// Development service worker: no caching, so changes show up on reload.
// The published app uses service-worker.published.js (see UploaderDoc.csproj).
self.addEventListener('fetch', () => { });
