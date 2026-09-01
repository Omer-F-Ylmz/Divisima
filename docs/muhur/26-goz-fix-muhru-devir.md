# GOZ-FIX MUHRU ve DEVIR KAYDI (26 Agustos 2026)

**KANIT SHA: `7c6b80d`** - her iki workflow yesil.

```
CI - Build & Test  run 32950126208  event=push  head_sha=7c6b80d  SUCCESS
Security CI        run 32950126207  event=push  head_sha=7c6b80d  SUCCESS
ALTI JOB'DA failure SEVIYELI ANNOTATION: 0
Annotation kume farki (taban 39, a244160): BOS - yeni uyari URETILMEDI
TestDbKurulum 1807 yeniden denemesi: HIC ATESLEMEDI (0) - retry devrede, gerekmedi
```

## GOZ-FIX KAPANDI

GOZ-1 kabul turunun on bir kaleminden **sekizi kapandi**: G1 (izgarada uydurma beden
stogu + yanlis "Son N urun!" kitlik iddiasi), G2 (sekme basliginin "Sayfa Bulunamadi"ya
yapismasi), G3 (`#toast`un dokunus calmasi - M10 sinifi), G4 (misafir odemede iki
secenegin de `disabled`-soluk gorunmesi), O1 (401 alan `cart/add`in yerelde "eklendi"
gibi gorunmesi), O2, O3 (`Failed to fetch`in kullaniciya sizmasi), O4 (bayat odeme
ozeti), O5 (sepeti bosaltma yolu - eklendi). **G5** (`search/products` camelCase zarfi -
`PagedResult` sizintisinin UCUNCU ornegi) ve **G6** (44x44 alti 99 dokunma hedefi) ACIK.

**O2'NIN GERCEK KOK SEBEBI - MERKEZIN HIPOTEZI OLCUMLE CURUDU.** Hipotez
"`payment/initialize` de 401 aliyor" idi; canli olculdu: **401 YOK** (`order/place` 201,
`initialize` 200, `coErr` bos, siparis Pending kaldi). Gercek sebep
`IyzicoClient.cs:84` - mock modda `CheckoutFormContent` olarak **bir HTML YORUMU**
donuyor; eski kod onu truthy gorup gomuyor ve `embedCheckoutForm` **kosulsuz**
`scrollIntoView` cagiriyordu (sayfa en alta zipliyor, gorunur hata yok).

PINLER: `KAYNAK_SOZLESMESI_IzgaraStogu_PRNG_ile_URETILMEZ_...` (P1) ve
`KAYNAK_SOZLESMESI_OdemeGomme_GORUNUR_ICERIK_YOKSA_Kaydirmaz_...` (P2). **Ikisi de
DURUST ETIKETLI kaynak-sozlesmesi pinidir**, davranis pini DEGILDIR; davranis kaniti
tarayici once/sonra olcumleridir.

## INSAN KABUL TURU ERTELENDI - TEK BIRLESIK TUR

Omer'in muhur turu **VITRIN-FIX-2 SONRASINA** birakildi ve M1..M9 ile **TEK BIRLESIK
TUR** olarak kosulacak. Gerekce: kuyrukta bekleyen yasal bloker (D-1 sahte yorumlar)
insan turunu bekleyemez; iki ayri tur kosmak ayni ekranlari iki kez gezdirirdi.

## KALICI KURAL - SINIFLANDIRICI ONCE BILINEN GIRDIYLE SINANIR

**Bir siniflandirici / karsilastirma / suzgec ifadesi, KARAR icin kullanilmadan once
BILINEN-POZITIF ve BILINEN-NEGATIF bir girdiyle sinanir.** Bu, izleyici cikis kosulu
kurallarinin genellesmis halidir - bedeli UC KEZ odendi:

| Vaka | Ifade | Zarar |
|---|---|---|
| FIX-1A / FIX-1C | `deleted_%@...` | KALDIRILAN ikizin bicimi `deleted-{Guid:N}@anonymized.local` (TIRE) idi; musteri 71 "yarim silinmis" raporlanacakti |
| FIX-1A | `$true -eq '[REDACTED]'` | payload sekli yerine tarih esigi varsayildi; "19 satir, 0 redakte" yaniltici cikti |
| GOZ-2 | `head -c 300` | `"success"` kesigin otesinde kaldi, 9 uc yanlislikla "DIGER" zarf sayildi |

**KARDES KURAL: sema/kolon ve rota/alan adlari KAYNAKTAN OKUNUR, TAHMIN EDILMEZ.**
Iki kez bedeli odendi: `product_reviews.is_approved` (gercek ad `review_status`, byte
0/1/2) ve `gift_cards.remaining_amount` (gercek ad `balance`). Ayni aile:
"YAPILMIS GORUNUP CALISMAYAN DUZELTME" - mekanizmanin CALISTIGI, sonucu bilinen bir
girdiyle BIR KEZ gozlenir.

## UC SUREC DERSI

