# GUVENLIK DALGASI 2 (YALNIZ OLCUM) ve GUVENLIK-FIX-3

Dalga 2 YALNIZ olcumdu (kod DEGISMEDI). Gerekce kullanicinin: G1-G9 turu ARTIK VAR OLMAYAN
bir kod tabanini olcmustu - o gunden beri mail altyapisi, sifre sifirlama arayuzu, misafir
checkout, admin panelinin bes ekrani, nginx storefront blogu, 56 FK, idempotency'nin auth
SONRASINA tasinmasi, iki yolda birden rate limit, arka plan bayragi ve DB'den uretilen menu
geldi. **Regresyon YOK: G1..G9'un kapandigi her yer HALA KAPALI** (12 kontrol tek tek suruldu).

## DALGA 2 BULGULARI

| # | Onem | Bulgu | Bloke | Durum |
|---|---|---|---|---|
| 1 | ORTA | Misafir checkout enumeration: kayitli e-posta **409**, kayitsiz **201** | hayir | **LAUNCH SONRASI** (karar) |
| 2 | ORTA | Ayni uc anonim COD siparisi + kurbana dogrulama maili uretiyor | hayir | **LAUNCH SONRASI** (karar) |
| 3 | ORTA | Rate limit bolumlemesi DAGITIM SEKLINE bagli (`KnownProxies` bos) | hayir | **KAPANDI** (checklist) |
| 4 | ORTA | Storefront'ta clickjacking korumasi YOK | hayir | **KAPANDI** (nginx) |
| 5 | DUSUK | Idempotency filtresi anahtari GOVDEYE bagli degil | hayir | **SUPHELI #22** |
| 6 | DUSUK | Ic dokumanlar public (`/API-CONTRACT.md` vb. 200) | hayir | **KAPANDI** (nginx) |
| 7 | DUSUK | Cerez `.divisima.com` kapsaminda - alt alan adi riski | hayir | **KAPANDI** (checklist) |
| 8 | DUSUK | `BackgroundJobs:Enabled` sessiz tuzagi | hayir | **KAPANDI** (checklist + example.json) |

**HIPOTEZ DOGRULANDI AMA TEMIZ CIKANLAR:** mail linkleri (hash fragment -> sunucuya gitmez,
Referer'a girmez; jeton kullanimdan sonra null'lanir) · tam-varlik map (zaten kayitli, Dalga B)
· `failed-jobs` payload sizdirmiyor · FK regresyonu yok (tum silmeler soft) · CSP
`unsafe-inline` XSS'e karsi hicbir sey katmiyor **ama gercek payload calismadi** (escape +
DOMPurify tek katman olarak TUTUYOR).

**B3 HIPOTEZI CURUTULDU - kanitiyla:** `nginx -> proxy_pass http://127.0.0.1:5000` loopback'tir
ve ASP.NET'in varsayilan `KnownProxies`'indedir, yani belgelenen topolojide XFF'e GUVENILIR.
Olculdu: `XFF=9.9.9.9` 10 istekte tukendi, `XFF=8.8.8.8` **taze kova** aldi. Kalan risk
topoloji degisikligidir - o da checklist'e alindi.

## GUVENLIK-FIX-3 - DORT KALEM

### #4 CLICKJACKING - ve UYGULARKEN CIKAN ASIL BULGU

`ops/infra/nginx.conf`'un `divisima.com` bloguna `X-Frame-Options: DENY` +
`Content-Security-Policy: frame-ancestors 'none'` eklendi. **Meta'ya eklemek COZMEZDI**:
`frame-ancestors` bir `<meta>` CSP'sinde SPEC GEREGI yok sayilir.

**UYGULARKEN CIKAN VE ASIL ONEMLI OLAN BULGU - nginx `add_header` DEVRALMA TUZAGI:**
`add_header` bir onceki seviyeden **YALNIZCA o seviyede hic `add_header` yoksa** devralinir.
Storefront blogunda kendi `add_header`ini tanimlayan **IKI** location vardi
(`= /admin.html` ve `~* \.(html|js|json)$`) - yani sunucu seviyesindeki HSTS / nosniff /
Referrer-Policy **tam da onem tasiyan sayfalara (index.html, admin.html, TUM JS) ULASMIYORDU**.
Basligi yalnizca sunucu seviyesine eklemek, **sessizce dusen** bir duzeltme olurdu.

