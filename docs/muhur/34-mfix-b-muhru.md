# MFIX-B MUHRU - BACKEND DURUSTLUK PAKETI (28 Agustos 2026)

**KOD SHA: `403251d`** (zemin `dfa6567`) - her iki workflow yesil.
Bu muhur AYRI ve docs-only bir commit'tir; **kendi SHA'sini ve kendi run kimliklerini
ICEREMEZ** (tavuk-yumurta) - muhrun kendi cift yesili MFIX-B raporunda verilir.
MFIX-1'de kurulan kalip.

```
MFIX-B KODU (403251d)
  CI - Build & Test  run 33117344289  event=push  head_sha=403251d  SUCCESS
  Security CI        run 33117344313  event=push  head_sha=403251d  SUCCESS
ALTI JOB'DA failure SEVIYELI ANNOTATION: 0   (39 annotation, HEPSI warning)
ANNOTATION KUME FARKI (taban 39, 65cd3c1): IKI YONDE DE BOS - yeni uyari URETILMEDI
Gitleaks (secret taramasi): SUCCESS - bolum 7 kurali geregi ADIM SONUCUNDAN okundu
format-check UC ZORUNLU ADIM: whitespace + style + "Model ile migration'lar SENKRON mu"
  -> UCU DE SUCCESS. "MIGRATION GEREKMEZ" IDDIASININ CI KANITI BUDUR.
TestDbKurulum 1807 ozeti (iki test job'inda da): "HIC ATESLEMEDI (0) - retry devrede,
  gerekmedi."
```

**ORTAM UYARISI (Divisima Eki 2.3):** olcumler `goz1` duzeneginde kosuldu;
`api-baslat.cmd` BES arguman veriyor - `--Iyzico:UseRealSdk=false`,
`--AdminSeed:Enabled=false`, `--BackgroundJobs:Enabled=false`,
`--RateLimit:AuthPermitLimit=100`, `--MailSettings:Host=`.
**Bunlar URUN VARSAYILANI DEGILDIR.**

**AJAN KISITI BU TURDA YOKTU.** Onceki dort dalgada (MFIX-1/2/3 ve GOZ-FIX) L1-L3
denetcileri DAGITILAMAMISTI; MFIX-B'de SDP'nin ongordugu denetciler GERCEKTEN dagitildi:
**10 ajan** (6 on olcum + 4 denetim), 354 arac cagrisi, ~4,29M token, 0 hata, 0 bos sonuc.

## KAPANAN UC KALEM - ONCE/SONRA

