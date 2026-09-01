# MANTIK-FIX-2R MUHRU - FATURA KAYITTAN BESLENIR (29 Agustos 2026)

**KOD SHA'LARI: `fb3b7b7` (MIG+K1) · `ae020a0` (K2) · `5081741` (K3) · `c63175b` (K4) ·
`25e723d` (MK-4b denetim duzeltmeleri)** - zemin `a5add91`; BES commit TEK push.
Bu muhur AYRI ve docs-only bir commit'tir; **kendi SHA'sini ve kendi run kimliklerini
ICEREMEZ** (tavuk-yumurta) - muhrun kendi cift yesili MANTIK-FIX-2R raporunda verilir.
MFIX-1'de kurulan kalip.

```
MANTIK-FIX-2R KODU (a5add91..25e723d, BES commit tek push)
  CI - Build & Test  run 33258130671  event=push  head_sha=25e723d  SUCCESS
  Security CI        run 33258130686  event=push  head_sha=25e723d  SUCCESS
ALTI JOB (format-check · build-and-test · dependency-scan · codeql · secret-scan · tests):
  HEPSI SUCCESS; failure SEVIYELI ANNOTATION: 0
ANNOTATION: 39, HEPSI warning. TABAN 39 ile BIREBIR - yol KUMESI de ayni
  (IEntityRepository.cs 24 + EfEntityRepositoryBase.cs 6 + .github 9 = 39).
  4.353 EKLENEN SATIRA RAGMEN YENI UYARI URETILMEDI; annotation yollari ile push
  diff'inin (17 dosya) KESISIMI 0 - komut POZITIF KONTROLLU (diff'ten enjekte edilen
  bir yolla 1 donuyor, yani tarama calisiyor).
format-check UC ZORUNLU ADIM (whitespace + style + "Model ile migration'lar SENKRON mu"):
  UCU DE SUCCESS -> 13. MIGRATION'IN TEMIZ DB'YE UYGULANDIGININ CI KANITI BUDUR
  (denetcinin "temiz DB'ye uygulanmadi" kor noktasini CI KAPATTI).
TestDbKurulum 1807 ozeti - IKI test job'inda da adim SUCCESS ve annotation metni:
  "TestDbKurulum: 1807 yeniden denemesi bu kosumda HIC ATESLEMEDI (0) - retry devrede,
   gerekmedi."
Gitleaks (secret taramasi): SUCCESS - bolum 7 kurali geregi ADIM SONUCUNDAN okundu
TESHIS adimi: iki job'da da skipped
CI tam suit: 587/587 (Sql 347) - yereldeki uc Docker kapili kirmizi kosucuda YESIL
```

**ORTAM UYARISI (Divisima Eki 2.3):** olcumler `goz1` duzeneginde kosuldu; API surecinin
komut satirinda BES ARGUMAN var ve **BUNLAR URUN VARSAYILANI DEGILDIR** -
`--Iyzico:UseRealSdk=false` · `--AdminSeed:Enabled=false` · `--BackgroundJobs:Enabled=false`
· `--RateLimit:AuthPermitLimit=100` · `--MailSettings:Host=`.
Rig artefakti bulgu SANILMADI: K3'te `coupon_usages` 0 kaldi cunku kullanim satirini onay
olayinin ISLEYICISI yazar ve rig'de arka plan isleri KAPALI.

## IKI "DUR" - BELIRSIZLIKTE DURMA KURALININ SAHADA ISLEYISI

Dalga IKI KEZ durdu ve ikisinde de karar merkezden geldi; ikisi de kod yazilmadan ONCE.

