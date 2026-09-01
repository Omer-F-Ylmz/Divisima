# DALGA D - D1 + D4 + D5 (TAMAMLANDI)

D-SEMA karari uygulandiktan sonra Dalga D'nin kalan kalemleri **gercek zeminde** kosuldu.
Sira kullanici karariyla D1 -> D4 -> D5. (D3 ve D6 sonraya birakildi.)

## D1 - GORSEL YUKLEME SIZINTISI ve YETIM SATIRLAR

### OLCUM GUNCELLENDI - TABLO DEGISMISTI

Kapsama denetimindeki "79 dosya" bayatlamisti; ustelik sizintinin YERI de degismisti:

```
Divisima.API/wwwroot/uploads/products             : 96 dosya (HEPSI 64 bayt)
Divisima.IntegrationTests/bin/.../uploads/products: 35 dosya (HEPSI 64 bayt)
product_images satiri                             : 3 (dosyalari diskte YOK)
KESISIM                                           : BOS
```

**SIZINTI NEDEN URETIM WWWROOT'UNA GECMISTI:** Sprint 8 madde 4 `LocalImageStorage`i DOGRU
sekilde `WebRootPath`e tasidi (oncesinde CWD'ye yaziyor, sunum baska dizinden yapiliyordu -
E2b'deki canli 404'lerin sebebi buydu). Test host'unun ContentRoot'u `Divisima.API` oldugu
icin WebRoot da deponun kendi `wwwroot`'u oldu. **Duzeltme dogruydu; eksik olan TESTIN ayri
bir koke yazmasiydi.**

### YAPILANLAR

