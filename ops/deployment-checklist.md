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

Yerelde karşılığı `docker compose up` — `frontend` servisi aynı davranışı `:5173`'te verir
(`ops/infra/frontend-dev.conf`).

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

## Zorunlu adımlar
- [ ] `Iyzico:BaseUrl` = `https://api.iyzipay.com` (sandbox değil)
- [ ] `Webhook:AllowedIps` = Iyzico production IP aralıkları
- [ ] `AllowedOrigins` = yalnız gerçek frontend domain(ler)i
- [ ] DB kullanıcısı en az yetki (SELECT/INSERT/UPDATE; DDL/DROP yok)
- [ ] `dotnet list package --vulnerable` temiz
- [ ] Serilog SIEM sink aktif (bkz. serilog-siem.md)
- [ ] Rate limit eşikleri prod trafiğine göre ayarlandı

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