**(1) ON OLCUMUN SEKIZ CELISKISI.** A-F fan-out'u tarifle CELISEN sekiz nokta cikardi
(kargo kaleminin urun kimligi, bedava kargoda kalem yazilip yazilmayacagi, gorunur
faturanin kaynagi, govdenin hangi katmanda kurulacagi, "sabit oran 0 gecis" olcutunun
kapsami, KDV kirilimi, K4'un pin sekli, D-YAN sayisi). Yorumla cozulmedi; MANTIK-FIX-2R
REVIZE TARIFI onun uzerine yazildi (C1-C8 + D1 + D2).

**(2) C8 <-> SINIRLAR TARIF-ICI CELISKISI - MERKEZ ONCUL HATASI (ACIKCA).** Revize tarif
K2'yi baslatirken IKI maddesi birbiriyle carpisti: C8 bos-durum sozlesmesini ve iki pinin
guncellenmesini ISTIYORDU, ama SINIRLAR bolumu ayni pinlerin bulundugu bacaga
DOKUNULMAMASINI soyluyordu. **Merkez onculunun de yanlis olabilecegi** (sart-2 dersi) bir
kez daha dogrulandi; EK-2 ile cozuldu: **"SOZLESME KORUNUR, SATIR DEGIL"** - korunan sey
kultur-sizintisi YASAGI, degisen sey OLCUM BICIMI.

## MERKEZ KARARLARI (C1-C8 + D1/D2 + E1-E7) - GEREKCELERIYLE

| Karar | Icerik | Gerekce |
|---|---|---|
| **C1** | TEK migration; `product_id` NULLABLE. **Sahte "Kargo" urunu YASAK** | katalogda var olmayan bir urun uydurmak, faturayi urun kimligi uzerinden okuyan HER tuketiciyi yaniltirdi |
| **D1** | Bedava kargoda da **0,00 tutarli kalem YAZILIR** - her faturada TAM 1 kargo kalemi | kalem sayisi kosula gore degisirse mutabakat sorgusu her faturada AYRI yazilmak zorunda kalir |
| **C2** | Gorunur fatura **KAYITTAN** beslenir | ekran belgeyi YENIDEN HESAPLIYORDU; kayit ile ekran ayrisabilir durumdaydi |
| **C3** | Govde **ISTEMCIYE** tasinir; `RequestLocalization` KAPALI kalir; `CulturePinTests` korunur | Sprint 8 madde 13'te dil acma OLCEREK reddedilmisti - o karar bu dalgada ACILMADI |
| **C4** | "sabit oran 0 gecis" olcutu YALNIZ **GORUNTULEME** yolunu kapsar | uretim kaynagi (`InvoiceManager.cs:24` + `EInvoice:KdvRate`) ayri bir karardir, sayima girmez |
| **C5** | KDV **tek oran DEGIL**, oran BAZINDA kirilim | agirlikli ortalama ekrana oran olarak cikarsa Turkiye'de VAR OLMAYAN bir deger beyan edilir (canli ornek: fatura 55 -> %14,16) |
| **C6** | K4 pini **URETIM ANI** sozlesmesidir, global DB esitligi DEGIL | tarihsel artiklar (2-8) bugunku kodun uretemeyecegi bir halde; global esitlik pini onlari "kusur" sayardi |
| **C7** | D-YAN 64 -> **70 + 15** | kargo ailesi 70; indirim-payi AYRI aile 15 |
| **C8** | Bos-durum sozlesmesi + iki pin guncellenir | belge UYDURULMAZ |
| **D2** | `invoices` krediyi KAYDETMEZ (statuko) | kredi bir ODEME ARACIDIR, fiyat indirimi degil - matrahi dusurmez |
| **E1** | Uc **YERINDE** evrilir; paralel uc ACILMAZ | iki uc = iki sozlesme = kacinilmaz ayrisma |
| **E2** | R-F2b once SINIF olculur, sonra kosulur | - |
| **E3** | MK-6 mutasyon zorunlulugu **YALNIZ P-F1 ve P-F4** | butce |
| **E4** | Kargo etiketi SOZLUKTEN; DB'deki ad ekrana **HAM BASILMAZ** | sunucu `product_name`'i NULL gonderir -> istemci adabi degil, **YAPISAL** |
| **E5** | "SOZLESME KORUNUR, SATIR DEGIL": uc **SAYI BICIMLEMEZ** | kultur sizintisi yasagi korunur, olcum bicimi degisir |
| **E6** | Kanit **FIKSTURLE**; iptal yolu **OLCULEREK** secilir; elle SQL YASAK; siparis 28 CC'ye KAPALI | gercek veriye dokunmadan kanit uretmek |
| **E7** | Adres sapmasi dusuruldu + lokal checkpoint commit'leri | - |

## MIG - `20260829010821_KargoKalemiIcinProductIdNullable`

TEK `AlterColumn` (nullable:true), **FK KORUNDU**. Dort kriter:
```
(1) migration TEK islem mi          -> EVET (tek AlterColumn)
(2) FK duruyor mu                   -> canli: FK_invoice_items_product_id, NO_ACTION
(3) kolon gercekten nullable mi     -> canli: is_nullable = 1
(4) sema dosyasi yeniden uretildi mi-> 01_schema.sql 3036 -> 3067 satir, baslik korundu
```
**AYIRT-ETME KANITI (kapi deseni):** `dotnet ef migrations has-pending-model-changes`
migration UYGULANMADAN **exit 1**, uygulandiktan sonra **exit 0**. Yani kapi kendi
kendine dogru diyen bir kontrol degil - AYNI komut iki durumu AYIRT EDIYOR.
`InvoiceItem.cs`'e KARGO SOZLESMESI yorum olarak yazildi: `product_id` NULL ise bu kalem
bir URUN DEGIL, siparisin KARGO BEDELIDIR.

## K1 - KARGO AYRI FATURA KALEMI

```
ONCE   kargo urun kalemine GOMULU: adet x birim <> satir toplami. 64 satirda fark TAM
       shipping_cost. Fatura kalemi MALI BEYANDIR; kargo urun bedeli gibi gorunuyordu.
SONRA  her faturada TAM 1 kargo kalemi (D1): product_id NULL, quantity 1,
       unit_price = line_total = shipping_cost; matrah/KDV ayri hesaplanir.
       Urun brutu = total - kargo; yuvarlama artigi SON URUN kalemine yazilir.
R-F1a  siparis 264: urun 499,80 + kargo 49,90 -> kalem 2, toplam 549,70 = invoices.total
R-F1b  ESIK USTU siparis 267 (etkin fiyat 249,90 x9 = 2.249,10 >= 2000) -> kargo 0,00,
       kalem YINE YAZILDI (D1 sozlesmesi bedava kargoda da gecerli)
R-F1c  e-fatura satirlari (EInvoiceLine) kargoyu AYRI satir olarak tasiyor
```
**KENDI HATAM:** R-F1b'yi ilk denemede LISTE fiyatiyla (299,90) hesapladim; ETKIN fiyat
249,90 idi ve siparis 265 esigi GECMEDI. Siparis 267 ile duzeltildi.

## K2 - GORUNUR FATURA KAYITTAN BESLENIR

```
ONCE   ekran invoices/invoice_items'i HIC OKUMUYOR, belgeyi SIPARIS verisinden YENIDEN
       HESAPLIYOR ve KDV'yi sabit %20 basiyordu. DB'deki gercek dagilim:
       0.20 -> 73 · 0.10 -> 11 · 0.1416 -> 1  =>  12 oran + 7 iptal artigi = 19 sapma
SONRA  yeni InvoiceViewResponseDto; uc KAYITTAN besleniyor. Sahiplik sozlesmesi KORUNDU.
R-F2a  faturasiz siparis -> has_invoice=false; belge UYDURULMAZ (C8)
R-F2b  IPTALLI fatura -> invoice_is_cancelled=true. Kanit FIKSTURLE uretildi (E6);
       iptali gerceklestiren yol OLCULEREK secildi (admin durum yolu), ELLE SQL YOK,
       siparis 28 CC'ye KAPALI kaldi
R-F2c  karisik oranli sepet -> kirilim 2 grup (0.10 + 0.20), toplamlar belgeyle tutarli
R-F2d  E5: uc SAYI BICIMLEMEZ - HAM decimal doner; kultur sizintisi YAPISAL olarak imkansiz
```

## K3 - FATURA GOVDESI ISTEMCIDE, UC DILDE

```
ONCE   govde %100 SUNUCU HTML'i: 17 TR dizge, lang="tr", sabit " TL", dd.MM.yyyy.
       dvsLocale bir FRONTEND fonksiyonu oldugu icin o dizeye ERISEMIYORDU.
SONRA  faturaGovdesiniCiz(kutu, d) - DOM ile kurulur; etiketler SOZLUKTEN (12 yeni anahtar
       x 3 dil), para/tarih dvsLocale uzerinden, DB metni textContent ile.
       Eski HTML-enjeksiyon yolu OLDU; guvenliYaz'in KENDISI korundu (sozlesme sayfalari).
R-F3a  siparis 268 uc dilde: TR "Bu fatura iptal edilmistir" / 549,70 TL / 29 Agustos 2026
       EN "This invoice has been cancelled" / 549.70 TL / 29 August 2026
       AR (Arapca iptal metni) / 549.70 TL / Arapca ay adi
R-F3b  SIZINTI DEDEKTORU: EN 0 / AR 0. POZITIF KONTROL: ayni dedektor TR'de 11 satir
       buluyor -> dedektor CALISIYOR. Urun adi DB icerigidir ve ISTISNA.
R-F3c  bos durum uc dilde cizildi.
```
**R-F3c'NIN DURUST SINIRI:** uctan uca faturasiz siparis **URETILEMEDI** - musteri 119'un
BES siparisinin BESI DE faturaliydi; online yol Pending uretirdi (**YASAK**), fatura silmek
de YASAK. Bu yuzden TASIMA katmani **GERCEK bos-durum yanitiyla** beslendi; renderer ve
modal akisi GERCEK. Uc seviyesindeki bos-durum sozlesmesi **P-F2a ile AYRICA kanitli**.

## K4 - BRUT SOZLESMESI PINI

C6 geregi GLOBAL DB ESITLIGI PIN DEGIL; pin **URETIM ANI** sozlesmesidir: fatura BRUTTEN
kesilir, magaza kredisi MATRAHI DUSURMEZ. Fikstur 1.000,00 urun + 49,90 kargo = 1.049,90
brut, 200,00 kredi -> `invoice.total` **1.049,90** (kredi dusulseydi 849,90 cikardi).
**KREDI TASIYAN FIKSTUR EKLENDI** - bu sinifta kredi tasiyan tek bir kurgu YOKTU, kredi
olmadan pin **VAKUM** olurdu.

## SIPARIS 268 - ONCE / SONRA (iptal yolunun karsit kontrolu)

```
                       ONCE (Confirmed)      SONRA (admin yolundan IPTAL, durum 5)
orders.total_price     549,70                549,70    DEGISMEDI
orders.shipping_cost    49,90                 49,90    DEGISMEDI
invoices durum         1 (Sent)              3 (iptal)
ekran                  normal belge          "Bu fatura iptal edilmistir" (uc dilde)
```
Bu tablo ayni zamanda **ACIK OLCUM (1)**'in karsit kontroludur: ADMIN durum-degistirme
yolu tutarlari **SIFIRLAMIYOR**.

## MK-4b KAPANIS DENETIMI - BES BULGU

Denetci KENDI worktree'sinde kostu; **MK-4a beyani TUTTU** (pwd `.../mf2r-denetim`,
HEAD `c63175b`) ve ampirik izolasyon kaniti verdi. SONUC **UYUMSUZ (DAR)**: kural-uyum
UYUMLU, kapsam sizintisi YOK, uc gercek + iki kozmetik bulgu.

