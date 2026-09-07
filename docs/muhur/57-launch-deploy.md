# 57 · LAUNCH-DEPLOY-1 (LD-1) — SUNUCU KURULUMU

**Zemin:** `6139b84` (56 · LAUNCH GO) · **Sunucu:** Hetzner CPX22 `167.233.254.198` ·
**Alan adı:** `divisima.net` (vitrin `divisima.net` + `www`, API `api.divisima.net`)

> **SIR KURALI (SDP v1.5 · 1.12.6-b):** bu mühürde hiçbir gizli değer yoktur. Anahtarlar
> sunucuda üretilir, ekrana basılmaz; burada yalnızca **anahtar ADLARI**, komutlar ve
> ölçüm sonuçları durur.

---

## FAZ 0 — LF-2: ALAN ADI GEÇİŞİ + SITEMAP KÖKÜ

### 0.1 TARİF ↔ KOD ÇELİŞKİSİ (DUR-rapor → merkez kararı)

Tarif iki şey söylüyordu ve **ikisi birden sağlanamazdı**:

| | Metin |
|---|---|
| **Kısıt** | "frontend'e YALNIZ CSP meta origin satırı" |
| **Kabul ölçütü** | "`divisima.com` yalnız tarihsel mühürlerde kalır — ops/frontend/appsettings'te **0**" |

Ölçüm (üreten ifade `git grep -o "divisima\.com" \| wc -l`): **190 geçiş / 16 dizin**.
`frontend/index.html`in 8 satırından **yalnız 1'i** CSP idi; kalanlar `og:url` · `og:image` ·
iki JSON-LD bloğu (`Organization` + `WebSite` `url`) · ürün şeması · `mailto:` · i18n dizesi.
Kısıt harfiyen uygulansaydı **vitrin canlıda kendi kanonik URL'sini sahip olunmayan bir alan
adı diye ilan edecekti**. Merkez kararı: **ölçüt uygulanır, mühürler hariç hepsi.**

### 0.2 YAPILAN

**29 dosya yalnız yeniden adlandırma · 3 dosya esaslı değişiklik** (üreten ifade:
`git diff -U0 -- <f> | grep '^+[^+]' | grep -cv 'divisima\.net'`).

```
kalan divisima.com (mühür hariç) : 0      [ölçüt karşılandı]
mühürlerdeki divisima.com        : 33     [DEĞİŞMEDİ - MK-11/d]
docs/muhur diff'te               : BOŞ
```

### 0.3 SITEMAP KÖKÜ — CANLI KUSUR KAPANDI (AV-3 / SD-4, GF-7'den öne çekildi)

Ölçülen önceki hal: `Sitemap([FromQuery] string? baseUrl)` + `(baseUrl ?? "<sabit>")`.
**İki kusur birdeydi:**

1. **Sabit yedek yanlış alan adıydı.** `/api/seo/sitemap`e doğrudan gelen her istek, sahip
   olunmayan bir alan adının URL'lerini üretiyordu. nginx'in `?baseUrl=` eki bunu
   **maskeliyordu** — yani kusur yalnızca proxy'yi ATLAYAN çağrılarda görünürdü.
2. **Daha ağırı:** `baseUrl` **istemci girdisiydi** ve `[AllowAnonymous]` bir uçtan doğrudan
   XML gövdesine yazılıyordu — arama motoruna sunulan belgenin içeriğini herhangi bir anonim
   çağıran belirleyebiliyordu, üstelik değer **kaçışlanmıyordu** (XML enjeksiyonu).

**Karar:** sorgu parametresi **tamamen kaldırıldı** (opsiyonel bırakmak aynı kapıyı açık
tutardı) · kök tek kaynaktan `Storefront:BaseUrl` · boşsa **gürültülü 500** · gövdeye giren
değer `SecurityElement.Escape` ile kaçışlanır · nginx'teki maskeleyen `?baseUrl=` eki kalktı.

### 0.4 PİNLER — `LaunchFix2SozlesmeTests` (7 test)

