# MANTIK-FIX-4 MUHRU - VITRIN DURUSTLESIR ve i18n TAMAMLANIR (1 Eylul 2026)

**KOD SHA: `e3e7f94`** (zemin `6e2b06d`; SEKIZ commit TEK push). Bu muhur AYRI ve docs-only
bir commit'tir; **kendi SHA'sini ve kendi run kimliklerini ICEREMEZ** (tavuk-yumurta) -
muhrun kendi cift yesili MANTIK-FIX-4 raporunda verilir. MFIX-1'de kurulan kalip.

```
MANTIK-FIX-4 KODU (6e2b06d..e3e7f94, SEKIZ commit tek push)
  CI - Build & Test  run 33494569361  event=push  head_sha=e3e7f94  SUCCESS
  Security CI        run 33494569335  event=push  head_sha=e3e7f94  SUCCESS
ALTI JOB (build-and-test · format-check · tests · codeql · secret-scan · dependency-scan):
  71 ADIM -> 69 SUCCESS + 2 skipped (TESHIS, iki job'da da) · failure SEVIYELI ADIM 0
ZORUNLU ADIMLARIN HEPSI SUCCESS (ham adlariyla, MK-7):
  "SQL gerektiren testler (ATLANMAMALI)" · "Testler + coverage" · "Coverage raporunu yukle" ·
  "Bicimlendirme dogrulama - whitespace (ZORUNLU)" · "Bicimlendirme dogrulama - style
  (ZORUNLU)" · "Model ile migration'lar SENKRON mu (ZORUNLU)" · "Entegrasyon testleri" ·
  "Gitleaks (secret taraması)" [bolum 7 kurali: ADIM SONUCUNDAN] ·
  "Açık bağımlılık KAPISI (üretim projeleri)" ·
  "TestDbKurulum - 1807 yeniden deneme ozeti (annotation)" (iki job'da da)
ANNOTATION: 38 (failure 0 · warning 38). TABAN 39 - **fark 1, AZALMA yonunde**.
  YOL DAGILIMI  IEntityRepository.cs 24 (taban 24) · EfEntityRepositoryBase.cs 6 (taban 6)
                · .github 8 (taban 9)  -> nullable ailesi 30 SABIT.
  DIFF KESISIMI **0**: uc annotation yolunun HICBIRI push diff'inin 3 dosyasinda YOK;
  POZITIF KONTROL 1 (diff'ten enjekte edilen `frontend/api-bridge.js` bulunuyor). Yani
  fark bu commit'in urettigi bir sey DEGIL - `.github` uyarilari GitHub kosucusunun kendi
  deprecation notlaridir (Node.js 20 x5 · CodeQL v3 x1 · TestDbKurulum ozeti x2).
  **YENI UYARI URETILMEDI.**
TestDbKurulum retry kanali (iki job'da da, anonim okundu):
  "TestDbKurulum: 1807 yeniden denemesi bu kosumda HIC ATESLEMEDI (0) - retry devrede,
   gerekmedi."
TABAN DOGRULAMASI (kendi olcumumle): zemin 6e2b06d iki PUSH run'i -> 6 job · 39 annotation ·
  failure 0 · yol dagilimi 24+9+6 = CLAUDE.md kaydiyla BIREBIR. Zemindeki ucuncu-dorduncu
  run'lar (DAST/schedule ve Security CI/schedule) izleyici kurali geregi ASIL IKI WORKFLOW
  disinda tutuldu.
```

**ORTAM UYARISI (Divisima Eki 2.3):** olcumler `goz1` duzeneginde kosuldu; API surecinin
komut satirinda BES ARGUMAN var ve **BUNLAR URUN VARSAYILANI DEGILDIR** -
`--Iyzico:UseRealSdk=false` · `--AdminSeed:Enabled=false` · `--BackgroundJobs:Enabled=false`
· `--RateLimit:AuthPermitLimit=100` · `--MailSettings:Host=`.

**AJAN KISITI:** uygulama fazi TEK AKIS (subagent YOK); ajan yalnizca MK-4b kapanis
denetiminde kullanildi.

## ADIM 0 - FLAKE TEMIZLIGI (worktree'siz, YERINDE, UC ARDISIK)

