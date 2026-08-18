# Divisima Frontend Entegrasyon Rehberi

Bu klasör storefront'u backend'e bağlayan katmanı, admin panelini ve PWA dosyalarını içerir.

## Dosyalar
| Dosya | Görev |
|-------|-------|
| `api-client.js` | Tüm backend uçlarını saran JS istemci (JWT, otomatik token yenileme, CSRF, hata yönetimi) |
| `admin.html` | Kendi kendine yeten yönetim paneli (dashboard grafikleri + ürün/sipariş/iade/kargo/kupon) |
| `manifest.json` | PWA manifesti (mobil + masaüstü kurulabilir uygulama) |
| `service-worker.js` | Offline önbellek + push bildirim |
| `pwa-register.js` | SW kaydı + bildirim izni + FCM token kaydı |

## 1. API adresini ayarla
Her iki dosyada da (`admin.html` ve storefront) API adresi:
```js
const API_BASE = "https://api.divisima.com"; // kendi backend adresin
localStorage.setItem("divisima_api_base", API_BASE); // admin.html bunu okur
```

## 2. Storefront (index.html) bağlama
`index.html`'in `</body>` öncesine ekle:
```html
<script src="/api-client.js"></script>
<script>
  const api = new DivisimaAPI("https://api.divisima.com");

  // Ürünleri çek ve mock veriyi değiştir
  async function loadProducts(){
    const res = await api.products.list();
    const products = res.data || res;
    // products dizisini mevcut render fonksiyonuna ver
    renderProducts(products); // kendi render fonksiyonun
  }

  // Sepete ekle
  async function addToCart(productId, size, qty){
    await api.cart.add(productId, size, qty);
  }

  // Giriş
  async function login(email, pass){
    await api.auth.login(email, pass); // token otomatik saklanır
  }

  // Sipariş + ödeme akışı
  async function checkout(addressId, couponCode){
    const order = await api.orders.place({
      address_id: addressId, coupon_code: couponCode,
      payment_type: 0, request_id: crypto.randomUUID() // idempotency
    });
    const orderId = (order.data||order).id;
    const pay = await api.payment.initialize(orderId);
    // pay.data.checkout_form_content → Iyzico iframe HTML'ini sayfaya göm
    document.getElementById("payment-area").innerHTML = (pay.data||pay).checkout_form_content;
  }

  loadProducts();
</script>
```

Değiştirilecek yerler: mock ürün dizisi → `api.products.list()`; sepet işlemleri → `api.cart.*`;
kullanıcı girişi → `api.auth.login`; ödeme → `api.orders.place` + `api.payment.initialize`.

## 3. PWA (mobil + masaüstü kurulabilir)
`index.html`'in `<head>`'ine:
```html
<link rel="manifest" href="/manifest.json">
<meta name="theme-color" content="#111111">
<link rel="apple-touch-icon" href="/icons/icon-192.png">
```
`</body>` öncesine (api-client.js'ten sonra):
```html
<script src="/pwa-register.js"></script>
```
Bu kadar — kullanıcı tarayıcıda "Ana ekrana ekle" / "Uygulamayı yükle" görür. Aynı kod hem
mobilde (Android/iOS) hem masaüstünde (Chrome/Edge) kurulur, offline açılır.

### İkonlar (gerekli)
`/icons/` altına: `icon-192.png`, `icon-512.png`, `icon-maskable-192.png`, `icon-maskable-512.png`.

## 4. Push bildirim (opsiyonel)
Firebase projesi + VAPID key gerekir. Giriş sonrası:
```html
<script>
  window.DIVISIMA_VAPID_KEY = "BXXXX..."; // Firebase Console → Cloud Messaging → Web Push certs
  // firebase SDK yüklenir, messaging = firebase.messaging()
  await window.DivisimaPush.enable(api, messaging); // izin ister + token'ı backend'e yollar
</script>
```
Backend'de `Push:Enabled=true` + FCM kimlikleri ayarlanınca sipariş kargoya/teslime geçince otomatik push gider.

## 5. Dağıtım notları
- **HTTPS zorunlu** — Service Worker + PWA + push yalnız HTTPS'te (veya localhost) çalışır.
- CORS: backend `AllowedOrigins`'e frontend domain'ini ekle.
- CSRF: backend antiforgery çerezi (`XSRF-TOKEN`) set eder; istemci otomatik `X-XSRF-TOKEN` başlığı yollar.
- Admin paneli `/admin.html` — yalnız yönetici hesabıyla giriş yapılabilir (backend `RequireUserType.Admin`).

## Alternatif: native mobil/masaüstü
PWA çoğu ihtiyaç için yeterli. Native gerekiyorsa aynı `api-client.js` React Native / Electron
projesinde de kullanılabilir (fetch tabanlı, çerçeveden bağımsız).
