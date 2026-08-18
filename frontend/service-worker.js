/*
 * Divisima Service Worker
 * -----------------------
 * - Statik varlıkları önbelleğe alır (cache-first) → offline açılış + hız
 * - API isteklerini network-first yapar (taze veri, offline'da son önbellek)
 * - Push bildirimlerini gösterir (FCM data mesajları)
 * NOT: Bu bir üretim dosyasıdır; Claude artifact'ı değildir - localStorage/SW tam çalışır.
 */
const CACHE = "divisima-v1";
// Açıklama: Uygulama kabuğu (offline açılış için gerekli çekirdek dosyalar)
const SHELL = ["/", "/index.html", "/manifest.json", "/api-client.js"];

// Kurulum: kabuğu önbelleğe al
self.addEventListener("install", (e) => {
  e.waitUntil(caches.open(CACHE).then((c) => c.addAll(SHELL)).then(() => self.skipWaiting()));
});

// Aktivasyon: eski önbellekleri temizle
self.addEventListener("activate", (e) => {
  e.waitUntil(
    caches.keys().then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k)))).then(() => self.clients.claim())
  );
});

// Fetch stratejisi
self.addEventListener("fetch", (e) => {
  const url = new URL(e.request.url);
  if (e.request.method !== "GET") return; // yalnız GET önbelleklenir

  // API: network-first (taze veri; offline'da önbellekten)
  if (url.pathname.startsWith("/api/")) {
    e.respondWith(
      fetch(e.request).then((res) => {
        const copy = res.clone();
        caches.open(CACHE).then((c) => c.put(e.request, copy));
        return res;
      }).catch(() => caches.match(e.request))
    );
    return;
  }

  // Varlıklar: cache-first (hız + offline)
  e.respondWith(
    caches.match(e.request).then((cached) => cached || fetch(e.request).then((res) => {
      const copy = res.clone();
      caches.open(CACHE).then((c) => c.put(e.request, copy));
      return res;
    }).catch(() => caches.match("/index.html")))
  );
});

// Push bildirimi (FCM data payload)
self.addEventListener("push", (e) => {
  let data = { title: "Divisima", body: "Yeni bildirim" };
  try { if (e.data) data = e.data.json(); } catch (_) { if (e.data) data.body = e.data.text(); }
  const title = data.title || (data.notification && data.notification.title) || "Divisima";
  const body = data.body || (data.notification && data.notification.body) || "";
  e.waitUntil(self.registration.showNotification(title, {
    body, icon: "/icons/icon-192.png", badge: "/icons/icon-192.png", data: data.data || {}
  }));
});

// Bildirime tıklama → uygulamayı aç
self.addEventListener("notificationclick", (e) => {
  e.notification.close();
  const target = (e.notification.data && e.notification.data.url) || "/";
  e.waitUntil(clients.matchAll({ type: "window" }).then((list) => {
    for (const c of list) { if (c.url.includes(target) && "focus" in c) return c.focus(); }
    if (clients.openWindow) return clients.openWindow(target);
  }));
});
