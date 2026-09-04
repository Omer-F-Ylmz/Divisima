# DIVISIMA BACKEND — GÜVENLİK DÖKÜMANI

Bu belge tehdit modelini, uygulanan tüm güvenlik katmanlarını ve operasyonel önerileri içerir.

> **Önemli:** "Hiçbir açık yok" hiçbir sistemde mutlak garanti edilemez. Güvenlik bir durum değil
> süreçtir: bağımlılık güncellemeleri, düzenli penetrasyon testi, secrets rotasyonu, izleme gerekir.
> Aşağıdakiler uygulama-seviyesi bilinen tüm yaygın saldırı vektörlerini (OWASP Top 10 dahil) kapatır.

## 1. Kimlik Doğrulama & Oturum
| Katman | Uygulama |
|--------|----------|
| Şifre saklama | HMAC-SHA512 hash + benzersiz salt (düz şifre asla saklanmaz) |
| Şifre politikası | Min 8 karakter, büyük/küçük harf + rakam (FluentValidation) |
| JWT | Kısa ömürlü access token + jti (token id) |
| Token iptali | `ITokenBlacklist` (Redis-uyumlu) + `TokenBlacklistMiddleware` — logout/şifre değişiminde token anında geçersiz |
| Refresh token | httpOnly + Secure + SameSite=Strict cookie (JS erişemez → XSS'te çalınamaz), rotation |
| Hesap kilitleme | 5 başarısız denemede 15 dk kilit (brute-force) |
| Şifre sıfırlama | Tek kullanımlık token (30 dk), enumeration-safe, sıfırlamada tüm oturum iptali |
| 2FA/MFA | RFC 6238 TOTP (Google Authenticator uyumlu, ±1 pencere, `ITwoFactorService`) |
| Bot koruması | `ICaptchaValidator` (Cloudflare Turnstile) — register/forgot/riskli login |

## 2. Yetkilendirme (IDOR)
- `[RequireUserType(Admin/Customer)]` custom policy + authorization handler.
- **Kaynak sahipliği:** `SecureControllerBase.CurrentCustomerId` — kullanıcı kimliği JWT'den alınır,
  route/body'den ASLA. Address/Cart/Wishlist/Order/Payment kendi kaynağına erişim zorunlu (IDOR engeli).

## 3. Ödeme Güvenliği (en kritik)
| Vektör | Koruma |
|--------|--------|
| Sahte callback | HMAC-SHA256 imza doğrulama (timing-safe) |
| Callback güveni | Sonuç **sunucu-sunucu** Iyzico'dan token ile çekilir, callback gövdesine güvenilmez |
| Tutar manipülasyonu | Ödenen tutar == sipariş tutarı kontrolü |
| Para birimi | Sipariş = ödeme para birimi kontrolü |
| Fraud | Iyzico fraudStatus onayı zorunlu |
| Kart testi | Velocity limiti (müşteri başına 10 dk'da 5 deneme) + rate limit (10/dk) |
| PCI-DSS | Kart bilgisi sunucuya HİÇ gelmez (Iyzico Checkout Form iframe) |
| IDOR | Kullanıcı yalnızca kendi siparişini öder (JWT) |
| Sipariş durumu | Sadece Pending + ödenmemiş + tutar>0 siparişe ödeme |
| Replay | Idempotency + token 30 dk zaman aşımı |
| Race condition | Distributed lock (`IDistributedLock` — Redis RedLock) + kilit sonrası double-check |
| Yedek teyit | Webhook (bant-dışı, idempotent) |

## 4. Girdi & Enjeksiyon
- **SQL injection:** Yok — tüm sorgular EF Core LINQ (parametreli), raw SQL kullanılmaz.
- **Mass assignment:** Entity'ler doğrudan bind edilmez; ayrı Request DTO'ları + FluentValidation.
- **Model validation:** `[ApiController]` otomatik 400 + tüm mutasyonlarda validator.
- **Idempotency:** `IdempotencyMiddleware` — `Idempotency-Key` başlıklı tüm POST/PUT'larda çift işlem engeli.

## 5. Veri Koruma
- **Field-level encryption:** `IEncryptionProvider` (AES-256-GCM — gizlilik + bütünlük). 2FA secret DB'de şifreli.
- **Hassas veri maskeleme:** `SensitiveDataMask` — kart benzeri sayılar + token'lar loglanmadan maskelenir.
- **Response sızıntısı:** password_hash/salt asla DTO'da değil (ayrı response DTO'ları).
- **Secrets:** `ISecretProvider` (config/env → Azure Key Vault/AWS Secrets Manager iskeleti). Kod dokunulmadan kasaya geçiş.

## 6. Altyapı & DoS
- **Rate limiting:** Global 100/dk + auth 5/dk + payment 10/dk (IP başına, endpoint-bazlı).
- **Request limiti:** Kestrel body 1 MB + header 32 KB (dev payload DoS).
- **Transport:** HTTPS redirect + HSTS (production) + güvenli cookie.
- **Güvenlik başlıkları:** X-Frame-Options (clickjacking), X-Content-Type-Options (MIME), CSP, Referrer-Policy, Permissions-Policy, Server gizleme.

## 7. İzleme & Müdahale
- **Güvenlik olay logu:** `SecurityEvent` (başarısız login, kilitlenme, ödeme reddi, fraud, IDOR) — ayrı akış + structured log (Serilog → SIEM).
- **Anormallik/alerting:** Critical olaylarda admin'e anlık bildirim (SignalR/mail).
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
- **nginx.conf:** TLS 1.2/1.3, HSTS preload, OCSP stapling, rate/connection limit, güvenlik başlıkları, Hangfire iç-ağ kısıtı.
- **waf-rules.md:** Cloudflare/AWS WAF/ModSecurity (OWASP CRS), DDoS, bot koruması, rate limiting.
- **least-privilege.sql:** DB kullanıcısı yalnız CRUD (DDL/DROP/xp_cmdshell yok).
- **encrypted-backup.sql:** TDE (AES-256 at-rest) + şifreli yedek.
- **rotate-secrets.sh:** JWT + encryption key rotasyonu (Key Vault, 90 günlük).
- **serilog-siem.md:** güvenlik olayları → Elasticsearch/Seq + alerting kuralları.
- **deployment-checklist.md:** production öncesi feature flag + secret + yetki kontrol listesi.
- **Dockerfile:** non-root kullanıcı, minimal image, secret gömülmez, healthcheck.

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
   > **Bu belge daha önce 7 diyordu**: yalnız jenerik biçim sayılmış, Address ve Category'nin
   > güncelleme yolları atlanmıştı. Sayı `GuvenlikFix4SozlesmeTests` ile pinlidir - eşleme
   > yüzeyi değişirse test kırmızı verir ve bu paragraf güncellenmeden geçilemez.
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
