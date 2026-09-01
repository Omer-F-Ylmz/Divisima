# DALGA C - YAYIN ALTYAPISI (TAMAMLANDI)

Dalga A "ilk musteri zinciri"ni, Dalga B "gelen siparisi yonetebilme"yi kapatti. Dalga C'nin
sorusu farkli: **depo bugun yayina cikabilir mi?** Alti kalemin ortak ozelligi, uygulamanin
CALISMASI degil YAYINLANABILMESIYDI.

## C1 - STOREFRONT'U KIMSE SUNMUYORDU

```
Dockerfile           : yalniz Divisima.API publish ediliyor; frontend/ HIC kopyalanmiyor
docker-compose.yml   : sqlserver + redis + api   (frontend servisi YOK, nginx YOK)
ops/infra/nginx.conf : TEK server block -> server_name api.divisima.com
                       divisima.com icin HICBIR TANIM YOK
Divisima.API/wwwroot : yalniz uploads/products   (frontend dosyasi yok)
```

Yani "dosyalari bir yere kopyala" adimi depoda **yazisiz** kaliyordu.

**KULLANICI KARARI: nginx + ayri statik konteyner (iki origin KORUNUR).** Gerekce olcumden
geldi: depo ZATEN iki origin varsayiyor ve bunu **uc yerde** belgeliyor -
`AllowedOrigins: ["https://divisima.com", ...]`, `Storefront:BaseUrl = https://divisima.com`,
ve Sprint 8 madde 6'da eklenen `Cookies:Domain = .divisima.com` (gerekcesi birebir:
*"storefront (divisima.com) ile API (api.divisima.com) FARKLI HOSTLAR"*). API'nin
wwwroot'undan sunmak bir bosluk doldurma degil, **bu ucunu birden geri alma** olurdu.

YAPILAN:
- `ops/infra/nginx.conf`'a `divisima.com` server blogu: statik kok, SPA fallback
  (`try_files ... /index.html` - hash router), `/sitemap.xml` proxy'si, `admin.html` icin
  `X-Robots-Tag: noindex`, kod tasiyan dosyalarda `no-cache`.
- `docker-compose.yml`'a `frontend` servisi (nginx:alpine, `./frontend` **salt-okur** mount)
  + `ops/infra/frontend-dev.conf`. Yerelde bugune kadar elle kurulan duzen artik
  `docker compose up` ile geliyor.
