# CLAUDE.md — Divisima Backend calisma kurallari

Bu dosya, bu depoda calisan asistanin uyacagi kurallari tanimlar.
Kurallar kullanici tarafindan konulmustur; asistan bunlari kendi basina gevsetemez.

---

## 1. Kanit standardi

- **PAT (kisisel erisim jetonu) veya tarayici eklentisi ASLA istenmez.** Kullanicidan
  kimlik bilgisi talep etmek cozum degildir; kanit halka acik kanallardan toplanir.
- Gecerli kanit sunlardir:
  - GitHub job API'sinden okunan **adim sonuclari** (SUCCESS / FAILURE) — hangi adimin
    kirildigi tek tek gorulur.
  - **check-runs annotations** — anonim olarak okunabilir
    (`GET /repos/{owner}/{repo}/check-runs/{id}/annotations`, HTTP 200).
- `$GITHUB_STEP_SUMMARY` **yalniz imzali kullaniciya gorunur**. Bu yuzden ayrintili
  cikti (son 100 satir, ortam bilgisi) oraya yazilir.
- Annotation'lar **PUBLIC**. Oraya yalniz `Failed` / `Expected` / `Actual` satirlari
  basilir; cikti kuyrugu ve ortam bilgisi Summary'de kalir, annotation'a sizdirilmaz.
- Run izleme **SHA bazlidir** (`head_sha=` ya da `?branch=main` + SHA eslesmesi).
  "En son run" ile calisilmaz — Dependabot kosulari araya girer ve yanlis run raporlanir.

### Izleyici adabi (GitHub API kotasi)

Anonim GitHub API kotasi **60 istek/saat**. Izleyici bunu yakarsa hicbir kanit
okunamaz hale gelir. Bu yuzden:

- Izleyici nabzi **>= 300 saniye**. Kisa nabizli yoklama yasak.
- Tur basina **TEK konsolide cagri**: run listesi + jobs + annotations ayni turda
  toplanir, ayri ayri turlara bolunmez.
- Kota yandiysa **beklenir** (yeniden denemeye devam edilmez).
- PAT veya tarayici eklentisi **asla** istenmez — kota siniri bir gerekce degildir.

## 2. Push disiplini

- **Tek push -> tek run -> tek rapor.** Ayni is icin arka arkaya push edilmez.
- **Commit ve push karari her zaman kullanicidan gelir.** Onay yoksa is lokalde birikir;
  asistan kendi inisiyatifiyle commit/push yapmaz.
- Rapor, run tamamlandiktan sonra ve gercek adim sonuclarina dayanarak verilir.

## 3. Kod sinirlari

- **Uretim kodu YASAK.** Serbest olan: test kodu, workflow dosyalari, depo dokumani.
- **Supheli uretim davranisi DUZELTILMEZ.** Bulgular raporda ayri bir
  **"SUPHELI DAVRANISLAR"** basligi altinda toplanir; mevcut davranis testle *pinlenir*,
  degistirilmez. Duzeltme karari kullanicinindir.
- **Engelde dur ve raporla.** Sessiz gecistirme yok: cozulemeyen bir engel, tahminle
  doldurulmus bir sonuc yerine acikca bildirilir.

## 4. SQL test deseni

- **Iki modlu taban sinifi** (`SqlBackedTestBase`):
  - `DIVISIMA_TEST_SQL` set ise SQL **zorunludur**; baglanilamazsa
    `InvalidOperationException` firlatilir. Sessiz skip yok.
  - Set degilse LocalDB'ye duser (yerel gelistirme kolayligi).
- **Sinif basina AYRI veritabani.** xUnit test siniflarini paralel kosar; ortak DB
  kullanilsa bir sinifin `EnsureDeleted` cagrisi digerinin verisini silerdi.
- **`[Trait("Category","Sql")]`** her SQL gerektiren sinifa konur; CI'da adanmis adim
  `--filter "Category=Sql"` ile kosar. Yeni sinif eklenince workflow degismez.
- **Randomize veri izolasyonu:** her test kendi musterisini/urununu/siparisini `Guid` ile
  uretir. Var olan satirlara guvenilmez.

### Yerel SQL testleri (skip modu KULLANILMAZ)

Yerelde testler **her zaman** `DIVISIMA_TEST_SQL` ile kosulur:

```
export DIVISIMA_TEST_SQL="Server=localhost;Database=DivisimaCiTest;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
```

Gerekcesi: degisken set DEGILSE taban sinif LocalDB'ye duser ve baglanamazsa **sessizce**
`Skipped()` moduna gecer - testler `< 1 ms` icinde YESIL gorunur, hicbir sey olculmez.
Bu tuzak bir kez yasandi (LocalDB ornegi cokmustu, 6 test yalanci yesil verdi). Degisken
verildiginde ise baglanti hatasi `InvalidOperationException` ile gurultulu sekilde patlar.