| Pin | Tür |
|---|---|
| `ESKI_ALAN_ADI_YALNIZ_TARIHSEL_MUHURLERDE_KALIR` | kaynak-sözleşme |
| `TARAYICI_CALISIYOR_MUHURLERDE_ESKI_AD_HALA_VAR` | vakum kırıcı + POZ/NEG |
| `Sitemap_KOKU_YAPILANDIRMADAN_OKUR_SABIT_DEGIL` | **davranış** (ayırt edici deney) |
| `Sitemap_STOREFRONT_BASEURL_BOSSA_GURULTULU_DUSER` | **davranış** |
| `Sitemap_KOK_XML_KACISLANIR` | **davranış** |
| `Sitemap_HICBIR_SORGU_PARAMETRESI_KABUL_ETMEZ` | derlenmiş imza (reflection) |
| `Nginx_SITEMAP_PROXYSI_SORGU_EKI_TASIMAZ` | kaynak-sözleşme |

Davranış pinleri **veritabanı GEREKTİRMEZ**: DAL'lar elle yazılmış sahtelerle beslenir
(`00a:180` — launch öncesi yeni paket YASAK, bu yüzden Moq/NSubstitute eklenmedi).
`GetListAsync` dışındaki her üye çağrılırsa **gürültülü** düşer.

**TARAMA EVRENİ = `git ls-files`.** İlk yazımda dosya sistemi geziliyordu ve pin
**yerelde kırmızı / CI'da yeşil** olurdu. Ölçüldü: gezinti dört dosya yakaladı
(`appsettings.Development.json` + üç `logs/*.log`); dördü de **izlenmiyor ve gitignore'da**.
Gelistiricinin yerel ayarı ve çalışma zamanı logları deponun içeriği değildir. Sabit bir
"atla" listesi her yeni dosya türünde sessizce kör nokta açardı (LF-1/B-7'nin aynısı).

### 0.5 MK-6 MUTASYONLARI

| Mut | Ne | BUILD | Sonuç |
|---|---|---|---|
| **1** | sorgu parametresi geri (zorunlu) | **1** | *ayırt edici DEĞİL* — derleme kırıldı, pin ölçülemedi |
| **1b** | sorgu parametresi geri (**opsiyonel**, derlenir) | 0 | 1 isimli kırmızı — `..._SORGU_PARAMETRESI_KABUL_ETMEZ` |
| **2** | 500 yerine sessiz varsayılan (sed ile) | **1** | *sed sözdizimini bozdu* (MK-8) |
| **2b** | aynısı, hassas düzenleyiciyle | 0 | 1 isimli kırmızı — `..._BOSSA_GURULTULU_DUSER` |
| **3** | XML kaçışlama kaldırıldı | 0 | 1 isimli kırmızı — `..._XML_KACISLANIR` |
| **4** | nginx'e `?baseUrl=` geri kondu | 0 | 1 isimli kırmızı — `Nginx_..._SORGU_EKI_TASIMAZ` |
| **5** | izlenen üretim dosyasına eski ad | 0 | 1 isimli kırmızı — `ESKI_ALAN_ADI_...` |
| **6** | kök sabit yazıldı | 0 | 2 isimli kırmızı (ikisi de açıklanır: sabitleme kaçışlamayı da kaldırdı) |

Geri alma **yalnız ölçüm yedeğinden**; `git checkout`/`git stash` **kullanılmadı**;
üç dosyanın md5'i yedekle **birebir**, mutasyon izi **0**.

### 0.6 DOĞRULAMA

```
Biçim kapıları : whitespace 0 · style 0   (style ÖNCE 2 verdi - using sırası; MK-9 yakaladı)
Release build  : 0 hata
Category=Sql   : 415/415 · 415/415 · 415/415   (BİREBİR)
Tam suit       : 841/844 · 840/844 · 841/844
```

**ÜÇ KOŞUM BİREBİR DEĞİL — dürüst kayıt.** #1 ve #3 aynı (bilinen Docker üçlüsü:
`OrderEndpointTests.PlaceOrder_{ValidCart,InsufficientStock,ConcurrentRequests}`; yerelde
Docker yok). **#2'de DÖRDÜNCÜ bir kırmızı** çıktı ve bu kez **ADI YAKALANDI**:
`AuthRateLimitPinTests.AuthPolicy_IstemciBasina_OnIstekten_Sonra_429_VeKovaEndpointlerArasiPaylasilir`
— *"Expected 429 ... but found 401"*. İzole koşumda **3/3 yeşil**; dosya bu dalganın
diff'inde **YOK**. Yani LF-2'nin dokunduğu yüzeyle ilgisiz, tam-suit yükü altında zamana
duyarlı bir test. **`36·MANTIK-FIX-1`'in "İSİMSİZ FLAKE" kaydının aksine bu vaka İSİMLİ** —
ileride aynı test için kanıt zinciri buradan başlar.