1. **3 DB satiri URETIM YOLUYLA silindi** (`ProductImageManager.Delete`; ELLE SQL YOK).
   Kosucu DEPO DISINDA tutuldu ve is bitince silindi (7 iptal faturasi / siparis #33 kalibi).
   ```
   Delete(1/2/3) -> 200 OK        product_images: 3 -> 0
   urun 2'nin image_url'i uretimin KENDI mantigiyla NULL'landi
       (birincil silindi, kalan gorsel yok) - artik 404'leyen bir gorsel IDDIA ETMIYOR
   IKINCI KOSUM NO-OP: 404 x3, satir 0'da KALDI
   ```
2. **131 yetim dosya silindi.** Silme "hepsini sil" DEGIL, **OLCULEN IMZAYLA**: yalnizca
   64 baytliklar. Gercek bir gorsel oraya dusmus olsaydi DOKUNULMAZDI.
3. **Sizinti kapandi:** `TestWebRoot` (surec basina gecici dizin, OS temp'te) +
   `TestHostConfig`te `UseWebRoot` + surec cikisinda temizlik.

### KULLANICININ SARTI KORUNDU - PIN GUCLENDI

Sprint 8 madde 4 pini KIRILMADI ve `UseContentRoot(CWD)` GERI GELMEDI. Aksine pin **guclendi**:
yazma+sunum uyumu artik **UCUNCU** bir dizinde (ne CWD ne ContentRoot) kanitlaniyor ve iki
CIFT-ANLAM KIRICI eklendi - dosyanin DEPO agacina ve CALISMA DIZININE **yazilmadigi** ayri ayri
assert ediliyor.

**KANIT:** tam suit kosumu sonrasi depo kirliligi **0 -> 0**.

## D4 - IDEMPOTENCY: UC KUSUR OLCULDU ve DUZELTILDI

### CANLI TUR STATIK OKUMAMDAKI BIR HATAYI DUZELTTI

"Anahtar kapsami `key|path|user`" sanmistim - **MIDDLEWARE'da user bileseni YOKTU.**
Iki ayri mekanizma vardi ve davranislari CELISIYORDU.

### OLCULENLER (gercek API, gercek hesaplar)

| Olcum | Sonuc |
|---|---|
| ayni anahtar x2 | 201 -> 409, ikinci kayit YOK (asil vaat CALISIYOR) |
| **capraz kullanici** | A anahtar K -> 201; B **AYNI K -> 409**, B'nin kaydi **0** |
| **basarisiz istek** | bozuk govde 400 -> AYNI anahtar + GECERLI govde **409** (24 saat yandi) |
| **filtre replay'i** | isaretli ucta 2. istek **409**, `Idempotency-Replayed` basligi **YOK** |
| anahtarsiz istekler | 201 + 201 (vakum kirici) |

Turda **401 ve 405** ile de birebir ayni "anahtar yandi" sonucu alindi - yani anahtar
istegin CONTROLLER'A ULASMASINDAN once ve yanittan BAGIMSIZ olarak tutuluyordu.

### DUZELTMELER

1. **Middleware `UseAuthorization`DAN SONRAYA tasindi** ve anahtara **kullanici** bileseni
   eklendi. Yan kazanc: 401/403 alan istek artik anahtari HIC talep etmez.
   **KIMLIK KAYNAGI `ClaimTypes.NameIdentifier`** - `Identity.Name` DEGIL. Bu, pin
   yazilirken OLCULDU: JwtHelper token'a `ClaimTypes.Name` YAZMIYOR, dolayisiyla
   `Identity.Name` null doner ve TUM kimlikli kullanicilar AYNI kapsama duserdi; yani
   cakisma **kapanmis GORUNUR ama KAPANMAZDI** (ilk denemede B hala 409 aldi).
2. **Anahtar YALNIZCA 2xx yanitta tutulur.** Filtrede de ayni kural: eski kosul
   `status < 500` idi, yani bir 400 de "kesin sonuc" sayilip anahtari yakiyordu. 4xx bir
   ISTEMCI HATASIDIR ve duzeltilebilir.
3. **MEKANIZMA SECIMI (olcume dayali): FILTRE KALIR, MIDDLEWARE DARALIR.**
   Filtre yalnizca **dort para ucunda** (order/place, guest-checkout/place, loyalty/redeem,
   giftcard/redeem) ve orada **replay dogru davranistir** - ag tekrari yapan musteri ILK
   istegin sonucunu (siparis numarasi) OGRENMELIDIR. Middleware geri kalan TUM mutasyonlarda
   genis emniyet agi olarak kalir. Middleware artik endpoint metadata'sinda
   `IdempotencyAttribute` gorurse KENARA CEKILIYOR. **Ikisi de ULASILABILIR, OLU KOD YOK.**

### DUZELTIRKEN CIKAN DORDUNCU BULGU

**`IDistributedCache` YALNIZCA Redis dalinda kayitliydi.** ASP.NET Core onu varsayilan olarak
KAYDETMEZ. Sonuc: `IdempotencyAttribute` `cache == null` gorup `await next()` ile **sessizce
devre disi** kaliyordu - yani filtre **dev/test/CI'da HIC CALISMIYORDU**. Ustelik filtrenin
kendi yorumu *"Redis yoksa in-memory implementasyona duser"* diyordu; **o yorum YANLISTI**.
Redis-disi dala `AddDistributedMemoryCache()` eklendi ve yorum duzeltildi.

Yani filtre iki ortamda da ise yaramiyordu: uretimde middleware golgeliyordu, dev/test'te
servis yoktu. Bu, "filtre kalsin" kararini AMPIRIK olarak da destekledi - kalmasi icin
GERCEKTEN calisir hale getirilmesi gerekiyordu.

## D5 - REDIS: CANLI TUR OLCULMEDI, AYRISMA DUZELTILDI

### CANLI REDIS TURU YAPILAMADI - DURUST KAYIT

```
docker CLI YOK · yerli redis-server YOK · 6379 KAPALI
```

**Kullanici karari: secenek 3** - canli tur "olculmedi" olarak kaydedilir ve staging'e
ertelenir. Uydurulmadi.

### YINE DE OLCULEBILENLER

**(a) FAIL-FAST OLCULDU.** `Redis:Enabled=true` + erisilemez 6379 -> uygulama **HIC ACILMIYOR**
(`StackExchange.Redis.RedisConnectionException`). Sessizce in-memory'ye **DUSMUYOR**. Bu DOGRU
davranistir; ama hicbir yerde belgelenmemisti - `ops/backup-dr-runbook.md` (felaket senaryolari
tablosu + ayri bir bolum) ve `ops/deployment-checklist.md`'ye yazildi.

**(b) IKI GERCEK AYRISMA (kullanici karariyla DUZELTILDI - bunlar bir Redis testi degil,
YAPILANDIRMA HATASIYDI):**

```
kova     | YERLESIK yol (dev/test/CI)              | REDIS yolu (URETIM)
auth     | 10/dk, RateLimit:AuthPermitLimit'ten    | 5/dk, KAYNAKTA SABIT
payment  | 10/dk                                    | 10/dk
global   | 100/dk                                   | 100/dk
```

`app.UseRateLimiter()` **YALNIZCA** `Redis:Enabled=false` dalinda cagriliyordu. Yani uretimde:
`[EnableRateLimiting("auth"/"payment")]` oznitelikleri **ETKISIZ**, `RateLimit:*` ayarlari
**HIC OKUNMUYOR**, auth kovasi 10 degil **5**. `deployment-checklist.md`'deki "rate limit
esikleri prod trafigine gore ayarlandi" maddesi uretimde **KARSILIKSIZDI**.

Ayrisma **YALNIZ auth kovasindaydi** (digerleri ortusuyordu) - yani bilincli bir tasarim
tercihi degil, **gozden kacmis bir sapma**.

### DUZELTME

1. **`RateLimitPolitikasi` - kova tanimlarinin TEK KAYNAGI.** Hem `AddRateLimiter` hem
   `RedisRateLimitMiddleware` buradan okur; yol->kova eslesmesi de burada (kultursuz,
   `OrdinalIgnoreCase` - B3 dersi). Yeni bir esik eklendiginde iki yol OTOMATIK ayni degeri
   gorur; ayrisma YAPISAL olarak imkansiz.
2. **IKI YOL DA HER ZAMAN DEVREDE.** Middleware'in Redis'e bagimliligi YOK -
   `IDistributedRateLimiter` her iki dalda da kayitli (Redis ya da in-memory), yalnizca ARKA
   DEPO degisiyor. Boylece **dev/test ve URETIM AYNI BORU HATTINI kosuyor**; onceden uretimin
   gercek rate limit yolu HICBIR TESTTE kosmuyordu ve ayrisma bu yuzden gorunmemisti.
3. Kazanan deger **YERLESIK yolunki** secildi (10, yapilandirilabilir). Gerekce: iki yoldan
   biri secilecekse YAPILANDIRILABILIR ve BELGELENMIS olani kazanmalidir - aksi halde
   checklist'teki ayar yine yalan olurdu.

### CIFTE SAYIM SORUSU - AMPIRIK YANIT: HAYIR

Kullanicinin sordugu olcum. `RateLimitTekKaynakTests`, limiti **3** yapip (5 ve 10'a esit
OLMAYAN, ayirt edici bir deger) uctan uca olcuyor:

```
1., 2., 3. istek -> 429 DEGIL      (cifte sayim olsaydi 2. istekte 429 gorurduk)
4. istek         -> 429            (mekanizma GERCEKTEN calisiyor - vakum kirici)
/health          -> 429 DEGIL      (auth kovasinin tukenmesi DIGER kovalari kapatmiyor)
```

**Gerekce:** iki sayac da AYNI istekte, AYNI bolumleme anahtariyla (`RemoteIpAddress`) ve AYNI
limitle artiyor - yani KILITLI ADIMDA ilerliyorlar. Etkin limit ikisinin MINIMUMU'dur ve ikisi
esit oldugu icin beklenen degere esittir.

## PINLER

**`IdempotencyContractTests` (5)** - ucu D4 duzeltmelerinin davranis pini:
- `AyniAnahtar_IKINCI_ISTEK_409_ve_IKINCI_KAYIT_OLUSMAZ` (+ vakum kirici: FARKLI anahtar islenir)
- `ANAHTARSIZ_Istekler_ETKILENMEZ`
- `CAPRAZ_KULLANICI_ETKILENMEZ_HER_KULLANICI_KENDI_KAPSAMINDA` (+ cift-anlam kirici: AYNI
  kullanici + AYNI anahtar HALA 409 - kullanici kapsami korumayi KALDIRMADI)
- `BASARISIZ_ISTEK_ANAHTARI_YAKMAZ_DUZELTILMIS_TEKRAR_DENEME_ISLENIR` (+ cift-anlam kirici:
  BASARILI istekten sonra ayni anahtar HALA engellenir - "hep birak" uygulamasi gecemez)
- `FILTRE_REPLAYI_CALISIR_IKINCI_ISTEK_ILK_YANITI_DONER` (+ cift-anlam kirici: replay KOZMETIK
  DEGIL - 500 puanin yalniz 100'u harcanmis olmali)

**`RateLimitTekKaynakTests` (2)** - D5 uctan uca:
- `IKI_YOL_AYNI_ANDA_ETKIN_CIFTE_SAYIM_YOK_LIMIT_YAPILANDIRMADAN_GELIR`
- `LIMIT_YAPILANDIRMASI_OKUNMASAYDI_BU_TEST_GECMEZDI` (karsit kontrol: depodaki varsayilanlar
  5 ve 10; test host'u 3 veriyor - ayar okunmasaydi 4. istek GECERDI)

**`RateLimitPathScopeTests`** - `AUTH_LIMITI_YAPILANDIRMADAN_GELIR_KAYNAKTA_SABIT_DEGIL`
(ayirt edici degerler 37/41/43 - ne 5 ne 10; + cift-anlam kirici: config YOKKEN varsayilanlar
10/10/100, yani "her zaman config" degil "config VARSA config")

**`AdminStockAndImageTests.UploadedImage_NosniffVeMagicByte_PINLENIR`** - D1 assertleri eklendi
(yukleme test WebRoot'una duser; DEPO agacina ve CWD'ye DUSMEZ).

### BILINCLI KIRILAN DORT PIN

| Kirilan | Yerine | Gerekce |
|---|---|---|
| `SUPHELI_CAPRAZ_KULLANICI_AYNI_ANAHTAR_IKINCININ_ISTEGINI_DUSURUR_PINLENIR` | `CAPRAZ_KULLANICI_ETKILENMEZ_...` | zarar duzeltildi; eski pin YANLIS sozlesmeyi savunurdu |
| `SUPHELI_BASARISIZ_ISTEK_ANAHTARI_YAKAR_..._409_PINLENIR` | `BASARISIZ_ISTEK_ANAHTARI_YAKMAZ_...` | ayni |
| `SUPHELI_FILTRE_REPLAYI_ULASILAMAZ_MIDDLEWARE_ONCE_409_DONER_PINLENIR` | `FILTRE_REPLAYI_CALISIR_...` | ayni |
| `SUPHELI_AUTH_LIMITI_REDIS_YOLUNDA_5_YERLESIK_YOLDA_10_PINLENIR` | `AUTH_LIMITI_YAPILANDIRMADAN_GELIR_...` | ayni |

Ayrica `RateLimitPathScopeTests`in iki mevcut pinindeki sabit `5` -> `10` guncellendi
(assert'lerin OLCTUGU sey - yol->kova eslesmesi - DEGISMEDI, yalnizca beklenen limit degeri
tek kaynaga tasindi).

## DIS KONTROLU + 5. KONTROL

**DIS:** 6 assert ters cevrildi (ALTI AYRI test, DORT ayri sinif) -> **6 AYRI ISIMLI KIRMIZI**.
Geri alindi.

**5. KONTROL - UC URETIM MUTASYONU:**

| Mutasyon | Sonuc | Olculen once-durum |
|---|---|---|
| **M1** middleware'den kullanici kapsami kaldirildi | `CAPRAZ_KULLANICI_...` KIRMIZI: B **409** buldu | canli turdaki tablonun BIREBIR aynisi |
| **M2** "yalniz 2xx'te tut" kosulu kaldirildi | `BASARISIZ_ISTEK_...` KIRMIZI: duzeltilmis tekrar deneme **409** | ayni |
| **M3** auth limiti kaynakta sabit 5 yapildi | `AUTH_LIMITI_...` **5 buldu** (beklenen 37); iki uctan uca pin de kirmizi (4. istek **200**) | D5'te olculen ayrismanin ta kendisi |

M1 ve M2'de TAM 1 pin kirmizi, 4 yesil kaldi (mutasyon lokalize). Ucu de geri alindi ve
`[MUTASYON]` kalintisi olmadigi ayrica dogrulandi.

## DALGA ICI DENETIM - D1/D4/D5

**KENDI HATALARIM (dort):**
1. **`Identity.Name` varsaydim, olcmedim.** Kullanici kapsamini ekledim ve capraz-kullanici
   pini HALA KIRMIZI kaldi - JwtHelper `ClaimTypes.Name` yazmiyormus. Duzeltme "yapilmis
   gorunup calismiyor" olacakti; pin yakaladi.
2. **`IDistributedCache`in kayitli oldugunu varsaydim.** Filtrenin kendi yorumu oyle diyordu;
   YANLISTI ve filtre dev/test'te HIC calismiyordu. Replay pini yakaladi.
3. **Mutasyonlari `powershell -File` ile kosmaya calistim** - yurutme politikasi engelledi ve
   uc mutasyon da **HIC UYGULANMADI**, testler "14 basarili" dedi. Kalinti kontrolu olmasa
   "mutasyon lokalize" diye YANLIS rapor yazacaktim. **DERS: her mutasyondan sonra dosyada
   `[MUTASYON]` izi ARANIR** (grep ile dogrulama kurali zaten vardi, arac degisince tekrarladi).
4. **PowerShell here-string / .ps1 kodlama tuzagina UC KEZ dustum** (box-drawing, `ç`,
   ve backtick-`r`). **DERS: PowerShell'e yazilan ESLESTIRME dizgeleri SALT ASCII olmali;
   Turkce satirlar SATIR INDISIYLE bulunur.** Ayrica `@"..."@` icinde backtick bir KACIS
   karakteridir - `` `review_id `` yazmak dosyaya CR yazar.

**YAN ETKI TARAMASI:** `RedisRateLimitMiddleware` kurucusu degisti -> tek cagrisi
`RateLimitPathScopeTests`, guncellendi. `IdempotencyMiddleware` artik `Divisima.API.Filters`e
bagimli (ayni derleme). Boru hatti sirasi degisti -> tam suit 489/492 ile dogrulandi.
`TestHostConfig.UseWebRoot` TUM test host'larini etkiliyor -> depo kirliligi 0-0 ve suit yesil.

**PIN DURUSTLUGU:** bu dalgadaki 8 yeni/degisen pinin **tamami DAVRANIS pini** (gercek HTTP
istekleri, gercek DB satirlari, gercek dosya sistemi). Kaynak sozlesmesi pini YOK.

## CI KIRMIZISI cd51a52 ve DUZELTMESI - ARKA PLAN ISLERI TESTLERLE YARISIYORDU

`cd51a52` push'unda **CI KIRMIZI** oldu: Security CI tamamen yesil, `format-check` yesil,
ama `build-and-test` -> `Testler + coverage` FAILURE. Annotation'lar arasinda **TEK ISIMLI
kirmizi** vardi:

```
Failed PaymentCallbackSecurityTests.YanEtkiHatasi_OdemeSUCCESS_KALIR_..._TAMAMLANIR
Expected mesaj.retry_count to be 1 because deneme sayaci artmali, but found 2.
```

Diger "failure" seviyeli satirlar (`Invalid object name 'contents'`, Kerberos, DbUpdateException)
uygulamanin KENDI Serilog ciktisidir - TESHIS adimi kosum sirasindaki istisna satirlarini da
basar (bolum 7'de kayitli). `SQL gerektiren testler` adimi SUCCESS oldugu icin SQL saglamdi.

**KOK SEBEP:** `Program.cs`'te `AddHangfireServer()` ve `RecurringJob.AddOrUpdate(...)`
cagrilari KOSULSUZDU. Yani **HER test host'u** bir Hangfire sunucusu calistirip
`outbox-processor` isini **DAKIKADA BIR** kosuyordu. Test kendi drenajini yapip
`retry_count == 1` beklerken arka plan isi araya giriyor ve 2 yapiyordu.

**YARIS ONCEDEN VARDI, YALNIZCA GORUNMUYORDU.** Dakikalik bir is ancak host YETERINCE UZUN
yasarsa atesler. Ayni test yerelde **3/3 GECTI** (izole kosumda host saniyeler yasiyor);
CI'da suit daha uzun surdugu icin atesledi - ustelik bu dalgada iki yeni test SINIFI eklendi.
Yani "degisiklik kirdi" degil, **"degisiklik ORTAYA CIKARDI"**; sonuc yine de CI kirmizisi.
**CLAUDE.md'de kayitli ISIMSIZ FLAKE'lerin en olasi aciklamasi da budur.**

**YAN BULGU:** Hangfire depolamasi `ConnectionStrings:DivisimaDb`e bagli - yani her test
host'u GELISTIRICININ veritabanina recurring job tanimi yaziyordu.

**DUZELTME:** `BackgroundJobs:Enabled` bayragi (varsayilan **TRUE** - uretim ve gelistirme
davranisi DEGISMEZ). `AddHangfireServer()` ve recurring kayitlarinin ikisi de bu bayraga
bagli; `TestHostConfig` false veriyor. Testler arka plan ZAMANLAMASINA dayanmiyor - outbox'i
olcen her test isleyiciyi KENDISI cagiriyor (`OutboxProcessor.ProcessPendingAsync`), yani
kapatmak hicbir testin OLCTUGU seyi kaldirmaz, yalnizca YARISI kaldirir.

**PINLER (`ArkaPlanIsleriIzolasyonTests`, 2):**
- `TEST_HOSTUNDA_HANGFIRE_ARKA_PLAN_SUNUCUSU_KOSMAZ` - **DAVRANIS**: DI'dan cozulen
  `IHostedService` listesinde Hangfire tipi BULUNMAMALI (+ vakum kirici: liste GERCEKTEN
  dolu olmali, yoksa iddia bedava dogru olurdu)
- `ARKA_PLAN_KAPALI_OLSA_DA_OUTBOX_ISLEYICISI_COZULEBILIR` - **CIFT-ANLAM KIRICI**: bayrak
  yalnizca ZAMANLAYICIYI kapatir, isleyicinin kendisini DEGIL

**5. KONTROL:** bayrak `true`ya cevrildi -> `TEST_HOSTUNDA_HANGFIRE_...` KIRMIZI
(`found {"Hangfire.BackgroundJobServerHostedService"}`), ikinci pin YESIL kaldi (lokalize).
Geri alindi.

## CI KIRMIZISI 10d794d - GEREKSIZ BIR VERITABANI 47. KATILIMCI OLDU ve BASKALARINI DUSURDU

Hangfire duzeltmesinin push'unda **CI YESIL, Security CI KIRMIZI** oldu. Adim bazinda okundu:
`secret-scan` / `codeql` / `dependency-scan` SUCCESS; **`tests` -> `Entegrasyon testleri`
FAILURE**. Annotation'larda **BES AYRI SINIF**, hepsi AYNI kokle:

```
System.InvalidOperationException : DIVISIMA_TEST_SQL verildi ancak <X> ortami hazirlanamadi
---- SqlException : Could not obtain exclusive lock on database 'model'. Retry the operation later.
```

Dusen bes sinif: `InvoiceLineVatTests`, `InactiveAccountTokenTests`, `ContentSeedAndSanitizeTests`,
`LaunchFixMailZinciriTests`, `NotificationSubscriptionTests`.

**KOK SEBEP (olculdu):** SQL Server `CREATE DATABASE` / `DROP DATABASE` islemlerini **`model`
veritabani uzerinden SERILESTIRIR**. Depoda her test SINIFI kendi veritabanini kuruyor
(CLAUDE.md bolum 4 - xUnit siniflari paralel kostugu icin DOGRU bir tasarim). Olcum:

```
kendi veritabanini kuran dosya : 46
AYRI veritabani adi            : 55
DDL cagrisi (Deleted+Created)  : 136     <- hepsi `model` uzerinde serilesir
```

`ArkaPlanIsleriIzolasyonTests` bu kalibi KOPYALAYARAK kendi veritabanini kuruyordu.
**AMA O VERITABANINI HIC KULLANMIYORDU** - sinifin iki pini de YALNIZCA DI kayitlarina bakiyor
(`IHostedService` listesi + `OutboxProcessor` cozumu), tek bir sorgu bile calistirmiyor.
Yani 47. katilimci **gereksizdi** ve bedeli **BASKA siniflarin dusmesi** oldu; kirilanlar
arasinda bu sinif YOKTU.

**MARJ BICAK SIRTIYDI (kayit):** bir onceki push `cd51a52` IKI yeni sinif ekledi ve Security CI
**tamamen yesildi** (46 katilimci). 47. eklenince bes sinif dustu. Yani yaris ONCEDEN vardi;
bu commit onu tetikledi.

**DUZELTME:** sinif artik **HIC veritabani olusturmuyor** (sifir DDL) ve
`[Trait("Category","Sql")]` KALDIRILDI - SQL gerektirmiyor. Host yine de IZOLE, KASITLI OLARAK
VAR OLMAYAN bir veritabani adina yonlendiriliyor; amac onu kullanmak degil, acilistaki
`ContentSeeder`in GELISTIRICININ veritabanina yazmasini ENGELLEMEK (ayni dalgada yazilan
"TEST, URUNUN GERCEK KAYNAKLARINA DOKUNMAZ" kurali). Var olmayan veritabani acilis
tohumlamasini dusurur; `Program.cs` bunu ACIKCA yakalayip loglar ve uygulama DEVAM EDER
("Tohumlama hatasi uygulamayi DURDURMAZ") - host saglikli kalkiyor, pinlerin olctugu DI
kayitlari eksiksiz. KANIT: sinif veritabanisiz **2/2 yesil, 231 ms**.

**DERS (ayni dalgada yazilan kuralin UCUNCU bicimi):** "test urunun gercek kaynaklarina
dokunmaz" kuralinin ikizi var - **test IHTIYACI OLMAYAN kaynagi da OLUSTURMAZ.** Kalip
kopyalamak bedavaymis gibi gorunur; burada bedeli PAYLASILAN bir kaynakta (SQL Server'in
`model` kilidi) BASKALARI odedi.

### KALICI COZUM - KULLANICI KARARI: (A) + RETRY

Yalniz (A) marj'i 46'ya geri dondururdu (o hal yesildi) ama **bicak sirtini KORURDU**;
D3/D6 daha sinif ekleyecek. Paralelligi dusurmek (tek dosyalik `xunit.runner.json`) kok
sebebi GIZLEDIGI icin REDDEDILDI. Uygulanan: `Divisima.IntegrationTests/TestDbKurulum.cs` -
veritabani kurulumunun TEK NOKTASI ve **1807'ye ozel yeniden deneme**.

**DORT SART, DORDU DE UYGULANDI:**

1. **YALNIZ 1807.** Yuklem `HataKoduIceriyorMu(ex, 1807)` - ic-istisna zincirini yurur ve
   `SqlException`larin HATA KOLEKSIYONUNU tarar (tek istisna birden cok `SqlError` tasiyabilir;
   `ex.Number` yalnizca ilkini verir). Baska HICBIR kod yutulmaz - farkli numarali bir
   `SqlException` bile ANINDA firlar.
2. **SINIRLI DENEME.** `MaxDeneme = 6`, artan bekleme + **serpinti** (serpinti olmadan ayni
   anda dusen istekler KILITLI ADIMDA yeniden denerdi ve cakismayi surdururdu). Hak bitince
   hata GURULTULU firlar - sessiz sonsuz dongu YOK.
3. **OLCUM KANALI AYRI.** "Yesil cunku hic 1807 gelmedi" ile "yesil cunku retry calisti"
   ayrimi PIN ile yanitlanamaz (kosuma baglidir), bu yuzden AYRI bir kanal var:
   `YenidenDenemeSayisi` / `BasariliIslemSayisi` sayaclari + her denemede `Console.Error`'a
   ve `%TEMP%\divisima-testdb-retry.log`'a basilan satir.
4. **OLCUM (asagi).**

**MEKANIK DEGISIKLIK GUVENLIYDI - ONCE OLCULDU:** 136 cagrinin tamami YALNIZCA BES bicimde
yaziliydi (`ctx|pre|db` + `.Database.Ensure{Deleted,Created}Async()`), hepsi tek satirlik.
Yardimci AYNI namespace'te oldugu icin **tek bir `using` satiri eklenmedi** - CLAUDE.md'de
iki kez bedeli odenen "sed ile dosya basina using ekleme" tuzagi bu yuzden HIC dogmadi.

**OLCUM (sart 4) - duzeltme SONRASI:**

```
veritabani kuran sinif dosyasi : 46      (once 47 - ArkaPlanIsleriIzolasyonTests cikti)
AYRI veritabani adi            : 55
DDL cagrisi                    : 136     (hepsi TestDbKurulum uzerinden)
dogrudan Ensure* cagrisi       : 0       (atlayan sinif yok - pinli)
ArkaPlanIsleriIzolasyonTests   : 0 DDL
tam suit                       : 496 basarili / 499, 1 dk 37 sn   (once 1 dk 15 sn / 494)
Category=Sql                   : 312/312
GERCEK 1807 (yerel kosumlar)   : 0  -> retry DEVREDE ama YERELDE GEREKMEDI
```

**Son satir bilincli olarak boyle yazildi:** yerel makinede SQL Server sicak ve tek kosucu
var; 1807 hic gelmedi. Yani yerelde "retry calisti" DENEMEZ - yalnizca "devrede ve gerekmedi"
denir. Retry'in DAVRANISI pinlerle, DEVREDE OLDUGU kaynak taramasiyla kanitlandi; GERCEKTEN
ATESLENDIGI ancak 1807 ureten bir ortamda (CI'nin soguk SQL konteyneri) gorulur ve o zaman
kosum ciktisinda `[TestDbKurulum] 1807 (...) - yeniden deneniyor` satiri belirir.

**PINLER (`TestDbKurulumTests`, 5):**
- `YENIDEN_DENEME_YALNIZ_1807_ICIN_BASKA_HATA_KODU_YUTULMAZ` - GERCEK bir `SqlException`
  uretilir (sifira bolme) ve yuklem ona HAYIR demeli. Vakum kirici: tarayici kendi hata
  numarasini BULMALI (yoksa "her zaman false don" da gecerdi). Cift-anlam kirici: ayni
  gercek istisna 1807 SAYILMAMALI.
- `YENIDEN_DENENEBILIR_HATA_TEKRAR_DENENIR_ve_SONUNDA_BASARIR` - vakum kirici: islem
  GERCEKTEN uc kez cagrilmis olmali.
- `YENIDEN_DENENEMEYEN_HATA_ANINDA_FIRLAR_YUTULMAZ` - TEK deneme.
- `SINIRLI_DENEME_SONRASI_GURULTULU_DUSER_SESSIZ_SONSUZ_DONGU_YOK` - tam `maxDeneme` kadar,
  sonra firlatir; ayrica uretim `MaxDeneme` degeri makul aralikta.
- `HICBIR_TEST_SINIFI_KURULUM_YARDIMCISINI_ATLAMAZ` - KAPSAM pini; iki vakum kirici
  (tarama >40 dosya okumali, yardimci >100 kez cagrilmis olmali).

**KIRILAN PIN YOK.**

**DIS KONTROLU:** 5 assert ters -> **5 AYRI ISIMLI KIRMIZI**, geri alindi.
**5. KONTROL - UC URETIM MUTASYONU** (yeni kural geregi her birinde (a) dosyaya indi mi,
(b) temiz build, (c) kirmizi olmadiysa "uygulanmadi" suphesi ONCE elenir):

| Mutasyon | Kirilan pin | Uretilen once-durum |
|---|---|---|
| M1 bir sinif dogrudan `EnsureCreatedAsync`a donduruldu | `HICBIR_TEST_SINIFI_..._ATLAMAZ` | duzeltme oncesi "atlayan sinif" hali |
| M2 `ModelKilidiMi` -> her zaman `true` | `YENIDEN_DENEME_YALNIZ_1807_...` | sart 1 ihlali: gercek hatalar YUTULUR |
| M3 `MaxDeneme` -> 1000 | `SINIRLI_DENEME_..._SONSUZ_DONGU_YOK` | sinirsiz yeniden deneme riski |

Ucunde de TAM 1 pin kirmizi / 4 yesil (lokalize). Hepsi geri alindi, `[MUTASYON]` izi
depoda **0 dosya**.

### PUSH RAPORU `84b0275` - HER IKI WORKFLOW TAMAMEN YESIL

Push `10d794d..84b0275`. Adim bazinda + annotation duzeyinde dogrulandi.
`CI - Build & Test` (`build-and-test` + `format-check`) ve `Security CI`
(`tests` + `codeql` + `secret-scan` + `dependency-scan`) - **alti job da SUCCESS**,
hicbirinde **failure seviyeli annotation YOK**. Bes sinifi dusuren
"Could not obtain exclusive lock on database 'model'" satiri KAYBOLDU.

**RETRY CI'DA ATESLENDI MI - BUGUN ANONIM OLARAK OKUNAMIYOR (olculdu, varsayilmadi).**
Yeniden deneme satiri (`[TestDbKurulum] 1807 (...) - yeniden deneniyor`) kosum ciktisina
basiliyor, ama:

```
GET /actions/jobs/{id}/logs   -> HTTP 403 (anonim)
GET /actions/runs/{id}/logs   -> HTTP 403 (anonim)
TESHIS adimi                  -> yalniz `if: failure()` kosuyor; YESIL run'da hic calismaz
```

Yani yesil bir kosumda bu satir hicbir anonim kanaldan gorulemiyor. **Bu kosum icin durust
ifade: "retry DEVREDE; ATESLEYIP ATESLEMEDIGI OLCULEMEDI"** - "1807 hic gelmedi" DE denemez,
cunku o da okunamiyor. Kanitlanan tek sey belirtinin (bes sinifin dusmesi) KAYBOLDUGU.

**ACIK - KARAR BEKLIYOR:** kanali gorunur kilmak tek adimlik bir workflow degisikligidir -
`test-output.txt` icinde bu satir aranip `::warning::` olarak basilirsa anonim okunabilir
(annotation'lar anonim okunuyor; `warning` seviyeli olanlari bu depoda zaten okuyoruz).
Kapsam disi oldugu icin YAPILMADI.

## ACIK KALAN (D5)

- **Canli Redis turu OLCULMEDI** - dagitik kilit, blacklist, idempotency'nin Redis yolu ve
  rate limit'in dagitik sayaci staging'de sürülmeli. Bu makinede Docker/Redis YOK.
- Fail-fast davranisi belgelendi ama **Redis kesintisi = deploy blokaji** sonucu bir URUN
  karari olarak yeniden degerlendirilebilir (acil durumda `Redis:Enabled=false` + TEK INSTANCE
  kacis yolu runbook'a yazildi).

