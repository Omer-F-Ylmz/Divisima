## KALITE SUPURMESI - DALGA 1 (ENVANTER + TARAMA) ve DALGA-1-FIX

Launch oncesi son cila fazi. Dalga 1 YALNIZ olcumdu; duzeltmeler ayri bir commit'te geldi.

### DALGA 1 BULGULARI (ozet - ayrinti dalga raporunda)

| # | Siniif | Bulgu | Durum |
|---|---|---|---|
| B1 | VERI-BOZAN | Ayni e-posta ile IKI HESAP acilabiliyor (canli kanit: id 14/15) | **KAPANDI** |
| B2 | ISLEV-KIRAN | "i" iceren kupon kodu kucuk harfle calismiyor | **KAPANDI** |
| B3 | ISLEV-KIRAN | Auth rate-limit'i BUYUK HARFLI URL ile atlatilabiliyor (Redis yolu) | **KAPANDI** |
| B4 | ISLEV-KIRAN | CSV'de `product_type` dogrulanmiyor, bozuk deger sessizce 0 | **KAPANDI** |
| B5 | KAPSAM | 150 API ucunun 100'u HTTP duzeyinde test gormuyor | ERTELENDI (ayri kapsam dalgasi) |
| B6 | PERFORMANS | CORS preflight onbellegi yok - her cagri cift gidis-donus | ERTELENDI (Dalga 3) |
| B7 | PERFORMANS | Yinelenen istekler (my-orders x2, order/get x2) | ERTELENDI (Dalga 3) |
| B8 | KOZMETIK | `Messages.cs`'te 255 degistirilebilir `public static string` | Launch sonrasi defter |
| B9 | KOZMETIK | Odeme sonuc sayfasinin kendi basligi yok | **KAPANDI** |

**ELENENLER (bulgu DEGIL):** `CA5350 HMACSHA1@TotpService` -> RFC 6238 TOTP standardi, false
positive. Uretimdeki 14 `CS8602/CS8604` -> orneklendi, hepsi guard'li ama derleyicinin
kanitlayamadigi desen. `CA1001` uretimde 0.

**TEMIZ CIKANLAR:** 12 rota gezildi, konsol hatasi SIFIR, 404 asset YOK, sayfa basliklari
dogru, sessiz token yenileme calisiyor (401 -> refresh -> 200).

### DALGA-1-FIX - YAPILANLAR

**0) CI COLLATION HIZALAMASI.** Iki workflow'un SQL container'ina `MSSQL_COLLATION=Turkish_CI_AS`.
Container varsayilani Latin1'dir ve orada `'irem' = 'IREM'` **ESIT** doner - yani B1/B2 sinifi
hatalar CI'da HIC GORUNMEZDI. META-PIN (`CollationMetaPinTests`) bagli oldugu DB'nin
collation'ini assert eder + Turkce karsilastirmanin GERCEKTEN yururlukte oldugunu ayrica
dogrular (cift-anlam kirici: etiket dogru ama davranis farkli olsaydi ilk assert yesil kalirdi).

**1) KOK ILKE + DEPO TARAMASI.** Tum `ToLower()/ToUpper()` ve karsilastirma-turu verilmemis
`StartsWith` cagrilari tarandi ve KIMLIK/GORUNTU olarak siniflandirildi. Kalici kural
**bolum 6c**'ye yazildi. KIMLIK olarak siniflandirilip cevrilenler:
e-posta (AuthManager, SellerAuthManager, AdminSeeder, EfCustomerDal, EfSellerDal,
StockNotificationManager, PriceDropManager) · kupon kodu (CouponManager, EfCouponDal) ·
URL yolu (RedisRateLimitMiddleware) · MIME tipi (ProductImageManager, LocalImageStorage) ·
HTTP baslik semasi (AntiforgeryMiddleware) · saglayici durum kodu (NetgsmSmsService) ·
uretilen kodlar (GiftCard, OrderNumber, Referral).
GORUNTU olarak BIRAKILANLAR: urun adi/marka aramasi (`SearchManager`) ve admin listesindeki
**ad** aramasi - insan metnidir, Turkce kucultme orada DOGRU olandir. Ayni arama kutusundaki
**e-posta** yarisi ise KIMLIK sayilip invariant'a cevrildi (`AdminCustomerManager` iki ayri
normalize terim kullaniyor; gerekce kodda).

**2) B1.** Kod tarafi invariant + `EmailKanonikNormalizasyon` migration'i. Migration Sprint 6
kalibinda: cakisma cikarsa **hicbir satir yazmadan** RAISERROR; Turkce-hasarli satirlar
(icinde `ı`/`İ` gecen) **OTOMATIK ONARILMAZ** - karakter degistirmek TAHMIN olurdu - yalnizca
gurultulu raporlanir. `IX_customers_email` **ZATEN UNIQUE** (olculdu), yeni indeks gerekmedi:
sorun indeks degil, saklanan degerin kendisiydi. Sondaj hesaplari (id 14/15) migration icinde
silindi. Yerel dogrulama: 12 -> 10 musteri, hasarli satir 0.

**3) B3.** Yol karsilastirmasi `OrdinalIgnoreCase`. Pin MIDDLEWARE duzeyinde
(`RateLimitPathScopeTests`) - bu yol yalniz `Redis:Enabled=true` iken pipeline'a girdigi icin
uctan uca test gercek bir Redis isterdi.

**4) B2.** Kanonik bicim `KimlikDizgesi.KanonikKod` (Turkce harf katlamasi + invariant buyultme).
**PIN YAZARKEN OLCULDU:** duz `ToUpperInvariant` YETMIYOR - Turkce klavyede `İNDİRİM10` yazan
musteri icin hicbir sey eslesmiyordu. Bu, dalga sirasinda bulunan ve duzeltilen bir ARA BULGUDUR.

**5) B4.** `product_type` diger dokuz kolon gibi dogrulaniyor. **Yan bulgu:** ice aktarim ucu
hata AYRINTILARINI donmuyor, yalnizca sayiyi ("1 hatali satir") - kozmetik, deftere yazildi.

**6) B9.** Sonuc sayfasi basligi (basarili/basarisiz ayrimiyla).

### PINLER

`CollationMetaPinTests` (2) · `KimlikDizgesiSozlesmeTests` (5): ayni adresin farkli casing'i
ikinci kayitta REDDEDILIR · kayitli kullanici HER casing'le giris yapabilir · kupon kodu hangi
yazimla girilirse girilsin eslesir (+ var olmayan kod BULUNMAZ - cift-anlam kirici) · bozuk
`product_type` hata listesine duser · gecerli `product_type` iceri alinir (vakum kirici).
`RateLimitPathScopeTests` (4): buyuk harfli auth yolu AUTH kovasina duser · kucuk harfli de
duser (vakum kirici) · payment/global yollari dogru kovaya duser ve buyuk harf FARK ETMEZ
(cift-anlam kirici).

### DIS KONTROLU + 5. KONTROL

5 assert ters, BES AYRI test -> **5 AYRI ISIMLI KIRMIZI** (geri alindi).
5. kontrol: `AuthManager` + `EfCustomerDal` kulturlu `.ToLower()` haline donduruldu ->
`AyniAdresinFarkliCasingi_...` ikinci kayitta **201 Created** buldu (= IKI HESAP) ve
`KayitliKullanici_HER_CASING_...` buyuk harfli giriste **401** buldu. **Dalga 1'de olculen
canli tablonun BIREBIR aynisi.** Diger uc test yesil kaldi (mutasyon lokalize). Geri alindi.