### 0.7 KENDİ HATALARIM (FAZ 0)

1. **ZATEN YAPILMIŞ İŞİ YENİDEN YAZDIM.** `AllowedOrigins` yerel-origin fail-fast kapısını
   ve `docker-compose.prod.yml`in `AllowedOrigins__0/1` · `ForwardedHeaders__` · `AdminSeed__` ·
   `Webhook__` satırlarını yazdım — **ikisi de `b382065`'te (LF-1/F-TURU B-4) ZATEN VARDI**.
   HEAD'e bakmadan yazdım. Son durum doğrulandı: kapı **tam 1 kopya**, `Program.cs` ve
   `docker-compose.prod.yml` diff'i **yalnız yeniden adlandırma**. Yani mükerrer kopya
   depoya GİRMEDİ, ama bu tesadüf değil ölçümle saptandı. *("Aynı kuralın ikinci kopyası"
   ailesi — bu depoda 7 kez bedeli ödendi.)*
   **AÇIKLANAMAYAN ARA ÖLÇÜM:** düzenlemeden hemen sonra `git diff --numstat` **22/0**
   okudu; sonraki her ölçümde (yedek dosyası dahil) satır sayısı 867 = HEAD ile aynı.
   Mekanizmayı **açıklayamıyorum**; uydurma bir gerekçe yazmak yerine böyle kaydediyorum.
   Son durum kanıtla doğrulandı.
2. **İki ölü dedektör.** (a) `nslookup` çıktısını `2>/dev/null` ile süzdüm — Windows
   `nslookup` cevap başlığını **stderr'e** yazıyor, süzgeç boşaldı ve **"api.divisima.net A
   kaydı YOK"** diye yanlış okudum; negatif kontrol yakaladı, çapa **ham çıktıdan**
   kopyalandı (MK-7) ve üç ad da `167.233.254.198` çıktı. (b) Döngü içindeki
   `grep -v "^\+\+\+"` BRE/ERE kaçışını karıştırdı, "esaslı değişiklik" sayacı **her dosya
   için 0** dedi; tek başına koşturunca 41 verdi.
3. **Yanlış aritmetik beklentisi.** `git grep -c` **satır** sayar, `grep -o | wc -l`
   **geçiş**; "beklenen 7" yazdım, 10 gördüm. Ölçüm doğruydu, beklentim yanlıştı.

---

## FAZ 1 — SUNUCU SERTLEŞTİRME

### 1.1 TABAN (kurulumdan ÖNCE ölçüldü)

```
hostname          divisima-prod
OS                Ubuntu 26.04.1 LTS        <- TARİF "24.04" DİYORDU (sapma)
kernel            7.0.0-30-generic
RAM / CPU / disk  3809 MB · 2 · 75G (%2)
swap              YOK
ufw               inactive
fail2ban          KURULU DEĞİL
docker            KURULU DEĞİL
sshd              passwordauthentication YES <- TARİF "no teyidi" DİYORDU; DEĞİŞTİRİLDİ
timezone          Etc/UTC                    <- zaten doğru (K11 ile uyumlu)
unattended-upgr.  kurulu
80/443 dinleyen   0
```

**İKİ SAPMA RAPORLANDI:** (a) işletim sistemi 26.04 (codename `resolute`), tarif 24.04
diyordu — Docker deposunun `resolute` süiti **ölçüldü** (`InRelease HTTP=200`, aday sürüm
`5:29.8.0-1~ubuntu.26.04~resolute`), varsayımla kurulmadı. (b) `PasswordAuthentication`
**açıktı**; tarif "teyit et" diyordu, gerçekte **değiştirilmesi** gerekti.

### 1.2 UYGULANAN