Cozum: `ops/infra/divisima-security-headers.conf` (TEK KAYNAK), uc yerden `include` edilir.
**FAIL-SAFE:** devralma yine de calisiyor olsaydi include YALNIZCA gereksiz olurdu, ZARARSIZ;
calismiyorsa (belgelenen davranis) ZORUNLUDUR - iki okumada da dogru taraftadir.

**API BLOGUNA CSP EKLENMEDI - OLCUME DAYALI:** `SecurityHeadersMiddleware` her API yanitina
zaten `frame-ancestors 'none'` iceren TAM bir CSP basiyor ve `UseStaticFiles` ONDAN SONRA
geliyor - yani yuklenen gorseller de kapsamda. nginx'ten ikinci bir CSP eklemek her yanitta
iki bagimsiz politika dogururdu, kazanc SIFIR. Storefront ise STATIK dosyadir, hicbir
middleware kosmaz; tek kaynak nginx'tir. Karar pinli.

**CSP BASLIGI YALNIZ `frame-ancestors` TASIR:** `script-src`/`connect-src` gibi direktifleri
buraya koymak, `ops/set-api-origin.sh`in BILMEDIGI ikinci bir senkron noktasi acardi (o betik
yalniz HTML meta'sini yazar) - M1'in ta kendisi. Cift-anlam kirici assert bunu koruyor.

### #6 IC DOKUMANLAR

nginx'te `.md` / gizli dosya / yedek artigi / `/test/` icin `return 404` kurallari.

**KAPSAM OLCULDU, UYDURULMADI:** `frontend/` agacindaki **24 dosyanin tamami** nginx location
cozumlemesi simule edilerek tarandi. Sonuc: **6 kapali** (API-CONTRACT.md, INTEGRATION.md,
SEO-ANALYTICS.md, pwa/README.md, vendor/README.txt, test/mobil-erisilebilirlik.js),
**18 acik**. Hicbir kod `.md`'ye referans vermiyor (grep: 0) ve `/test/`e referans veren kod
YOK - o betik olcum sirasinda ELLE yuklenir.

**`.well-known` ACIK MUAFIYETI ZORUNLU:** gizli dosya kurali RFC 9116
`/.well-known/security.txt`i de 404'lardi. `^~` prefix'i regex'lerin TAMAMINI yener, yani
muafiyet kural sirasindan BAGIMSIZ gecerlidir. 5. kontrolde M2 tam bunu uretti.

**`/test/` icin `^~` SART:** dosya `.js` ile bittigi icin `~* \.(html|js|json)$` regex'ine
takilir ve SERVIS EDILIRDI.

**DEV IKIZI (`frontend-dev.conf`) - IKI BILINCLI AYRISMA:** ayni deny kurallarini tasir ama
(a) `/test/` YERELDE ACIK KALIR (Dalga 4'un pin boslugunu telafi eden olcum betigi elle
yuklenir), (b) clickjacking basligi yoktur. Ikisi de o dosyanin TLS/HSTS icin zaten yazili
olan gerekcesiyle ayni sinifta. Ayrica dev'deki ayni devralma tuzagi da duzeltildi (nosniff
iki location'da tekrarlandi).

### #3 KnownProxies + API PORTU (checklist)

`ops/deployment-checklist.md`'ye yeni bolum: topoloji tablosu (loopback / ayri makine),
zorunlu `KnownProxies` maddesi, `ForwardLimit` notu ve **yayin sonrasi DAVRANIS dogrulamasi**
(iki farkli XFF ile ayri kova alinip alinmadigi). `example.json` bu ayari ZATEN ayrintisiyla
belgeliyordu; eksik olan checklist maddesiydi.

**docker-compose DEGISTIRILMEDI - olcume dayali:** `ASPNETCORE_ENVIRONMENT: Development` yazar
ve basligi "yerel gelistirme ortami" der, yani URETIM ARTEFAKTI DEGILDIR. `5000:5000` ve
`5173:80` acilimlari BILINCLIDIR - gercek cihaz turu (Dalga 4, telefon LAN uzerinden) icin
storefront'un DA API'nin DE LAN'dan erisilebilir olmasi gerekir; `sqlserver`/`redis` ise
gerekcesiyle `127.0.0.1:`e baglidir. Checklist'e "uretimde yalniz nginx disari bakar" maddesi
ve compose'un uretim artefakti OLMADIGI notu eklendi.

### #8 BackgroundJobs + #7 cerez kapsami (checklist)

`BackgroundJobs:Enabled` hicbir ayar dosyasinda YOKTU - operatore gorunmez bir bayrakti.
`example.json`'a uc aciklama satiri + `"BackgroundJobs": { "Enabled": true }` eklendi,
checklist'e **davranis** dogrulamasi kondu (siparisten ~2 dk sonra `outbox_messages` satiri
`status = 1 (Processed)` oldu mu). **Konfigurasyona degil SONUCA bakilir** - ve ozellikle
onemli: bayrak yanlissa `failed-jobs` listesi de BOS KALIR, cunku mesajlar `Pending(0)`da
takilir, `Failed(2)` olmaz (olculdu: `DashboardManager.GetFailedJobs` yalniz `Failed`
sorguluyor). Yani operatorun baktigi yer de sessizdir.

