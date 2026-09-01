# MFIX-3b MUHRU - VITRIN TEMIZLIK + i18n TAMAMLAMA + KABUL TURU BULGULARI (28 Agustos 2026)

**KOD SHA'LARI: `288d0c0` (asil) + `31802e1` (MK-4 denetim duzeltmeleri)** - zemin `58b41df`.
Bu muhur AYRI ve docs-only bir commit'tir; **kendi SHA'sini ve kendi run kimliklerini
ICEREMEZ** (tavuk-yumurta) - muhrun kendi cift yesili MFIX-3b raporunda verilir.
MFIX-1'de kurulan kalip.

```
MFIX-3b KODU (58b41df..31802e1, IKI commit tek push)
  CI - Build & Test  run 33164555253  event=push  head_sha=31802e1
  Security CI        run 33164555224  event=push  head_sha=31802e1
ALTI JOB'DA failure SEVIYELI ANNOTATION: 0   (39 annotation, HEPSI warning)
ANNOTATION KUME FARKI (taban 39): 2/2 IKI YONDE, IKISI DE nullable ailesinde ve DOKUNULMAMIS iki dosyada -> ARTEFAKT
Gitleaks (secret taramasi): SUCCESS - bolum 7 kurali geregi ADIM SONUCUNDAN okundu
format-check UC ZORUNLU ADIM (whitespace + style + migration SENKRON): UCU DE SUCCESS
TestDbKurulum 1807 ozeti (iki test jobinda da): "HIC ATESLEMEDI (0) - retry devrede, gerekmedi."
```

**ORTAM UYARISI (Divisima Eki 2.3):** olcumler `goz1` duzeneginde kosuldu;
`api-baslat.cmd` BES arguman veriyor - `--Iyzico:UseRealSdk=false` ·
`--AdminSeed:Enabled=false` · `--BackgroundJobs:Enabled=false` ·
`--RateLimit:AuthPermitLimit=100` · `--MailSettings:Host=`.
**Bunlar URUN VARSAYILANI DEGILDIR.**

**AJAN KISITI:** MFIX-3b'nin OLCUM ve UYGULAMA fazinda AgentTool cagrisi yasakti; L1-L3
denetcileri ancak MK-4 turunda (asagi) dagitilabildi. Disiplin o faza kadar ELDE uygulandi
(on-kayit + karar kriteri, append-only defter, `[YOKLUK]` negatif kontrolleri, suzgec
sinamasi, TAM KAPSAMA dis kontrolu, 5. kontrol).

## MERKEZ ONCUL DUZELTMESI (ACIK KAYIT)

Merkezin kapi metni *"kabul turu musteri 10'da iz birakir"* diyordu. **OLCULDU: YANLIS.**
Tur `goz1.musteri` hesabinda kostu ve o hesap **MUSTERI 74**'tur; Omer'in E2b hesabi
(`e2b.sandbox@example.com`) **MUSTERI 10**'dur ve **AYRIDIR**.

```
Tur kayitlari : musteri 74, siparis 234-237 (28 Agu 01:01-01:06)
Omer (m10)    : COUNT=38, MAX=211  -> TUR SIRASINDA ARTMADI, MUHUR BOZULMADI
MAX musteri/adres: tur YENI musteri ya da adres URETMEDI
```

**BUNDAN SONRA:** kabul turlarinin izi **m74**'te aranir. `38 / 211` muhru E2b hesabina
aittir ve kabul turlarindan **BAGIMSIZ olarak SABIT** kalir. Bu, "ad/rota/kolon kaynaktan
okunur, tahmin edilmez" kuralinin (SDP 1.7/2) hesap kimligine uygulanmis halidir.

## T1 - DIL DEGISIMI SEPETI YENIDEN YAZIYORDU (kabul turunun yeni bulgusu)

**IKI AYRI KUSUR, ikisi de canli uretildi** (kurgu hesap; stok GERCEK YOLDAN dusuruldu -
ikinci hesap ayni beden icin Pending siparis verdi, satilabilir 4 -> 2):

```
POST /api/cart/add -> 400 {"message":"Yetersiz stok. Istenen adet mevcut degil."}
TOAST: "Sepet sunucuya yazilamadi. Internet baglantini kontrol edip tekrar dene."
       tip=(TIPSIZ) -> ekranda BASINDA ONAY ISARETIYLE
```

