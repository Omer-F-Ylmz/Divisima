# DIVISIMA BACKEND

Cafixo mimarisine **birebir** uyularak yazılmış, production-grade katmanlı ASP.NET Core (net8.0)
e-ticaret backend'i. Frontend (Divisima kadın moda) domain'i baz alındı.

## Solution yapısı (Cafixo 5 katman + test)
```
Divisima.Core             → IEntity/IUser/IDto, Result pattern, generic repository (+AsNoTracking/paging),
                            HashingHelper (HMAC-SHA512), JwtHelper, RequireUserType authorization,
                            UnitOfWork soyutlaması, ICacheService, IMailService, INotificationService,
                            IIyzicoClient, enum'lar
Divisima.Entity           → 25 entity (düz, byte status/type, nullable) + DTO'lar
Divisima.Dal              → I{X}Dal + Ef{X}Dal + DivisimaDbContext + UnitOfWork
Divisima.Bussiness        → I{X}Service + {X}Manager (tuple imza) + AutoMapper + FluentValidation +
                            Event pipeline + Outbox + Autofac DI
Divisima.API              → thin controller'lar + Program.cs + middleware'ler + SignalR Hub + Swagger
Divisima.IntegrationTests → WebApplicationFactory + Testcontainers (gerçek DB entegrasyon testleri)
```

## Modüller (13 servis, uçtan uca)
Product (+ProductStock) · Category (+SubCategory) · Coupon (+CouponUsage) · Collection (+CollectionItem) ·
Order zinciri (Order/OrderItem/OrderSnapshot) · Stock (+StockMovement) · ProductReview · Auth (+Customer+UserSession) ·
Content · **Payment (Iyzico)** · **Address** · **Cart** · **Wishlist** · **Search** · **AuditLog**

## Cafixo kalıbı (birebir)
- Entity: **düz (nav property yok)**, `byte status/type`, nullable `?`, ilişki serviste kompozisyonla
- DAL: minimal; detay/join servis katmanında çoklu DAL çağrısı
- Servis: `Task<(HttpStatusCode, Result)>` tuple → controller `StatusCode((int)x.Item1, x.Item2)`
- Result: SuccessResult/ErrorResult/SuccessDataResult<T>/ErrorDataResult<T>
- DI: Autofac AutofacBusinessModule (InstancePerLifetimeScope), event IEnumerable<IHandler> resolve
- Auth: [RequireUserType(UserTypeEnum.Admin/Customer)] custom policy
- `// Açıklayıcı yorum:` yorumları

