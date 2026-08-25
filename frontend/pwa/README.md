> **ARSIV — FAZ 0 / K5.** Bu dizin ARSIVDIR: canli PWA dosyalari DEPO KOKUNDE
> (`frontend/manifest.json`, `frontend/pwa-register.js`, `frontend/service-worker.js`)
> ve `index.html` onlari oradan yukler. Buradaki manifest.json / offline.html /
> service-worker.js / sw-register.js kopyalarina HICBIR YERDEN referans yoktu
> (olculdu: 0 gecis) ve SILINDI. Bu dosya, tarihsel tasarim notu olarak ve
> `GuvenlikFix3SozlesmeTests` deny-kurali kapsam pini `/pwa/README.md` yolunu
> aradigi icin KALDI.

# Divisima PWA (Progressive Web App)

Bu dosyalar, mevcut web arayüzünü **tek kod tabanıyla** hem mobilde hem masaüstünde
kurulabilir bir uygulamaya çevirir. Native uygulama derlemeye gerek yok.

## Ne sağlar
- **Mobil:** "Ana ekrana ekle" - telefonda ikon, tam ekran, offline açılış
- **Masaüstü:** Chrome/Edge "Uygulamayı yükle" - ayrı pencere, uygulama gibi
- **Offline:** app shell cache'lenir; bağlantı yokken offline.html
- **Push bildirim:** backend `IPushNotificationService` (FCM) ile entegre

## Kurulum (index.html'e ekle)
```html
<head>
  <link rel="manifest" href="/manifest.json">
  <meta name="theme-color" content="#1a1a1a">
  <link rel="apple-touch-icon" href="/icons/icon-192.png">
</head>
<body>
  ...
  <button id="pwa-install-btn" style="display:none">Uygulamayı Yükle</button>
  <script src="/sw-register.js"></script>
</body>
```

## Dosyaların yeri (web sunucu kökü)
| Dosya | Konum |
|-------|-------|
| manifest.json | `/manifest.json` |
| service-worker.js | `/service-worker.js` (kökte olmalı - scope için) |
| sw-register.js | `/sw-register.js` |
| offline.html | `/offline.html` |
| ikonlar | `/icons/icon-{72,96,128,192,512}.png` |

## İkonlar
72/96/128/192/512 px PNG üret (192 ve 512 zorunlu; maskable önerilir). Logo + arka plan.

## Gereksinim
- **HTTPS zorunlu** (localhost hariç) - service worker yalnız güvenli bağlamda çalışır
- nginx zaten TLS'i sağlıyor (ops/infra/nginx.conf)

## Native alternatif (gerekirse)
Daha derin native entegrasyon (uygulama mağazası, native API) istenirse:
- **Mobil:** React Native veya .NET MAUI (aynı backend API'sini tüketir)
- **Masaüstü:** Electron (web arayüzünü sarar) veya .NET MAUI
PWA bu ihtiyaçların ~%90'ını çok daha az işle karşılar; native'e ancak mağaza/derin donanım gerekirse geçilir.