**K1 - ANONIM DETAY STOGU ARTIK SATILABILIR (F-M1-H2).**
Kok sebep AYNI SINIFTA IKI STOK TANIMIYDI: liste yolu (`ListeyiZenginlestirAsync`) Sprint 8
madde 5'ten beri `available` uzerinden dolduruyordu, anonim detay projeksiyonu
(`ProductManager.cs:370`) FIZIKSEL `stock_quantity` donuyordu ve istemci TELAFI EDEMIYORDU
(`ProductStockDto`'da `reserved` alani YOK).

```
urun 937 (DB fiziksel 12/10/11 - rezerve 1/6/0)
  ONCE  detay S=12 M=10 L=11 (toplam 33)   liste 26   -> 7 FARK
  SONRA detay S=11 M= 4 L=11 (toplam 26) = liste = DB -> 0 FARK
  ON URUNDE liste<->detay<->DB: 0/10 fark
  Tarayici: _ss {S:11,M:4,L:11} ; addToCart(937,"M",5) sepette 4'E KIRPILDI
```

**`ProductStockDto` DEGISMEDI** (E4a karari korundu): `reserved_quantity` anonim uclara
ACILMADI, yalnizca donen sayinin ANLAMI duzeldi. Sozlesme kodda yorumla sabitlendi -
**anonim uclarda `stock_quantity` = SATILABILIR; FIZIKSEL deger yalniz admin
`GET /api/Stock/{productId}` (`ProductStockDetailDto`)**.

**TEK KAYNAK: `Divisima.Core/Utilities/Stock/StokHesabi.Satilabilir(stok, rezerve)`**
(`PricingHelper` idiyomu). `Divisima.Core`'un **hic ProjectReference'i olmadigi** icin
imza PRIMITIVE; ProductManager'in **iki yolu da** ona baglandi.
**DURUST SINIR (kodda yazili):** StockManager/SearchManager'in 7 bellek-ici formul sitesi ve
`EfProductStockDal`'in 2 EF expression-tree'si (ortak C# metoduna CEKILEMEZ) BILINCLI olarak
kapsam disi; `product/filter{in_stock}` yuklemi hala FIZIKSEL, `search?in_stock_only`
SATILABILIR - kume farki `{1, 955}`.

**YAN KAZANC:** MFIX-2'nin acikca biraktigi *"beden BASINA ust sinir HALA FIZIKSEL"* siniri
**KAPANDI**. Ayrica K1, `api-bridge`'teki `detaydanUrun`un da fiziksel topladigi **ikinci bir
H3-sinifi kusuru SESSIZCE kapatti** (celiski avcisi buldu).

**K2 - GECERSIZ/UYGUNSUZ KUPON ARTIK 400.**
`OrderManager.cs:237` else dali kuponu SESSIZCE yok sayiyordu: uc 201 donuyor, indirim
uygulanmiyor, `coupon_code` NULL yaziliyor ve musteri sebebi HIC ogrenmiyordu.

```
ONCE  gecersiz kupon -> 201 {"data":224}, indirim 0, coupon_code NULL
SONRA gecersiz kod / suresi dolmus / minimum tutar / yalniz-ilk-siparis / kisi-basi limit /
      toplam kullanim limiti -> HEPSI 400 + KENDI MESAJI
      gecerli kupon (vakum kirici) -> 201 + indirim 20.00 GERCEKTEN uygulandi
ASIMETRI KAPANDI: CouponManager.ValidateCoupon per_user_limit'i HIC kontrol etmiyordu -
      vitrin "gecerli" derken checkout reddediyordu. Ayni eksen (PaidOrderSpec.PaidStatuses
      + PendingGraceMinutes) BIREBIR kopyalandi; UCUNCU BIR EKSEN YOK.
```

YENI sabit: `Messages.CouponPerUserLimitReached`.
**SIRA KANITI (L1 dogruladi):** ret :266 << `BeginTransaction` :325 << `ReserveStock` :345 -
reddedilen istek stok/siparis/odeme satiri BIRAKMAZ (musteri 82'de bes deneme, ucu
reddedildi -> DB'de TAM 2 siparis).
**TOCTOU'da checkout'un 400 ile kirilmasi BILINCLI KABUL.** Istemciye dokunulmadi: vitrin
mevcut hata gostergesiyle sebebi zaten gorunur kiliyor (canli dogrulandi).

**K3 - `place` yaniti `{id, order_number}` (F-M8'in kok sebebi).**
Uc donus noktasi (`OrderManager` :118 / :440 / :449) ciplak `int` donuyordu; istemci siparis
numarasini ya IKINCI bir istekle kurtariyor ya da misafirde HIC ogrenemiyordu.

```
ONCE  {"data":224}
SONRA {"data":{"id":228,"order_number":"DVS20260827-1A3290E915"}}
      misafir sonuc ekrani: "Referans: 224" -> GERCEK DVS NUMARASI (DB birebir)
      replay: 200 AYNI NESNE -> replay mesaji artik gercek numara tasiyor
      checkout'ta api.orders.get: 2 -> 0 (kalan uc cagri MESRU)
      payment.initialize HALA id ile calisiyor
```

Yeni DAR DTO `Divisima.Entity/Dtos/Order/OrderPlaceResponseDto` (IDto, snake_case, yalniz
`id` + `order_number`; AutoMapper kaydi gerekmez). Swagger iki controller'da guncellendi.
Istemci **AYNI COMMIT'TE** hizalandi.

**K4 - STATUKO (kod yok, merkez karari).** `SmtpMailService`'in Host-bos sessiz donusu
BILINCLI ve dosyasinda BELGELENMIS bir sozlesmedir; ucuncu maddesi DOGRULANDI: `Program.cs`
non-Development'ta uygulamayi **ACTIRMIYOR**, yani uretimde bu dal **ERISILEMEZ**. Oneri
hazir (`return` -> `throw`), bedeli yalniz Development'i etkiler ve sinifin "ortami bilmesin"
gerekcesini degistirir. `SmtpMailService:42/81`'deki ham e-posta loglari **LOG-FIX**'te.

## ZORUNLU KAPSAM EKI - `frontend/admin.html` (SDP'NIN DEGER KANITI)

On olcum fan-out'una eklenen **KAPSAM ELESTIRMENI** rolu, benim ve BES OKUYUCUNUN
kacirdigi bir **[VERI-BOZAN]** yol buldu; bagimsiz olarak dogrulandim ve **kural-uyum
denetcisi de bagimsiz teyit etti**:

```
admin.html:306  duzenleme formu stok satirlarini ANONIM detay ucundan dolduruyor
admin.html:376  ayni degerleri geri POST ediyor
ProductManager.cs:292  onu FIZIKSEL kolona yaziyor
=> K1 TEK BASINA gonderilseydi: admin 937'yi acip YALNIZ ADINI degistirip kaydettiginde
   fiziksel 10 -> 4 duser, rezerve 6 kalir, available -2 -> 0 olurdu.
   Dalga B'nin "tam-varlik map -> sessiz veri kaybi" sinifinin BIREBIR tekrari.
```

Duzeltme: form artik **ADMIN ucundan** okuyor (`api.stock.byProduct` -> `/api/Stock/{id}`,
zaten mevcut ve admin korumali) ve **FAIL-CLOSED** - stok okunamazsa form **HIC ACILMAZ**.

**KALICI KURAL (bu vakadan dogdu): KAPSAM ELESTIRMENI ROLU, ON OLCUM FAN-OUT'UNUN
ZORUNLU UYESIDIR.** Gorevi bulgu aramak degil, **verilen tarifin kendisinin acacagi kapiyi**
aramaktir. Bu turda merkezin K1 tarifi, bes bagimsiz okuyucu ve ana akis - **dordu birden**
kacirdi; tek eleştirmen rolu yakaladi.

## PINLER - BACKEND'IN ILK DAVRANIS PINLERI

Mevcut Sql siniflarina eklendi; **yeni veritabani ACILMADI** (`10d794d` dersi).

- **P12** `KuponGecersizse_Place_400_ve_Validate_PerUserLimit_Reddeder` (`CouponRaceTests`)
- **P13** `Place_Yaniti_Id_ve_OrderNumber_Tasir` (`MisafirCheckoutTests`)
- **P14** `AnonimDetay_Stogu_SATILABILIR_Doner_FizikselDegil` (`StorefrontCatalogContractTests`)

DIS KONTROLU: her pinde **TAM 1 ISIMLI KIRMIZI**.
5. KONTROL: M-P14 (2 kirmizi, ikisi de ayni K1 sozlesmesi) · M-P13 (2 kirmizi) ·
M-P12 (asagi) - hepsi lokalize, hepsi geri alindi, iz 0.

**M-P12 DERSI - "KIRMIZI YOK" VAKASINDA PIN ZAAFI ile EKSIK MUTASYON AYRIMI.**
M-P12'nin ilk turunda P12 **YESIL KALDI**. Kural geregi once "mutasyon uygulanmadi" ihtimali
elendi ((a) iz dosyada, (b) build 0 hata) ve gercek sebep bulundu: **K2'nin IKI ret cikisi
var** (`coupon == null` erken donusu + `kuponRet != null` toplu donusu) ve mutasyon yalnizca
birini kaldirmisti. M-P12b ikinciyi de notrlestirdi -> TAM 1 kirmizi.
**Her iki cikis da AYRI bir pinle korunuyor.** Bu, MFIX-1'de yazilan sirali refleksin
("kirmizi yok -> ONCE pin suphesi") **UCUNCU** bicimidir: bu kez sonuc pin zaafi DEGIL
EKSIK MUTASYON cikti - yani refleks her iki yone de calisiyor.

**BILINCLI PREMIS DEGISIKLIKLERI (merkez onayli, gerekceli):**
- `CouponRaceTests` sinif premisi: *"yarisi kazanan DISINDAKI istekler sessizce kuponsuz
  gecer"* -> *"REDDEDILIR"*. Assert'ler de bu yonde guncellendi.
- `StorefrontCatalogContractTests` tohumu **7/0 -> 10/3**: eski tohumda `reserved = 0`
  oldugu icin K1 sozlesmesi OLCULEMEZDI - **VAKUM**.
- `LaunchFixMailZinciriTests:365` K3 sozlesmesine uyduruldu (`data.id`).

## CELISKI AVCISININ DORT DUZELTMESI (dordu de kabul edildi)

1. **YORUMUM YALAN SOYLUYORDU:** *"outbox mesaji BIRAKMAZ"* `PlaceOrder` icinde dogru,
   **UC DUZEYINDE YANLIS** - misafir yolunda reddedilen istek **musteri + adres + DOGRULAMA
   MAILI** birakiyor (olculdu: customers +1, addresses +1, outbox +1). Yorum kapsam acik
   yazacak sekilde DUZELTILDI.
2. **"IKI YOL BIR DAHA AYRISAMAZ" COK GENISTI** - `filter{in_stock}` FIZIKSEL,
   `search?in_stock_only` SATILABILIR yuklem; kume farki `{1, 955}` rezervenin VARLIGINI
   siziyor. Yorum DARALTILDI. (Avcinin kendi tespiti: **K1 durumu IYILESTIRDI** - once rezerve
   `detay - liste` ile TAM SAYI olarak cikarilabiliyordu.)
3. **IKI BAYAT YORUM**, biri MFIX-B'nin kendi commit'inde; ayrica K1'in sessizce kapattigi
   ikinci H3-sinifi kusur (`detaydanUrun`) kayda gecti.
4. **REGRESYON RISKI (kod degismedi, kayit):** sepet kirpma esigi artik satilabilir - adet
   BASKALARININ rezervasyonuyla sessizce dusebilir. Yon DOGRU (checkout ile tutarli), veri
   kaybi YOK (`_avq >= 1`).

**L3 NOTU:** `validate` ucu **ANONIM ORACLE URETMIYOR** - `CouponController:89` kimligi
token'dan eziyor, yani per_user_limit kontrolu kimligi disaridan sorgulanabilir hale
getirmiyor.

**CELISKI-1 (denetciler arasi, DURUST KAYIT):** kural-uyum ozeti *"izolasyon temiz"* derken
kendi kor noktasi *"olculemedi"* diyor. **IZOLASYON MADDESI OLCULMEMISTIR** - bu turda
cift-kor izolasyonu YALNIZ PROMPT duzeyindeydi; SDP 1.9'un istedigi TEKNIK izolasyon (ayri
calisma dizini) UYGULANAMADI cunku is COMMIT EDILMEMISTI ve bir worktree degisiklikleri
GOREMEZDI. Bu, asagidaki MK-4'u dogurdu.

## MK-3 GUCLENDIRILDI (KALICI)

**Her muhur/ozet degeri URETEN IFADESIYLE kaydedilir.** Pending birimi bu turdan itibaren
**DORTLU**:

```
BIRINCIL : status=0 AND id<=210  ->  COUNT=35 · MIN=9 · MAX=210 · SUM=3837
IKINCIL  : CHECKSUM_AGG(id)=239
```

**XOR-CEBIRSELLIK NOTU (canli ornek):** `CHECKSUM_AGG` XOR temellidir ve TEK BASINA KOR
KALABILIR - bu veritabaninda `id>210` olan **ALTI** yeni Pending satirin toplami **TAM 0**
cikti, yani kume degisirken muhur "degismedi" diyebilirdi. Tarihsel `561429369` degeri
**ON IKI ifadeyle YENIDEN URETILEMEDI**; dolayisiyla o deger artik birincil olcut degildir.

## MK-4 (YENI KALICI MIKRO-KURAL)

**Denetim dagitimindan ONCE is LOKAL COMMIT'e alinir; L3 ve kural-uyum denetcileri AYRI bir
`git worktree`'de o commit uzerinde kosar.** Boylece cift-kor TEKNIK izolasyon (SDP 1.9)
lokal islerde de saglanir.

Gerekce OLCULDU: MFIX-B'de is commit EDILMEMIS oldugu icin bir worktree calisma agacindaki
degisiklikleri GOREMEZDI; izolasyon zorunlu olarak yalniz prompt duzeyinde kaldi ve
denetciler bunu CELISKI-1 olarak isaretledi. Commit'i denetimden ONCE atmak bu kisiti
tumden kaldirir ve commit'in **amend edilmemesi** disinda hicbir bedeli yoktur.

## KURGU KAYIT ENVANTERI (MFIX-B)

**ANA AKIS:** musteri 81/82/83 · siparis 224-228 · kupon `MFXEXP*` / `MFXOK*` / `MFXPUL*`.
**DENETCILER:** musteri 84-88 · siparis 229-233 · adres 52'ye kadar · kupon `L3*` serisi.
**3 YETIM MISAFIR MUSTERI** (siparissiz, dogrulanmamis) - bunlar celiski avcisinin ve L3'un
*"reddedilen misafir istegi KAYIT BIRAKIR"* bulgusunun **KANITIDIR**; bilinen-sinir notuyla
D-YAN temizlik listesine onerilir.

**MUHURLER:** OMER (musteri 10) **38 / 211 SABIT** · Pending yukaridaki DORTLU birimle.

## KUYRUK

```
1. MFIX-3b     (a) wishlist.toggle sozlesmesi · (b) variantsOf ONCE OLCUM sonra karar ·
               (c) toast ikon tipi · (d) kampanya geri sayimi olcumu ·
               (e) i18n kalan 161 aday · (f) enrichProduct olu dali
2. FIX-1B      F4 + F8 zinciri, kara liste desenlesmesi, sentetik yazma pinleri
3. ADMIN-FIX
4. IMPORT-FIX  [KRITIK YOL - katalogda gercek urun 0; katalog gelirse ONE CEKILIR]
5. FIX-1C      F5 · F6 · F7 · F9 · F13 · D-YAN dev-veri temizligi
6. LOG-FIX     bes ham log satiri -> KanitMaskesi (SmtpMailService:42/81 DAHIL)
7. FIX-2       B-6 · C-1 · G5 · B-5 · D-3
8. FIX-3 / B13 kupon geri bildirimi · terk edilmis Pending TTL
```

**OMER'IN BIRLESIK DOGRULAMA TURU (12 madde) MUHUR YESILI SONRASI - KABUL KAPISI.**
Istege bagli 13. madde: **urun detayindaki beden adetleri = liste = satilabilir**.
Liste OMER'DE; CC kendi isini onaylayamaz.

**D-YAN TEMIZLIK LISTESINE EK:** MFIX-B'nin kurgu kayitlari - musteri 81-88, siparis
224-233, adresler 52'ye kadar, `MFX*`/`L3*` kuponlar ve 3 yetim misafir musteri. MFIX-3'un
79/80/46/223'u, MFIX-2'nin 78/45/221/222'si, MFIX-1'in 218-220'si ve Dalga B'nin 213-217'siyle
birlikte TEK temizlik isinde ele alinir.

---