**(1) GEREKSIZ SEPET YAZIMI.** Zincir kaynaktan okundu: `setLang` -> `renderCart()` ->
api-bridge sarmalayicisi -> 250 ms debounce -> `syncCartToServer`. Sepet **HIC DEGISMEDIGI**
halde her dil degisimi `GET /api/cart` + her kalem icin `POST /api/cart/add` uretiyordu.
**COZUM SINIF DUZEYINDE:** senkron artik **SEPETIN IMZASINA** bagli (urun|beden|adet,
siralamadan bagimsiz). Salt-cizim yollari (dil, para birimi, sekme gorunurlugu) hicbir yazma
tetiklemez; **ILK SENKRON (birlestirme) kapidan MUAF** - F-A1/P4 sozlesmesi korunur.

**(2) YANLIS TESHISLI TOAST (durustluk kusuru).** Gercek sebep ZATEN ELDEYDI:
`api-client._parse` sunucunun `message` alanini tasiyan bir `Error` firlatiyor ve
`err.status` de mevcut. Artik `e.status` VARSA sunucunun kendi sebebi, YOKSA baglanti metni.

**R-T1 BIRINCI TURU KENDI TASARIMIMI CURUTTU:** imzayi YALNIZ *basarili* senkronda
kaydediyordum; sepette **KALICI reddedilen** bir kalem varsa kapi HIC devreye girmiyordu
(3 dil degisimi = 15 istek, ayni toast 3 kez). Semantik **"SEPET DURUMU BASINA TEK DENEME"**
olarak duzeltildi - imza HER DENEMEDEN SONRA kaydedilir.

**R-T1 NIHAI COMMIT (`31802e1`) UZERINDE YENIDEN KOSULDU - ZORUNLUYDU:** fix'in kalbi olan
`sepetImzasi` ILK commit'te **CIFT TANIMLIYDI ve OLUYDU** (asagi, denetci avlari); tekillestirme
`31802e1`'de yapildi. Yani ilk commit uzerindeki kanit, fix'in DOGRU KOPYASINI olcmemis olabilirdi.

```
NIHAI COMMIT UZERINDE (kurgu musteri 91, sunucu sepeti 954/TEK x2 + 950/TEK x1)
  ilk senkron BIRLESTIRDI: yerel 0 -> 2 kalem, rozet 3
  setLang en -> ar -> tr + setCur TRY  = DORT DEGISIM -> /api/cart istegi TAM 0
  hata toasti YOK (yalniz para biriminin kendi ok toasti)
  VAKUM KIRICI: addToCart(952,TEK,1) -> BES sepet istegi; sunucu sepeti 3 kaleme cikti
                (954|TEK|2 · 950|TEK|1 · 952|TEK|1), yerel 3 kalem
  YAN TEYIT: bedeni olmayan bir kalem denendiginde toast
             "Sepetin sunucuya yazilamadi: Yetersiz stok. Istenen adet mevcut degil." tip=err
             -> T1'in IKINCI yarisi da NIHAI commit uzerinde canli dogrulandi
```

## KALEM KALEM - KONTROLLU A/B (ONCE = `git show HEAD`, gecici servis, olcum bitince SILINDI)

| | ONCE | SONRA |
|---|---|---|
| `variantsOf` | function, **2 uydurma renk** | undefined |
| detay colorRow / swatch / thumb[data-h] | 1 / 2 / 2 | **0 / 0 / 0** |
| detay thumb[data-shade] | - | 4 (urunun **KENDI** `color_hex` tonlari) |
| kart card-cols / cdot | 1 / 4 | **0 / 0** |
| ana sayfa cdH / camp-clock / camp-eye | true / 1 / 1 | **false / 0 / 0** |
| indirim dealClock / deal-timer | true / 1 | **false / 0** |
| deal-strip (rozet seridi) | - | **1 (DURUYOR)** |

**DURUST KAYIT:** bugunku katalogda 24 urunun 24'u **gorselli** ve indirimli urun **0** -
yani iki REPRO'nun onkosulu CANLI VERIYLE SAGLANMIYOR. Kosul **IKI SURUMDE DE SENTETIK**
kuruldu (`p.img=null`; rozet icin sentetik `old` fiyat). Gecistirilmedi.
**YAN SONUC:** `rngOf` PRNG ureticisi de SOKULDU - `variantsOf` onun **SON CAGIRANIYDI**.

