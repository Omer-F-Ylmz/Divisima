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
const VERSION = "2026-09-04-gf2b";

// ══ GF-2b / K2 - GERI DONUS KAPISI (KILL SWITCH) ══════════════════════════════════════
//
// ── DUZELTILMIS GEREKCE (ILK YAZIM CURUDU - rapor denetcisi olctu) ───────────────────
// ILK YAZIMDA "service worker URETIMDE BUGUNE KADAR HIC KOSMADI ... ILK KEZ gercek
// kullanicilarda calisacak" yaziyordu. **YANLIS.** Olculdu: `index.html` ILK COMMIT'ten
// (df91863) beri `/pwa-register.js`i yukluyor ve o dosya `pwa-register.js:12`de VAR OLAN
// `/service-worker.js`i kaydediyor; dosya bu dalgada DEGISMEDI. Yani SW gercek tarayicida
// ZATEN KOSUYORDU ve GF-2a/K8'in kararlari (iki kova, /api network-only, cikis temizligi)
// da `1dd985b` ile ZATEN yayinda. K2'nin soktugu satir IKINCI ve HER ZAMAN DUSEN bir
// kayitti (var olmayan 'sw.js'e) - onu silmek hijyendir, SW'nin kosup kosmadigini
// DEGISTIRMEZ.
//
// GERCEK GEREKCE: bu dalga SW GOVDESINI degistiriyor ve VERSION'u bumpliyor, yani
// kullanicilara YENI bir SW surumu kurulacak ve `activate` eski kovalari silecek.
// Bir service worker kullanicinin tarayicisinda KALIR: hatali bir surum yayinlanirsa
// yeni dagitim ona ULASAMAYABILIR ve depoyu geri almak TEK BASINA yetmez, cunku kurulu
// SW zaten kullanicidadir. Dagitim duzeyinde geri alinabilen bir kapi bu yuzden gerekli.
//
// KULLANIMI: bu bayrak `true` yapilip dosya dagitilir. Kurulan her SW kendini SILER ve
// TUM kovalari bosaltir; sayfa bir sonraki yuklemede tamamen SW'siz calisir.
// TEPE DUZEYDE okunmasi BILINCLI: `install`, `activate` ve `fetch` AYNI karari gorur,
// yani yarim durum (kabugu silmis ama istekleri hala yakalayan SW) olusamaz.
const KAPAT = false;
// ══ GF-2a / K8 (D-6) - IKI KOVA ═══════════════════════════════════════════════════════
//
// OLCULEN ONCE-DURUM: TEK kova vardi ve `/api/` yanitlari UYGULAMA KABUGUYLA AYNI kutuya
// yaziliyordu (`caches.open(CACHE).put` dort yerde). Iki ayri zarar:
//  (1) KIMLIKLI API YANITLARI DISKE DUSUYORDU. Backend her yanita `no-store` koyuyor
//      (`SecurityHeadersMiddleware`), ama **Cache Storage API bu basligi UYGULAMAZ** -
//      `cache.put()` kosulsuz depolar. Iki dogru parca birlesince koruma DUSTU.
//      Ortak bilgisayarda cikis yapmis kullanicinin siparis/adres yaniti okunabilirdi.
//  (2) Cikista `caches.delete(CACHE)` denseydi OFFLINE ACILISI DA silecekti.
// COZUM: kabuk ve API AYRI kovalarda; `/api/` artik NETWORK-ONLY (hic yazilmiyor) ve
// cikista silinecek bir sey KALMIYOR - kova bos kaliyor, yine de temizlik kancasi var.
const CACHE = "divisima-shell-" + VERSION;
const API_CACHE = "divisima-api-" + VERSION;

// Açıklama: Uygulama kabuğu (offline açılış için gerekli çekirdek dosyalar)
const SHELL = ["/", "/index.html", "/manifest.json", "/api-client.js", "/api-bridge.js"];