#7 icin checklist'e DNS hijyeni maddesi: alt alan adlari sahipsiz birakilmaz (subdomain
takeover ile ele gecirilen bir alt alan adi `/api/auth/*` servis ederse refresh token'i alir).

## PINLER (`GuvenlikFix3SozlesmeTests`, 6 - VERITABANI ACMAZ)

`IKI_SERVER_BLOGU_DA_CLICKJACKINGE_KAPALI_ve_CSP_YALNIZ_frame_ancestors_Tasir` (vakum kirici:
dosya gercekten nginx yapilandirmasi olmali; cift-anlam kirici: baslik script-src/connect-src
TASIMAMALI) · `KENDI_add_header_TANIMLAYAN_HER_STOREFRONT_LOCATIONU_BASLIK_DOSYASINI_INCLUDE_Eder`
(YAPISAL pin - yarin eklenecek bir location da yakalanir; vakum kirici: en az iki boyle
location bulunmus olmali) · `API_BLOGUNA_IKINCI_CSP_BASLIGI_EKLENMEZ_UYGULAMA_ZATEN_Gonderiyor`
(kararin PREMISI de pinli - middleware'den frame-ancestors kalkarsa pin kirilir ve
"artik nginx kapatmali" der) · `IC_DOKUMANLAR_404_STOREFRONTUN_IHTIYACI_OLAN_DOSYALAR_SERVIS_EDILIR`
(location cozumlemesi SIMULE EDILIR; vakum kirici: kapatilan dokuman ve muafiyetin korudugu
dosya depoda GERCEKTEN bulunmali) · `DEV_KONFIGI_AYNI_DENY_KURALLARINI_Tasir_ama_OLCUM_BETIGI_YERELDE_ACIK_KALIR`
(cift-anlam kirici: "her seyi kapat" YANLIS duzeltmedir) · `CHECKLIST_PROXY_PORT_ARKAPLAN_ve_DNS_MADDELERINI_Tasir`.

**KIRILAN PIN YOK.**

**PIN SINIRI (DURUST KAYIT):** nginx bu suitte AYAGA KALDIRILAMAZ - olculdu, makinede ne
`nginx` ne `docker` var. Pinler artefakti okur ve location cozumlemesini SIMULE EDER;
simulasyon nginx'in gercek onceligini uygular (`=` > en uzun `^~` > regex YAPILANDIRMA
SIRASINDA > en uzun prefix) ama nginx'in TAMAMI degildir. "nginx gercekten boyle davraniyor"
kaniti ancak sunucuda `curl -sI` ile alinir; o adim checklist'e **UC AYRI ADRES** icin zorunlu
madde olarak yazildi (tek adrese bakan bir dogrulama, devralma tuzagi yuzunden YESIL gorunurdu).

## DIS KONTROLU + 5. KONTROL

**DIS:** 6 assert ters (ALTI AYRI test) -> **6 AYRI ISIMLI KIRMIZI**. Geri alindi, 6/6 yesil.

**5. KONTROL - UC URETIM MUTASYONU** (her birinde yeni kuralin (a)/(b)/(c) adimlari kosuldu):

| Mutasyon | Kirilan pin | Uretilen once-durum |
|---|---|---|
| M1 kod tasiyan dosyalar location'undan `include` kaldirildi | `KENDI_add_header_...` | index.html ve TUM JS guvenlik basliksiz - ve `robots.txt`e bakan bir dogrulama YESIL gorurdu |
| M2 `^~ /.well-known/` muafiyeti kaldirildi | `IC_DOKUMANLAR_404_...` | RFC 9116 `security.txt` 404 - "kapsam fazla genis" hatasi |
| M3 baslik dosyasindan X-Frame-Options + CSP kaldirildi | `IKI_SERVER_BLOGU_...` | Dalga 2'nin olculen once-durumu: storefront iframe'lenebilir |