**B1 `[MANTIK]` - SDP'NIN DEGER KANITI: K2'NIN ACILDIGI KUSURUN AYNI SINIFI, DALGANIN
KENDI CIKTISINDA.** D1 geregi bedava kargoda da kalem yazilir (tutar 0,00) ve K1 onu
KOSULSUZ `TaxRate` ile damgalar. Kirilim oran BAZINDA gruplayinca, urunleri %10 olan bir
sipariste ekrana **"KDV %20 (Matrah 0,00 TL) - 0,00 TL"** satiri girerdi: o siparis icin
VAR OLMAYAN bir oran BEYAN EDILIRDI.
**LATENT OLCUMU:** bugun tasiyici fatura **0 satir** (bedava kargolu + urun orani != 0.20
bilesimi mevcut veride YOK) -> **LATENT**, yeni kesilecek faturada **AKTIF**.
**COZUM KATMANI:** suzgec **KAYITTA DEGIL GORUNTULEMEDE** - D1 kargo kalemi AYNEN durur.

**B2 `[pin boslugu]`** P-F3'un `"innerHTML = "` olcutu **BOSLUKSUZ** bicimi kaciriyordu;
denetci `ad.innerHTML=...` mutasyonuyla gosterdi (**25/25 YESIL**). Olcut bosluk-ayiklanmis
karsilastirmaya cevrildi -> ayni mutasyon artik **TAM 1 ISIMLI KIRMIZI**.