| Adım | Kanıt |
|---|---|
| `apt update` + `upgrade` | `APT_UPDATE_EXIT=0` · `APT_UPGRADE_EXIT=0` · reboot-required: hayır |
| **ufw** — ÖNCE izin, SONRA etkinleştirme | `Status: active` · deny(incoming)/allow(outgoing) · 22,80,443/tcp ALLOW |
| **fail2ban** (sshd jail) | `active` · jail canlı: **Total failed 151** (halka açık IP taranıyor) |
| **swap 2 GB** | `/swapfile 2G` · `swap_MB=2047` · fstab kalıcı · `vm.swappiness=10` |
| **sshd sertleştirme** | `passwordauthentication no` · `kbdinteractive no` · `permitrootlogin prohibit-password` |
| **kilitlenme kontrolü** | değişiklikten SONRA **yeni** SSH oturumu açıldı — anahtarla giriş çalışıyor |
| **docker** | `29.8.0` + compose `v5.5.1` · enabled+active · **ayırt edici kanıt:** `hello-world` konteyneri gerçekten koştu |
| **unattended-upgrades** | `-security` + ESM kaynakları etkin · `apt-daily-upgrade.timer` enabled+active · NEG kontrol 0 |

**ufw sırası bilinçli:** `allow 22` **etkinleştirmeden ÖNCE** verildi; ters sıra kilitlenme
demekti. `/etc/sysctl.conf` 26.04'te **yok** — yedek dalım onu yanlışlıkla yarattı, silindi
ve ayar `/etc/sysctl.d/99-divisima.conf`e taşındı.

---

## SIRADAKİ

FAZ 2 (uygulama + `.env`) · FAZ 3 (veritabanı + migration) · FAZ 4 (nginx + TLS) ·
FAZ 5 (kanıt turu). `.env`de Ömer'in elle dolduracağı alanlar `CHANGE_ME` kalır; CC yalnız
`grep -c CHANGE_ME` ile yokluğunu ölçer.

---

## FAZ 0 — CI KANITI (push sonrası, `34be485`)