Ucunde de **TAM 1 kirmizi / 5 yesil** (mutasyon lokalize). Hepsi geri alindi; mutasyon izi
depoda **0 dosya**.

## SURECTE YASANAN (kayit - bes ders)

- **EN CIDDISI: CLAUDE.md SIFIRLANDI.** `awk ... $T/yedek > CLAUDE.md` zincirinde bir onceki
  komut yedegi ALAMAMISTI; `awk` girdiyi bulamayip dustu ama kabuk `>` yonlendirmesini
  komuttan ONCE actigi icin **6670 satirlik dosya budandi**. `git checkout -- CLAUDE.md` ile
  geri alindi (calisma agaci o dosya icin temizdi, KAYIP YOK) ve kalan uc ekleme
  "gecici ciktiya yaz -> satir sayisini dogrula -> tasi" ile yapildi. Kalici kural SUREC
  bolumune yazildi. **NOT: untracked bir dosyada ayni hata GERI ALINAMAZDI.**
- **PIN'IN KENDI HATASI - ILK KOSUMDA YAKALANDI.** `server_name` eslesmesi
  `\bdivisima\.com\b` regex'iyle yazilmisti ve bu desen **`api.divisima.com` ICINDE de**
  eslesiyor; storefront assert'i API blogu uzerinde kosuyordu. Token bazli eslesmeye cevrildi
  (`server_name` degeri ayristirilip TAM esitlik aranir) ve gerekce koda yazildi.
  Pin, kendi yanlisligini yeni bir olcum yapmadan gosterdi.
- **BUYUK ICERIKLI HEREDOC IKI KEZ KIRILDI** (`unexpected EOF while looking for matching`)
  - ~250 satirlik C# ve ~170 satirlik Markdown iceriklerde. Iki tur kaybedildi; icerik
  Write araciyla yazilip EKLEME islemi Bash'e birakildi. **DERS: buyuk/karisik tirnakli
  icerik heredoc ile degil dosya araciyla yazilir.**
- **`grep -c` SIFIR ESLESMEDE exit 1 DONDURUP `&&` ZINCIRINI KESTI** - CLAUDE.md'de
  `ops/set-api-origin.sh` dersinde ZATEN YAZILI olan tuzagin tekrari. `|| true` ile yutuldu.
- **`head -n -1` MUKERRER ANAHTAR BIRAKTI:** example.json'in son iki satiri (`AdminSeed` +
  kapanis) yerine yalniz kapanis silindi, `AdminSeed` IKI KEZ olustu. Yazmadan onceki kendi
  sayim kontrolu yakaladi; `head -n -2` ile duzeltildi ve JSON gecerliligi
  `ConvertFrom-Json` ile ayrica dogrulandi (60 anahtar).

## DEFTERE (duzeltme YOK, karar verildi)

- **#1 + #2 misafir checkout - LAUNCH SONRASI (kullanici karari).** Gerekce: **409 hesap ele
  gecirmeyi ENGELLIYOR** ve onu kaldirmak daha buyuk bir riski acar; G2 kalibini (ayni yanit +
  gercegi e-postayla soyle) uygulamak misafir akisinin TASARIMINI degistirir ve su an gereksiz
  risk. 10/dk/IP sinir yeterli hafifletme (olculdu: 11. istek 429).
- **failed-jobs PII riski - GERCEK MAIL TURUNDA yeniden olculecek.** Dalga 2'de PII tasiyan
  bir hata metni URETILEMEDI (SMTP kapaliydi), yani risk teorik kaldi. SMTP acildiginda
  (bkz. "GERCEK MAIL TURU - BEKLIYOR") gercek bir gonderim hatasi uretilip `error` alaninin
  ne tasidigi olculmeli.
- **YAN GOZLEM (kapsam disi, DOKUNULMADI): `frontend/pwa/` dizini OLU.** Olculdu: index.html
  `/manifest.json`, `/pwa-register.js` ve `/service-worker.js`i KOK'ten yukluyor; `pwa/`
  altindaki dort dosyaya (manifest.json, offline.html, service-worker.js, sw-register.js)
  referans veren **hicbir sey yok**. Ic dokuman OLMADIKLARI icin deny kurallari onlari
  bilerek kapsamiyor (`pwa/README.md` yalniz `.md` oldugu icin kapandi). Mukerrer/bayat bir
  yuzeydir; temizlik AYRI bir karardir.

---