**B3 `[pin boslugu]`** P-F3 renderer'in **CAGRILDIGINI** olcmuyordu; cagri yeri
`guvenliYaz(kutu, d.html, ...)` ile degistirilince K3'un TAMAMI bypass oluyor, renderer
OLU KODA doniyor ve pin YESIL kaliyordu (**25/25 YESIL**). Fatura modali govdesi taranir
hale getirildi -> **TAM 1 ISIMLI KIRMIZI**. **KAPSAM DAR:** `guvenliYaz(kutu, ...)`
sozlesme sayfalarinda MESRU kullaniliyor (`api-bridge.js:3186`), bu yuzden yasak DOSYA
GENELINE degil MODALA konuldu. Depoda **MFIX-3b/M4** ile AYNI bosluk sinifi.

**B4/B5 `[kozmetik]`** `api-client.js` bayat yorumu ("uc text/html doner, JSON DEGIL")
K2 gercegine cevrildi · olu sozluk anahtari `b_fatura_gosterilemedi` (2 tanim / 0 cagiran)
T ve AR'dan kaldirildi; **DIFF KANITI:** virgul-bazli karsilastirmada `index.html`'de
SADECE o iki parca gitti, baska satir YOK.

## PIN TABLOSU - 9 YENI + 7 GUNCELLENEN