Dizgede `Database=` **bulunmalidir**: `InvoiceCancellationTests` degiskeni ham kullanir
(diger siniflar `InitialCatalog`'u kendileri set eder), veritabani adi yoksa
`EnsureDeleted` "database name could not be determined" ile duser.

## 5. Bilinen tuzaklar (bir kez bedeli odendi, tekrar edilmez)

- `Product.description` ve `Product.color_hex` **zorunlu** alanlardir.
- Kategori **gercekten olusturulmalidir**; sadece id vermek yetmez.
- `customer_id > 0` sart — FluentValidation auto-validation, controller token'dan degeri
  set etmeden ONCE kosar.
- `coupon_code = ""` verilmelidir — non-nullable string oldugu icin binding zorunlu kilar.
- `TestAuthHelper` **yeniden kullanilir**, yeniden yazilmaz (gercek register/verify/login
  uclarindan token alir).
- Stok assertleri **`available` / `reserved`** uzerinden yapilir; tek basina
  `stock_quantity` rezervasyon modelini yanlis okur
  (`available = stock_quantity - reserved_quantity`).

## 6. Assert kalitesi

- **Vakum yasagi:** hicbir sey olmadiginda yesil kalan assert yazilmaz. Her testte en az
  bir pozitif olay kosulu bulunur (basari sayisi >= 1, satir olustu, bakiye degisti).
- **Cift-anlam yasagi:** yalniz durum koduna bakilmaz. 400 iki ayri sebepten gelebilir;
  govde mesaji ve/veya DB durumu da dogrulanir.
- **Dis kontrolu:** yeni testlerin gercekten olctugu, assert tersine cevrilip **isimli
  kirmizi** gozlenerek kanitlanir; sonra geri alinir ve kanit raporda belirtilir.

## 7. CI kurallari

- **CI script'leri YAML'dan cikarilip calistirilarak dogrulanir.** Varsayimla "calisir"
  denmez.
- `tee` kullanilan her yerde **`set -o pipefail` sarttir** — aksi halde basarisiz bir
  `dotnet test` adimi YESIL gorunur.
- Her job'da `timeout-minutes`, `dotnet test` kosan her adimda
  `--blame-hang --blame-hang-timeout 8m --blame-hang-dump-type none` bulunur.
- Testleri filtreyle **dislamak yasaktir**: sessiz skip degil, gurultulu hata istenir.
- Teshis kanallari: `if: failure()` adimi -> Summary (ayrintili) + `::error::`
  annotation (yalniz assert satirlari).

---

# OTURUM DEVRI (20 Agustos 2026)

Bu bolum, yeni bir oturumun tek basina devam edebilmesi icin yazildi.
**Ozetle celisen her noktada BU BOLUM kazanir.** Emin olunmayan her durumda tekrar okunur.

## DURUM

- **Son push: `153cf01`** — Sprint 4: kalem bazli KDV (`Category.vat_rate` +
  `Product.vat_rate` + `invoice_items` tablosu + oran snapshot'i), `phone` NULL'lanabilir
  (S-del Secenek A), `Seller:RegistrationEnabled` kapisi (varsayilan false),
  `CHANGE_IN_PRODUCTION` placeholder listesine eklendi.
- **Sprint 4 CI: HER IKI WORKFLOW TAMAMEN YESIL.** `SQL gerektiren testler` SUCCESS,
  `Testler + coverage` SUCCESS, `Coverage raporunu yukle` SUCCESS (coverlet.collector
  eklendikten sonra gercekten dosya uretiyor), codeql/dependency-scan/secret-scan/
  format-check SUCCESS. `TESHIS` adimi skipped (kirmizi yok).
- **Yerel (Sprint 4 sonrasi): 115/115 `Category=Sql`, 236/236 tam suit.**

## SPRINT 5 - PUSH EDILDI, RUN RAPORU BEKLENIYOR

Sprint 5 (odeme guvenlik dalgasi) tamamlandi, dogrulandi ve **PUSH EDILDI** -
commit `test: payment security wave 5 + accepted-risk & handover docs`
(SHA icin `git log --oneline -3`; bir commit kendi SHA'sini iceremedigi icin
buraya yazilamadi).

**RUN RAPORU BEKLENIYOR.** Beklenti: `SQL gerektiren testler` adiminda **131 test**.
Kanit = adimin **SUCCESS** olmasi + yereldeki sayi - sayilar CI ciktisindan okunamaz.

Commit'teki iki test dosyasi:

```
Divisima.IntegrationTests/PaymentCallbackSecurityTests.cs      (11 test)
Divisima.IntegrationTests/WebhookAndSessionSecurityTests.cs     (5 test)
```

**Yerel dogrulama (Sprint 5 dahil): Release build 0 hata, `Category=Sql` 131/131,
tam suit 252/252, dis kontrolu 4 assert ters cevrilip 4 isimli kirmizi gozlendi ve
geri alindi.** Yani push on-onayinin dort kosulu da saglandi.

On kalemin karsiligi:
1. HMAC reddi -> `GecersizImza_...` + `ImzaYOKSA_Reddedilir`
2. Replay yan-etkisizligi -> `BasariliCallback_IkinciKez_YanEtki_SIFIR`
3. 30 dk timeout -> `Token30dkdanEskiyse_Reddedilir_OdemeFailed`
4. payment-order kilidi 8-paralel -> `AyniSiparise_SekizParalelCallback_KILIT_SERILESTIRMIYOR_SADAKAT_CIFTLENIYOR_PINLENIR`
5. Tutar uyusmazligi -> `EksikOdeme_...` + `FazlaOdeme_MakulTaksitKomisyonu_KABUL_...`
6. Basarisiz odeme -> `FraudRed_...` + `ParaBirimiUyusmazligi_Reddedilir`
7. Webhook AllowedIps -> `WebhookAllowlist_BOS_...` + `..._DOLU_...` + `..._DigerUclar_Etkilenmez`
8. RefundAsync yolu -> `KartIadesi_Iyzicoya_DogruTutarla_Gonderilir`
9. Refresh rotasyonu -> `Refresh_YeniCiftUretir_ESKI_RefreshToken_REDDEDILIR` + `PasifHesabin_RefreshToken_i_Reddedilir`
10. Kumulatif iade -> `KumulatifIade_ToplamTotalPriceI_ASABILIYOR_PINLENIR`

## SPRINT 6 - PARA DUZELTMELERI (SIRADAKI; E4a'dan ONCE)

Sprint 5'te OLCULEN iki para bulgusu kapatilir. Kurallar onceki sprintlerle ayni
(tek push -> tek run -> tek rapor; push on-onayinin dort kosulu; dis kontrolu).

**(1) Kumulatif iade siniri.** `orders.refunded_amount` kolonu eklenir ve
`RefundToSourceAsync` bu degere gore KUMULATIF clamp yapar: toplam iade
`order.total_price`'i **ASLA** asamaz. Ikinci tam iade reddedilir ya da kalan
kadar kirpilir. Pin: **Iyzico'ya fazla iade GITMEZ** (saglayici cagri tutari da
dogrulanir, yalniz donen deger degil).

**(2) Sadakat kazanimi siparis basina TAM BIR KEZ.**
Once **KOK SEBEP**: `payment-order:{id}` kilidi 8 paraleli neden gecirdi -
kilidin kapsami mi dar (yanlis anahtar / erken birakma / kilit disinda kalan
bolum), yoksa `InMemoryDistributedLock` mu beklemeden donuyor? Teshis edilmeden
yama yazilmaz.
Sonra mekanizma onerisi: filtreli-unique indeks (`loyalty_transactions` uzerinde
order_id + Earn tipi) **ya da** kazanimi idempotent bir durum-gecisinin icine
almak. **Tesadufi sogurmaya guven YOK** - "asagi katman zaten emiyor" bir tasarim
guvencesi degildir.
Pin: 8 paralel callback -> `loyalty_transactions` **TAM 1 satir**.

**(3) Eski iki pin BILINCLI kirilir**, yenileri AYNI commit'te gelir:
- `KumulatifIade_ToplamTotalPriceI_ASABILIYOR_PINLENIR`
- `AyniSiparise_SekizParalelCallback_KILIT_SERILESTIRMIYOR_SADAKAT_CIFTLENIYOR_PINLENIR`

## SIRA

1. **Sprint 5 run raporu** (push edildi, rapor bekleniyor)
2. **SPRINT 6 - para duzeltmeleri** (yukaridaki bolum) - E4a'dan ONCE
3. **E4a** - stok-adjust + gorsel-yukleme admin ekranlari (LAUNCH ON KOSULU:
   ucler var, arayuz yok; operator bunlari panelden yapamiyor)
3. **E1** auth + katalog -> **E2** sepet+checkout+odeme -> **E3** hesap+siparis takibi
   - E2'nin iki somut isi: `checkout_form_content` gomme + `#/odeme/sonuc` donus sayfasi
     (callback bugun ham JSON donuyor)
   - E3: CMS sanitizasyonu IKI katman (yazma `InputSanitizer` + okuma DOMPurify)
4. **Sema kapanis dalgasi** - Sprint 5 sonuclariyla birlikte degerlendirilecek
   (seller migration DEGIL: `sellers` ve `seller_id` zaten `InitialCreate`'te;
   aday kalemler: `refunded_amount`, gift-card expiry)
5. **E4b** (musteri askiya alma, kategori, CMS ekranlari) - launch sonrasi olabilir

## KARARLAR (kapanmis)

- **AutoMapper: 12.0.1'de KAL, bump YOK.** Advisory (CVE-2026-32933) okundu, maruziyet
  olculdu, maruz DEGILIZ. Gerekce ve yeniden degerlendirme tetikleyicileri
  `SECURITY.md` "Kabul Edilen Riskler" bolumunde. **Onemli:** yamali surumler 15.1.1/
  16.1.1'dir ve AutoMapper 15+ **RPL-1.5 veya ticari lisansa** gecmistir; 12/13/14
  MIT ama ucu de ayni advisory kapsamindadir (olculdu). "MIT kalarak yamalanmak" mumkun degil.
- **Seller modulu**: dokunma, veri duzeyinde kapali, migrate/seed yok.
- **invoice_number**: entegrator (Nilvera) numarasi esas, bizimki ic referans - degisiklik yok.
- **Launch sonrasi defteri** (simdi is yok): gift-card expiry, 2FA enrollment ucu,
  step-up `auth_time` refresh'te sifirlanmasi, loyalty oransal geri alma + referral
  clawback, Dashboard tam-tablo agregalari. **Dusen kalem:** Http.Abstractions 2.2.0
  (hicbir csproj'de referans yok).
- **Auth modeli**: mevcut hibrit korunuyor (access localStorage + refresh httpOnly
  cookie + kosullu CSRF). Backend ile uyumlu oldugu dogrulandi.

## SUPHELI DAVRANISLAR - KARAR BEKLEYENLER

Bu ikisi Sprint 5'te OLCULDU ve mevcut davranis olarak PINLENDI; duzeltilmedi.

1. **Odeme callback kilidi serilestirmiyor + sadakat puani ciftleniyor.**
   Ayni siparise 8 paralel callback -> **8'inin 8'i** sunucu-sunucu sorguya ulasiyor
   ve hepsi 200 donuyor. Stok/odeme kaydi/fatura tarafini asagi katmanlar soguruyor
   (rezervasyon bir kez tuketiliyor, odeme UPDATE, fatura idempotent) ama
   **`loyalty_transactions` tek siparis icin 8 satir** olusuyor - para etkisi ciftleniyor.
   Sogurma bir tasarim guvencesi degil tesaduf: sogurmeyen bir yan etki eklendigi gun
   sekiz kez uygulanir.
2. **Kumulatif iade siniri YOK.** `RefundToSourceAsync` TEK cagride
   `refundAmount > order.total_price` ise kirpiyor ama ardisik iadelerin toplamini
   takip etmiyor. Olculdu: iki ardisik tam iade -> toplam **siparis tutarinin 2 kati**,
   ve Iyzico'ya da iki kat iade gonderiliyor. (`refunded_amount` kolonu karari icin girdi.)

## SUREC (degismez)

- **Tek push -> tek run -> tek rapor.** Commit/push karari HER ZAMAN kullanicidan gelir.
- **Push on-onayinin dort kosulu**: (a) `Category=Sql` yerel komut yesil,
  (b) tam suit yesil, (c) Release build 0 hata, (d) o sprintin pinlerinde dis kontrolu
  (>=3 assert ters cevir -> isimli kirmizi gozle -> geri al).
- **Test sayilari CI'dan OKUNAMAZ.** Job log'u anonim erisime 403, Summary imza istiyor,
  annotation yalniz `Failed` satirlari tasiyor, check-run `output` bos (dordu de denendi).
  Kanit = **adimin SUCCESS olmasi** + yerelde `ci.yml`'dan cikarilan komutun verdigi sayi.
- **Izleyici adabi**: nabiz >= 300 sn, tur basina TEK konsolide cagri, kota yandiysa bekle.
  Dependabot run'i beklenmez - asil iki workflow (CI + Security) yeter.
- **PAT veya tarayici eklentisi ASLA istenmez.**
- **Yerel SQL**: `DIVISIMA_TEST_SQL` her zaman set edilir (skip modu kullanilmaz);
  dizgede `Database=` bulunmalidir. LocalDB cokmus durumda ve **`sqllocaldb delete`
  YASAK** (ayni ornekte baska bir projenin `GarajimDb` veritabani var). Tam ornek
  (`Server=localhost`) kullaniliyor.
- **Uretim kodu**: yalniz kullanicinin acikca izin verdigi kalemlerde. Kapsam disi
  bulgular duzeltilmez, **SUPHELI DAVRANISLAR** basligiyla raporlanir.