```
TUR1  Category=Sql 356/356   tam suit 604 (601 gecti / 3 basarisiz)
TUR2  Category=Sql 356/356   tam suit 604 (601 gecti / 3 basarisiz)   FAIL sayisi Sql'de 0
TUR3  Category=Sql 356/356   tam suit 604 (601 gecti / 3 basarisiz)   FAIL sayisi Sql'de 0
KIRILAN KUME TUR2 == TUR3 BIREBIR (diff BOS):
  OrderEndpointTests.PlaceOrder_ConcurrentRequests_NoOverselling
  OrderEndpointTests.PlaceOrder_InsufficientStock_Returns400_And_NoPartialData
  OrderEndpointTests.PlaceOrder_ValidCart_Returns201_And_DecrementsStock
UCU DE tabandaki Docker'li sinif (yerelde Docker kapali, CI'da YESIL kosar).
DORDUNCU KIRMIZI YOK -> push serbest. Yerelde 1807 ve TIMEOUT retry HIC ATESLEMEDI (0/0).
```

**SUZGEC DERSI (yeni, kayda):** `dotnet test ... 2>&1 > dosya` yonlendirmesi stderr'i ESKI
stdout'a (terminale) birakir; kirilan test adlari `[FAIL]` satirlari STDERR'e gittigi icin
log dosyasinda BULUNMAZ. Dogrusu `> dosya 2>&1`. TUR1'de bu yuzden adlar yalniz EKRAN
CIKTISINDAN okunabildi; TUR2 ve TUR3 duzeltilmis yonlendirmeyle kosuldu.

## KALEMLER (rapor aynen)

| Kalem | Commit | Test Δ | Olculen once -> sonra |
|---|---|---|---|
| **K1** | `f343acc` | +1 | suzgec sayaci **9 -> 8** · urun 1 old 1.299,90 -> 0 ve pdPrice "899,90 TL 1.299,90 TL %31" -> "899,90 TL" (BILINCLI kayip) · kalan 8 urunun old/pct degerleri **BIREBIR AYNI** (123: 399,90/38 dahil) |
| **K2** | `b550a38` | +1 | kart "689,74 TL" -> **"Siparis toplami 689,74 TL" / "Order total 689.74 TL" / (Arapca)** · kredisiz siparis 260 AYNI etiket · detay "Kalan odeme 489,74 TL" ile tutarli |
| **K3** | `4ce2ecc` | 0 | cekmece "Toplam/Total/الإجمالي" -> **"Ara Toplam/Subtotal/المجموع الفرعي"** · `b_toplam` cagirani **4 SABIT** · `t('total')` 0 · olu `subtotal` anahtari canlandi |
| **K4** | `8a360e0` | 0 | 17 canli ikili-dil satiri sozluge (31 yeni anahtar T+AR birer) · KOSULLU 2 ULASILABILIR cikti -> **NAME_EN/DESC_EN SOKULDU** · uc olu anahtar kaldirildi · T/AR ortusme pini **CIFT YONLU** · sizinti dedektoru (diyakritik + ASCII, POZ/POZ2/NEG sinanmis) bu yuzeylerde **EN 0 / AR 0** |
| **K5** | `502c81b` | +1 | katlama zinciri **2 -> 1** · misafir yolu ARTIK CEVIRILI ("Gecerli bir telefon girin. / Enter a valid phone number. / أدخل رقم هاتف صالح.") · 500 capasiz · 429 sebep iddiasiz · bilinmeyen-mesaj simulasyonu -> notr varsayilan, ham metin SIZMADI |
| **K6** | `5d34d42` | +1 | ONCE yedi kirici, SONRA `dir`: filtre paneli **1205..1463 -> 937..1195** · sortbox **753..845 -> 71..163** · body text-align start -> **right** · toast AR -4px/TR 4px · a11y sw -18/+18 · mobil drawer kapali +343 acik none |
| **K7** | `0d16818` | +1 | ValidationRules dizini TARANIR (sabit liste DEGIL) · pin YALNIZ REGEX uzerinde · KACISSIZ |
| **MK-4b** | `e3e7f94` | +1 | denetim bulgulari BULGU-1 · BULGU-2 · BULGU-4 |

**KANIT KOMUTU:** `dotnet test --filter FrontendDokunmaHedefiTests` -> son satir `Gecti: 33`.

## PIN TABLOSU