**YENI (9):**
```
P-F1   Sql  kargo AYRI kalem; adet x birim = satir toplami
P-F1b  Sql  bedava kargoda da TAM 1 kalem (D1)
P-F2a  Sql  bos durum: belge UYDURULMAZ
P-F2b  Sql  iptal isareti KAYITTAN
P-F2c  Sql  oran BAZINDA kirilim, tek oran DEGIL
P-F2d  Sql  uc SAYI BICIMLEMEZ (ham decimal)
P-F3   -    govde istemcide, uc dilde, DB metni textContent (KAYNAK SOZLESMESI - durust etiket)
P-F4   Sql  fatura BRUTTEN; kredi matrahi DUSURMEZ (URETIM ANI sozlesmesi - C6)
P-F5   Sql  sifir katkili grup KIRILIMDA gorunmez, KALEM kayitta KALIR (B1)
```
**GUNCELLENEN (7):**
- **`InvoiceLineVatTests` - BES pin, PREMIS DARALTMASI.** Eski premis "kargo urun kalemine
  gomulu" idi; K1 sonrasi o premis YANLIS bir sozlesmeyi savunurdu. Assert EKSENLERI
  (adet x birim = satir toplami; kalem toplami = belge brutu) **DEGISMEDI**.
- **`CulturePinTests` fatura bacagi - AD DEGISIKLIGIYLE evrildi:**
  `FaturaUcu_SAYI_BICIMLEMEZ_HAM_DEGER_Doner_KulturSizintisi_YAPISAL`.
  **KORUNAN SOZLESME:** kultur sizintisi yasagi. **DEGISEN:** olcum bicimi - uc artik HTML
  degil ham decimal donduruyor, dolayisiyla "bicimlenmis dizge yok" iddiasi hem tr hem
  invariant bicim icin assert ediliyor. Fatura DISI bacaklara **DOKUNULMADI** (denetci
  bagimsiz dogruladi).
- **`ResultOverloadPinTests`** - fatura bacagi K2 sozlesmesine uyduruldu (`data.id`).

## MUTASYONLAR - ZAMAN CIZGISIYLE

```
K1 ANI    P-F1 zorunlu mutasyonu (E3)  -> TAM 1 ISIMLI KIRMIZI
KAPANIS   M1a (kargo yeniden gomulur)  -> 6 KIRMIZI, AILE ICI: olculen once-durum
          (1149.90) BIREBIR uretildi. Bu, GUNCELLENEN BES PININ EKSENI KORUDUGUNUN
          kanitidir - premis daraldi ama olctukleri sey ayni kaldi.
K2        M2a                          -> TAM 1 LOKALIZE
K3        M3a                          -> TAM 1 LOKALIZE
K4 ANI    P-F4 zorunlu mutasyonu (E3, IKINCI ve SON) - krediyi matraha dusur
                                       -> TAM 1 ISIMLI KIRMIZI
MK-4b     M3c (bosluksuz innerHTML)    -> TAM 1 (denetcide 25/25 YESILDI)
MK-4b     M3d (renderer bypass)        -> TAM 1 (denetcide 25/25 YESILDI)
MK-4b     M-B1 (sifir-grup suzgeci)    -> ISIMLI KIRMIZI, 1/12 lokalize
```
Her mutasyonda (a) dosyada iz, (b) TEMIZ BUILD hata sayisi, (c) kirmizi yoksa ONCE
"mutasyon uygulanmadi" suphesi elendi. Hepsi geri alindi; iz 0.

## SUIT - 339 -> 347 / 578 -> 587

