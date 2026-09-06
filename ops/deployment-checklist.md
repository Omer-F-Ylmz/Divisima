# Production Deployment Kontrol Listesi

## Feature flag'ler (appsettings / env) - PRODUCTION'DA AÇILACAK
| Flag | Dev | Production |
|------|-----|------------|
| `Iyzico:UseRealSdk` | false | **true** (gerçek Iyzipay anahtarlarıyla) |
| `Captcha:Enabled` | false | **true** (gerçek Turnstile secret) |
| `Redis:Enabled` | false | **true** (dağıtık cache/lock/blacklist) |
| `Vault:Enabled` | false | **true** (secret'lar Key Vault'ta) |

## Secret'lar (Key Vault'a - appsettings'te ASLA)
- `TokenOptions--SecurityKey` (256-bit)
- `Encryption--Key` (32 byte base64)
- `Iyzico--ApiKey`, `Iyzico--SecretKey`
- `Captcha--SecretKey`
- `ConnectionStrings--DivisimaDb`

## Veritabanı şeması - UYGULAMA AÇILMADAN ÖNCE (D-ŞEMA-FIX)

**Uygulama açılışta migrate ETMEZ** (`Program.cs`'te `Migrate()` çağrısı yoktur) ve bu
checklist'in aşağıdaki maddesi uygulamanın DB kullanıcısına **DDL yetkisi vermez**. Yani şema
kurulumu **ayrı ve ayrıcalıklı bir adımdır**; kimse yapmazsa uygulama boş/eksik bir şemaya
bağlanır. Bu adım bugüne kadar checklist'te **hiç yoktu**.

**Tek doğruluk kaynağı: `Divisima.Dal/Migrations`.** İki uygulama yolu vardır, ikisi de aynı
sonucu verir — ortamda .NET araç zinciri varsa (a), yoksa (b):

- [ ] **(a) EF ile** — DDL yetkili bir hesapla, uygulama sürümüyle **aynı** commit'ten:
      `dotnet ef database update --project Divisima.Dal --startup-project Divisima.API`
- [ ] **(b) Script ile** — `database/mssql/01_schema.sql` (üretilmiş, idempotent):
      `sqlcmd -S <sunucu> -d Divisima -b -f 65001 -i database/mssql/01_schema.sql`
      **`-b` ve `-f 65001` ZORUNLU** — gerekçesi dosyanın başında ve `database/README.md`'de
      (bayraksız koşum, script'in yarısı çalışmasa bile `EXIT 0` döndürür).
- [ ] Veritabanı **`Turkish_CI_AS`** collation ile oluşturuldu
      (`CREATE DATABASE Divisima COLLATE Turkish_CI_AS`) — Latin1 kurulumda kimlik
      karşılaştırmaları sessizce yanlış çalışır (CLAUDE.md bölüm 6c)
- [ ] Şema kurulduktan **sonra** uygulama başlatıldı (AdminSeeder ilk admini o anda oluşturur)
- [ ] Kurulum sonrası doğrulandı: `SELECT COUNT(*) FROM sys.foreign_keys` → **56** ve
      `sys.tables` → **45** (+ `__EFMigrationsHistory`)
- [ ] Uygulamanın çalışma zamanı DB kullanıcısı **DDL yetkisiz** (aşağıdaki "Zorunlu adımlar")
      — migration'ı koşan hesap AYRI ve yalnızca dağıtım anında kullanılır
- [ ] **Recovery modeli `FULL`** — `SELECT DATABASEPROPERTYEX('Divisima','Recovery')` → `FULL`.
      **SIMPLE ise runbook'un RPO 15 dk hedefi ve point-in-time geri yükleme proseduru
      IMKANSIZDIR** (D6 tatbikatında ölçüldü: `BACKUP LOG` → `Msg 4208`). `FULL`'e geçildikten
      **sonra** bir full yedek alınmalı — log zinciri ancak öyle başlar.
- [ ] SQL Server sürümü **Express DEĞİL** — Express `backup compression` ve `TDE`
      desteklemiyor (D6'da ölçüldü: `Msg 1844`), yani "yedekler şifreli olmalı" maddesi
      Express'te karşılanamaz

> **SIRA:** şema → (opsiyonel `02_seed.sql`) → uygulama açılışı → frontend dağıtımı.
> Migration üreten bir sürüm yayınlanıyorsa şema adımı **kod deploy'undan ÖNCE** koşar
> (expand-migrate-contract; bkz. `ops/backup-dr-runbook.md`).

## Frontend origin'i - HER YAYINDA (DALGA-4-FIX-2 / M1)

Storefront ve admin panelinin API tabanı **kaynakta sabit gömülüdür** ve dağıtımda
yazılmalıdır. Depoda commit'li değer `http://localhost:5000`'dir; yerelde hiçbir ek adım
gerekmez, **yayında ise bu adım atlanırsa istekler son kullanıcının kendi makinesine
gider ve katalog boş gelir** (ölçüldü).

```bash
ops/set-api-origin.sh https://api.divisima.com   # yaz
ops/set-api-origin.sh --verify                   # doğrula (tutarsızsa exit 1)
```

Betik **tek girdiden** hem `meta[name="divisima-api-origin"]` değerini hem de CSP'nin
`img-src` / `connect-src` / `form-action` direktiflerini yazar - elle senkron YOKTUR.
Sayfa açılırken bir **tutarlılık guard'ı** aynı kontrolü tarayıcıda tekrarlar ve
uyuşmazlıkta ekrana kırmızı bir uyarı basar (sessizce yanlış origin'e düşmez).

- [ ] `ops/set-api-origin.sh <origin>` koşuldu ve `--verify` **exit 0** verdi
- [ ] Backend `Iyzico:CallbackUrl` origin'i **aynı** origin (form-action senkron kuralı -
      callback POST'u tarayıcıdan gelir; uyuşmazsa ödeme sonucu sessizce kaybolur)
- [ ] `frontend/service-worker.js` içindeki `VERSION` bump'landı (eski önbellek temizlensin)
- [ ] Yayın sonrası: storefront gerçek adresinden açıldı, katalog **dolu** geldi ve
      konsolda `[DIVISIMA YAPILANDIRMA]` satırı **yok**

## Storefront'u kim sunuyor - DALGA C / C1

İki origin: **API `api.divisima.com`**, **storefront `divisima.com`**. Bu ayrım depo genelinde
varsayılıdır (`AllowedOrigins`, `Storefront:BaseUrl`, `Cookies:Domain=.divisima.com`).

- [ ] `ops/infra/nginx.conf` sunucuya kuruldu — **iki server block da** (`api.divisima.com`
      ve `divisima.com`) yürürlükte
- [ ] `frontend/` içeriği **`ops/set-api-origin.sh` koşulduktan SONRA** `/var/www/divisima`
      altına kopyalandı (sıra ters olursa storefront localhost'a bakar)
- [ ] `https://divisima.com/sitemap.xml` **200 + XML** döndü (nginx `/api/seo/sitemap`'e
      proxy'ler; `robots.txt` bu adresi gösteriyor)
- [ ] `https://divisima.com/admin.html` yanıtı `X-Robots-Tag: noindex` taşıyor
- [ ] `ops/infra/divisima-security-headers.conf` **nginx.conf ile aynı dizine** kuruldu
      (varsayılan `/etc/nginx/`). Eksikse nginx **açılmaz** — bu bilinçli: sessiz bir
      başlık boşluğu yerine gürültülü bir hata
- [ ] `nginx -t` **exit 0** (include yolu ve iki server block sözdizimi doğrulanır)
- [ ] **Clickjacking (GÜVENLİK-FIX-3 / #4)** — yayın sonrası `curl -sI` ile ÜÇ adres
      ayrı ayrı kontrol edildi; **üçünde de** `X-Frame-Options: DENY` **ve**
      `Content-Security-Policy: frame-ancestors 'none'` görünüyor:
      `https://divisima.com/` · `https://divisima.com/index.html` · `https://divisima.com/admin.html`
      > **Üçü de ayrı ayrı bakılır, gerekçesi ölçülmüş bir nginx davranışıdır:**
      > `add_header` bir önceki seviyeden YALNIZCA o seviyede hiç `add_header` yoksa
      > devralınır. Bu üç adres, kendi `add_header`ını tanımlayan location'lara düşer;
      > `include` satırı düşerse başlıklar **yalnız onlarda** sessizce kaybolur — yani
      > sadece `https://divisima.com/robots.txt`e bakan bir doğrulama YEŞİL görünürdü.
- [ ] **İç dokümanlar kapalı (GÜVENLİK-FIX-3 / #6)** — hepsi **404**:
      `/API-CONTRACT.md` · `/INTEGRATION.md` · `/SEO-ANALYTICS.md` · `/vendor/README.txt` ·
      `/test/mobil-erisilebilirlik.js`
- [ ] **Kapsam fazla geniş değil** — hepsi **200**: `/` · `/index.html` · `/api-bridge.js` ·
      `/manifest.json` · `/robots.txt` · `/vendor/purify.min.js` ·
      **`/.well-known/security.txt`** (RFC 9116; gizli-dosya kuralına takılırdı, açık
      muafiyeti vardır — bu satır o muafiyetin tek doğrudan kanıtıdır)

Yerelde karşılığı `docker compose up` — `frontend` servisi aynı davranışı `:5173`'te verir
(`ops/infra/frontend-dev.conf`). **İki bilinçli ayrışma vardır** (ikisi de TLS/HSTS'in orada
bulunmamasıyla aynı gerekçede — yerel düz-HTTP bir geliştirme sunucusu farklı bir tehdit
modelidir): `/test/` yerelde **açık kalır** (Dalga 4'ün pin boşluğunu telafi eden ölçüm
betiği tarayıcıya elle yüklenir) ve clickjacking başlığı yerelde **yoktur**. İç doküman /
gizli dosya / yedek artığı 404'leri **ikisinde de aynıdır**.

## Yüklenen görsellerin kalıcılığı - DALGA C / C2

- [ ] API konteynerinde `/app/wwwroot/uploads` bir **kalıcı volume**'e bağlı
      (compose'da `uploads_data`; k8s/başka orkestratörde eşdeğeri)
- [ ] Konteyner yeniden oluşturulduktan sonra **var olan bir ürün görseli hâlâ 200 dönüyor**
      (volume yoksa `product_images` satırları var olmayan dosyaları gösterir → kalıcı 404)

## İlk admin - DALGA C / C3

- [ ] `AdminSeed:Enabled=true` + `AdminSeed:Email` + `AdminSeed:Password` **secret olarak**
      verildi (appsettings'e YAZILMAZ)
- [ ] Şifre politikayı karşılıyor (≥8, büyük, küçük, rakam) — karşılamıyorsa admin
      **oluşturulmaz** ve log'a `AdminSeed sifresi POLITIKAYA UYMUYOR` düşer
- [ ] Uygulama açıldıktan sonra **panele gerçekten giriş yapıldı** (seed hatası uygulamayı
      durdurmaz; tek doğrulama girişin kendisidir)
- [ ] Giriş doğrulandıktan sonra `AdminSeed:Enabled` **false**'a çekildi ve `Password`
      secret'ı kaldırıldı/rotate edildi

## Redis ve rate limit - DALGA D / D5

**`Redis:Enabled=true` iken Redis ERİŞİLEMEZSE uygulama HİÇ AÇILMAZ** — ölçüldü:
`StackExchange.Redis.RedisConnectionException`, `Program.cs`'te bağlantı kurulurken.
Sessizce in-memory'ye **düşmez**. Bu bilinçli ve doğru: dağıtık kilit/sayaç olmadan açılan bir
sunucu, koruma varmış gibi davranırdı. Ama sonucu şudur: **Redis kesintisi = deploy blokajı**,
dolayısıyla Redis'i uygulamadan **önce** ayağa kaldırın.

- [ ] Redis erişilebilir ve `Redis:Connection` doğru (uygulama açılmıyorsa önce burayı bakın —
      hata mesajı `redis` kelimesini içerir, şema/JWT ile ilgisi yoktur)
- [ ] **`RateLimit` BÖLÜMÜNÜN AYAR DOSYASINDA GERÇEKTEN VAR OLDUĞU doğrulandı.**
      FAZ 1'de ÖLÇÜLDÜ: bu bölüm `appsettings.json` ve `appsettings.Development.json`
      dosyalarının **HİÇBİRİNDE YOK**; yalnız `appsettings.Development.example.json` içinde
      örnek olarak duruyor. Bölüm yoksa `RateLimitPolitikasi.Olustur` sessizce **KOD
      VARSAYILANINA** düşer (auth 10 / payment 10 / global 100) ve aşağıdaki "eşikler
      ayarlandı" maddesi **KARŞILIKSIZ** kalır — kimse bir şey ayarlamamıştır, yalnızca
      varsayılan yürürlüktedir. Belirti sessizdir: yanlış bir değer değil, **hiç değer
      olmaması** durumu. D5, iki yolun ayrışmasını kapattı; bu madde ayarın VAR OLDUĞUNU
      kapatır. [HAVALE→FAZ 8]
- [ ] `RateLimit:AuthPermitLimit` / `PaymentPermitLimit` / `GlobalPermitLimit` prod trafiğine
      göre ayarlandı. **Bu ayarlar artık HER İKİ yolda da okunur** (D5 öncesinde Redis yolu
      kaynakta sabit 5/10/100 kullanıyordu ve ayarları HİÇ okumuyordu)
- [ ] Yayın sonrası bir auth ucuna ayarlanan limitten bir fazla istek atıldı ve **429** alındı
      (limitin gerçekten yürürlükte olduğunun tek doğrudan kanıtı)

> Rate limit iki katmanlıdır ve **ikisi de her ortamda devrededir**: yol bazlı dağıtık
> middleware (çok sunucuda merkezi sayaç) + .NET yerleşik limiter (`[EnableRateLimiting]`
> öznitelikleri). İkisi de değerleri `RateLimitPolitikasi`den okur. Çifte sayım **yoktur** —
> ölçüldü: iki sayaç aynı bölümleme anahtarıyla ve aynı limitle kilitli adımda ilerler
> (`RateLimitTekKaynakTests`).


## Ters proxy ve gerçek istemci IP'si - GÜVENLİK-FIX-3 / #3

**Rate limit, webhook IP allowlist ve audit IP'nin ÜÇÜ DE `RemoteIpAddress`'e dayanır.**
Uygulama `X-Forwarded-For`'a **yalnızca güvenilen bir proxy'den geldiyse** güvenir
(`Program.cs`, `ForwardedHeadersOptions`): `ForwardedHeaders:KnownProxies` **boşsa** ASP.NET'in
güvenli varsayılanı (`127.0.0.1` + `127.0.0.0/8`) yürürlükte kalır ve keyfi bir XFF başlığı
**yok sayılır** — yani spoofing kapalıdır, bu doğru taraftır.

Ama sonucu **dağıtımın şekline bağlıdır** ve iki yönlü hata mümkündür:

| Topoloji | `KnownProxies` | Sonuç |
|---|---|---|
| nginx API ile **aynı makinede** (`proxy_pass http://127.0.0.1:5000`) | boş bırakılabilir | XFF güvenilir → **istemci başına** kova ✅ |
| nginx/LB **ayrı makinede/konteynerde** (bulut LB, k8s ingress, compose ağı) | **boş** | XFF yok sayılır → **herkes tek kovada**: auth limiti tüm site için 10/dk ❌ |
| aynısı | **dolu** | XFF güvenilir → istemci başına kova ✅ |

Depodaki `ops/infra/nginx.conf` **loopback'e** proxy'ler, yani belgelenen topolojide boş
bırakmak doğrudur (GÜVENLİK DALGASI 2'de ölçüldü: `XFF=9.9.9.9` 10 istekte tükendi,
`XFF=8.8.8.8` **taze kova** aldı). Farklı bir topolojiye geçen ekip bunu **fark etmez** —
hata sessizdir, tek belirtisi "rate limit çok erken tetikleniyor" şikâyetidir.

- [ ] Topoloji belirlendi: nginx/LB API ile **aynı makinede mi**? Değilse
      `ForwardedHeaders:KnownProxies` = proxy/LB'nin IP'leri (`appsettings` **değil**,
      ortam değişkeni/secret)
- [ ] Proxy `X-Forwarded-For` ve `X-Forwarded-Proto` başlıklarını **gerçekten ekliyor**
      (depodaki nginx.conf ekler; başka bir ingress kullanılıyorsa doğrulanır)
- [ ] **Yayın sonrası doğrulama — bölümleme gerçekten çalışıyor mu:** aynı auth ucuna
      `X-Forwarded-For: 9.9.9.9` ile limitin bir fazlası kadar istek atılır (**429** beklenir),
      hemen ardından `X-Forwarded-For: 8.8.8.8` ile bir istek atılır. İkincisi **429 DEĞİLSE**
      bölümleme çalışıyor demektir. **İkincisi de 429 ise** herkes tek kovadadır →
      `KnownProxies` doldurulmamıştır
- [ ] `ForwardLimit = 1` yeterli mi kontrol edildi — **birden fazla** proxy hop'u varsa
      (CDN → LB → nginx) değer hop sayısına çıkarılmalıdır, aksi halde okunan IP bir
      önceki proxy'nin IP'sidir

### API portu dışarı açılmaz

- [ ] Üretimde **yalnız nginx** dışarı bakar (443/80). API'nin `5000` portu **public
      DEĞİL** — aksi halde nginx'in TLS'i, güvenlik başlıkları, rate limit'i ve
      `/hangfire` kilidi **atlanabilir**

> `docker-compose.yml` bir **üretim artefaktı DEĞİLDİR** — `ASPNETCORE_ENVIRONMENT:
> Development` yazar ve dosyanın başlığı "yerel geliştirme ortamı" der. Oradaki
> `5000:5000` ve `5173:80` açılımları **bilinçlidir**: gerçek cihaz turu (Dalga 4, telefon
> LAN üzerinden) için storefront'un DA API'nin DE LAN'dan erişilebilir olması gerekir.
> `sqlserver` ve `redis` ise gerekçesiyle `127.0.0.1:`e bağlıdır. Üretim orkestrasyonu
> ayrı bir artefakttır ve bu dosya o amaçla **kullanılmamalıdır**.

### Çerez kapsamı ve DNS hijyeni - GÜVENLİK DALGASI 2 / #7

`Cookies:Domain = .divisima.com` **bilinçlidir**: storefront (`divisima.com`) ile API
(`api.divisima.com`) farklı hostlardır ve CSRF double-submit'in çalışması için `csrf_token`
çerezinin storefront JS'i tarafından okunabilmesi gerekir (Sprint 8 madde 6'da ölçüldü).
Bedeli: `refresh_token` (httpOnly, path `/api/auth`) **her alt alan adına** gönderilir.

- [ ] Alt alan adları **sahipsiz bırakılmaz** — kullanılmayan `CNAME`/`A` kayıtları silinir
      (subdomain takeover ile ele geçirilen bir alt alan adı `/api/auth/*` servis ederse
      kullanıcıların refresh token'ını alır)
- [ ] Üçüncü taraf bir servise alt alan adı devredilmez (`*.divisima.com` wildcard
      yönlendirmesi verilmez)

**YENİ BİR ALT ALAN ADI AÇILMADAN ÖNCE (GÜVENLİK-FIX-4 / Dalga-2 #7):**

`Cookies:Domain = .divisima.com` **bugün var olanları değil, TÜM alt alan adlarını** kapsar —
yarın açılacak `staging.`, `blog.`, `cdn.`, `docs.` de otomatik olarak dahildir. Yani çerez
kapsamı bir kez verilen değil, **her yeni alt alan adında yeniden değerlendirilmesi gereken**
bir karardır.

- [ ] Yeni alt alan adı açılırken çerez kapsamı **yeniden değerlendirildi**: bu ada
      `refresh_token` ve `csrf_token` gitmesi **gerekiyor mu**? Gerekmiyorsa ya ayrı bir
      kayıt alanı (`divisima-cdn.com` gibi) kullanılır ya da `Cookies:Domain` daraltılır
- [ ] **Az güvenilir / üçüncü taraf içerik bu alan adının alt alan adına KONMAZ** —
      barındırılan blog/durum sayfası/pazarlama aracı/müşteri yüklemesi gibi içerikler dahil.
      Böyle bir alt alandaki tek bir XSS, `.divisima.com` kapsamındaki çerezlere erişir
      (`csrf_token` JS'ten okunabilir; `refresh_token` httpOnly ama aynı kapsamdaki bir
      sayfadan `/api/auth/*`'a giden isteklere **otomatik eklenir**)
- [ ] Statik varlıklar için ayrı bir alan adı kullanılıyorsa, o alan adı `divisima.com`'un
      **alt alanı değil** (aksi halde CDN sağlayıcısı çerez kapsamına girer)
## SIRALI DAĞITIM ADIMLARI (LF-1 — LAUNCH ÖLÇÜMÜNDEN ÜRETİLDİ)

> Her adımın **kanıtı** yazılıdır: "yaptım" demek yetmez, kanıt alınır.
> Şablon: `Divisima.API/appsettings.Production.example.json` (her anahtarın nereden alındığı
> orada tek satır yorumla yazılı). Ortam değişkeni biçimi: `Bolum__Anahtar` (iki alt tire).
> Üretim compose'u: `docker-compose.prod.yml` (geliştirme `docker-compose.yml` **KULLANILMAZ** —
> o dosya `ASPNETCORE_ENVIRONMENT: Development` taşır ve TÜM prod fail-fast kapılarını atlar).

| # | Adım | Kanıt nasıl alınır |
|---|------|--------------------|
| 1 | DNS: `divisima.com`, `www`, `api.divisima.com` A/AAAA kaydı | `dig +short` her üç ad için IP döner |
| 2 | TLS sertifikası `/etc/ssl/divisima/` altında | `openssl x509 -noout -dates -in fullchain.pem` |
| 3 | SQL Server: veritabanı **`COLLATE Turkish_CI_AS`** ile yaratıldı | DB **İÇİNDEN**: `SELECT DATABASEPROPERTYEX(DB_NAME(),'Collation')` |
| 4 | Recovery model **FULL**, `AUTO_CLOSE` **OFF** | `SELECT recovery_model_desc, is_auto_close_on FROM sys.databases WHERE name='DivisimaDb'` |
| 5 | En az yetkili DB kullanıcısı (`ops/db/least-privilege.sql`) | Betik 0 hata ile koşar; DDL denemesi reddedilir |
| 6 | `TokenOptions:SecurityKey` üretildi (≥ 32 bayt) | `openssl rand -base64 48`; açılışta fail-fast SESSİZ geçerse doğru |
| 7 | `Encryption:Key` üretildi (**TAM 32 bayt** base64) | `openssl rand -base64 32`; açılışta "TAM 32 bayt" hatası GELMEZSE doğru |
| 8 | `ConnectionStrings:DivisimaDb` dolduruldu | `/health/ready` **200** |
| 9 | **`Cookies:Domain` = `.divisima.com` (BL-1)** | Giriş sonrası tarayıcı konsolunda `document.cookie` içinde **`csrf_token` GÖRÜNMELİ**; görünmüyorsa `/api/auth/refresh` 15 dk sonra kalıcı 403 verir |
| 10 | `ForwardedHeaders:KnownProxies` = LB/nginx IP'leri | `security_events` `RateLimitExceeded` satırının `ip_address` alanı **gerçek istemci IP'si** olmalı, proxy IP'si değil |
| 11 | `MailSettings:*` gerçek SMTP (Host boşsa **açılış düşer**) | Şifre sıfırlama maili GELMELİ — admin kurtarma yolu buna bağlı (jeton DB'de **özet**, ham değer okunamaz) |
| 12 | İyzico canlı: `ApiKey` · `SecretKey` · `BaseUrl` · `UseRealSdk=true` | Canlı `BaseUrl`; gerçek bir test ödemesi 3D akışını tamamlamalı |
| 13 | **`Iyzico:CallbackUrl` mutlak HTTPS (fail-fast ZORUNLU)** | Boş/HTTP ise uygulama **AÇILMAZ** — açılması kanıttır |
| 14 | CallbackUrl origin'i storefront CSP `form-action` listesiyle **AYNI** | Tarayıcı konsolunda CSP ihlali OLMAMALI (E2b'de "para çekildi, sipariş Pending" bu yüzden yaşandı) |
| 15 | `Storefront:BaseUrl` = `https://divisima.com` | Ödeme sonrası `#/odeme/sonuc` adresine yönlendirmeli |
| 16 | `ops/set-api-origin.sh` ile vitrinin API origin'i yazıldı | Betiğin `--verify` modu **EXIT 0** |
| 17 | Redis ayakta, `Redis:Enabled=true`, `Redis:Connection` | Redis erişilemezse uygulama **AÇILMAZ** (D5'te ölçüldü) — açılması kanıttır |
| 18 | `BackgroundJobs:Enabled=true` | Hangfire recurring job listesi dolu; satılabilir stok KALICI düşük KALMAMALI (kapalıyken rezervasyonlar temizlenmez) |
| 19 | `AdminSeed` ile ilk admin açıldı, sonra **`Enabled=false`** | Admin girişi 200; ikinci açılışta YENİ admin OLUŞMAMALI (idempotent) |
| 20 | **GÜNLÜK: `PaymentAfterTerminal` sorgusu** | `SELECT * FROM security_events WHERE event_type='PaymentAfterTerminal' AND severity='Critical' AND created_at >= CAST(GETDATE() AS date)` — **çıkan her satır ELLE İADE gerektirir** |

> **20. ADIM NEDEN ELLE:** `security_events` için **okuma ucu YOKTUR** (controller'larda geçiş 0)
> ve `Critical` olaylarda çağrılan `NotifyAdminsAsync` SignalR `"admins"` grubuna yayın yapar —
> **o grup BOŞTUR** (`JoinAdminGroup()` çağıranı yok, `51·AV-2` BİLİNEN kalemi). Yani bu olayın
> bugün **otomatik okuyucusu yoktur**; sorgu koşulmazsa iade gereken vaka GÖRÜLMEZ.

## Zorunlu adımlar
- [ ] **`Cookies:Domain` üst alan adı biçiminde ayarlandı (`.divisima.com`) — LF-1/K1**
      Boş bırakılırsa uygulama **AÇILMAZ** (fail-fast). Bu kapı LF-1'de eklendi; öncesinde
      arıza **sessizdi** ve ancak ilk access token süresi dolduğunda (dağıtımdan ~15 dk sonra,
      TÜM kullanıcılarda aynı anda) ortaya çıkardı.
- [ ] **Günlük `PaymentAfterTerminal` / `Critical` sorgusu operasyon takvimine yazıldı**
      (20. adım; otomatik okuyucu YOK)
- [ ] `Iyzico:BaseUrl` = `https://api.iyzipay.com` (sandbox değil)
- [ ] `Webhook:AllowedIps` = Iyzico production IP aralıkları
- [ ] `AllowedOrigins` = yalnız gerçek frontend domain(ler)i
- [ ] DB kullanıcısı en az yetki (SELECT/INSERT/UPDATE; DDL/DROP yok)
- [ ] `dotnet list package --vulnerable` temiz
- [ ] Serilog SIEM sink aktif (bkz. serilog-siem.md)
- [ ] Rate limit eşikleri prod trafiğine göre ayarlandı **ve `RateLimit` bölümü ayar
      dosyasında GERÇEKTEN VAR** (yoksa kod varsayılanı yürürlüktedir; ayrıntı yukarıdaki
      "Redis ve rate limit" bölümünde)
- [ ] **KVKK denetim izi redaksiyonu (FIX-1A) canlıda — İLK GERÇEK HESAP SİLMESİNDEN ÖNCE.**
      **SIRA BAĞIMLILIĞIDIR, GERİYE DÖNÜK YOLU YOKTUR.** Redaksiyon yalnızca *silme anında*
      koşar: bu sürüm canlıya çıkmadan önce silinen bir hesabın adı/e-postası/telefonu ve
      açık adresi `audit_logs.changes` içinde **kalıcı olarak** kalır ve sonradan temizleyen
      bir yol yoktur (dev veritabanında FAZ 1'in sildiği hesaplarda mevcut — ölçüldü).
      Doğrulama: `AccountManager.DenetimIziniRedakteEtAsync` yayınlanan sürümde var mı, ve
      yayın sonrası ilk silmede o müşterinin `audit_logs` satırları `[REDACTED]` taşıyor mu.

## Launch sonrası (bloke etmez)
- [ ] `og:image` için gerçek **1200×630** marka görseli hazırlanınca `frontend/index.html`'de
      değiştirilip `twitter:card` tekrar `summary_large_image` yapılabilir (bugün 512×512
      ikon + `summary` kullanılıyor — yanlış vaat etmemek için)

## Arka plan işleri nasıl izlenir - DALGA C / C4

**Hangfire panosu tarayıcıdan erişilemez** ve bu bilinçlidir: uygulamada tek kimlik şeması
`JwtBearer`'dır, tarayıcı gezintisi `Authorization` başlığı göndermez, dolayısıyla pano
filtresi her zaman reddeder. Panoyu açmak çerez tabanlı ikinci bir auth şeması gerektirirdi.

Operatörün baktığı yer: **Panel sekmesindeki "Başarısız Arka Plan İşleri"** listesi
(`GET /api/dashboard/failed-jobs`). Yeniden deneme hakkı tükenmiş outbox mesajlarını gösterir;
bu kayıtlar `DataRetentionJob` tarafından **silinmez** (yalnız `Processed` olanlar silinir).

- [ ] Yayın sonrası ilk gün panelde bu liste kontrol edildi (boş olması beklenen durumdur)
- [ ] Log dosyaları: günlük + 100 MB'da parçalanır, 30 dosya saklanır (`Program.cs`).
      Disk planlaması buna göre yapıldı

### `BackgroundJobs:Enabled` - GÜVENLİK-FIX-3 / #8

Bu bayrak **Dalga D'de test izolasyonu için** eklendi (her test host'u kendi Hangfire
sunucusunu kaldırıp dakikalık outbox işini testlerin kendi drenajıyla yarıştırıyordu) ve
`TestHostConfig` onu `false` yapar. **Varsayılanı `true`'dur** — ayar hiç yoksa arka plan
işleri çalışır, yani güvenli taraftadır (`Program.cs`: `!bool.TryParse(...) || bgj`).

Riski budur: üretimde **yanlışlıkla `false`** verilirse uygulama sorunsuz açılır, uçlar
200 döner, siparişler oluşur — ama `outbox-processor` **hiç koşmaz**: sipariş onay
e-postası, fatura, sadakat puanı ve iade bildirimleri **sessizce** birikir. Hiçbir hata
üretilmez, `failed-jobs` listesi **boş kalır** (mesajlar `Pending` durumundadır, `Failed`
değil), yani operatörün baktığı yer de sessizdir.

> **BAYRAĞIN ANLAMI GENİŞLEDİ (FLAKE-FIX).** Yukarıdaki paragraf bayrağın **eski** anlamıyla
> yazılmıştı (yalnız Hangfire *sunucusu* + recurring kayıtları). Bugün `false`, o düğümde
> **Hangfire'ın TAMAMINI** kapatır: **depolama yapılandırması** (`AddHangfire` /
> `UseSqlServerStorage`), **arka plan sunucusu**, **`/hangfire` panosu** ve **recurring iş
> kayıtları**. Bayrağı `false` olan bir düğüm Hangfire için SQL'e **hiç bağlanmaz**
> (ölçüldü: `/hangfire` → **404**, `HangFire` şeması oluşturulmaz).
>
> **ÇOK ÖRNEKLİ DAĞITIMDA BUNUN SONUCU VAR:** web düğümlerini `false`, worker düğümünü
> `true` yapan bir kurulumda **pano YALNIZCA worker düğümünde bulunur** — web düğümünün
> `/hangfire` adresi 404 döner. Bu bir arıza değil, bayrağın tanımıdır; panoyu arayan
> operatör **worker düğümüne** bakmalıdır. (Zaten pano tarayıcıdan erişilemez — tek kimlik
> şeması `JwtBearer`; operatörün gerçek yüzeyi `GET /api/dashboard/failed-jobs`'tur ve o uç
> **Hangfire'dan bağımsızdır**, outbox tablosunu doğrudan okur.)

- [ ] `BackgroundJobs:Enabled` **verilmedi** ya da açıkça `true` (env/appsettings tarandı)
- [ ] **Çok örnekli kurulumda: en az BİR düğümde `true`** — hepsi `false` ise hiçbir arka
      plan işi koşmaz ve outbox sessizce birikir
- [ ] **Yayın sonrası davranışla doğrulandı:** gerçek bir sipariş verildikten ~2 dakika
      sonra `outbox_messages` içinde o siparişin mesajı `status = 1 (Processed)` oldu.
      **Bayrak yanlışsa bu satır `status = 0`'da takılı kalır** — konfigürasyona bakmak
      yerine sonuca bakmak, bayrağın gerçekten yürürlükte olduğunun tek doğrudan kanıtıdır