| Pin | Sinif | Kirmizi-once | MK-6 mutasyon |
|---|---|---|---|
| **P-V1** IndirimSuzgeci | kaynak sozlesmesi | TAM 1 | TAM 1 (zorunlu; eski `old_price ? ... : kapi` yuklemi geri) |
| **P-V2** SiparisKarti | kaynak sozlesmesi | TAM 1 | TAM 1 (denetci: `ao-lbl` etiketi kaldirildi) |
| **P-V3** CekmeceEtiketi | kaynak sozlesmesi | TAM 1 | TAM 1 (etiket `total`a donduruldu) |
| **P-V5** HataEslemesi | kaynak sozlesmesi | TAM 1 | TAM 1 (denetci: `sifreHatasiniCevir` kopyasi geri, `[şŞ]` 1->2) |
| **P-V6** BelgeYonu | kaynak sozlesmesi | TAM 1 | TAM 1 (denetci: `[dir=rtl] .filter-side` medya blogu DISINA) |
| **P-V7** TelefonKurali | kaynak sozlesmesi | TAM 1 | TAM 1 (zorunlu; SellerRegister `{7,20}` -> `{7,25}`, **ihlalciyi ADIYLA** soyledi) |

**ALTISI DA DURUST ETIKETLI KAYNAK SOZLESMESI PINIDIR**, davranis pini DEGILDIR - depoda
JS/DOM kosucusu YOK (Dalga 4'ten beri acik kalem). Davranis kaniti yukaridaki tarayici ve
DB olcumleridir.

**GUCLENDIRILEN UC MEVCUT PIN:** T/AR ortusme kontrolu CIFT YONLU yapildi (tek yonluyken
T'den silinip AR'da unutulan anahtar SESSIZCE gecerdi) · uc olu anahtarin kaldirildigi
AYRICA assert edildi · P-H6'nin iki assert'i premis degisikligiyle guncellendi.

**YENI SQL SINIFI ACILMADI** (`10d794d` dersi): alti pinin altisi da mevcut SIFIR-DDL
sinifa (`FrontendDokunmaHedefiTests`) eklendi; `Category=Sql` sayisi **356 -> 356 DEGISMEDI**.

## MERKEZ ONAYLARI (KAYIT)

1. **K1 KAPI-ONCE / DEGER-SONRA bicimi ONAY.** Naif "old_price'i tumden birak" bicimi urun
   123'un (299,90 / 249,90 / 399,90) ustu cizili fiyatini 399,90 -> 299,90 yapardi ve
   "kalan 8 DEGISMEDI" kriterini IHLAL EDERDI. DB on olcumu: old_price DOLU aktif urun
   **TAM 2** (id 1 ve 123). Secilen bicim iki kriteri birden karsilar.
2. **K4 NAME_EN/DESC_EN SOKUMU ONAY `[DURUSTLUK]`.** Gomulu tablolar MOCK katalogun
   adlariydi; bugunku katalogda hala aktif olan urun 1'in DB adi "Siyah Midi Elbise" iken
   EN modda **"Satin Midi Dress"**, urun 2'nin adi "E4a Test Urun" iken "Soft Knit Sweater"
   goruluyordu. Bu bir ceviri boslugu DEGIL **UYDURMA ICERIKTIR** (VITRIN-FIX-2 / F-D1
   sinifi). `typeof NAME_EN` -> **undefined** (canli teyit).
3. **P-H6 PREMIS DEGISIKLIGI ONAY.** Olculen SOZLESME AYNI ("istemci on-dogrulama yapmaz"),
   yalniz OLCUM YERI degisti: `v_name` artik `wireAccount` govdesinde DEGIL, IIFE ust
   duzeyindeki capa tablosunda. Eski assert ("cagriDAN SONRA gecmeli") K5 sokumuyle
   bayatlamisti.
4. **`share_copied` METIN DEGISIKLIGI ONAY.** Mevcut anahtar yeniden kullanildi (yeni anahtar
   ACILMADI, "ayni kavram icin iki anahtar" tuzagi tekrarlanmadi); bedeli metnin
   "Baglanti kopyalandi" -> "Link kopyalandi!" degismesi oldu.
5. **FATURA ZEMINI 119 PREMIS-DUZELTMESI ONAY (SDP BELIRSIZLIK maddesi).** Tarif kisiti
   "fatura 118" diyordu; olculdu ki zemin **119**'du - fatura 119, ON OLCUM fazinda uretilen
   siparis 286'nin faturasidir (30 Agu 14:30). Uygulama fazinda fatura URETILMEDI. Duzeltme
   yalnizca bir SAYIYI gercege uydurur, is EKLEMEZ.

## MK-10 (YENI KALICI MIKRO-KURAL)

**Her commit/push kapisinda HEAD'in bir dal uzerinde oldugu dogrulanir
(`git symbolic-ref -q --short HEAD`); SHA'ya checkout yapilan her olcum donusu dala
checkout ile biter.**

**Gerekce OLCULDU (MANTIK-FIX-3 push turu):** C provenans olcumunun donusunde
`git checkout 974ce41` yapildi - yani SHA ile; dogrusu `git checkout main` idi. HEAD
DETACHED kaldi, FF commit'i DALA DEGIL detached HEAD'e dustu ve `git push origin main`
yalniz ALTI commit'i itti. Kapi kontrolu HEAD SHA'sini, agaci, zinciri, farki, worktree'yi
ve stash'i dogruluyordu ama **"HEAD BIR DAL UZERINDE MI"** sorusu SORULMADI.

**NUMARA OLCULDU, TAHMIN EDILMEDI:** CLAUDE.md'de tam sayili mikro-kurallar MK-1..MK-9
(POZ kontrol: `MK-9` 3 gecis · NEG kontrol: `MK-99` 0 gecis); MK-4a ve MK-4b HARFLIDIR ve
tam sayi TUKETMEZ. Siradaki tam sayi **MK-10** - merkezin beklentisiyle ORTUSUYOR, sapma YOK.

## MK-4b KAPANIS DENETIMI - DORT BULGU

Tek denetci, AYRI worktree (`../mf4-denetim`), izole DB (`DivisimaMf4Denetim`), MK-4a beyani
TUTTU (`pwd=/c/Users/pc/Desktop/smart/mf4-denetim` · `HEAD=0d16818` · DETACHED).
**SONUC: UYUMSUZ (DAR).** Denetci BES pinin BESINI de uretim mutasyonuyla sinadi; hepsi
TAM 1 ISIMLI KIRMIZI / 31 yesil.

- **BULGU-1 `[PIN-ZAAFI]` KAPANDI.** P-V6'nin vakum kiricisi **GECIS** sayiyordu; gecis
  sayimi ZEMINDE de 20 idi (bir satirda birden cok secici var), yani K6'nin TUM
  override'lari geri alinsa bile esigi gecerdi - KIRICI DEGILDI. Olcut **SATIR** bazina
  cevrildi: zemin **12 satir** -> K6 sonrasi **17**.
- **BULGU-2 `[PIN-ZAAFI]` KAPANDI.** K3 tek basina **PINSIZDI** - cekmece etiketi sessizce
  `total`a donerse suit yesil kalirdi. **P-V3** eklendi (dis kontrolu + MK-6 mutasyonu, TAM
  1 kirmizi); cift-anlam kiricisi `b_toplam`in DEGERINI ve dort cagiranini KORUR.
- **BULGU-3 `[MANTIK]` KAPSAM DISI -> VITRIN-KALAN.** Bes ikili-dil satiri kaldi (`fmtDay` ·
  `couponUI` · `showLegal` · `accStatus` · `accOrders`). Denetcinin EN AGIR iddiasi
  ("AR'da sozlesme metni Turkce, ustelik artik RTL sayfada") OLCULDU ve **TESHISI CURUDU**:
  `window.showLegal` **api-bridge:3398'de EZILIYOR**, yani index.html'in `var L=lang==='en'?1:0`
  satiri HIC calismiyor. BELIRTI GERCEK ama SEBEBI **CMS ICERIGI** (contents tablosunda AR
  karsiligi yok), sozluk DEGIL. B'nin "ULASILAMAZ 3" kaydi DOGRULANDI.
- **BULGU-4 `[KOZMETIK]` KAPANDI.** K6 yorumu "uc noktanin ucu de buraya baglandi" diyordu;
  olculdu ki acilis betigi (`index.html:50`, `<head>` icinde) yardimciyi CAGIRAMAZ - o blok
  fonksiyon tanimlarindan ONCE kosar. Yorum kapsami dogru yazacak sekilde duzeltildi.

## MF-3 / K4 TELAFISININ CANLI TEYIDI

K5 olcum turunda misafir checkout'a UC gecersiz istek gonderildi (telefon "dfg", uc dilde)
ve **UCU DE 400** aldi. Sonrasinda `mf4k5.%` desenli musteri sayisi **0**, `MAX(customers.id)`
**158** ve `MAX(orders.id)` **286** DEGISMEDI. Yani MANTIK-FIX-3'un K4 telafi silmesi
(basarisiz sipariste yeni yazilan musteri + adresin geri alinmasi) **CANLI OLARAK CALISTI**
ve bu tur hicbir yetim kayit birakmadi.

## RIG KOR NOKTASI - KAYDA IKI EKLEME

Dalga 4'ten beri kayitli olan "harness compositing yapmiyor" siniri bu turda IKI YENI
bicimde karsimiza cikti:

1. **CSS TRANSITION ILERLEMIYOR.** K6'nin mobil olcumunde `.filter-side` elemanina `.open`
   sinifi eklendi ve transform **DEGISMEDI** (700 ms beklendigi halde). Sebep: `requestAnimationFrame`
   ateslemedigi icin `transition:transform .32s` hic ilerlemiyor. `transition:none !important`
   ile tekrarlanarak dogru degerler alindi (AR kapali +343.2, acik `none`).
   **KURAL: gecise bagli hicbir geometri olcumu DOGRUDAN alinmaz.**
2. **JS/DOM KOSUCUSU YOK.** Bu dalganin ALTI pini de KAYNAK SOZLESMESI pinidir; tarayici
   semantigi (hit-test, CSS ozgullugu, computed style, `elementFromPoint`) CI'da
   pinlenemiyor. Davranis kaniti YALNIZCA muhurdeki tarayici olcumleridir. Dalga 4'ten beri
   acik kalem (yeni bagimlilik + `dependency-scan` kapsami).

## CC HATALARI (8)

```
 1 K1 ilk tasarimim old_price'i tumden birakiyordu; kod yorumu urun 123'u isaret etti, DB
   ile dogrulandi (old_price dolu TAM 2) ve bicim duzeltildi - KOD YAZILMADAN once
 2 `#/urun/123` "Sayfa Bulunamadi" verdi; urun bellekte YOKTU, kusur DEGILDI
 3 K3 SONRA olcumunde PRODUCTS=0 cikti - API kapaliydi (P-V2 build'i icin durdurulmustu);
   konsol ERR_CONNECTION_REFUSED gosterdi, tur tekrarlandi
 4 P-V5 dis kontrolunde perl yorum ekleyip C# argumanini bozdu, BUILD 2 HATA -> tur
   GECERSIZ sayildi, yorumsuz tekrarlandi (kuralin (b) adimi yakaladi)
 5 K6 mobil olcumunde `.open` transform DEGISMEDI - rigin CSS-transition kor noktasi
   (D'nin kaydi); `transition:none` ile tekrarlandi
 6 MK-4b duzeltmesinde yorum blogunu boldum (`*/` erken kapandi) - hemen fark edildi
 7 Release build'de yanlis cozum adi (Divisima.sln yok, Divisima-Backend.sln)
 8 Edit'te Unicode kacis eslesmeleri iki kez tutmadi (2501/2367) - kucuk parcalara bolundu
```

**8. HATA KACIS-KAYBI AILESINE GIRMEZ - OLCULDU.** O vakada kaynak dosyada `'⌂'`
KACIS OLARAK yazili, ben gercek karakteri (⌂) aradim: **kayip yok, eslesme bicimi farki**.
Ailenin sayaci `git log -S` ile olculdu: `"KACIS-KAYBI AILESI - DORDUNCU ORNEK"` 1 commit ·
`"... ALTINCI ORNEK"` 1 commit (`a5add91`) · `"... BESINCI ORNEK"` ve `"... YEDINCI ORNEK"`
**0 commit** (NEG kontrol `ZZZINCI` 0). **Sayac ALTINCI'da KALIR.**
**KAYIT:** MK-4b denetcisinin MUT-3b turunda gercek bir kacis-kaybi yasandi (`sed` ters bolu
kacisi yuzunden mutasyon dosyaya INMEDI ve test yesil dondu) - kuralin (a) adimi yakaladi;
o AJANIN kaydidir, ana akisin degil.

## PUSH TURUNUN EK CC HATASI (1)

**YONLENDIRME SIRASI.** `dotnet test ... 2>&1 > dosya` yazildi; bu, stderr'i ESKI stdout'a
(terminale) baglar ve stdout'u dosyaya yonlendirir - yani `[FAIL]` satirlari log dosyasina
GIRMEZ. TUR1'de kirilan adlar yalniz EKRAN CIKTISINDAN okunabildi. Dogrusu `> dosya 2>&1`;
TUR2 ve TUR3 duzeltilmis bicimde kosuldu ve adlar log'dan `comm`/`diff` ile karsilastirildi.

## KURGU KAYIT ENVANTERI

**UYGULAMA FAZI HICBIR YENI KAYIT URETMEDI.** MAX'lar zeminle AYNI: musteri **158** ·
siparis **286** · adres **118** · fatura **119**. `id > 210` Pending kumesi **10** (zemindeki
10 - degismedi). Omer'in hesabi (musteri 10) ve kabul turu kayitlari KULLANILMADI.

**TEK YAZMA - URETIM YOLUNDAN:** K2 kanitini almak icin musteri 102'nin
(`mfix1.once@example.com`, MANTIK-FIX-1 kurgusu) sifresi **uretim yolundan** sifirlandi:
`POST /api/auth/forgot-password` 200 -> jeton `customers.password_reset_token`'dan okundu ->
`POST /api/auth/reset-password` 200 -> `POST /api/auth/login` 200. Elle SQL YOK. (Kurgu sifre
degeri muhre GIRMEZ; "politikaya uygun kurgu" olarak anilir.)

**MK-3 UCLUSU BIREBIR TUTTU (ureten ifadeleriyle):**
```
SELECT COUNT(*), MAX(id) FROM orders WHERE customer_id = 10;              -> 38 / 211
SELECT COUNT(*), MIN(id), MAX(id), SUM(CAST(id AS bigint))
  FROM orders WHERE status = 0 AND id <= 210;                             -> 35 / 9 / 210 / 3837
SELECT COUNT(*), SUM(total_price), STRING_AGG(CAST(status AS varchar),',')
  FROM orders WHERE customer_id = 74 AND id BETWEEN 234 AND 237;          -> 4 / 4698,60 / 0,0,1,1
```

Denetcinin izole veritabani (`DivisimaMf4Denetim`) DROP edildi; worktree kaldirildi
(`git worktree list` tek satir); calisma agaci **0 satir**.

## BILINCLI TAVIZLER ve ACIK KALEMLER

- `share_copied` MEVCUT anahtar yeniden kullanildi -> metin "Baglanti kopyalandi" ->
  "Link kopyalandi!" DEGISTI (tek kaynak korundu, sozluk sismedi).
- **Dil degisimi UC yuzeyi TAZELEMIYOR** (sekme basligi · a11y paneli · komut paleti) -
  ONCEDEN DE BOYLEYDI (ayni ternary'ler de cizim aninda degerlendiriliyordu), K4 ne yaratti
  ne cozdu. Rota degisiminde dogru dil geliyor. -> VITRIN-KALAN.
- K6 kozmetik 3 (`.sup-panel` transform-origin · `.sup-msg` radius · `.achip` padding)
  DUZELTILMEDI. -> VITRIN-KALAN.
- K7: mesaj metni ve `NotEmpty` kullanimi dort sitede AYRISIYOR; pin YALNIZ REGEX uzerinde
  kuruldu (E'nin uyarisi: "birebir ayni" diyen bir pin ILK KOSUMDA kirmizi verirdi).
  -> VITRIN-KALAN.
- BULGU-3'un kalan bes satiri ve `POPULAR_L`in AR'da Turkce arama etiketleri gostermesi
  (denetcinin yan bulgusu) -> VITRIN-KALAN.

## DOKUNULMAYANLAR

Tum sunucu sozlesmeleri · fiyat/indirim URETIM mantigi · `OrderManager` · `InvoiceManager` ·
`GuestCheckoutManager` sunucu tarafi · kupon/kredi mantigi · `RequestLocalization` ·
`CulturePinTests` · `b_toplam` anahtarinin DEGERI · D-YAN kayitlari · mevcut Pending kumesi ·
Omer'in hesaplari · **`Divisima.Bussiness/ValidationRules`** (K7 YALNIZ OKUDU - denetci
bagimsiz dogruladi: 0 degisiklik; POZ kontrol `frontend/*` 2).

`git diff 6e2b06d..e3e7f94 --name-only` -> **UC dosya**: `FrontendDokunmaHedefiTests.cs` ·
`frontend/api-bridge.js` · `frontend/index.html`.

## SUIT

| | ONCE (zemin 6e2b06d) | SONRA (e3e7f94) |
|---|---|---|
| `Category=Sql` | 356 / 356 | **356 / 356 DEGISMEDI** (yeni SQL sinifi ACILMADI) |
| Tam suit | 598 (595 gecti / 3) | **604 (601 gecti / 3)** = **+6** (alti yeni pin) |
| Release | 0 hata | 0 hata |
| whitespace + style | exit 0 | exit 0 |
| CI | - | `Testler + coverage` SUCCESS -> Docker'li ucluyle birlikte **604/604** |

Kirilan 3'un UCU DE tabandaki Docker'li `OrderEndpointTests` (yerelde Docker kapali).

## VITRIN-KALAN (YENI KUYRUK KALEMI - TEK LISTE)

```
1. i18n TAZELEME UCLUSU - dil degisimi sekme basligini, a11y panelini ve komut paletini
   tazelemiyor (uc yuzey de "bir kez kur" kalibinda). ONCEDEN DE BOYLEYDI.
2. K6 KOZMETIK 3 - .sup-panel transform-origin · .sup-msg radius · .achip/.pwa-pill padding
3. K7 MESAJ/NotEmpty AYRISMASI - dort validator'da regex AYNI ama mesaj metni ve NotEmpty
   kullanimi FARKLI ("Gecerli bir telefon girin." vs "Gecerli telefon giriniz.")
4. BULGU-3 KALAN BES SATIR - fmtDay · couponUI · showLegal · accStatus · accOrders
5. POPULAR_L - AR'da Turkce arama etiketleri (`POPULAR_L[lang]||POPULAR_L.tr`)
6. showLegal CMS - AR kullanici sozlesme metnini Turkce goruyor; sebep SOZLUK DEGIL,
   `contents` tablosunda AR karsiliginin olmamasi (icerik isi, i18n isi degil)
```

## DEVIR ID'LERI

```
DV1  request_id REPLAY YOLU K4 TELAFISINDEN KACIYOR [VERI-BOZAN] - GuestCheckoutManager:263
     telafi kosulu `!siparisSonuc.Success`; replay dali Success=TRUE donduruyor -> telafi
     ATESLEMIYOR. Yetim musteri+adres VE o e-postanin misafir checkout'ta KALICI 409'u.
     -> GUVENLIK-FIX'in BAS KALEMI
DV2  Yetim musteri 153 ve 155 + siparis 270-275 (bozuk adresli, R-H5 ONCE kaniti) -> D-YAN
DV3  429 UC AYRI KAYNAKTAN (cop-misafir guard'i · Redis rate-limit · yerlesik limiter -
     sonuncusunun GOVDESI BOS) + 500 yolunun RFC 7807 zarfinda `message` alani YOK
     -> GUVENLIK-AV-1 girdisi
DV4  Suzgec sayaci 9 -> 8; MANTIK-FIX-1'in "8 -> 2" kaydi BAYAT (git show 4d8d4c2 ile
     dogrulandi: o gun `old` YALNIZ old_price'tan geliyordu ve olcum O KODLA tutarliydi)
DV5  "Ayni kuralin ikinci kopyasi" ailesinin 6. vakasi (K5'in yuttugu iki esleme kopyasi)
     + merkez payi: tekil satir / bayat numara kayitlari
DV6  index.html:50 BILINCLI-'ltr' arkeolojisi - `git log -S "setAttribute('dir','rtl')"`
     HICBIR COMMIT bulmuyor; hem RTL CSS'i hem 'ltr' sabitlemesi ILK COMMIT'ten (df91863)
     yan yana duruyor. Yazar RTL destegini YAZMIS ama ACMAMIS.
```

## KUYRUK

```
1. ARSIV-1 (docs-only; tarif merkezden)                        <- SIRADA
2. GUVENLIK-AV-1 (ultracode pilotu)
3. GUVENLIK-FIX (DV1 bas kalem)
4. VITRIN-KALAN
5. FIX-1B
6. ADMIN-FIX
7. IMPORT-FIX
8. FIX-1C
9. LOG-FIX
10. FIX-2
11. FIX-3 / B13
```
