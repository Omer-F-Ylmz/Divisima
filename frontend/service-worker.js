/*
 * Divisima Service Worker
 * -----------------------
 * - Uygulama kabuğunu önbelleğe alır → offline açılış + hız
 * - Kod taşıyan dosyalar (navigasyon / .html / .js) network-first → YAYINLANAN DÜZELTME ULAŞIR
 * - Diğer statik varlıklar (ikon, resim, font, manifest) cache-first → hız
 * - API isteklerini network-first yapar (taze veri, offline'da son önbellek)
 * - Push bildirimlerini gösterir (FCM data mesajları)
 * NOT: Bu bir üretim dosyasıdır; Claude artifact'ı değildir - localStorage/SW tam çalışır.
 *
 * ── E2b: SÜRÜMLEME + NETWORK-FIRST (ÖLÇÜLEN İKİ ZARARIN DÜZELTMESİ) ──────────────────
 *
 * ÖNCEKİ HÂLİ VE ÖLÇÜLEN ZARARLAR:
 *  1) CACHE adı sabitti ("divisima-v1"). `activate` yalnız `k !== CACHE` olanları sildiği
 *     için HİÇBİR ŞEY silinmiyordu; SW dosyası da değişmediğinden tarayıcı yeni SW hiç
 *     kurmuyordu. Sonuç: index.html (yani CSP meta etiketi) ve api-bridge.js ilk ziyaretten
 *     sonra kullanıcının tarayıcısında DONUYORDU - yayınlanan hiçbir düzeltme, güvenlik
 *     yaması dahil, mevcut kullanıcıya ULAŞMIYORDU. E2b'de bu dört kez tur bozdu: CSP
 *     düzeltmeleri ancak Ctrl+Shift+R ile ulaştı, normal yenilemede eski sürüm geri geldi.
 *  2) Fetch handler'ın son dalı HER varlık için `.catch(() => caches.match("/index.html"))`
 *     idi. Statik sunucu öldüğünde (ölçüldü: curl -> http=000) tarayıcı yine de sayfayı
 *     açtı ve ÖNBELLEKTEKİ ESKİ index.html servis edildi - cache-buster sorgusu dahil.
 *     Yani origin çöktüğünde kullanıcı hiçbir hata görmüyor, aylar önce önbelleğe alınmış
 *     bir sürümü kullanmaya devam ediyordu ve kesinti müşteri tarafında GÖRÜNMÜYORDU.
 *
 * DÜZELTMENİN DAYANDIĞI İKİ AYAK (biri unutulsa diğeri kurtarır):
 *  a) VERSION değişince CACHE adı değişir, `activate` eski önbellekleri GERÇEKTEN siler,
 *     `skipWaiting` + `clients.claim` ile yeni SW hemen devralır.
 *  b) Kod taşıyan dosyalar NETWORK-FIRST. Böylece VERSION bumpı unutulsa BİLE yayınlanan
 *     düzeltme kullanıcıya ulaşır; sürümleme temizlik için, tek dayanak değil.
 *
 * OFFLINE YEDEĞİ YALNIZ NAVİGASYONA VERİLİR. Bir .js/.png isteği başarısızsa artık
 * sessizce HTML dönmüyoruz (yukarıdaki 2. zarar); istek gerçekten başarısız olur.
 */

// SÜRÜM - HER DAĞITIMDA DEĞİŞMELİDİR.
// Bu depoda derleme adımı yok (statik dosyalar olduğu gibi sunuluyor), bu yüzden sürüm elle
// bumplanan bir sabit. Dağıtım otomasyonu geldiğinde buraya commit SHA'sı yazılmalı; bump
// unutulursa (a) ayağı devre dışı kalır ama (b) ayağı sayesinde düzeltmeler yine ulaşır.
const VERSION = "2026-08-21-e3";
const CACHE = "divisima-" + VERSION;

// Açıklama: Uygulama kabuğu (offline açılış için gerekli çekirdek dosyalar)
const SHELL = ["/", "/index.html", "/manifest.json", "/api-client.js", "/api-bridge.js"];

// Kurulum: kabuğu önbelleğe al, sıraya girmeden hemen devral
self.addEventListener("install", (e) => {
  e.waitUntil(caches.open(CACHE).then((c) => c.addAll(SHELL)).then(() => self.skipWaiting()));
});

// Aktivasyon: BU sürüm dışındaki TÜM önbellekleri sil, açık sekmeleri hemen devral.
// CACHE adı artık VERSION ile değiştiği için bu satır gerçekten iş yapar.
self.addEventListener("activate", (e) => {
  e.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

// Açıklama: Kod taşıyan istek mi? Navigasyon, .html ve .js düzeltmeleri taşır - bunlar
// network-first olmalı. Geri kalan varlıklar (ikon/resim/font/manifest) cache-first kalır.
function kodTasiyorMu(request, url) {
  if (request.mode === "navigate") return true;
  return /\.(html|js)$/i.test(url.pathname);
}

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

  // Kod taşıyanlar: NETWORK-FIRST. Ağ yanıt verirse onu kullan ve önbelleği tazele;
  // ancak ağ YOKSA önbelleğe düş (offline açılış korunur).
  if (kodTasiyorMu(e.request, url)) {
    e.respondWith(
      fetch(e.request).then((res) => {
        const copy = res.clone();
        caches.open(CACHE).then((c) => c.put(e.request, copy));
        return res;
      }).catch(() =>
        caches.match(e.request).then((cached) => {
          if (cached) return cached;
          // Offline yedeği YALNIZ navigasyona verilir - bir .js isteğine HTML dönmek
          // "sunucu öldü" durumunu gizler (E2b'de ölçüldü).
          if (e.request.mode === "navigate") return caches.match("/index.html");
          return Response.error();
        })
      )
    );
    return;
  }

  // Diğer varlıklar: cache-first (hız + offline)
  e.respondWith(
    caches.match(e.request).then((cached) => cached || fetch(e.request).then((res) => {
      const copy = res.clone();
      caches.open(CACHE).then((c) => c.put(e.request, copy));
      return res;
    }))
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