- `set-api-origin.sh` DAGITIM adimi olarak AYNEN kaldi; checklist'e sira sarti yazildi
  (once betik, sonra kopyalama - tersi olursa storefront localhost'a bakar).

## C2 - YUKLENEN GORSELLER KONTEYNER DEGISINCE KAYBOLUYORDU

Compose'da `mssql_data` ve `redis_data` vardi, **yuklemeler icin volume yoktu**.
`LocalImageStorage` dosyalari `WebRootPath/uploads/products` altina yazar (Sprint 8 madde 4),
yani konteynerin YAZILABILIR KATMANINA -> yeni surumde `product_images` satirlari var olmayan
dosyalari gosterir (vitrinde kalici 404).

**KRITIK SORU VARSAYILMADI, OLCULDU** (non-root konteynerde volume yazilabilir mi):

```
dotnet publish ciktisinda wwwroot VAR -> 77 dosya (dev'de yuklenmis gorseller)
Dockerfile: COPY --from=build ... ; chown -R divisima:divisima /app ; USER divisima
=> /app/wwwroot/uploads imajda VAR ve divisima:divisima sahipli
=> adlandirilmis volume ilk mount'ta imaj icerigini VE SAHIPLIGI devralir -> yazilabilir
```

**AMA `.dockerignore` DUZELTMESI BU ZINCIRI KIRIYORDU** (uygularken fark edildi): dev
gorsellerini build context'inden dislamak -> publish **bos dizini kopyalamaz** -> imajda
`/app/wwwroot/uploads` YOK -> Docker volume'u **root:root** olusturur -> `USER divisima`
YAZAMAZ. Bu yuzden Dockerfile'a `RUN mkdir -p /app/wwwroot/uploads/products` eklendi ve
**chown'dan ONCE** konumlandirildi. Sira pinlendi (indeks karsilastirmasiyla).

## C3 - ILK ADMIN: SIFRE POLITIKASININ BESINCI VE GOZDEN KACMIS GIRISI

`AdminSeeder.SeedAsync` acilista ZATEN cagriliyordu (idempotent, `Enabled=false` varsayilan).
Iki bulgu:

- **`SifrePolitikasi` UYGULANMIYORDU.** A2-FIX (SUPHELI #21) kurali TEK MERKEZE tasimis ve
  DORT girise baglamisti (kayit, satici kaydi, sifre degistirme, sifre sifirlama). Bu
  **besincisiydi**: sistemin EN YETKILI hesabi, kayit ucunun reddedecegi `abc` gibi bir
  sifreyle acilabiliyordu.
- Tohumlama hatasi yalniz loglaniyor (uygulama devam ediyor) - yanlis yapilandirilmis bir
  `AdminSeed` **sessiz** kaliyordu.

**FAIL-FAST SECILMEDI - GEREKCE:** `AdminSeed` tek seferlik bir ONYUKLEME bayragidir; yanlis
yazilmis bir sifre yuzunden uygulamanin acilmamasi **siteyi tumden indirir**. Dogru davranis
"admini OLUSTURMA ve GURULTULU soyle". Mesaj IHLAL EDILEN KURALI adiyla yaziyor.

**TEMIZ VERITABANINDA UCTAN UCA SURULDU** (`DivisimaSeedTest`, migration'la kuruldu, is bitince
DROP edildi):

```
ZAYIF SIFRE ("abc")
  API acildi: 200            <- bayrak siteyi INDIRMEDI
  log: [ERR] AdminSeed sifresi POLITIKAYA UYMUYOR, ilk admin OLUSTURULMADI:
       Şifre en az 8 karakter olmalı. (AdminSeed:Password duzeltilip ... )
  DB : ADMIN_SAYI 0   MUSTERI_SAYI 0

GECERLI SIFRE
  log: [INF] İlk admin oluşturuldu (ilk.admin@divisima.com).
  DB : id=1  user_type=1  email_verified=1  is_active=1  phone dolu
  GERCEK GIRIS: HTTP 200, token uretildi, customer_id=1

IKINCI ACILIS (FARKLI e-posta ile)
  log: [INF] Admin zaten mevcut - tohumlama atlandı.
  DB : ADMIN_SAYI 1  TUM_MUSTERI 1  -> e-posta ILK acilistaki
```

## C4 - ARKA PLAN IS HATALARI KIMSEYE GORUNMUYORDU

```
Uretimde kosan recurring is : 7 adet (outbox islemcisi, veri saklama, rezervasyon temizligi,
                              terk sepet, dogum gunu, win-back, yorum daveti)
Hangfire panosu : filtre "authenticated + user_type=1" ister
                  AMA uygulamada TEK kimlik semasi JwtBearer (AddCookie YOK)
                  tarayici /hangfire'a giderken Authorization basligi GONDERMEZ
                  => IsAuthenticated HER ZAMAN false => HERKESE KAPALI
                  (ustelik nginx'te ikinci kilit: allow 10.0.0.0/8)
Outbox/is durumu donen admin ucu : SIFIR (tarandi)
```

`ops/deployment-checklist.md`'deki *"Hangfire dashboard yetkilendirme (yalniz admin - su an
acik!)"* maddesi **BAYATTI** - filtre var ve kapali; sorun tersineydi. Madde duzeltildi.

**KULLANICI KARARI: salt-okur admin ucu** (Hangfire panosunu acmak yerine). Gerekce olcumden:
`DataRetentionJob` YALNIZCA `status=1` (Processed) mesajlari siliyor, **`Failed` olanlar
KALICI**. Yani basarisiz arka plan isinin dayanikli kaydi ZATEN veritabaninda - gostermek icin
yeni depolama ya da yeni bir kimlik yuzeyi (cerez semasi) acmaya gerek yok.

- `GET /api/dashboard/failed-jobs` (`DashboardController`, sinif duzeyinde `RequireUserType(Admin)`)
- **`payload` BILINCLI OLARAK DISARIDA**: mesaj govdesi e-posta adresi/jeton/siparis ayrintisi
  tasir ve operatorun sorusuna ("hangi is, kac denemede, hangi hatayla") gerekli DEGILDIR.
  Hata metni ayrica `KanitMaskesi`'nden gecirilir (bolum 1).
- Panel: **Panel sekmesine** konuldu (ayri sekmede gozden kacardi). Canli teyit - Dalga A'nin
  SMTP turundan kalan GERCEK basarisiz mesaji buldu:
  `18 | OrderPlaced | 5 | Hedef makine ... reddettiginden baglanti kurulamadi. | 23.08.2026`

**LOG SAKLAMA:** `WriteTo.File(..., rollingInterval: Day)` disinda sinir YOKTU.
Serilog.Sinks.File 5.0.0 varsayilanlari `fileSizeLimitBytes=1GB`, **`rollOnFileSizeLimit=false`**,
`retainedFileCountLimit=31`. Tehlikeli olan ikincisiydi: bir gunun dosyasi 1 GB'a ulasinca sink
**yazmayi SESSIZCE birakir** - yani en cok log ureten (en cok sorun yasanan) gunde loglar tam da
ihtiyac duyuldugu anda kesilir. Degerler ACIKCA yazildi: gunluk + **100 MB'da parcala** +
30 dosya. (`DataRetentionJob` bu bosluğu kapatmiyor - o yalniz VERITABANINI temizler.)

## C5 - PAYLASIM ONIZLEMESI ve SITEMAP ZINCIRI

```
frontend/robots.txt      : VAR, "Sitemap: https://divisima.com/sitemap.xml" diyor
GET /api/seo/sitemap     : VAR ve gercek sitemap uretiyor (urun + kategori)
divisima.com/sitemap.xml : bunu SUNAN hicbir sey YOK   <- zincirin ORTA halkasi kopuk (C1)
og:type/site_name/title/description/locale : VAR
og:image, og:url                            : YOK
twitter:card                                : "summary_large_image" (GENIS gorsel VAAT ediyor,
                                               hicbir gorsel VERMIYOR -> bos kutu)
Organization schema logo : https://divisima.com/logo.png  <- depoda BOYLE BIR DOSYA YOK
```

YAPILAN: `og:image` (mutlak URL, `icons/icon-512.png` - **depoda var oldugu pinde dogrulaniyor**),
`og:url`, `og:image:width/height/alt`. **`twitter:card` "summary"ye CEKILDI**: elimizdeki varlik
512x512 KARE, `summary_large_image` 1200x630 bekler - yanlis vaat etmek yerine dogru kart turu
secildi (gercek marka gorseli hazirlaninca geri genisletilebilir, checklist'te madde).
Organization logosu var olan dosyaya cevrildi. Sitemap zinciri C1'in nginx blogu ile kapandi.

**DURUST SINIR:** `setProductSchema` calisma aninda og etiketlerini guncelliyor ama **paylasim
botlari JS CALISTIRMAZ** - URUNE OZEL onizleme bu etiketlerle SAGLANAMAZ; onun icin sunucu
tarafi render/prerender gerekir (ayri is). Buradaki etiketler SITE DUZEYI onizlemeyi calisir
hale getirir.

## C6 - IKI DAR KALEM

**(a) `ProductManager.Update` stok dongusu ATOMIK.** Dalga B upsert'e gecmisti ama dongu HALA
transaction'sizdi: ortada bir DB hatasi bazi bedenleri yazilmis bazilarini yazilmamis birakirdi.
`IUnitOfWork` zaten `InstancePerLifetimeScope` kayitliydi - kurucuya tek satir.
`ExecuteInTransactionAsync` SECILDI (manuel `BeginTransaction` DEGIL): `Program.cs`'in kendi notu
*"EnableRetryOnFailure acilirsa manuel BeginTransaction retry stratejisi tarafindan REDDEDILIR"*
diyor. **KAPSAM EN DAR:** yalniz stok dongusu (+ pasifleme). Urun satirinin kendi yazimi tek
`SaveChanges`, zaten atomik; `NotifyPriceDrop` DIS IS yapar ve transaction icinde tutulmaz.

**(b) Kargolanmayi bekleyenler listesi.** Kargo ekrani KOR FORMDU - operatorden siparis ID'si
elle isteniyordu. **Hangi durumun "bekliyor" oldugu UYDURULMADI, durum makinesinden turetildi:**
`OrderStatusMachine`'e gore `Shipped`'e YALNIZ `Preparing` gecebilir ve `CreateShipment` bunu
zaten dogruluyor. **Backend degisikligi SIFIR** - `AdminOrderFilterDto.status` filtresi vardi.
Pin bu degeri elle yazmiyor, **makineden HESAPLIYOR** (makine degisirse pin kirilir).

Canli: bos durum -> "Kargolanmayi bekleyen siparis yok" · siparis Preparing'e alindi -> liste
doldu · "Kargo gir" formu doldurdu (#92) · kargo olusturuldu -> form temizlendi, liste yeniden
bosaldi (siparis Shipped'e gecti).

## C5'IN B5'LE ILISKISI YOK - HAVALE UYKUDA

Dalga B'de verilen karar (havale yuzeyi acilmaz) DEGISMEDI; bu dalgada dokunulmadi.

## SURECTE ORTAYA CIKAN BULGU - GELISTIRICI SECRET'LARI TEST HOST'UNA SIZIYORDU

`AdminSeed_KAPALIYKEN_HICBIR_ADMIN_ACILMAZ` pini ilk kosumda **YEREL MAKINEDE KIRILDI**
(1 admin buldu). Kok sebep: `WebApplicationFactory<Program>` uygulamanin TAM yapilandirmasini
yukler - Development ortaminda **USER-SECRETS DAHIL**. Bu makinede
`dotnet user-secrets list` ciktisinda `AdminSeed:Enabled=true` (+ e-posta/sifre) VARDI,
dolayisiyla **HER test host'u** acilirken o testin veritabanina beklenmeyen bir admin satiri
yaziyordu.

Zarar iki yonlu: (a) "admin sayisi" olcen bir pin **yerelde kirmizi, CI'da (secret yok) yesil**
olur - sonuc MAKINEYE gore degisir; (b) tersi de mumkun: bir yetki pini hazir bulunan admin
yuzunden YANLIS SEBEPTEN yesil kalabilir.

DUZELTME `TestHostConfig`'te (TUM test host'larini kapsar): `AdminSeed:Enabled` varsayilan
**false**. Tohumlamayi OLCEN testler bayragi KENDILERI aciyor (`UseSetting` daha SONRA
cagrildigi icin oradaki deger kazanir). Ayrica ilgili pin degeri artik ACIKCA veriyor -
**yapilandirmayi olcen bir pin, degeri kendisi vermelidir.**

## PINLER (14 yeni)

**`DalgaCYayinAltyapisiTests` (7, SQL + HTTP):**
- ilk admin temiz DB'de olusur ve **GERCEKTEN GIRIS YAPABILIR** (vakum kirici: satirin var
  olmasi yetmez - yanlis hash'lenmis/dogrulanmamis bir admin tum alan assertlerini gecer ama
  operator panele GIREMEZ)
- zayif sifreyle olusturulmaz **ve uygulama YINE ACILIR** (vakum kirici: fail-fast bilincli
  olarak secilmedi)
- idempotent: ikinci acilis **FARKLI e-postayla** da ikinci admin ACMAZ
- bayrak kapaliyken hicbir admin acilmaz (cift-anlam kirici; deger ACIKCA veriliyor -
  gerekcesi yukarida)
- basarisiz arka plan isleri admin ucundan gorunur, **payload SIZMAZ** (cift-anlam kirici:
  yalniz Failed donmeli, Processed donmemeli; kisisel veri yanitta olmamali)
- uc anonim ve musteri tarafindan okunamaz (401 / 403)
- urun guncelleme stok dongusu atomik: reddedilen istek **hicbir iz birakmaz** (+ vakum kirici:
  gecerli istek GERCEKTEN yazar)

**`DalgaCDagitimSozlesmesiTests` (7, artefakt sozlesmesi):**
- nginx hem API hem storefront blogunu tasir + sitemap proxy + SPA fallback (vakum kirici:
  dosya gercekten nginx yapilandirmasi olmali)
- compose storefront servisini tasir ve **yapilandirmasi depoda**
- yukleme dizini kalici volume'de **ve SAHIPLIK ZINCIRI kurulu** (mkdir'in chown'DAN ONCE
  geldigi INDEKS KARSILASTIRMASIYLA dogrulanir)
- paylasim etiketleri tam **ve kart turu gorselle tutarli** (+ og:image'in gosterdigi dosyanin
  depoda GERCEKTEN var oldugu; olmayan logo yolu geri gelemez)
- robots'taki sitemap adresi nginx'in **sundugu yolla ORTUSUR** (yol robots.txt'ten
  AYRISTIRILIP nginx'te aranir - iki taraf elle esitlenmiyor)
- kargo ekrani bekleyen listesi tasir **ve durum makinesiyle tutarli** (beklenen durum
  `OrderStatusMachine`'den HESAPLANIR)
- admin tohumlama varsayilani kapali ve sifre alani bos

**KIRILAN PIN YOK.**

**PIN SINIRI (durust kayit):** Docker imaji ve nginx yapilandirmasi bu suitte AYAGA
KALDIRILAMAZ; `DalgaCDagitimSozlesmesiTests` **artefaktin kendisini** okur. "Konteyner gercekten
ayaga kalkiyor ve volume yaziliyor" bir CI-with-Docker isidir (ayri karar). Sahiplik zincirinin
DOGRU KURULDUGU olcumle (publish ciktisi + chown sirasi) gerekcelendirildi, calistirilarak
degil.

## DIS KONTROLU + 5. KONTROL

**DIS:** 6 assert ters (ALTI AYRI test, IKI sinif) -> **6 AYRI ISIMLI KIRMIZI**. Geri alindi.

**5. KONTROL - DORT URETIM MUTASYONU** (farkli testleri vurduklari icin ayristirilabilir):

| Mutasyon | Kirilan pin | BULUNAN | Olculen once-durum |
|---|---|---|---|
| M1 `AdminSeeder`'dan politika cagrisi kaldirildi | `IlkAdmin_ZAYIF_SIFREYLE_OLUSTURULMAZ...` | `"abc"` ile admin **OLUSTU** (found True) | C3'un besinci-kopya boslugu |
| M2 DTO'ya `payload` geri kondu | `BasarisizArkaPlanIsleri_..._PAYLOAD_SIZMAZ` | `error` alani **`{"To":"gizli.musteri@example.com",...}`** - kisisel veri SIZDI | C4'un sizinti kapisi |
| M3 nginx storefront blogu kaldirildi | `NGINX_HEM_API_HEM_STOREFRONT...` + `ROBOTS_SITEMAP_ADRESI...` | storefront blogu ve `/sitemap.xml` yolu YOK | C1'in olculen once-durumu |
| M4 Dockerfile `mkdir` satiri kaldirildi | `YUKLEME_DIZINI_KALICI_VOLUME...` | mkdir satiri YOK -> sahiplik zinciri kirik | C2'nin sessiz kirilma yolu |

Dordu de geri alindi; 14/14 yesil.

## YEREL DOGRULAMA

290/290 `Category=Sql` · tam suitte **471 basarili / 474** (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`) · Release 0 hata · whitespace + style **exit 0**.

## ACIK KALANLAR (bloke etmez)

- **Docker/nginx CALISTIRILARAK dogrulanmadi** - artefakt sozlesmesi pinli, ayaga kaldirma
  CI-with-Docker isi (yeni bagimlilik + kosum suresi; ayri karar).
- **URUNE OZEL paylasim onizlemesi** sunucu tarafi render gerektirir; bugun site duzeyi
  onizleme calisiyor.
- **1200x630 marka gorseli** hazirlaninca `og:image` degistirilip `twitter:card` geri
  genisletilebilir (checklist'te madde).
- **Hangfire panosu bilincli olarak KAPALI** - operatorun yuzeyi `failed-jobs` ucudur.
- **120 yetim `product_stocks` satiri** DALGA D'ye (kullanici karari): temizligin URETIM
  YOLUYLA mi migration'la mi yapilacagi orada olculecek.

---