| Run | Sonuç | Kritik adımlar |
|---|---|---|
| Security CI `34128723792` | success | **`Gitleaks (secret taraması)` = SUCCESS** (adım sonucundan; annotation'dan **değil**) · codeql · dependency-scan |
| CI - Build & Test `34128723840` | success | kilitli graf · Derle · `SQL gerektiren testler (ATLANMAMALI)` · `Testler + coverage` · whitespace · style · `migration'lar SENKRON` |

Yerelde kırmızı olan üç `OrderEndpointTests` CI'da **yeşil** → "sebep Docker yokluğu" teşhisi
bağımsız olarak bir kez daha doğrulandı. İsimli flake CI'da tekrarlamadı.

---

## FAZ 2 — UYGULAMA + `.env`

Klon `/opt/divisima` @ `34be485` (SHA eşleşmesi doğrulandı). Anahtarlar **sunucuda** üretildi
(`openssl rand`); hiçbir değer ekrana/deftere/loga basılmadı. `.env` **600 root:root**.

`docker compose config` **exit 0** · boşluklu değerler doğru çözüldü (`User Id=…`,
`Divisima <noreply@…>`) · çözülmemiş yer tutucu **0** · Ömer'in doldurduğu **5** anahtar
sonrasında atama satırlarında `CHANGE_ME` **0**.

**Ölçüm hatası (kendi):** `.env`i doğrulamak için `source` kullandım; bağlantı dizesindeki
`User Id=` boşluğu bash'i kırdı. Compose `.env`i **kendi ayrıştırıcısıyla** okur — doğru
kanal `docker compose config`di, `source` değil.

---

## FAZ 3 — VERİTABANI

**Bilinçli sapma (merkez onayı):** veritabanı `docker-compose.db.yml` ile **ayrı dosyada**
ayağa kalkar; `docker-compose.prod.yml`e **dokunulmadı**. Gerekçe ve sapmayı küçülten dört
önlem o dosyanın başında. Yönetilen veritabanı **launch sonrasına** bırakıldı.

```
collation Turkish_CI_AS · recovery FULL · autoclose 0
sys.tables 46 (45 + __EFMigrationsHistory)   [checklist 45 bekliyordu]
__EFMigrationsHistory 15                      [yerel migration dosyası 15]
sys.foreign_keys 56                           [checklist 56 bekliyordu]
divisima_app: CRUD izni 4 · DDL izni 0
```

Migration script'i **EF 8.0.30** ile üretildi. Yerel `dotnet ef` **10.0.10**'du; MK-8'in
kaydettiği çare uygulandı — izole `--tool-path` kurulumu. Script md5 yerel↔sunucu **birebir**,
uygulandıktan sonra **silindi**.

### BULGU-1 — EN AZ YETKİ ↔ HANGFIRE OTO-ŞEMA ÇELİŞKİSİ *(kapatıldı)*

Uygulama ilk açılışta **çöktü**: `SQL Error 208`, `Program.cs` `RecurringJob.AddOrUpdate`.
Hangfire kendi şemasını yaratmak ister; `divisima_app`in DDL yetkisi yoktur. **İkisi de
doğru kararlardı ve kimse ikisini birlikte ölçmemişti.** Şema dağıtım anında `sa` ile kuruldu
(sürüm **kilitli graftan**: 1.8.6), uygulamaya yalnız CRUD+EXECUTE verildi, DDL izni **0**
ölçüldü. Adım `ops/deployment-checklist.md`e **eklendi** — orada yoktu.

### BULGU-2 — `docker-compose.prod.yml` HEALTHCHECK'İ YAPISAL OLARAK KIRIK *(AÇIK)*

```
test: wget -q -O- http://127.0.0.1:5000/health/ready || exit 1
konteynerde:  command -v wget -> YOK      healthcheck log: "wget: not found" (x5)
              command -v curl -> /usr/bin/curl
uygulama:     /health, /health/ready, /health/live -> HTTP 200 (üçü de)
konteyner:    "unhealthy"
```

Uygulama sağlıklıyken konteyner sağlıksız görünür; sağlık kapısına bağlı her otomasyon
(`depends_on: service_healthy`, izleme) **yanlış bilgi alır**. CI bunu yakalayamazdı — CI
üretim compose'unu koşmuyor. **Tek token'lık düzeltme:** `wget -q -O-` → `curl -fsS`.
Dosya "DOKUNULMAZ" ilan edildiği için **değiştirilmedi**; karar merkezde.

### BULGU-3 — SQL SERVER SÜRÜMÜ **Express** *(AÇIK — checklist'i İHLAL EDİYOR)*

`MSSQL_PID: Express` seçildi. Checklist **"SQL Server sürümü Express DEĞİL"** diyor ve
gerekçesi ölçülmüştü (`Msg 1844`). Bu dağıtımda **aynı hata yeniden görüldü**:
`BACKUP ... WITH COMPRESSION` reddedildi. Sıkıştırma kaldırılarak yedek çalışır hale getirildi,
ama Express'in **10 GB veritabanı sınırı** ve **TDE yokluğu** duruyor — yani runbook'un
"yedekler şifreli olmalı" maddesi bugün **karşılanamaz**. Alternatifler lisans gerektirir
(Developer sürümü üretimde kullanılamaz); karar merkezde.

---

## FAZ 4 — NGINX + TLS + SERVİS + YEDEK

Sertifika **üç adı birden** kapsıyor (`openssl x509` SAN: `api.divisima.net, divisima.net,
www.divisima.net`), bitiş **2026-12-06**. `nginx.conf` deponun **birebir kopyası** (md5 aynı):
certbot'un yolları yeniden yazması yerine `/etc/ssl/divisima/` **sembolik bağ** yapıldı —
böylece sevk edilen conf ile depodaki conf ayrışmıyor. `nginx -t` exit 0 (iki `http2`
kullanımdan-kalkma uyarısı + OCSP stapling uyarısı; üçü de ölümcül değil).

Vitrin **`set-api-origin.sh` koşulduktan SONRA** kopyalandı (ters sıra storefront'u
localhost'a bakar bırakırdı); `--verify` **exit 0**, yedi kontrolün hepsi `https://api.divisima.net`.

`divisima.service` (systemd, iki compose dosyası birlikte) **enabled**. Günlük yedek
(`03:00` cron, 7 gün): **gerçekten koşuldu** — 6.2 MB `.bak` + uploads tar üretildi ve
`RESTORE VERIFYONLY` → **"The backup set on file 1 is valid"**.

---

## FAZ 5 — CANLI KANIT (sunucu tarafı)

```
https://divisima.net        200      https://www.divisima.net 200
https://api.divisima.net/health 200  /health/ready 200
http -> https                301 (divisima.net · api.divisima.net)
HSTS  storefront 1 · api 1   (TEK KAYNAK nginx - app.UseHsts KALDIRILMIŞTI, doğrulandı)
X-Frame-Options DENY · CSP frame-ancestors 'none' · X-Content-Type-Options nosniff
admin.html  ->  X-Robots-Tag: noindex, nofollow
```

**LF-2'nin canlı kanıtı:** `https://divisima.net/sitemap.xml` → 200, `<loc>` kökü
`https://divisima.net`, eski alan adı geçişi **0**. Ve saldırı denemesi:
`api/seo/sitemap?baseUrl=https://saldirgan.example` → gövdede `saldirgan.example` **0 kez**.
Kapatılan açık **üretimde de kapalı**.

### BULGU-4 — `KnownProxies` KONTEYNERDE SESSİZCE ÇALIŞMIYORDU *(kapatıldı)*

Auth hız sınırı canlıda doğru davrandı (**10× 401, sonra 429**), ama `security_events`
satırlarının IP'si **`::ffff:172.18.0.1`** — yani Docker köprü ağ geçidi, gerçek istemci
değil. nginx `127.0.0.1:5000`'e proxy'lese de paket konteynere **ağ geçidinden** girer;
`KnownProxies=127.0.0.1` eşleşmez, XFF güvenilmez sayılır. Sonuç: **tüm istemciler tek
kovada** — auth limiti site geneli 10/dk olur ve olay izi soruşturmaya yaramaz.

Ağ geçidi **ölçüldü** (`docker inspect … .Gateway` → `172.18.0.1`), `.env` düzeltildi,
API yeniden başlatıldı; sonraki olaylar **gerçek istemci IP'sini** taşıdı. Tuzak
checklist'e tablo satırı + ölçüm komutuyla **eklendi**.

**Tarayıcı kalemleri (üye ol → doğrulama maili · csrf_token çerezi · 16 dk sonra refresh ·
sandbox 3D ödeme · SW/offline · konsolda CSP ihlali) göz turunda Ömer'de.**

---

## EK — SOFT-LAUNCH KAPISI (LD-1 eki)

Site **canlı ama kapı arkasında**. Amaç: gerçek altyapıda çalışırken erken ziyaretçiyi ve
**indekslenmeyi** önlemek.

### Yapılan

| # | Kalem | Kanıt |
|---|---|---|
| 1 | Vitrin + admin **HTTP Basic Auth** (`divisima`) | kimliksiz `/`·`/index.html`·`/admin.html`·`www` → **401**; doğru parolayla → **200 + 953.557 bayt**; yanlış parolayla → **401** |
| 2 | **API kilitsiz** (bilinçli) | `/health` 200 · `/health/ready` 200 · `POST /api/payment/callback` → **400** (uygulamaya ULAŞIYOR, 401 değil) |
| 3 | `X-Robots-Tag: noindex, nofollow` **üç hostta da** | `divisima.net` · `www` · `api` → hepsi taşıyor; **401 yanıtında da** var (`always` çalışıyor) |
| 4 | Sitemap **geçici 404** | `https://divisima.net/sitemap.xml` → **404** |
| 5 | Parola sunucuda üretildi | `/root/.htpasswd-parola.txt` **600 root:root** · `/etc/nginx/.htpasswd` **640 root:www-data** · değer **hiçbir yere basılmadı** |

**Parolayı okuma:** `cat /root/.htpasswd-parola.txt`

**API neden kilitlenmedi — dürüst sınır:** Basic Auth API'ye konsaydı İyzico'nun sonuç POST'u
ve orkestratör sağlık probları **401** alırdı; ödeme sonucu sessizce kaybolurdu. API'yi bugün
koruyan şey CORS allowlist'i + CSRF double-submit + yetkilendirmedir. Basic Auth **o katmanın
yerine geçmez**; gizlilik değil, **erken ziyaretçi ve indekslenme** kapısıdır.

### DUR-NOTU — ÖDEME YÖNTEMLERİ `.env` İLE KAPATILAMAZ

Tarif "kapıda ödeme ve havale/EFT KAPALI" diyordu. **Ölçüldü:**

```
GirdiSinirlari.GecerliOdemeYontemleri = static readonly byte[] { 0, 1, 2 }   // DERLEME ZAMANI SABITI
yapilandirmadan okunmuyor  ->  .env ile acilip kapatilamaz
```

Tarifin öngördüğü DUR-notu bu. Ayrıca **fiili durum ölçüldü** ve tarifin varsaydığından farklı:

- **Havale/EFT (2): vitrinde HİÇ SUNULMUYOR.** UI yalnız `online`/`cod` üretir
  (`payment_method: checkoutState.method === "cod" ? 1 : 0`). API doğrulayıcısı 2'yi kabul
  eder ama erişilebilir bir yüzey yok.
- **COD (1): misafir siparişinin TEK yolu** (`payment_method: 1` sabit — kullanıcı kararı
  "seçenek iii", misafire oturum verilmez). **COD'u kapatmak misafir siparişini tamamen
  kapatmak demektir.**

Yani "checkout'ta yalnız online kalır" bugünün durumu **değildir** ve kod değişikliği
olmadan sağlanamaz. Karar merkezde.

### HUKUKİ METİNLER — ÖLÇÜLDÜ, EKSİK VAR

`contents` tablosunda **10 kayıt**, hepsi `is_active=1`. Altı yasal gerekliliğe karşı:

| Gereklilik | slug | Durum |
|---|---|---|
| KVKK aydınlatma | `kvkk` | VAR (621 B) |
| Mesafeli satış | `mesafeli-satis` | VAR (943 B) |
| **Ön bilgilendirme formu** | — | ❌ **YOK** |
| Cayma/iade | `iade` | VAR (617 B) |
| **Unvan · adres · MERSİS** | `iletisim` | ⚠️ **276 B, yetersiz** — metin kendini *"Bu bir tasarım simülasyonudur; iletişim bilgileri temsilidir"* diye ilan ediyor |
| **ETBİS** | — | ❌ **YOK** |

**4/6.** Tohumlanan metinler **tasarım örneğidir, hukuki metin değildir**; ticari bir sitede
"temsilidir" ibaresi canlı kalamaz. Checklist'e ölçüm sorgusu + `LIKE '%temsilidir%' → 0`
kontrolüyle **açılış günü ön koşulu** olarak eklendi.

### KENDİ HATAM — VAKUMLAŞAN PİN

Sitemap'i 404'e çevirip proxy bloğunu yoruma alınca, `DalgaCDagitimSozlesmesiTests`in
`Contain("/api/seo/sitemap")` assert'i **bedava doğru** oldu: dizge artık **yalnızca yorumda**
geçiyordu ve pin "sitemap sunuluyor" diye **yalan söylerdi**. MK-8 EKİ'nin tam olarak
uyardığı durum. Pin **yorumsuz metin** üzerinde koşacak şekilde yeniden yazıldı; ayrıca
"geri açma bloğu yorumda duruyor" ayrı bir assert'le pinlendi (silinirse geri açmak yeniden
yazmak olurdu).

**MK-6:** MUT-8 (API bloğuna `auth_basic`) → 1 isimli kırmızı · MUT-9 (paylaşılan başlıktan
`X-Robots-Tag` silindi) → 1 isimli kırmızı. Geri alma ölçüm yedeğinden, md5 birebir.

---

## EK — LF-3 (LD-1'in açık iki bulgusu kapatıldı)

### LF-3(a) — HEALTHCHECK `wget` → `curl` *(KAPANDI, canlı doğrulandı)*

```
ONCE : test: wget -q -O- …/health/ready   -> healthcheck log "wget: not found" (exit=1 x5)
        uygulama /health · /health/ready · /health/live = 200 ama konteyner "unhealthy"
SONRA: test: curl -fsS …/health/ready     -> healthcheck exit=0
        docker ps: "Up 10 seconds (healthy)"   (~15 sn icinde)
```

Ayırt edici ölçüm konteynerin içinden alındı: `command -v wget` → **YOK**,
`command -v curl` → **`/usr/bin/curl`**. Yani doğru araç imajda **zaten vardı**.
**Pin KALICIDIR** (`UretimComposeSaglikKontrolu_IMAJDA_OLMAYAN_ARACI_CAGIRMAZ`);
MUT-10 (`curl`→`wget`) → **1 isimli kırmızı**.

CI bunu yakalayamazdı — CI üretim compose'unu koşmuyor. **Gerçek dağıtım gösterdi.**

### LF-3(b) — EXPRESS KALIR · TDE YERİNE YEDEK DOSYASI ŞİFRELEME *(KAPANDI)*

Lisans kararı şirket sahibinin; 10 GB sınırı bugünkü hacim için uzak. Express **TDE
desteklemediği** için runbook'un *"yedekler şifreli olmalı"* maddesi **yedek artefaktı**
düzeyinde karşılandı: günlük iş `.bak` ve `uploads.tar.gz`ı `age` ile şifreler ve
**şifresiz kopyaları siler** (`ls | grep -cv '\.age$'` → **0**).

**Geri yüklenebilirlik — ayırt edici çift kanıt:**

| Girdi | Sonuç |
|---|---|
| Şifreli dosya doğrudan `RESTORE VERIFYONLY` | **`Msg 3241` — media family incorrectly formed** → içerik *gerçekten* şifreli, adı değişmiş bir `.bak` değil |
| `age -d` ile çözülmüş dosya | **"The backup set on file 1 is valid"** (exit 0) |
| `uploads-*.tar.gz.age` çözülüp `tar tzf` | exit 0 |

> **DÜRÜST SINIR — bu TDE DEĞİLDİR.** `.mdf`/`.ldf` diskte **hâlâ şifresiz**; korunan
> yalnızca dışarı taşınan yedek artefaktıdır.
>
> **ANAHTAR KAYBI = YEDEK KAYBI.** `/root/.divisima-backup.key` (600) sunucu **dışında** da
> saklanmalı — aksi halde sunucunun kaybedildiği senaryoda, yani felaket kurtarmanın tam da
> işe yarayacağı anda, yedekler açılamaz. Bu satır checklist'e ve runbook'a **kutu olarak**
> eklendi; şifreleme, doğrulanmamış bir geri yükleme prosedürünü sessizce kullanılamaz hale
> getirebilecek **yeni bir adım** ekler.

### LF-3(c) — AdminSeed

`DIVISIMA_ADMIN_SEED=true` bugün **açık** (ilk admin için). Ömer ilk girişi yaptığını
bildirince `false` çekilip API yeniden başlatılacak. Checklist adım 19 zaten bunu istiyor.

### LF-3(d) — BİLİNEN'e eklenen iki kalem

`57·LAUNCH-DEPLOY-1` başlığıyla CLAUDE.md B9'a girdi: **SQL Server Express** (TDE yok →
dosya şifrelemesi; anahtar kaybı = yedek kaybı) · **`KnownProxies` = Docker ağ geçidi**
(`172.18.0.1`; ağ yeniden yaratılırsa değişir ve `.env` güncellenmelidir — yanlış kalırsa
hız sınırı ve olay izi **sessizce** ağ geçidi IP'sinde toplanır).

### KENDİ HATAM — İKİNCİ VAKUMLAŞAN PİN

"AÇILIŞ GÜNÜ" bölümüne eklediğim **altı satırlık numaralı hukuki metin tablosu**,
`LaunchFix1SozlesmeTests`in IRL adım sayacını kırdı: pin `^\| (\d+) \|` desenini **belge
genelinde** sayıyordu ve 20 yerine **26** buldu (üç koşumda da kırmızı).

Kusur eklenen tabloda değil **pindeydi**: koruduğu iddia *"IRL tablosunda 20 sıralı adım
var"*dır, *"belgede başka hiçbir yerde numaralı tablo satırı yok"* değil. Sayım artık IRL
tablosunun başlığından başlayıp bir sonraki başlıkta biter. Bu, bu dalgadaki **ikinci**
vakumlaşan-pin vakası (birincisi sitemap `Contain`ıydı) — ikisi de aynı kökten: **bir pin,
koruduğu şeyi belge/dosya geneli üzerinden ölçerse, ilgisiz her ekleme onu kırar ya da
bedava doğru yapar.**
