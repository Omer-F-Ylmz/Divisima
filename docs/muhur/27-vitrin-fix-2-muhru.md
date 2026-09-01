# VITRIN-FIX-2 MUHRU (26 Agustos 2026)

**KANIT SHA: `65cd3c1`** - her iki workflow yesil.

```
CI - Build & Test  run 33012526834  event=push  head_sha=65cd3c1  SUCCESS
Security CI        run 33012526878  event=push  head_sha=65cd3c1  SUCCESS
ALTI JOB'DA failure SEVIYELI ANNOTATION: 0   (39 annotation, HEPSI warning)
TestDbKurulum 1807 yeniden deneme ozeti: iki test job'inda da SUCCESS
Gitleaks (secret taramasi): SUCCESS - bolum 7 kurali geregi ADIM SONUCUNDAN okundu
```

**ANNOTATION KARSILASTIRMASI - TABAN 39, KUME FARKI DOSYA:SATIR DUZEYINDE KAPATILDI.**
Toplam 39 == 39 ama path dagilimi KAYDI (`IEntityRepository.cs` 18 -> 24,
`EfEntityRepositoryBase.cs` 12 -> 6; nullable ailesi toplami 30'da SABIT). Kural geregi
`dosya:satir` duzeyine inildi:

```
YENI'de olup TABAN'da OLMAYAN satir : YOK (BOS)   <- yeni uyari URETILMEDI
TABAN'da olup YENI'de olmayan       : 6 satir, hepsi EfEntityRepositoryBase.cs
                                       (45, 50, 60, 61, 88, 96)
git diff --name-only 546d799..65cd3c1 -> FrontendDokunmaHedefiTests.cs · api-bridge.js
                                          · index.html
nullable ailesinin IKI dosyasi bu diff'te: 0
```

Yani ANNOTATION YUZEYE-CIKARMA ARTEFAKTI, yeni uyari DEGIL. Bu, `a244160` turunda
belgelenen desenle AYNI aile ve AYNI dosya cifti.

## VITRIN-FIX-2 KAPANDI

### F-D1 - SAHTE YORUM URETIMI SILINDI (LAUNCH BLOKERI)

`index.html`'deki `reviewsOf`, urun id'sinden tohumlanan bir PRNG ile UYDURMA yorum
uretiyordu: `count=8+floor(r()*150)`, uydurma yildiz dagilimi, `RV_NAMES` (20 uydurma
Turkce isim), `RV_TR`/`RV_EN` (uydurma metin havuzu), `RV_AGO_TR/EN` (uydurma tarih),
uydurma "faydali" oylari, **kosulsuz "Dogrulanmis Alici" rozeti** ve urunun **KENDI katalog
gorsellerinin "musteri fotografi"** olarak gosterildigi bir serit. Ustelik
`setProductSchema` bu uydurma ortalamayi **JSON-LD `aggregateRating`** olarak arama
motorlarina BEYAN EDIYORDU.

**OLCULDU (tarayici, 24 urunluk sayfa):** once 24 urun icin **1630 uydurma yorum iddiasi**
ve urun 955'te `aggregateRating {"ratingValue":"4.5","reviewCount":8}` - veritabaninda
`product_reviews` **0 SATIR**. Sonra: **0** ve `aggregateRating` **YOK**.

Yildiz/sayi artik YALNIZ sunucudan gelir (`average_rating` + `review_count`; hem
`ProductListResponseDto` hem `ProductDetailResponseDto` tasiyor, `mapProduct` ve
`enrichProduct` esler). Yorum METINLERI gercek anonim uctan TEMBEL cekilir
(`GET /api/productreview/product/{id}` -> `yorumlariCiz`, urun basina onbellekli).
`review_count > 0` degilse kart / cross-sell yildiz blogu **HIC cizilmez**, karsilastirma
tablosu tire gosterir, detayda GORUNUR ve DURUST bos durum yazilir ("Bu urun icin henuz
yorum yok." - tr/en/ar).

**DURUST SINIR:** `ProductReviewResponseDto` yorum sahibinin **ADINI** ve
**`is_verified_purchase`** alanini TASIMIYOR (entity'de VAR, `ProductReviewManager.cs:73`
dolduruyor, DTO'da yok). Bu yuzden isim, avatar, "Dogrulanmis Alici" rozeti, alinan beden,
fotograf ve "faydali" oyu **CIZILMEZ - hicbiri uydurulmadi**. Yorum YAZMA formu bu dalgada
ACILMADI (kapsam sabit, yalniz okuma). Kapsam disi PRNG yuzeyleri (fit/renk/kumas)
DOKUNULMADI - `rngOf` duruyor.

**`setProductSchema` KAYIT CEVABI:** `aggregateRating` **tamamen kaldirilmadi** -
`if(rv.count>0)` kosulu KORUNDU ve `rv` artik `reviewsOf(p)`'den, yani GERCEK
`average_rating`/`review_count`'tan geliyor; bugun DB'de 0 yorum oldugu icin hic yazilmiyor,
gercek yorum girildigi an gercek degerlerle uretilecek (yani gercek-veri yolu ZATEN bagli;
FAZ 2 DTO kalemi yalniz isim/rozet icin gerekli).

### F-A1 - ILK SENKRON SILMEZ, BIRLESTIRIR

Eski akista her senkron "yereldeki her kalem SET, sunucuda olup yerelde OLMAYAN kalem SIL"
idi; bos bir tarayicida giris yapmak sunucudaki KALICI sepeti temizliyordu.

**KONTROLLU A/B (gercek kurgu hesabi, gercek uclar, gercek DB sayimlari):**
```
ONCE  (eski kod)  yerel 0 -> 0   SUNUCU 2 aktif -> 0 aktif   << KALICI SEPET SILINDI
SONRA (yeni kod)  yerel 0 -> 2   SUNUCU 2 aktif -> 2 aktif   << BIRLESTIRILDI
      yerel kalemler 947|M x2 · 950|TEK x1 · rozet #badge 3 · dvs_cart kalici yazildi
AYNA BIRLESTIRMEDEN SONRA HALA SILIYOR: yerelden 950|TEK silindi -> sunucuda yalniz
      947/M aktif kaldi
```

Bayrak **"giris olayi"na degil ILK SENKRON'a** bagli. Gerekce olculdu: ikinci bir giris yolu
var - `api-bridge.js:733` `if (api.isLoggedIn()) window.loggedIn = true;` ile sayfa gecerli
jetonla acildiginda **hicbir login olayi ATESLENMEZ** ama ilk `renderCart` yine senkronu
tetikler; eski kodda kalici sepeti silen ikinci yol tam da buydu. Giris **ve** cikis bayragi
yeniden silahlandirir (baska bir kullanici girerse onun kalici sepeti de birlestirilmeli).

**KORUNANLAR:** sunucudaki bir kalemin urunu o an KATALOGDA yoksa yerele indirilemez -
`renderCart` (index.html) `byId(it.id)` bulamadigi kalemi **SESSIZCE siler**. Boyle bir
kalemi ayna dongusunun silmesi de veri kaybi olurdu; anahtari korumaya alinir ve SIL dongusu
onu ATLAR. Bu olmadan "asla silmez" ikinci gecisde YALAN olurdu.

`api.cart.get` tur basina **TEK** istek (eski satir `.items` bos dustugunde ayni ucu IKINCI
KEZ cagiriyordu).

### PINLER / DIS / MUTASYON

`FrontendDokunmaHedefiTests` (sifir-DDL sinif, 9 -> 11 `[Fact]`; yeni veritabani acan sinif
YOK - 10d794d dersi):
- **P3** `KAYNAK_SOZLESMESI_Yorumlar_PRNG_ile_URETILMEZ_ve_Yildiz_GERCEK_ALANDAN_Turer`
- **P4** `KAYNAK_SOZLESMESI_IlkSenkron_SILMEZ_Birlestirir_Ayna_SONRA_Baslar`

**Ikisi de DURUST ETIKETLI KAYNAK SOZLESMESI pinidir**, davranis pini DEGILDIR (depoda
JS/DOM kosucusu yok); davranis kaniti yukaridaki tarayici ve DB olcumleridir.
Vakum kiricilar: `rngOf` hala >1 kullanilmali · govdeler bos okunmus olamaz ·
**AYNA SILMESI HALA VAR OLMALI** (silme tumden kalksaydi "ilk senkron silmez" BEDAVA dogru
olurdu). Cift-anlam kiricilar: eski kosulsuz `card-rate` bicimi geri gelemez · bayrak hem
giriste hem cikista silahlanmali (>=3 gecis) · `api.cart.get` tur basina TAM 1.

```
DIS KONTROLU (tam kapsama)  P3 ters -> TAM 1 isimli kirmizi, 10 yesil
                            P4 ters -> TAM 1 isimli kirmizi, 10 yesil
5. KONTROL  M-P3 reviewsOf'a PRNG geri  -> P3 TAM 1 kirmizi / 10 yesil (LOKALIZE)
            M-P4 birlestirme kosulu off -> P4 TAM 1 kirmizi / 10 yesil (LOKALIZE)
            ikisi de geri alindi; kod/test dosyalarinda iz 0
SUIT  333/333 Category=Sql · 556 basarili / 559 (kirilan 3'un UCU DE Docker'li
      OrderEndpointTests) · taban 554 -> 556 (+2 pin) BIREBIR tuttu
      Release 0 hata · whitespace exit 0 · style exit 0
```

## CC'NIN YEDI HATASI (bu dalgada)

1. **`--no-build` ILE BAYAT IKILI - KAYITLI KURALIN TEKRARI, AYRICA ISARETLENIR.**
   Dis kontrolunden sonra kaynak geri alindi (`DIS-FLIP` izi 0 dogrulandi) ama **YENIDEN
   DERLENMEDI**; M-P3 turu onceki ikiliyle kostu ve P4'u SAHTE kirmizi gosterdi -
   "mutasyon lokalize degil" diye YANLIS rapor yazilacakti. Bu, SUREC bolumunde
   **"5. KONTROLUN KENDISI DOGRULANIR"** maddesinin **(b) TEMIZ BUILD** adiminin birebir
   ihlalidir ve CLAUDE.md'de zaten yazili olan "bayat ikili" tuzaginin tekrari. Hata
   mesajindaki `DIS-FLIP-P4` gerekcesi yakalatti; derlenip tekrarlandi.
2. Izleyici cikis kosulu UC girdide de bos dondu (bilinen-pozitif DAHIL) - desen
   `"total_count":[0-9]*`, yanit PRETTY-PRINT. Kural olmasa sonsuz dongu.
3. Job id suzgeci `[0-9]{12,}` yazildi; job id'leri 11 hane -> annotation taramasi
   bilinen-pozitifte 0 dondu.
4. Izleyicinin run-id suzgeci DEPO ID'sini de yakaladi (`[0-9]{10,}` -> `1338865652`) ve
   scratchpad'de eski oturum artiklari vardi; karisimi okumak "failure" iceren YANLIS bir
   job raporu uretecekti.
5. `degistir.sh` satir-satir yazildi; cok satirli literal ASLA eslesemez. Iyi tarafi:
   **REDDETTI, BOZMADI** - cok satirli bloklara sinir-dogrulamali aralik ekleme kullanildi.
6. Rozet yanlis seciciyle olculdu (`#cartBadge` yok, `.badge` favori rozetini yakaladi) ->
   "0" gorulup bir an duzeltme eksik sanildi; gercek rozet `#badge` ve **3**.
7. Panel metni `innerText` ile olculdu, `""` dondu ve bos durum yok sanildi; `textContent`
   ile metin oradaydi ve `offsetParent` gorunur diyordu.

**2, 3 ve 4** ayni ailedir ve bu turda kalici cozume baglandi: VITRIN-FIX-2 push'unun
izleyicisi baslatilmadan ONCE **bes suzgecin tamami** bilinen-pozitif (546d799: 2 run /
6 job / 39 annotation / 0 failure) VE bilinen-negatif (sifir SHA: 0 run) girdiyle SINANDI,
ve dosya adlari TURA OZGU secildi.

## HAVALELER

- **[HAVALE->FAZ 2, DTO KALEMI] `ProductReviewResponseDto` yorum sahibinin ADINI ve
  `is_verified_purchase` alanini tasimiyor** (entity'de VAR). Bugun isim ve "Dogrulanmis
  Alici" rozeti GOSTERILEMIYOR. DTO acilirsa `reviewCards` icine eklenir; o gune kadar
  uydurma YOK.
- **[DEFTER] Olu handler'lar:** `data-rvsort` / `data-hv` / `data-rvphotos` click
  handler'lari artik ULASILAMAZ (o dugmeler hic cizilmiyor) ama zararsiz. Kaldirmak AYRI
  bir kucuk kalem.
- **[D-YAN TEMIZLIK LISTESINE] Dev-veri artigi:** olcumun actigi kurgu hesabi
  `fixa1.kurgu@example.com` (musteri **76**) duruyor - 1 aktif + 3 pasif sepet kalemi,
  0 siparis. Hesap silme uretim yolu bu dalganin kapsami degildi; D-YAN'in tek temizlik
  isine eklendi.

## KUYRUK GUNCELLEMESI

**VITRIN-FIX-2 KAPANDI.** Sirada:

```
1. OMER'IN BIRLESIK KABUL TURU (M1..M9)   <- SIRADA
   GOZ-FIX'in ertelenen insan kabulu + VITRIN-FIX-2 BIRLIKTE, tek tur.
   LISTE OMER'DE, CC'YE VERILMEZ - CC kendi isini onaylayamaz.
2. FIX-1B            (F4 + F8 zinciri, kara liste desenlesmesi, sentetik yazma pinleri)
3. IMPORT-FIX        (katalog gelisine gore ONE CEKILEBILIR)
4. FIX-1C            (F5 · F6 · F7 · F9 · F13 · D-YAN dev-veri temizligi)
5. LOG-FIX           (bes ham log satiri -> KanitMaskesi)
6. FIX-2             (B-6 · C-1 · G5 · B-5 · D-3)
7. FIX-3 / B13       (kupon geri bildirimi · terk edilmis Pending TTL)
```

---

