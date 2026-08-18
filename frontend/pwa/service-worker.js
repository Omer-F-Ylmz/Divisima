// Açıklayıcı yorum: Divisima PWA Service Worker. Uygulama kabuğunu (app shell) cache'ler -> offline açılış,
// hızlı yükleme, "ana ekrana ekle". API çağrıları network-first (taze veri), statik varlıklar cache-first.
const CACHE_VERSION = 'divisima-v1';
const APP_SHELL = [
  '/',
  '/index.html',
  '/manifest.json',
  '/css/style.css',
  '/js/app.js',
  '/offline.html'
];

// Açıklayıcı yorum: Kurulumda app shell'i önbelleğe al
self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_VERSION).then((cache) => cache.addAll(APP_SHELL))
  );
  self.skipWaiting();
});

// Açıklayıcı yorum: Aktivasyonda eski cache sürümlerini temizle
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((k) => k !== CACHE_VERSION).map((k) => caches.delete(k)))
    )
  );
  self.clients.claim();
});

// Açıklayıcı yorum: Fetch stratejisi
self.addEventListener('fetch', (event) => {
  const { request } = event;
  const url = new URL(request.url);

  // Açıklayıcı yorum: API çağrıları -> network-first (taze veri; offline'da cache'e düş)
  if (url.pathname.startsWith('/api/')) {
    event.respondWith(
      fetch(request)
        .then((res) => {
          // GET yanıtlarını kısa süre cache'le
          if (request.method === 'GET') {
            const clone = res.clone();
            caches.open(CACHE_VERSION).then((cache) => cache.put(request, clone));
          }
          return res;
        })
        .catch(() => caches.match(request))
    );
    return;
  }

  // Açıklayıcı yorum: Statik varlıklar -> cache-first (hız); yoksa network + cache
  event.respondWith(
    caches.match(request).then((cached) =>
      cached ||
      fetch(request)
        .then((res) => {
          const clone = res.clone();
          caches.open(CACHE_VERSION).then((cache) => cache.put(request, clone));
          return res;
        })
        .catch(() => caches.match('/offline.html'))
    )
  );
});

// Açıklayıcı yorum: Push bildirim al (FCM ile entegre - backend IPushNotificationService gönderir)
self.addEventListener('push', (event) => {
  const data = event.data ? event.data.json() : {};
  const title = data.title || 'Divisima';
  const options = {
    body: data.body || '',
    icon: '/icons/icon-192.png',
    badge: '/icons/icon-96.png',
    data: data.data || {}
  };
  event.waitUntil(self.registration.showNotification(title, options));
});

// Açıklayıcı yorum: Bildirime tıklanınca ilgili sayfayı aç
self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  const url = event.notification.data.url || '/';
  event.waitUntil(clients.openWindow(url));
});