```
                       Category=Sql    TAM SUIT
zemin a5add91              339           578
MIG+K1                     341           580
K2                         345           584
K3                         345           585
K4                         346           586
MK-4b                      347           587
FINAL                      347/347       587 / 584 basarili / 3 basarisiz
```
**9 pin eklendi: 8'i Sql, 1'i (P-F3, sifir-DDL sinif) degil.**
Kirilan UC, TABANDAKI Docker kapili `OrderEndpointTests` - adlar HAM logdan:
`PlaceOrder_ConcurrentRequests_NoOverselling` ·
`PlaceOrder_InsufficientStock_Returns400_And_NoPartialData` ·
`PlaceOrder_ValidCart_Returns201_And_DecrementsStock`.
Release **0 Hata** · whitespace **exit 0** · style **exit 0**.

## KURGU ENVANTERI (D-YAN'a)

Musteri **118** (`mf2.k1admin`, `user_type=1`) · **119** (`mf2.k1musteri`) · adres **77** ·
siparis **264, 265, 266, 267** (COD/Confirmed) + **268** (Confirmed -> URETIM yolundan
IPTAL, durum 5) · fatura **97-101** (101 durum 3).
**MAX 119 / 268 / 77 / 101.** **YENI PENDING: 0.**
Kupon `MFXK1KARGO` **yeniden kullanildi** (yeni kupon acilmadi).
**Omer'in hesaplari KULLANILMADI.**
Denetci ve P-F5 kosumlari `DivisimaCiTest`te kaldi; `DivisimaDb` envanterine EKLEME YOK.

## CC'NIN HATALARI (10 KALEM + IKI SUREC NOTU)

```
 1 PLAN satiri ajanlar dagitildiktan SONRA yazildi (SDP 1.2 sira ihlali).
 2 API sureci "Divisima.API.exe" diye arandi; gercek ad dotnet.exe (siniflandirici hatasi).
 3 dotnet ef --no-build ile kosuldu -> migration derlenmis derlemede YOKTU; "already up to
   date" derken kolon degismemisti. KARAR KRITERI (a) yakaladi.
 4 R-F1b LISTE fiyatiyla hesaplandi (299,90); ETKIN fiyat 249,90 idi -> siparis 265 esigi
   gecmedi, 267 eklendi.
 5 MIG yarim birakildi: 01_schema.sql yeniden URETILMEDI -> SemaTekKaynakTests KIRMIZI.
   PIN YAKALADI.
 6 Birlestirme sirasinda BOM dosyanin ORTASINA dustu -> 1 yerine 4 kirmizi; cat -A ile
   teshis, sed ile ayiklandi.
 7 "ic BOM" suzgecim dosyanin KENDI bas BOM'unu sayiyordu (yanlis alarm); head -c 3 ile
   elendi.
 8 PIN TASARIM KUSURU: 549,90 icin NotContain(invariant) AYIRT EDICI DEGIL - invariant N2
   ("549.90") ham JSON sayisinin TA KENDISI. Uc DOGRU davranirken pin kirmizi verdi.
   Ayirt edici olcutlere gecildi; CulturePinTests'te (1049,70 binlik ayrac tasiyor) her iki
   bicim de arandi.
 9 MUKERRER SOZLUK ANAHTARI: b_fatura_yok ZATEN VARDI ve FARKLI anlamdaydi ("liste bos").
   JS'te son tanim kazandigi icin ekranda MEVCUT metin cikti, benimki HIC gorunmedi.
   TARAYICIDA yakalandi; ayri anahtara alindi, mevcut anahtara DOKUNULMADI.
10 ESCAPE-KAYBI AILESINE YENIDEN DUSTUM: sed ile yazilan Replace("\t") zinciri dosyaya
   GERCEK TAB/CR/NEWLINE olarak indi ve dize literalini satir ortasindan boldu.
   cat -A ile teshis, KACISSIZ coozume gecildi (tirnakli-EOF heredoc + verbatim).
SUREC NOTU 1: bir splice CAPA KONTROLUNE BAGLANMADAN kosup yorum blogunu boldu; onarildi
   ve ayni turda bayat yorum da duzeltildi.
SUREC NOTU 2: API kosarken alinan bir build MSB3027 ile GURULTULU dustu (sessiz gecmedi);
   surec PID ile durduruldu ve tur tekrarlandi. schtasks /End sureci OLDURMEDI - kill
   PID gerekti.
```

## ACIK OLCUM SONUCLARI