**TOAST TIPI:** `ok` = onay isareti `.t-ok` · `err` = uyari `.t-err` · `info` = bilgi
`.t-info` · **TIPSIZ -> info**. Canli: sepete ekleme (ok) · sepet-senkron hatasi (err,
**onay isareti YOK**) · misafir kalp (info + `#/giris` + yerel yazma YOK).
**DURUST SAPMA:** merkezin ornek verdigi *"gecersiz kupon"* yolu **TOAST KULLANMIYOR** -
MFIX-1 tasarimi geregi cekmece ici `.cp-msg err` gosteriyor.

**WISHLIST:** `POST /api/wishlist/toggle?productId=951` -> 200 + DB satiri. api-bridge kendi
elle kurdugu kopyayi birakti -> **TEK SOZLESME**. Eski govde bicimi canli olculdu: `productId`
0'a bagliniyor ve uc **HTTP 500** donuyordu.

**T3:** tek satilabilir bedende cip **OTOMATIK secili** (`on=true`, `#bedenSel="TEK"`) - uc
olcumde de.

**REGRESYON BESLISI:** kupon `E2YUZDE` 8.359,20 -> **-835,92** (`srvAmount`, SUNUCUDAN) ->
7.523,28 GECTI · misafir sepet F5'te KORUNDU · favori hesaba ozgu (cikista bos, giriste geri)
GECTI · detay = liste = satilabilir GECTI · **CIFT-TIK TEK SIPARIS CANLI KOSULMADI** (adressiz
hesapta checkout tamamlanamadi); MFIX-1'in `request_id` sozlesmesi **P6 kaynak piniyle**
korunuyor ve o pin yesil.

## STOK-MAT + OMER'IN "5 -> 3 ALDIM -> 1 KALDI" GOZLEMI