// Kurulum: kabuğu önbelleğe al, sıraya girmeden hemen devral
self.addEventListener("install", (e) => {
  // GF-2b/K2: kapali moddayken kabuk ONBELLEGE ALINMAZ - dogrudan devral ve `activate`te
  // kendini sil. Kurulum sirasinda `addAll` yapilsaydi silinecek seyi once yazmis olurduk.
  if (KAPAT) { e.waitUntil(self.skipWaiting()); return; }
  e.waitUntil(caches.open(CACHE).then((c) => c.addAll(SHELL)).then(() => self.skipWaiting()));
});

// Aktivasyon: BU sürüm dışındaki TÜM önbellekleri sil, açık sekmeleri hemen devral.
// CACHE adı artık VERSION ile değiştiği için bu satır gerçekten iş yapar.
self.addEventListener("activate", (e) => {
  // ══ GF-2b / K2 - KAPALI MOD: KENDINI SIL, TUM KOVALARI BOSALT ═══════════════════════
  // Suzgec YOK: normal dalda `k !== CACHE && k !== API_CACHE` ile SECEREK siliniyor,
  // burada AYRIM YAPILMADAN hepsi siliniyor - amac geri donus, koruma degil.
  // `unregister()` KOVALARDAN SONRA cagrilir: ters sirada olsaydi kayit silindikten
  // sonra silme sozunun tamamlanacagi GARANTI olmazdi.
  if (KAPAT) {
    e.waitUntil(
      caches.keys()
        .then((keys) => Promise.all(keys.map((k) => caches.delete(k))))
        .then(() => self.registration.unregister())
        .then(() => self.clients.claim())
    );
    return;
  }
  e.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE && k !== API_CACHE).map((k) => caches.delete(k))))
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
  // GF-2b/K2: kapali moddayken HICBIR istege karisilmaz - `respondWith` cagrilmadigi
  // icin tarayici istegi dogrudan aga goturur, yani sayfa SW YOKMUS gibi calisir.
  if (KAPAT) return;
  const url = new URL(e.request.url);
  if (e.request.method !== "GET") return; // yalnız GET önbelleklenir

  // GF-2a / K8: CAPRAZ-ORIGIN isteklere SW HIC dokunmaz. Onceki hal `pathname`e bakiyordu,
  // yani baska bir origin'deki `/api/...` de eslesiyordu; ayrica CDN'den gelen `.js`
  // dosyalari `kodTasiyorMu` dalina duesuep onbellege yaziliyordu (opak kopya SRI'yi
  // dusurebilir). Kapi ONCE origin'e bakar.
  if (url.origin !== self.location.origin) return;

  // ══ GF-2a / K8 - API: NETWORK-ONLY. ONBELLEGE YAZILMAZ, ONBELLEKTEN OKUNMAZ ═════════
  // Onceki hal "network-first" idi ve her API GET yanitini kaliciya yaziyordu; kimlik
  // ayirt edici hicbir kosul yoktu (Authorization/Vary/Cache-Control 0 gecis).
  // BILINCLI BEDEL: `/api/` icin offline yedek KALKTI. Kabuk (HTML/JS/ikon) hala
  // onbellekte oldugu icin UYGULAMA OFFLINE ACILMAYA DEVAM EDER; yalniz veri gelmez.
  if (url.pathname.startsWith("/api/")) {
    return; // respondWith YOK -> tarayicinin kendi agi, SW araya girmez
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

// ══ GF-2a / K8 - CIKISTA API KOVASI SILINIR ═══════════════════════════════════════════
// Uygulama cikis yaptiginda `postMessage({type:"divisima-logout"})` gonderir; burada
// YALNIZ API kovasi silinir - KABUK KOVASINA DOKUNULMAZ, boylece offline acilis SURER.
// (Bugun API kovasi zaten bos kaliyor cunku `/api/` network-only; bu kanca gelecekte
// bir onbellekleme geri gelirse temizligin YERI belli olsun diye ve savunma derinligi
// icin duruyor. Onceki halde HICBIR temizlik kancasi YOKTU - `caches.` gecisi 0 idi.)
self.addEventListener("message", (e) => {
  if (!e.data || e.data.type !== "divisima-logout") return;
  e.waitUntil(caches.delete(API_CACHE));
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