**(1) SIPARIS 2-8'IN `0,00` MUTASYONUNUN KAYNAGI.**
`OrderManager.CancelItem` - satir **1070** `order.total_price = 0m`, satir **1084**
`order.shipping_cost = 0m`: yani **MUSTERI KALEM-IPTAL** yolu. Siparis 2-8 bugun
`shipping_cost 49,90 + total_price 0,00` tasiyor; bu bilesim **BUGUNKU kodun
URETEMEYECEGI** bir haldir (bugunku kod IKISINI DE sifirliyor) -> kayitlar
**Dalga-2/B12 duzeltmesinden ONCEKI artiklardir** (22-23 Temmuz).
**KARSIT KONTROL:** ADMIN durum-degistirme yolu tutarlari SIFIRLAMIYOR - siparis 268
iptal edildikten sonra 499,80 / 49,90 / 549,70 KORUNDU.
**GOZLEM KALEMIDIR (C6); FIX BU DALGADA DEGIL - kuyruk karari MF-4 SONRASINDA.**

**(2) GORUNTULEME BOLGESINDE KAYNAK DUZEYI SAYIM.**
Bolge `OrderManager.GetInvoiceView` (satir 561-636), **YORUMLAR AYIKLANMIS**:
`"/1.20m"` **0** · `"KDV (%"` **0** · sunucu para bicimleme (`:N2` / `ToString("N")` /
`" TL"`) **0**. **NEGATIF KONTROL:** ayni bolgede `"invoice"` 19 gecis -> tarama CALISIYOR.
Controller ucu de bicimleme YAPMIYOR. Dosya genelinde yorum disi kalinti YOK.
**URETIM KAYNAGI SAYIMA GIRMEDI ve DOKUNULMADI** (C4): `InvoiceManager.cs:24` (`0.20m`)
VAR, `EInvoice:KdvRate` VAR.

## COMMIT LISTESI

```
fb3b7b7  feat(fatura): MIG+K1 - kargo ayri fatura kalemi
ae020a0  feat(fatura): K2 - gorunur fatura KAYITTAN beslenir
5081741  feat(fatura): K3 - fatura govdesi istemcide, uc dilde
c63175b  test(fatura): K4 - brut sozlesmesi pini
25e723d  fix(fatura): MK-4b - denetim bulgulari (B1..B5)
```
**SAPMA (raporlandi):** merkez **DORT** commit bekliyordu; **BESINCI**, MK-4b denetiminin
cikardigi **kapsam-ICI** bulgularin kapatilmasidir ve AYRI tutuldu (`git bisect`
okunabilirligi).

## KALICI KURALLAR (bu dalgadan)

**MK-8 - AKIS DUZENLEYICIYLE METIN YAZILMAZ.**
"Kacis, tirnak, BOM ya da cok-satir tasiyan icerik akis duzenleyicileriyle (sed/perl/echo)
yazilmaz - dosya araci ya da tirnakli-EOF heredoc kullanilir; yazilan ve birlestirilen her
metin artefakti bayt duzeyinde dogrulanir (`cat -A` · `head -c 3`)."
**Gerekce OLCULDU:** kacis-kaybi ailesine KAYITLI derse ragmen bu dalgada YENI bir dusus
oldu (hata 10: `sed` ile yazilan `Replace("\t")` zinciri dosyaya GERCEK tab/CR/newline
olarak indi ve dize literalini satir ortasindan boldu) ve ayrica BOM birlestirme sirasinda
dosyanin ORTASINA dustu (hata 6: 1 yerine 4 kirmizi). Iki vaka da ayni kokten: **akis
duzenleyici, metnin BAYTLARINI korumaz.**
**NUMARA:** mevcut en yuksek tam sayi MK-7 idi (MK-4b harflidir, tam sayi TUKETMEZ),
dolayisiyla **MK-8** atandi - merkez beklentisiyle ORTUSUYOR.

**KALICI DERSLER:**
- **YASAK-BICIM ASSERT'I AYIRT EDICI DEGERLE KURULUR ve ayirt ediciligi KANITLANIR.**
  `549,90` icin `NotContain(invariant)` ayirt edici DEGILDIR: invariant `N2` bicimi
  `"549.90"`, ham JSON sayisinin TA KENDISIDIR - uc dogru davranirken pin kirmizi verir.
  Binlik ayrac tasiyan bir deger (`1.049,70`) secilirse iki bicim gercekten ayrisir.
