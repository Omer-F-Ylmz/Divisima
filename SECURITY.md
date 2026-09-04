# DIVISIMA BACKEND — GÜVENLİK DÖKÜMANI

Bu belge tehdit modelini, uygulanan tüm güvenlik katmanlarını ve operasyonel önerileri içerir.

> **Önemli:** "Hiçbir açık yok" hiçbir sistemde mutlak garanti edilemez. Güvenlik bir durum değil
> süreçtir: bağımlılık güncellemeleri, düzenli penetrasyon testi, secrets rotasyonu, izleme gerekir.
> Aşağıdakiler uygulama-seviyesi bilinen tüm yaygın saldırı vektörlerini (OWASP Top 10 dahil) kapatır.

## 1. Kimlik Doğrulama & Oturum
| Katman | Uygulama |
|--------|----------|
| Şifre saklama | PBKDF2-SHA512 (100k iterasyon, 69 baytlık v2 zarfı, benzersiz salt) — `HashingHelper`. Eski v1 (HMAC-SHA512) kayıtlar doğru şifreyle girişte sessizce v2'ye taşınır; migration yoktur. Düz şifre asla saklanmaz. |
| Şifre politikası | Min 8 karakter, büyük/küçük harf + rakam (FluentValidation) |
| JWT | Kısa ömürlü access token + jti (token id) |
| Token iptali | `ITokenBlacklist` (Redis-uyumlu) + `TokenBlacklistMiddleware` — logout/şifre değişiminde token anında geçersiz |
| Refresh token | httpOnly + Secure + SameSite=Strict cookie (JS erişemez → XSS'te çalınamaz), rotation |
| Hesap kilitleme | 5 başarısız denemede 15 dk kilit (brute-force) |
| Şifre sıfırlama | Tek kullanımlık token (30 dk), enumeration-safe, sıfırlamada tüm oturum iptali |
| 2FA/MFA | E-POSTA OTP: 6 hane, SHA-256 hash'li saklanır, 5 dk, tek kullanımlık, sabit-zamanlı karşılaştırma (`AuthManager.cs:349-364`, `:373-400`). `TotpService` (RFC 6238) sınıf olarak vardır ve DI'ya kayıtlıdır (`Program.cs:294`) ama üretim akışında hiç çağrılmaz (ölçüldü: tüketici 0) — Google Authenticator akışı bugün YOKTUR. |
| Bot koruması | `ICaptchaValidator` (Cloudflare Turnstile) — register/forgot/riskli login |

## 2. Yetkilendirme (IDOR)
- `[RequireUserType(Admin/Customer)]` custom policy + authorization handler.
- **Kaynak sahipliği:** `SecureControllerBase.CurrentCustomerId` — kullanıcı kimliği JWT'den alınır,
  route/body'den ASLA. Address/Cart/Wishlist/Order/Payment kendi kaynağına erişim zorunlu (IDOR engeli).

## 3. Ödeme Güvenliği (en kritik)
| Vektör | Koruma |
|--------|--------|
| Sahte callback | Otorite **sunucu-sunucu retrieve**; imza (HMAC-SHA256, timing-safe) GELİRSE doğrulanır ve tutmazsa 400 döner + güvenlik olayı yazılır. Sağlayıcı bugün imza GÖNDERMİYOR (ölçüldü 22 Ağu 2026: gövdede `signature` yok, `X-Iyz-Signature` başlığı VAR ama BOŞ) — imza tek başına kapı DEĞİLDİR. Kapıyı kuran zincir: yalnız-Pending + token 30 dk (tarayıcı yolu) + tutar + para birimi + fraud. Gerekçe: `PaymentNotificationChannelEnum.cs`. |
| Callback güveni | Sonuç **sunucu-sunucu** Iyzico'dan token ile çekilir, callback gövdesine güvenilmez |
| Tutar manipülasyonu | Ödenen tutar == sipariş tutarı kontrolü |
| Para birimi | Sipariş = ödeme para birimi kontrolü |
| Fraud | Iyzico fraudStatus onayı zorunlu |
| Kart testi | Velocity limiti (müşteri başına 10 dk'da 5 deneme) + rate limit (10/dk) |
| PCI-DSS | Kart bilgisi sunucuya HİÇ gelmez (Iyzico Checkout Form iframe) |
| IDOR | Kullanıcı yalnızca kendi siparişini öder (JWT) |
| Sipariş durumu | Sadece Pending + ödenmemiş + tutar>0 siparişe ödeme |
| Replay | Idempotency (yalnız Pending işlenir) + token 30 dk zaman aşımı. 30 dk sınırı **ProviderWebhook kanalında BİLİNÇLİ OLARAK UYGULANMAZ** (`IyzicoPaymentManager.cs:199`, `:270`); gerekçe `PaymentNotificationChannelEnum.cs:38-46` — gecikmiş ama gerçek bir bildirim, parası alınmış ödemeyi "Failed" diye defterliyordu (sipariş #33). |
| Race condition | Distributed lock (`IDistributedLock` — Redis RedLock) + kilit sonrası double-check |
| Yedek teyit | Webhook (bant-dışı, idempotent) |

## 4. Girdi & Enjeksiyon
- **SQL injection:** Yok — tüm sorgular EF Core LINQ (parametreli), raw SQL kullanılmaz.
- **Mass assignment:** Entity'ler doğrudan bind edilmez; ayrı Request DTO'ları + FluentValidation.
- **Model validation:** `[ApiController]` otomatik 400 + tüm mutasyonlarda validator.
- **Idempotency:** `IdempotencyMiddleware` — `Idempotency-Key` başlıklı tüm POST/PUT'larda çift işlem engeli.

## 5. Veri Koruma
- **Field-level encryption:** `IEncryptionProvider` (AES-256-GCM — gizlilik + bütünlük). 2FA secret DB'de şifreli.
- **Hassas veri maskeleme:** `Divisima.Core.Utilities.Text.KanitMaskesi` — ham gövdeyi/jetonu çıktıya veya loga koyan her yer buradan geçer. Ayrıca GF-5/K6 ile **global bir nokta** açıldı: Serilog'un iki sink'i de `MaskeliFormatter` (`ITextFormatter`) üzerinden yazar, çünkü sızan satırları uygulama kodu değil EF Core / SQL Server üretiyordu (ölçüldü: maskeli ve maskesiz satırın md5'i aynıydı). Çerçeve metinleri için ölçüt `LogMetniMaskesi`dedir (SQL "Truncated value" + EF parametre dökümü); `KanitMaskesi`nin kendi ölçütü GENİŞLETİLMEDİ. `SensitiveDataMask` sınıfı depoda durur ama **çağıranı yoktur (0) — ölü koddur**.
- **Response sızıntısı:** password_hash/salt asla DTO'da değil (ayrı response DTO'ları).
- **Secrets:** `ISecretProvider` (config/env → Azure Key Vault/AWS Secrets Manager iskeleti). Kod dokunulmadan kasaya geçiş.

## 6. Altyapı & DoS
- **Rate limiting:** Global 100/dk + auth 10/dk + payment 10/dk + **hassas 20/dk** (IP başına, endpoint-bazlı) — tek kaynak `RateLimitPolitikasi.Olustur` (`RateLimitPolitikasi.cs:70-79`), `RateLimit:*PermitLimit` ile ezilebilir. Redis yolundaki eski auth 5/dk **BİLİNÇLİ OLARAK TERK EDİLDİ** (`RateLimitPolitikasi.cs:62-64`). 429 reddi artık güvenlik olayı yazar (60 sn örneklemeyle, kova+IP başına).
- **Request limiti:** Kestrel body 1 MB + header 32 KB (dev payload DoS).
- **Transport:** HTTPS redirect (`app.UseHttpsRedirection()`) + güvenli cookie. **HSTS uygulama katmanında DEĞİL, TEK KAYNAK nginx'tedir** (`ops/infra/nginx.conf:26`); `app.UseHsts()` GF-3/K6 ile kaldırıldı çünkü iki farklı STS başlığı üretiliyordu (ölçüldü: depoda `app.UseHsts()` 0 geçiş).
- **Güvenlik başlıkları:** X-Frame-Options (clickjacking), X-Content-Type-Options (MIME), CSP, Referrer-Policy, Permissions-Policy, Server gizleme.

## 7. İzleme & Müdahale
- **Güvenlik olay logu:** `SecurityEvent` — bugün üretilen tipler: `LoginFailed` (kayıtlı **ve** kayıtsız e-posta; ikisi `customer_id`nin dolu/null olmasıyla ayrılır), `AccountLocked`, `ChangePasswordFailed`, `AccountDeleted`, `TwoFactorChallenge`, `TwoFactorFailed`, `RefreshTokenReuse`, `ResetPassword`, `Logout`, `IdorAttempt` (sahiplik ihlali — **kapsam Order + Payment**, kalan yedi manager BİLİNEN), `RateLimitExceeded`, `PaymentSignatureInvalid`. `ip_address` ve `user_agent` GF-5/K1 ile `SecurityEventManager` içinde doldurulur (önceden 7 çağrının 7'sinde de null geçiliyordu). Akış Serilog Console + File sink'lerine gider — **SIEM bağlı DEĞİLDİR** (`ops/serilog-siem.md`).
- **Anormallik/alerting:** `severity == "Critical"` olaylarda `NotifyAdminsAsync` çağrılır (`SecurityEventManager.cs:39-40`) ve SignalR `"admins"` grubuna yayın yapılır. **BUGÜN BU GRUP BOŞTUR**: `NotificationHub.JoinAdminGroup()` çağıranı yoktur (ölçüldü: istemci tarafında SignalR 0 geçiş) — hiçbir alarm bir insana ULAŞMAZ. Mail dalı YOKTUR. Okuyucu launch sonrasıdır.
- **Correlation id:** Her istek izlenebilir; audit log (kim neyi ne zaman değiştirdi).
- **Health checks:** /health (DB) + OpenTelemetry (tracing/metrics).

## Middleware pipeline (sıra)
```
Serilog request logging → ExceptionMiddleware → SecurityHeaders → CorrelationId →
HTTPS redirect → CORS → RateLimiter → Idempotency → Authentication →
TokenBlacklist → Authorization → Controllers
```

## Production öncesi zorunlu kontrol listesi
- [ ] `Encryption:Key` — 32 byte rastgele, Key Vault'ta
- [ ] `TokenOptions:SecurityKey` — güçlü, kasada, periyodik rotasyon
- [ ] `Iyzico:*` — production anahtarları, kasada
- [ ] `Captcha:Enabled=true` + gerçek Turnstile secret
- [ ] `Vault:Enabled=true` — gerçek kasa entegrasyonu
- [ ] DB kullanıcısı en az yetki (DDL yok)
- [ ] `dotnet list package --vulnerable` temiz + CI'da Dependabot/Snyk
- [ ] TLS 1.2+ zorunlu, HSTS preload
- [ ] Şifreli yedek + felaket kurtarma planı
- [ ] Penetrasyon testi + OWASP ZAP CI taraması
- [ ] KVKK/GDPR: saklama süreleri, silme hakkı, açık rıza

---

## 8. Gerçek Entegrasyonlar (feature flag ile dev↔production)
| Entegrasyon | Dev (flag=false) | Production (flag=true) |
|-------------|------------------|------------------------|
| Iyzico ödeme | Güvenli placeholder | Gerçek Iyzipay SDK (CF init + sunucu-sunucu retrieve) |
| Captcha | Atlanır | Gerçek Turnstile siteverify (fail-closed) |
| Cache/Lock/Blacklist | In-memory | Redis (dağıtık, RedLock) |
| Secrets | appsettings/env | Azure Key Vault (managed identity, 5 dk cache) |

## 9. CI/CD Güvenlik (`.github/`)
- **security.yml:** dependency-scan (`dotnet list --vulnerable`, build kırar) + CodeQL (security-extended) + Gitleaks (secret taraması) + testler. Haftalık zamanlanmış tarama.
- **dependabot.yml:** haftalık otomatik bağımlılık güncelleme, güvenlik güncellemeleri öncelikli.

## 10. Altyapı (`ops/`)
- **infra/nginx.conf:** TLS 1.2/1.3, HSTS preload, OCSP stapling, rate/connection limit, güvenlik başlıkları, Hangfire iç-ağ kısıtı.
- **infra/waf-rules.md:** Cloudflare/AWS WAF/ModSecurity (OWASP CRS), DDoS, bot koruması, rate limiting.
- **db/least-privilege.sql:** DB kullanıcısı yalnız CRUD (DDL/DROP/xp_cmdshell yok).
- **db/encrypted-backup.sql:** TDE (AES-256 at-rest) + şifreli yedek.
- **rotate-secrets.sh:** JWT signing key rotasyonu (Key Vault, 90 günlük). **Encryption key rotasyonu DESTEKLENMİYOR (SA-2):** `AesEncryptionProvider` tek anahtarlıdır (`keyId`/versiyonlama 0 geçiş) ve `Decrypt` çözemediği değeri OLDUĞU GİBİ döndürür (`AesEncryptionProvider.cs:53-57`) — anahtar değişirse eski şifreli alan düz metin sanılıp yeniden şifrelenir (çift şifreleme, sessiz veri kaybı). Script'in kendi uyarısında geçen re-encryption job'ı YOKTUR (`ops/rotate-secrets.sh:21-23`). Bugün `Encryption:Key` yalnız `customers.two_factor_secret` alanına uygulanır.
- **serilog-siem.md:** SIEM entegrasyonu için **TARİF** belgesi — bugün bağlı DEĞİL (aktif sink'ler yalnız Console + File, `Program.cs`; Elasticsearch/Seq paket referansı 0). SIEM launch sonrası.
- **deployment-checklist.md:** production öncesi feature flag + secret + yetki kontrol listesi.
- **Dockerfile (depo kökü, `ops/` altında DEĞİL):** non-root kullanıcı, minimal image, secret gömülmez, healthcheck.

## 11. Kabul Edilen Riskler

Bu bölüm, bilerek kapatılmayan güvenlik bulgularını, gerekçesini ve yeniden
değerlendirme tetikleyicisini kayda geçirir. Tarihsiz veya gerekçesiz kabul yoktur.

### AutoMapper — CVE-2026-32933 / GHSA-rvv3-g6hj-g44x (High, CVSS 7.5)
**Karar tarihi:** 20 Ağustos 2026 · **Durum:** kabul edilen risk · **Sürüm:** AutoMapper 12.0.1

**Zafiyet:** Derin iç içe nesne graflarında kontrolsüz özyineleme. Kendine referans veren
(~25.000+ seviye) bir nesne grafiği `StackOverflowException` üretir; .NET'te bu istisna
yakalanamadığı için tek bir istek değil **tüm süreç** ölür (DoS).

**Neden maruz DEĞİLİZ (kanıtlar):**
1. **`ProjectTo` kullanılmıyor** — tüm çözümde sıfır eşleşme.
2. **İstemci girdisinden entity'ye eşleme yalnız 10 noktada** — Address ×2, Category ×2,
   Collection ×2, Coupon ×2, Product ×2 (her biri için bir ekleme + bir güncelleme yolu)
   ve bu isteklerin DTO'ları **düz**: en derini
   `ProductAddRequestDto.stocks : List<ProductStockDto>`, `ProductStockDto` ise yalnız
   `string` + `int` içerir. İstemci girdisinden ulaşılabilen azami graf derinliği **2**.
   Hiçbir istek DTO'sunda kendine referans veya döngü yok - tip grafiği sonlu ve döngüsüz.

   > **Üreten ifade (bu sayı ezberden yazılmaz).** Eşleme İKİ AYRI BİÇİMDE yazılıyor ve
   > tek bir çapa ikisini birden yakalamaz:
   > ```
   > # ekleme yolları  - jenerik biçim, hedefi ENTITY olanlar (5)
   > grep -rnE '_mapper\.Map<(Address|Category|Collection|Coupon|Product)>' --include=*.cs Divisima.Bussiness
   > # güncelleme yolları - jenerik OLMAYAN biçim (5; 6. eşleşme bir YORUM satırıdır)
   > grep -rnE '_mapper\.Map\([^<]' --include=*.cs Divisima.Bussiness
   > # negatif kontrol
   > grep -rc 'ProjectTo' --include=*.cs Divisima.Bussiness Divisima.Dal Divisima.API   # -> 0
   > ```
   > Ölçüm: `_mapper.Map<` toplam 25 geçişin 20'si `*ResponseDto`/`List<*ResponseDto>`
   > hedefler (çıkış yönü, istemci girdisi DEĞİL); ENTITY hedefli olan 5'tir.
   > **Bu belge daha önce 7 diyordu.** Eski sayım Address, Category ve Collection'ı BİRER,
   > Coupon ve Product'ı İKİŞER kez sayıyordu (1+1+1+2+2 = 7). Yani biçim körlüğü KISMİYDİ:
   > jenerik-olmayan güncelleme yollarından ikisi (Coupon, Product) zaten sayılmıştı.
   > Atlanan **üç** kalem Address, Category ve Collection'ın güncelleme yollarıdır —
   > `AddressManager.cs:43`, `CategoryManager.cs:48`, `CollectionManager.cs:64`.
   > Sayı `GuvenlikFix4SozlesmeTests` ile pinlidir - eşleme yüzeyi değişirse test kırmızı
   > verir ve bu paragraf güncellenmeden geçilemez.
3. **JSON bağlama derinlik sınırı**: özel `MaxDepth` ayarı yok, yani System.Text.Json
   varsayılanı (64) geçerli. 25.000 seviyelik bir gövde AutoMapper'a ULAŞMADAN,
   deserialization aşamasında reddedilir.

**Neden yükseltmiyoruz:** Yamalı sürümler 15.1.1 ve 16.1.1'dir. AutoMapper 15'ten itibaren
lisans **RPL-1.5** (güçlü copyleft - onunla derlenen yazılımın kaynağını yayımlamayı
zorunlu kılar) **veya** Lucky Penny Software ticari lisansıdır. 12/13/14 sürümleri MIT'tir
ama **üçü de aynı advisory kapsamındadır** (ölçüldü: 13.0.1 ve 14.0.0 build'de aynı NU1903
uyarısını veriyor). Yani "MIT kalarak yamalı sürüme geçmek" mümkün değil.

**Yeniden değerlendirme tetikleyicileri (herhangi biri):**
- MIT (veya uyumlu izinli) lisanslı yamalı bir AutoMapper sürümü yayımlanırsa,
- Ticari lisans satın alınmasına karar verilirse,
- Eşleme yüzeyimiz değişirse: `ProjectTo` eklenirse, ya da bir istek DTO'suna kendine
  referans veren / döngü kurabilen bir alan eklenirse (bu belge o anda güncellenmelidir).

**Telafi edici kontroller:** `dependency-scan` kapısındaki istisna advisory-id bazlıdır
(`GHSA-rvv3-g6hj-g44x`) - başka bir zafiyet çıkarsa kapı yine kırar.

### Microsoft.Identity.Client.Extensions.Msal 4.67.2 — kullanım dışı, GEÇİŞLİ

**Kayıt tarihi:** 4 Eylül 2026 · **Durum:** kabul edilen risk (zafiyet değil, `Other` sınıfı
kullanım dışı kaydı) · **Konum:** GEÇİŞLİ — ölçüldü: `*.csproj` içinde doğrudan başvuru **0**,
`packages.lock.json` `"type": "Transitive"` (pozitif kontrol: `Microsoft.Identity.Client`
aynı dosyada `"Direct"`). İşlevi 4.61'den itibaren MSAL'ın kendisine katıldığı için üst
sürümü yok; yalnız `Azure.Identity` bırakınca düşer. Üst referans olarak **eklenmez** —
eklemek, çözümlenen sürümü zorlamadan sahte bir "doğrudan bağımlılık" yaratırdı.
**Yeniden değerlendirme:** `Azure.Identity` bir üst sürüme çıktığında zincir yeniden ölçülür.
Not: bu kayıt, GF-4'te kapatılan **CriticalBugs 6 → 0** sonucundan ayrıdır — "CriticalBugs
yok" ile "kullanım dışı paket yok" aynı şey değildir.