## Çalıştırma
```bash
dotnet restore
dotnet ef database update --project Divisima.Dal --startup-project Divisima.API
dotnet run --project Divisima.API
# Swagger:   https://localhost:xxxx/swagger
# Health:    https://localhost:xxxx/health
# Hangfire:  https://localhost:xxxx/hangfire
```
appsettings.json → connection string, JWT SecurityKey, CORS origin, Mail, Iyzico anahtarları
(production'da environment/Key Vault ile değiştir).

## Test
```bash
python3 tests/business_logic_sim.py     # 83 iş mantığı testi (.NET gerektirmez)
dotnet test                              # gerçek entegrasyon testleri (.NET SDK ile)
```

## Production özellikleri (IMPROVEMENTS.md'de detay)
- **Veri bütünlüğü:** Transaction/UnitOfWork (atomik sipariş), optimistic concurrency (overselling engeli), Outbox (garantili event)
- **Güvenlik:** JWT + refresh rotation, hesap kilitleme, şifre politikası, rate limiting, CORS, HSTS, secrets
- **Ödeme:** Iyzico 3DS (init→callback→onay/iptal+stok iade, idempotent)
- **Altyapı:** Serilog + correlation id, Hangfire arka plan işleri, health checks, cache (Redis'e hazır), SignalR, API versiyonlama
- **Test:** 83 iş mantığı testi + WebApplicationFactory/Testcontainers entegrasyon testleri

---

## Yeni Modüller (e-ticaret tamamlama)

### Admin/Dashboard API (`/api/dashboard`, yalnız admin)
- `summary` - ciro, sipariş, ortalama sepet, müşteri, stok uyarısı
- `daily-sales` - tarih aralığında günlük ciro grafiği
- `top-products` - en çok satan ürünler
- `order-status` - sipariş durumu dağılımı
- `low-stock` - eşik altı stok listesi

### İade/Değişim (`/api/return`)
- `create` (müşteri) - iade talebi (14 gün süre + sahiplik + teslim kontrolü)
- `my` (müşteri) - iade taleplerim
- `pending` (admin) - bekleyen iadeler
- `process` (admin) - onay/ret; onayda **Iyzico refund + stok iade** (atomik transaction)

### Fatura (`/api/invoice`)
- `create/{orderId}` (admin) - fatura kes (KDV %20 ayrıştırma, e-fatura sağlayıcıya gönderim)
- `my` (müşteri) - faturalarım
- `order/{orderId}` (müşteri) - siparişe ait fatura

### Bildirim (push/SMS)
- `IPushNotificationService` (FCM HTTP v1) - sipariş durumu bildirimleri, feature flag `Push:Enabled`
- `ISmsService` (Netgsm) - SMS bildirim/kod, feature flag `Sms:Enabled`

### e-Fatura sağlayıcı
- `IEInvoiceProvider` soyutlaması - Foriba/Uyumsoft/Paraşüt implementasyonu ile değiştirilir, feature flag `EInvoice:Enabled`

## Mobil + Masaüstü (PWA - `frontend/pwa/`)
Tek kod tabanı ile dört cephe: manifest.json + service worker (offline cache + push) + kurulum istemi.
Detay: `frontend/pwa/README.md`. Native alternatif (React Native/Electron/MAUI) belgede.

---

## Yeni Modüller (e-ticaret tamamlama)

### Admin/Dashboard API (`/api/dashboard`, yalnız admin)
- `GET summary` — ciro, sipariş, ortalama sepet, müşteri, stok uyarısı
- `GET daily-sales?start&end` — günlük ciro grafiği (varsayılan 30 gün)
- `GET top-products?top` — en çok satan ürünler
- `GET order-status` — sipariş durumu dağılımı (pasta grafik)
- `GET low-stock?threshold` — stok uyarıları

### İade/Değişim (`/api/return`)
- `POST create` (müşteri) — iade talebi (14 gün süre + sahiplik + teslim kontrolü)
- `GET my` (müşteri) — iade taleplerim
- `GET pending` (admin) — bekleyen iadeler
- `POST process` (admin) — onay (Iyzico refund + stok iade, atomik) / ret

### Fatura/e-Fatura (`/api/invoice`)
- `POST generate/{orderId}` (admin) — fatura üret (KDV ayrıştırma + e-fatura sağlayıcı, idempotent)
- `GET my` (müşteri) — faturalarım
- `GET order/{orderId}` (müşteri) — siparişin faturası
- Sipariş onaylanınca otomatik fatura üretilir.

### Bildirim (push/SMS)
- FCM push (`IPushNotificationService`) + Netgsm SMS (`ISmsService`) — feature flag ile.
- Sipariş kargoya verilince/teslim edilince müşteriye in-app + push + SMS (best-effort).

**Feature flag'ler (appsettings):** `Push:Enabled`, `Sms:Enabled`, `EInvoice:Enabled` — hepsi dev'de false, production'da açılır.

---

## Bu oturumda eklenenler (2. parti)

### Backend
- **CustomerDevice** (`/api/device`) — push token kaydı (upsert), müşteriye tüm cihazlarına push; geçersiz token pasifleşir. Sipariş kargoya/teslime geçince gerçek cihazlara FCM push gider.
- **Kargo takip** (`/api/shipment`) — admin kargo oluşturur (takip no → sipariş Kargoda), müşteri takip eder (kargo firması API'sinden güncel durum, teslimde sipariş Delivered). Firma soyutlaması: Yurtiçi/Aras/MNG/PTT/Sürat.

### Frontend (`frontend/`)
- **api-client.js** — tüm uçları saran JS istemci (JWT + otomatik token yenileme + CSRF).
- **admin.html** — çalışan yönetim paneli (dashboard grafikleri + ürün/sipariş/iade/kargo/kupon).
- **PWA** — manifest.json + service-worker.js + pwa-register.js → mobil + masaüstü kurulabilir uygulama, offline cache, push.
- **INTEGRATION.md** — index.html'i API'ye bağlama + PWA + dağıtım rehberi.

**Feature flag'ler:** `Shipping:Enabled` (kargo firması API'si). Diğerleri: Push/Sms/EInvoice/Redis/Vault/Iyzico:UseRealSdk/Captcha — hepsi dev'de false.

---

## Öncelik 1 tamamlananlar (launch blocker'lar - kod tarafı)

### #1 Frontend API'ye bağlandı
- **Backend zenginleştirme:** Product'a `image_url`, ProductListResponseDto'ya `image_url` + `sizes` (beden listesi). GetList artık bedenleri tek sorguyla dolduruyor.
- **`frontend/api-bridge.js`:** mevcut index.html'i (mock veriyle çalışan) gerçek API'ye bağlar — ürünleri çeker, frontend şekline map eder, grid'i yeniden çizer; kupon/checkout/auth'u gerçek uçlara bağlar. API erişilemezse mock veriyle devam eder.
- **`frontend/index.html`:** api-client.js + api-bridge.js + manifest + SW eklendi; CSP `connect-src` API originlerine açıldı. Artık gerçek mağaza (vitrin değil).

### #2 Stok rezervasyonu (oversell + terk edilen sepet koruması)
- ProductStock'a `reserved_quantity`; müsait = fiziksel - rezerve.
- StockReservation tablosu + StockManager: `ReserveStock` (sipariş anında rezerve, düşürmez), `ConfirmReservation` (ödeme başarılı → fiziksel düşer), `ReleaseReservation` (başarısız/iptal → geri).
- Akış değişti: PlaceOrder rezerve eder; ödeme başarılı → onaylar; başarısız → serbest bırakır.
- **ReservationCleanupJob** (Hangfire, 5 dk): süresi dolan rezervasyonları serbest bırakır (terk edilen sepetlerde hayalet stok kaybı önlenir).

### #4 Admin tüm-siparişler
- `POST /api/order/admin/list` (admin) — durum + tarih filtresi + sayfalama. Admin paneli artık tüm siparişleri görüyor.

**Test: 239/239.** Öncelik 1'in kalan 3 maddesi (#3 derleme+migration, #5 secret, #6 TLS/WAF) senin ortamında/config olarak hazır.

---

## Öncelik 2 tamamlananlar

### Backend
- **#7 Ürün görsel yükleme** — `IImageStorage` soyutlaması (LocalImageStorage/bulut), ProductImage tablosu (çoklu görsel + birincil), `POST /api/product-image/upload` (multipart, tür+boyut doğrulama: JPEG/PNG/WEBP, max 5MB), listeleme/silme/birincil. Statik dosya sunumu (wwwroot/uploads).
- **#8 E-posta doğrulama zorunlu** — login'de `email_verified` kontrolü (doğrulanmamış hesap giriş yapamaz).
- **#9 Admin müşteri yönetimi** — `POST /api/admin/customer/list` (arama+sayfalama), `/status` (askıya al/aktifleştir). Hassas alan (şifre/token) asla dönmez.
- **#10 Admin stok düzeltme** — `POST /api/stock/adjust` (yeni sevkiyat/sayım, mutlak değer + fark hareketi + rezerve koruması).
- **#11 Validasyon** — 7→12 validator (Return, Shipment, StockAdjust, Device, Address).

### Frontend
- **#13 SEO** — JSON-LD (Organization+WebSite+SearchAction), robots.txt, dinamik sitemap ucu.
- **#15 Analytics** — GA4 + Meta Pixel hook (`divisimaTrack` olay yardımcısı).
- **#16 PWA ikonları** — 4 PNG (192/512 + maskable), Divisima markası.
- **#14 Loading/error** — admin panelde mevcut; api-bridge'e görünür hata (toast) eklendi.

**Test: 256/256.** Detaylar: `frontend/SEO-ANALYTICS.md`.

---

## Öncelik 3 tamamlananlar (güvenlik sertleştirme)

Not: Öncelik 3'ün çoğu süreç/altyapı (senin ortamında deploy). Kod/config olarak eklenenler:

### Kod
- **#18 Redis dağıtık rate limit** — `RedisRateLimiter` (atomik Lua INCR+EXPIRE) + `RedisRateLimitMiddleware` (yol bazlı: auth 5/dk, ödeme 10/dk, genel 100/dk). Redis açıkken merkezi sayaç (çok sunucuda limit gerçekten paylaşılır), kapalıyken .NET yerleşik. Redis down ise fail-open. StackExchange.Redis Core.csproj'a eklendi (mevcut Redis lock/cache eksiğini de kapattı).
- **#19 admin.html CSP** — sıkı Content-Security-Policy (Chart.js CDN izinli, frame-ancestors none) + SRI notu.

### Config / CI
- **#17 DAST** — `dast-zap.yml` (OWASP ZAP baseline, haftalık) + `.zap/rules.tsv` + `ops/pentest-checklist.md` (OWASP Top 10 manuel kapsam).
- **#20 Secret rotasyon** — `secret-rotation.yml` (90 günde bir, Azure OIDC, production onayı).
- **#22 DDoS** — `ddos-protection.conf` (slowloris/rate/conn/bot) + `fail2ban-divisima.conf` (tekrarlayan saldırgan IP ban).
- **security.txt** (RFC 9116) — sorumlu açıklama.
- **#21 Dependabot + CodeQL** — mevcut (nuget + github-actions).

**Test: 262/262.** Kalan (senin ortamında): gerçek pentest, TLS/WAF/DDoS bulut deploy, secret'lar, Dependabot/CodeQL GitHub'da aktifleştirme.