- **YENI SOZLUK ANAHTARI EKLENMEDEN ONCE ANKRAJLI MUKERRER TARAMASI.** `b_fatura_yok`
  ZATEN VARDI ve FARKLI anlamdaydi; JS'te son tanim kazandigi icin ekranda MEVCUT metin
  cikti. Ankrajli sayim ("X:" deseni "onek_X:" ICINDE de esler) zorunludur; **P-F3 artik
  bunu TARIYOR**.
- **`dotnet ef --no-build` BAYAT-IKILI BICIMIDIR.** Migration derlenmis derlemede yoksa
  `database update` "already up to date" der ve kolon DEGISMEZ. Karar kriteri yakalar.
- **EF TOOL CI SURUM ESLEMESI:** kapiyi kuran surumle yerel surum ayrisabilir; izole bir
  `--tool-path` kurulumu ile ayni surum kullanilir.
- **KAPI AYIRT-ETME KANITI DESENI:** bir kapinin gercekten olctugu, AYNI komutun iki
  durumu ayirt etmesiyle gosterilir (**once exit 1 / sonra exit 0**) - "yesil verdi"
  tek basina kanit degildir.

## DEVIRLER ve DUZELTMELER

**`a5add91` MUHRUNDEKI KIRPIK AD DUZELTMESI (a5add91 metnine DOKUNULMADI).**
O muhur iki test adini KIRPIK yazmisti; TAM adlar HAM logdan aynen:
`PlaceOrder_InsufficientStock_Returns400_And_NoPartialData` ·
`PlaceOrder_ValidCart_Returns201_And_DecrementsStock`.
**KOK SEBEP:** cikarma desenim `[A-Za-z_]+` RAKAMDA kesiyordu (`Returns400` -> `Returns`).
Bu, **MANTIK-FIX-1 hanesine 7. HATA** olarak islenir. MK-7'ye eklenen cumle
("bilinen-pozitif seti hedef alfabeyi temsil eder - rakam dahil") tam bu vakadan dogdu.
**MANTIK-FIX-1'in 6. hatasi** (10 haneli DEPO ID'sinin run kimligi sanilmasi) CC-hatalari
kaydina gecer ve karsiligi **SUZGEC KUTUPHANESI S4** girdisidir.

**F SONUCU (celiski notu DUSTU):** MFIX-3b muhrunun "8/31" sayilarini ANDIGI iddiasi
olculdu ve **CURUDU** - iki capa da o metinde **0 gecis** veriyor (NEG kontrol calisiyor).
Kutuphane sayilari YALNIZ MANTIK-FIX-1 mühründedir; celiski YOKTUR.

**D-YAN GUNCELLEMESI:** kargo ailesi **64 -> 70** · indirim-payi **AYRI aile 15** · bu
dalganin kayitlari (musteri 118-119, siparis 264-268, adres 77, fatura 97-101) ·
**adres MAX 76** duzeltmesi (65-75 envanterine +1; MANTIK-FIX-1 C1 fiksturu).

**KAPSAM-DISI GOZLEMLER (DOKUNULMADI):**
- e-fatura saglayici payload'inda kargoyu ayirt eden **tek sey sabit Turkce "Kargo" adi**;
  `EInvoiceLine` kimlik alani TASIMIYOR. Kuyruk karari sonra.
- `invoice-html` rota adi artik yaniti tarif etmiyor - **E1 geregi BILINCLI**.
- **DENETCI KOR NOKTALARI (kaydi):** tarayici kullanilmadi (B1'in ekrandaki hali
  gorulmedi) · `Down()` surulmedi · migration temiz DB'ye uygulanmadi -> **bu sonuncusunu
  CI KAPATTI** ("Model ile migration'lar SENKRON mu" adimi + TestDbKurulum satiri) ·
  DB izolasyonu kismi · tam suit kosulmadi (yalniz 4 sinif) · kargo kurus davranisi tek
  deger disinda sinanmadi.

## KUYRUK

```
1. MF-3 [KVKK/HESAP]                                          <- SIRADA
2. MF-4 [VITRIN + i18n]
3. GUVENLIK-AV-1 (ilk genisletilmis-tarama pilotu; tetik kelimesini prompt'a merkez ekler)
4. GUVENLIK-FIX paketi
5. FIX-1B
6. ADMIN-FIX
7. IMPORT-FIX
8. FIX-1C
9. LOG-FIX
10. FIX-2
11. FIX-3 / B13
```

---