| Adim | DB (fiz/rez/sat) | liste | detay | vitrin | cip |
|---|---|---|---|---|---|
| T0 | 15/0/15 | 15 | 15 | (yok, esik <=5) | normal |
| Kartla odeme (Pending 11) | 15/11/**4** | 4 | 4 | "Son 4 urun!" `*` | normal |
| COD (3) | 12/11/**1** | 1 | 1 | "Son 1 urun!" | low |

`*` **YENILEMESIZ** vitrin ESKI degeri gosterdi (katalog tek sefer cekiliyor; siparis
**BASKA HESAPTAN** geldi) - **BILINEN SINIR**, kusur degil. Esikler KAYNAKTAN: kart `<=5`,
cip `<=2`. Kirpma canli: satilabilir 1 iken `addToCart(...,3)` -> sepette 1.

**OMER'IN GOZLEMI ACIKLANDI** (rezervasyon defterinden, varsayim degil) - urun 953:

```
tur oncesi  fiziksel 9, rezerve 3            -> SATILABILIR 6
01:01:13    siparis 234 (Pending)  -> rezerve 4 -> 5   <-- gordugu "5"
01:02:28    siparis 235 (Pending)  -> rezerve 5 -> 4
01:06:24    siparis 236 (COD, 3)   -> fiziksel 6       <-- "3 aldim"
SONUC       fiziksel 6, rezerve 5             -> 1     <-- "1 kaldi"
```

5-3=2 **DEGIL 1** cikmasinin sebebi: **kendi IKINCI odenmemis kart denemesi (235) hala BIR
ADET rezerve tutuyor.** Mekanikle TAM UYUMLU, **STOK KAYBI YOK**. Kanit: 953 icin `status=0`
rezervasyonlar `{218,219,220,234,235}` = 5 = `reserved_quantity`; hareket (Out) toplami 9,
fiziksel 6 -> baslangic 15.

## i18n KAPANISI

**ESKI 156'LIK LISTE BAYATTI** - satir numaralari 0..+27 kaymis, numara eslesmesi yalniz 21
rastlanti. **ICERIKLE ESLESTIRILDI: 155/156.** Kayip 1 tanesi MFIX-B/K3'un yeniden yazdigi
misafir sonuc bloku - **dizgeleri kaybolmadi, bicimi degisti.**

| | sayi |
|---|---|
| Yeni sozluk anahtari | **171** (94 duz + 52 HTML parcasi + 25 ASCII) + index.html'de 5 + L3'ten 7 |
| `ceviri()` anahtari (benzersiz) | **218**, tamami T **ve** AR'da |
| Sozluk | **T=792 / AR=792**, karsilikli eksik **0** |
| api-bridge'de kalan TR dizge | **5** - merkezin DOKUNULMAZ dedigi olu 4 + cikmayan 4 satirin ta kendisi |
| Sizinti dedektoru (13 rota + sepet cekmecesi) | **EN 0 / AR 0** (CMS icerigi ve urun adlari ISTISNA) |

Tarih/sayi: **TEK KAYNAK `dvsLocale()`** - TR `1.049,70 TL` / `28 Agu 2026` · EN
`1,049.70 TL` / `28 Aug 2026` · AR `1,049.70 TL` / `28 (Arapca ay) 2026`
(`ar-EG-u-nu-latn`: Arap-Hint rakam **DEGIL**). Arama normalizasyonu **KIMLIK islemidir** -
DOKUNULMADI (bolum 6c).

## MK-4 ILK SAHA UYGULAMASI

```
git worktree add ../mfix3b-denetim 288d0c0 -> C:\Users\pc\Desktop\smart\mfix3b-denetim
   L3 cift-kor + kural-uyum denetcileri O DIZINDE kostu (ana agac yolu promptlarda YASAK)
git worktree remove ../mfix3b-denetim      -> kaldirildi, worktree listesi tek satir
```

**MEKANIK CALISTI:** ana calisma agaci denetim oncesi ve sonrasi **TEMIZ** (`git status` 0),
worktree HEAD `288d0c0` ve status 0.

**IZOLASYON KANITI URETILEMEDI - DURUST KAYIT.** Iki denetcinin transkript dosyasi **0 BAYT**
(harness kalici yazmamis). **NEGATIF KONTROL:** ayni dizindeki en buyuk transkript 842.791
bayt ve `Divisima` 2095 kez geciyor -> grep **CALISIYOR**, dosyalar **GERCEKTEN BOS**. Yani
SDP 1.9'un istedigi *"ana agac yolu gecisi 0 / worktree yolu gecisi >0"* olcumu **YAPILAMADI**.

### MK-4a (YENI KALICI MIKRO-KURAL)

**Her worktree denetcisi, RAPORUNUN BASINA kendi `pwd` + `git rev-parse HEAD` olcumunu
koyar** (beklenen worktree yolu + beklenen SHA). Transkript grep'i ancak transkript VARSA
**EK** kanittir; **birincil kanit denetcinin kendi beyan ettigi olcumdur.**

Gerekce OLCULDU: MK-4'un ilk uygulamasinda transkript kanali BOS cikti ve izolasyon iddiasi
desteksiz kaldi. Kural-uyum denetcisi bunu M8'de kendi calisma dizinini beyan ederek kismen
telafi etti (ve git nesne veritabaninin dolayli erisimini kendi DURUST SINIRI olarak yazdi) -
yani cozum ZATEN sahada dogmustu; kural onu zorunlu kiliyor.

## DENETCI AVLARI ve DERSLER

**IKISI DE GERCEK KUSUR BULDU; hepsi `31802e1`'de kapatildi.**

| Denetci | Sonuc | Yakaladigi |
|---|---|---|
| **L3 cift-kor** | 6 ONAY + **1 ITIRAZ** + 9 ek gozlem | **IDDIA 5 YANLISTI** |
| **Kural-uyum** | M1/M2/M3/M4/M7/M8 ONAY · M5 ITIRAZ (2) · M6 ITIRAZ (1) -> **UYUMSUZ (DAR)** | uc pin kusuru |

**L3 - ENVANTERIN ASCII KOR NOKTASI.** *"Kullanici-gorunur Turkce literal kalmadi"* iddiam
YANLISTI. Envanterim **DIYAKRITIK TABANLIYDI** ve iki kor noktasi vardi: (a) kacisli tirnak
tasiyan satirlar ayristiricidan kaciyordu, (b) **ASCII-only Turkce** (`Adet`, `Kapat`,
`Fatura`, `Beden `, ` adet`, `Takip no`, `Bakiyeyi kullan`, `Adres kaydedilemedi`,
`Kart bilgilerin Divisima'da saklanmaz`) **YAPISAL OLARAK gorunmuyordu**. Denetci **12 CANLI
nokta** cikardi; on ikisi de sozluge tasindi (7 yeni anahtar, uc dil). En kotusu: dogrulama
ekraninda cumlenin **ILK yarisi ceviriliyken KUYRUGU Turkce yapistiriliyordu** - EN/AR
kullanicisi **YARI CEVRILI** cumle goruyordu. Ayrica `Page Not Found` tek kaynak disinda
literal olarak duruyordu; o da anahtara indi. **CANLI TEYIT:** kaynakta kalan **0**.

**YENI KALICI KURAL (bu vakadan dogdu): i18n / metin envanter araclari DIYAKRITIK **ve**
ASCII-SOZCUK olmak uzere IKI YONTEMLE kurulur ve her ikisi de bilinen-POZITIF (bilerek
yerlestirilmis ASCII-only bir Turkce dizge) ve bilinen-NEGATIF girdiyle SINANIR.**
Tek yontemli bir envanter **eksigi GIZLER** ve "temiz" der - bu, SDP 1.7/1'in metin
envanterine uygulanmis halidir.

**KURAL-UYUM - UC PIN KUSURU:**

1. **OLU ASSERT (bolum 6 vakum yasagi).** Dort pinin `NotContain("rngOf")` asserti, `rngOf`
   tumden sokuldugu icin **ARTIK HICBIR KOSULDA KIRILAMAZ** hale gelmisti; koydugum vakum
   kirici ("dosya okundu") korunan sozlesme hakkinda hicbir sey soylemiyordu. Olcut **ADDAN
   SINIFA** tasindi: yeni `RASTGELELIK` deseni `Math.random` / `crypto.getRandomValues` /
   `rngOf` / `mulberry32` / `xorshift` / `seed =` kaliplarini birlikte tarar.
   **KANIT (M-D1):** `sizeStockOf`'a `Math.random` eklenince **IKI pin birden kirmizi**.
2. **OLU DONGU.** P16'da `foreach (var tip in {ok,err,info})` degiskeni **HIC
   KULLANILMIYORDU**; ayni iddia uc kez kosuyor, `_TOAST_IKON`'dan bir girdi silinse pin
   YESIL kaliyordu. Artik **her tip ayri araniyor**. **KANIT (M-D2):** `err` silinince kirmizi.
3. **CIFT TANIMLI `sepetImzasi`.** Ayni IIFE icinde MFIX-1'den gelen bir tanim **ZATEN VARDI**
   ve JS hoisting geregi benim yazdigim fonksiyonu **EZIYORDU** - yani yeni fonksiyon **HIC
   CALISMIYORDU**. Bu, bu depoda **alti kez** bedeli odenen *"ayni kuralin ikinci kopyasi"*
   sinifinin canli ornegidir (B10 · D5 · K7 · Faz 0/K1 · D-SEMA · bu). Ikinci kopya
   KALDIRILDI; mevcut tanim kullaniliyor (olculdu: `cartItemsPayload` hicbir kalemi suzmuyor,
   yani imza sepetin tamamini temsil ediyor). Ayrica `index.html`'de 20 satir arayla
   birbirini yalanlayan iki yorum vardi (`rngOf SOKULDU` / `rngOf DURUYOR`) - duzeltildi.

**BU AV, MK-4'UN VARLIK SEBEBIDIR:** uc kusurun ucu de **kendi commit'imde** duruyordu ve
suit yesildi; ancak AYRI bir worktree'de, sonuclarimi GORMEYEN bir denetci onlari buldu.

## PINLER / DIS / MUTASYON

`FrontendDokunmaHedefiTests` **18 -> 21 `[Fact]`** (SIFIR-DDL sinif; yeni veritabani
**ACILMADI** - `10d794d` dersi):
- **P15** `KAYNAK_SOZLESMESI_UydurmaRenk_ve_SahteAciliyet_Uretilmez`
- **P16** `KAYNAK_SOZLESMESI_Toast_TipTasir_ve_WishlistToggle_QueryString`
- **P17** `KAYNAK_SOZLESMESI_TarihBicimi_LocaleBagli_ve_DilDegisimi_SepetYazmayi_Tetiklemez`

**UCU DE DURUST ETIKETLI KAYNAK SOZLESMESI PINIDIR**, davranis pini DEGILDIR - depoda JS/DOM
kosucusu YOK (Dalga 4'ten beri acik kalem). Davranis kaniti kontrollu A/B tarayici + DB
olcumleridir.

**DIS KONTROLU (TAM KAPSAMA):** P15 · P16 · P17 -> her turda **TAM 1 ISIMLI KIRMIZI**.
**5. KONTROL:** M-P15 (geri sayimi FARKLI duzenekle geri koy) · M-P16 (toggle govdeye) ·
M-P17 (`rvTarih` sabit `tr-TR`) -> **ucu de TAM 1 LOKALIZE**. Denetim sonrasi ayrica M-D1
ve M-D2 kosuldu (yukarida). Her turda (a) iz dosyada dogrulandi, (b) build 0 hata,
(c) geri alindi; iz **0**.

## MERKEZ ONAYLARI (KAYIT)

1. **`camp_title` DORDUNCU METIN DEGISIKLIGI ONAY.** Merkezin listesi uc metin sayiyordu;
   `"Gunun Firsatlari" -> "Secili Urunlerde Indirim"` dorduncusuydu. Gerekce: geri sayim
   kalkinca *"GUNUN"* hala GUNLUK bir pencere IDDIA EDIYOR.
2. **ALTI BILINCLI PREMIS DEGISIKLIGI ONAY:** (a) `rngOf` vakum kiricisi 4 pinde -> olcut
   ADDAN SINIFA · (b) iki Turkce literal olcutu ANAHTARA (`b_yeni_siparis_yok`,
   `b_odenmemis_duruyor`) · (c) wishlist uc literali api-client'a tasindi -> `wireFav` olcutu
   `api.wishlist.toggle(` · (d) `MisafirA3` iki literali ANAHTARA (`b_kartla_odeme_icin`,
   `b_uye_girisi_link`) · (e) AR sozluk degerleri artik TEK ya da CIFT tirnakli olabilir
   (apostrof kacisi gerekcesi) · (f) P16 ikon dongusu her tipi ayri arar hale geldi.
   **KALICI KURAL NOTU (MFIX-2'de konuldu, ucuncu kez uygulandi):** bir pinin PREMISI
   degistiginde HER ZAMAN raporda gerekceli yazilir ve muhurde **merkez onayiyla** kayda
   gecer. Assert degerini degistirmeden premisi sessizce kaydirmak, pini yalanci yesile
   cevirmenin en sinsi yoludur.
3. **CIFT-TIK CANLISININ KOSULAMADIGI DURUST KAYIT ONAY:** regresyon beslisinin bu maddesi
   adressiz kurgu hesapta checkout tamamlanamadigi icin canli surulemedi; MFIX-1'in
   `request_id` sozlesmesi **P6 kaynak piniyle** korunuyor.

## KAPSAM DISI UCLU -> MANTIK-AV-1 SONRASI TEK FIX PAKETI (MERKEZ KARARI)

Uc kalem de **DUZELTILMEDI** ve **TEK PAKETTE** dalgalanacak (gezgin turu bulgulariyla
birlikte):

1. **[BACKEND] Siparis zaman-cizelgesi notlari TR kaliyor.** EN modda siparis detayinda
   `"Siparis olusturuldu"`, `"Kapida odeme - siparis onaylandi"`. Kaynak **SUNUCUDUR**
   (`OrderManager` `RecordAsync` cagrilari + `OutboxProcessor`) - musteri-gorunur backend
   metinleri. NOT: ayni ekranda **TARIHLER dogru cevrildi** (`28 August 2026`) - yani
   `dvsLocale()` duzeltmesi canlida calisiyor.
2. **[UX/PARA] Kupon indirimi sepet kuculunce tazelenmiyor.** 8.359,20 TL sepette
   `srvAmount` 835,92; sepet 329,90'a dusunce panel `-835,92 TL` yaziyor ama toplam **0**
   gosteriyor (`couponDiscount` ara toplama KIRPIYOR, gosterilen satir kirpmiyor).
   `validateCoupon` YALNIZ `coupon.min` kontrolu yapiyor, sunucuya YENIDEN SORMUYOR.
   MFIX-1 kodu.
3. **L3'un 9 EK GOZLEMI** (defterde): mock `NEWREAL` 442 KB tek satir · `mirror()`'in TUM
   toast kuyrugunu silmesi · olu CSS gruplari · bos catch yogunlugu (21 + 99) · ilk senkron
   sonrasi FAZLADAN bir tur · `divisimaCheckout` gelistirici metni ·
   `api-bridge:449 "Page Not Found"` (BU DALGADA KAPATILDI).

## KURGU KAYIT ENVANTERI

Musteri **89** (`mfix3b.t1@`), **90** (`mfix3b.t1b@`), **91** (`mfix3b.rt1@` - R-T1'in nihai
commit uzerindeki tekrari) · siparis **238** (m90 Pending online), **239** (m90 Pending
online), **240** (m90 COD Confirmed) · adres **53**, **54** (m90) ·
`wishlist_items` m89 -> 951 ve 953 · m89 ve m91 `cart_items`.
**MAX musteri 91 / adres 54 / siparis 240.**

**Omer'in hesabi ve turunun kayitlari KULLANILMADI, SILINMEDI.**

**MUHURLER (MK-3, URETEN IFADESIYLE):**
```
SELECT COUNT(*), MAX(id) FROM orders WHERE customer_id = 10;
  -> 38 / 211   SABIT
SELECT COUNT(*), MIN(id), MAX(id), SUM(CAST(id AS bigint))
  FROM orders WHERE status = 0 AND id <= 210;
  -> 35 / 9 / 210 / 3837   DEGISMEDI
SELECT COUNT(*) FROM orders WHERE customer_id = 74 AND id BETWEEN 234 AND 237;
  -> 4/4   TURUN KAYITLARI KORUNDU
```
`id > 210` Pending kumesi 10 satir: `213,214` (Dalga B) · `218,219,220` (MFIX-1) · `222`
(MFIX-2) · **`234,235` (KABUL TURU, m74)** · `238,239` (MFIX-3b). Dortlu `id <= 210` ile
sinirli oldugu icin tur ve kurgu kayitlarindan **YAPISAL OLARAK ETKILENMEZ** - merkezin
"tur yeni Pending uretirse dortlu yeniden olculur" sarti bu kumeyle karsilandi.

## KABUL TURU KAPANISI

**Omer'in birlesik dogrulama turunda 12+1 maddede `X` YOK.** MFIX-1 · MFIX-2 · MFIX-3 ·
MFIX-B **ve** MFIX-3b **KABUL EDILDI**. Turun kendi urettigi dort yeni kalem (T1 ·
tarih-locale · sizinti dedektoru · T3 · STOK-MAT) bu dalgada kapandi; `5 -> 3 -> 1` gozlemi
rezervasyon defteriyle aciklandi ve **kusur olmadigi** kanitlandi.

## KUYRUK

```
1. MANTIK-AV-1                                                <- SIRADA (MERKEZDEN)
2. MANTIK-FIX paketi (kapsam disi UCLU + gezgin turu bulgulari)
3. FIX-1B      F4 + F8 zinciri, kara liste desenlesmesi, sentetik yazma pinleri
4. ADMIN-FIX
5. IMPORT-FIX  [KRITIK YOL - katalogda gercek urun 0; katalog gelirse ONE CEKILIR]
6. FIX-1C      F5 · F6 · F7 · F9 · F13 · D-YAN dev-veri temizligi
7. LOG-FIX     bes ham log satiri -> KanitMaskesi (SmtpMailService:42/81 DAHIL)
8. FIX-2       B-6 · C-1 · G5 · B-5 · D-3
9. FIX-3 / B13 kupon geri bildirimi · terk edilmis Pending TTL
```

**D-YAN TEMIZLIK LISTESINE EK:** MFIX-3b'nin kurgu kayitlari - musteri 89, 90, 91, siparis
238-240, adres 53-54, m89 wishlist satirlari, m89/m91 sepet kalemleri. MFIX-B'nin 81-88 /
224-233'u, MFIX-3'un 79/80/46/223'u, MFIX-2'nin 78/45/221/222'si, MFIX-1'in 218-220'si ve
Dalga B'nin 213-217'siyle birlikte **TEK temizlik isinde** ele alinir.

---