- **UYDURULMUS SHA ILE IZLEYICI KURULMAZ.** Push'tan once tahmin edilen bir SHA ile
  kurulan izleyici, gercek SHA farkli oldugu icin SONSUZA KADAR "run yok" der. Izleyici
  **push CIKTISINDAN okunan** SHA ile kurulur ve ilk turda **prefix eslesmesi**
  bilinen bir girdiyle dogrulanir.
- **KALICI SUREC COZUMU: `schtasks /Create /XML` + `InteractiveToken`.** Bu ortamda
  `Start-Process -WindowStyle Hidden`, `Win32_Process.Create` ve `cmd.exe` sarmalayicili
  gorev **OLUYOR** (`schtasks` Last Result `-1073741510` = `STATUS_CONTROL_C_EXIT`;
  `^C` log dosyasina dusuyor). `S4U` gorev tipi **admin ister**. Iki tuzak: `/TR`
  **261 karakter** siniri (uzun scratchpad yolu asiyor - XML sart) ve sema surumu
  (`DisallowStartOnRemoteAppSession` / `UseUnifiedSchedulingEngine` **1.2'de YOK**).
  Gorev adlari: `DivisimaGoz1Api`, `DivisimaGoz1Statik`.
  **BUILD ONCESI GOREV DURDURULUR** - kosan API `Divisima.*.dll`leri kilitler ve build
  `MSB3027` ile duser; **build SONRASI yeniden baslatilir**, aksi halde sonraki olcum
  bayat ikililerle kosar.
- **JS/DOM KOSUCUSU BOSLUGU ACIK KALEM.** GOZ-FIX'in sekiz kaleminin tamami TARAYICI
  once/sonra olcumuyle kanitlandi; CI'da tutulan sey yalniz KAYNAK KOSULU. Dalga 4'ten
  beri acik (yeni bagimlilik + `dependency-scan` kapsami; karar kullanicinin).

## FIX-1B DEVRI (olculdu, UYGULANMADI)

- **F4 - erisim jetonu iptali.** Cozum **kosulsuz `Set`** + `user_sessions`'a **`jti`
  kolonu**. OLCULEN GERCEK: logout istegi **kendi FALSE'unu** ekliyor - yani kayip
  KALICI ve DETERMINISTIK, "arada bir" degil.
- **F8 - step-up sinirsiz tazeleme.** Cozum `authenticated_at` kolonu + **rotasyonda
  kopya** (yeni oturum satiri eskinin `authenticated_at`ini DEVRALIR, tazelemez).
- **C - kara liste ACIK LISTE BIRLESIM DESEN.** `DenetimGizlilik.SirAlanlari` bugun bir
  AD FOTOGRAFI; `*token*` / `*secret*` / `*hash*` / `*salt*` desenleri eklenir.
  Gerekce olculdu: **`two_factor_code` dersi** - adi listede olmayan yeni bir sir alani
  bugun VARSAYILAN OLARAK CIPLAK yazilir.
- **D - SENTETIK YAZMA PINLERI.** `refresh_token` / `device_token`in **yazma anindaki**
  maskesi bugun yalniz `Customer` satirlari uzerinden pinli; `UserSession` /
  `CustomerDevice` ekseninde sentetik yazma ile pinlenir.
- **`MapInboundClaims` BELIRSIZLIGI POZITIF 401 PINIYLE KAPANIR** - claim adi
  esleniyorsa iptal calisir, eslenmiyorsa calismaz; ikisini ayirt eden tek durust kanit
  "iptal edilmis jetonla korumali uc **401**" pinidir.

## FIX-1C DEVRI (olculdu, UYGULANMADI)

- **F5 - `birthdate` sessizce siliniyor.** Kok sebep **PUT-ez semantigi**: gonderilmeyen
  alan varsayilanina duser. Cozum: validator + PATCH semantigi. **KANIT SATIR 1556.**
- **F6 - UC BASLI.** (a) bildirim tercihi ucu `ConsentRecord` YAZMIYOR, (b) **15 rizasiz
  misafir** kaydi var, (c) ozet ile kapi (`MarketingGate`) AYRISIYOR - ekran "acik"
  derken kapi kapali.
- **F7 - capraz hesap cihaz kaydi.** Cozum **DEVRALMA-TEK-SATIR**: token basina TEK satir,
  rebinding TEK TRANSACTION'da `customer_id`yi GUNCELLER (pasifle+ekle DEGIL) +
  `SecurityEvent`. **Migration YOK.** CANLI KANIT: **musteri 66'nin push'u OLU**,
  `customer_devices` kimlik dizisinde **3 tuketilmis ve geri alinmis**.
- **F9 - reddedilen adres istegi cagiranin kendi varsayilanini dusuruyor.** Cozum:
  SIRA (once dogrula, sonra yaz) + transaction. Kanit zinciri: `updated_at IS NULL`
  (guncelleme yolu elenir) + sonrasinda `Added` satiri YOK + `addresses`ta kimlik
  bosluğu YOK (denenmis insert elenir).
- **F13 - soft-delete edilmis adreste `is_default=True` kaliyor.** IKI silme yolunun
  IKISI DE varsayilani dusurur.
- **D-YAN - TEK DEV-VERI TEMIZLIGI.** Eski-ikiz artigi (`deleted-...@anonymized.local`
  bicimli satirlar) + **sifir degerli 3 kupon** (`E2TEST`, `DALGABOLCUM`, `PANELDEN30` -
  tip=Yuzde ama `value=0.00`, Dalga B/B1 alan adi uyusmazligi artigi) TEK temizlik
  isinde ele alinir. **URETIM YOLUYLA, elle SQL YOK.**

## GOZ-2 / HIJYEN KARARLARI

- **ICERIK GUNCELLEME API PROSEDURU checklist'e.** Marka kimligi gunu icin
  `content/update` sablonu (10 sozlesme sayfasinin govdesi panelden degil API'den
  guncellenir; yazma katmani `InputSanitizer`den gecer - E3'te pinli).
- **MODERASYON `approve`/`reject` SABLONU checklist'e** (`review_status` 0/1/2).
- **LOG-FIX KALEMI** - ham PII/jeton loglayan bes satir `KanitMaskesi`nden gecirilir:
  `SmtpMailService.cs:42` ve `:81` (alici e-postasi - **38 canli satir**),
  `IyzicoClient.cs:196` ve `:198` (`token={Token}` ham), `IyzicoPaymentManager.cs:231`.
- **B3-3 - localhost CORS maddesi checklist'e** (uretimde `AllowedOrigins`ta localhost
  kalmamali).
- **B3-5 / B4-6 / B4-7 HAVALE EDILDI.**

## FAZ 2 KARARLARI

- **D-1 SAHTE YORUMLAR = LAUNCH BLOKERI, BU DALGADA** (VITRIN-FIX-2 / F-D1).
- **IMPORT-FIX KAPSAMI SABIT** (gercek katalog gelmeden **SART**): tek transaction +
  on dogrulama + **gercek satir no** + degerli hata mesaji + **uretilen id listesi
  yanitta** + validator birligi (uc ile CSV ayni kurali kullanir).
- **B-2 LAUNCH PRATIGI:** acilis CSV'sinde `sale_price` **BOS**; indirimler import
  SONRASI `update` ile verilir (iki bagimsiz indirim mekanizmasinin acilis gununde
  carpismamasi icin).
- **D-2 BAYRAK MODELI BILINCLI** (degistirilmez).
- **B-6 / C-1 / G5 / B-5 / D-3 -> FIX-2 KUYRUGU.**

## FAZ 3 KARARLARI

- **PARA YOLU SAGLIKLI (olculdu).** Rezerve/onay/serbest zinciri, kupon kilidi + sayac
  turetme, iptal ve basarisiz odeme dallari kaynak duzeyinde tutarli; `product_stocks.
  reserved_quantity` toplami aktif rezervasyon toplamiyla BIREBIR ortusuyor.
- **B13 TASARIMI (uygulanmadi):** **saatlik** job; `Pending` + `payment_type=0` (Online)
  + **24 saatten eski** -> **MEVCUT Pending-iptal yolu** cagrilir (yeni bir iptal yolu
  YAZILMAZ; `OrderManager.cs:714-717` rezervasyonu zaten serbest birakiyor).
  **B13 KORPUSU: 29 Pending** (defterdeki "17" bayatladi).
- **A-1 (giriste sepetin silinmesi) BU DALGADA** - VITRIN-FIX-2 / F-A1.
- **A2 BILINCLI KABUL:** `CartItem`'da fiyat kolonu YOK, fiyat sepette DONMAZ; musteri
  SIPARIS ANINDAKI fiyati oder. Donma noktasi `order_items.unit_price`
  (`OrderManager.cs:312`). Degistirilmez.
- **C3 -> FIX-3 NOTU:** gecersiz kupon SESSIZCE yok sayiliyor (400 donmuyor); musteri
  "kuponum neden uygulanmadi" bilgisini bu uctan ALMIYOR.

## KUYRUK (sirayla)

```
1. VITRIN-FIX-2      (F-D1 sahte yorumlar + F-A1 sepet birlestirme)   <- SU AN
2. Omer BIRLESIK KABUL TURU (M1..M9)
3. FIX-1B            (F4 + F8 zinciri, kara liste desenlesmesi, sentetik yazma pinleri)
4. IMPORT-FIX        (katalog gelisine gore ONE CEKILEBILIR)
5. FIX-1C            (F5 · F6 · F7 · F9 · F13 · D-YAN dev-veri temizligi)
6. LOG-FIX           (bes ham log satiri -> KanitMaskesi)
7. FIX-2             (B-6 · C-1 · G5 · B-5 · D-3)
8. FIX-3 / B13       (kupon geri bildirimi · terk edilmis Pending TTL)
```

---

