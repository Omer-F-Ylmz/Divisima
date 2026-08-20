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
- **`EfEntityRepositoryBase.GetAsync` TRACKED'dir.** Ayni `DbContext` icinde bir satiri
  ikinci kez okumak DB'deki taze degeri getirmez - EF identity resolution ilk okunan
  (bayat) nesneyi dondurur. "Kilit aldiktan sonra durumu tekrar oku" gibi her savunma
  satiri bu yuzden SESSIZCE olu kalabilir. Taze deger gerektiginde
  `GetListNoTrackingAsync` kullanilir. (Sprint 6 kok sebebi buydu.)
- **`ExecuteUpdateAsync` change-tracker'i ATLAR.** Atomik CAS ile guncellenen bir kolon,
  cagiranin elindeki izlenen varlikta ESKI degerde kalir; o varlik uzerinden yapilan
  tam-varlik `UpdateAsync` (tum kolonlari yazar) atomik guncellemeyi SESSIZCE geri alir.
  Bellekteki deger de esitlenmelidir.
- **Autofac modulundeki servisler `services.AddScoped` ile EZILEMEZ.**
  `AutofacServiceProviderFactory` once `Populate(services)` yapar, `AutofacBusinessModule`
  SONRA kaydeder ve Autofac'te son kayit kazanir. Testte bir modul servisini degistirmek
  icin host builder'a MODULDEN SONRA calisacak bir `ConfigureContainer<ContainerBuilder>`
  eklenir - `WebApplicationFactory.CreateHost(IHostBuilder)` override edilerek.
  **`ConfigureTestContainer` minimal hosting'de ATESLENMIYOR** (olculdu: sarmalayici
  yerine gercek uygulama cozuldu). `IIyzicoClient` istisna: o `Program.cs`'te
  ServiceCollection'a kayitli, orayi `AddScoped` ile ezmek calisir.
- **Test siniflarindaki STATIK hata-enjeksiyon bayraklari test sinirini asar.**
  `InitializeAsync`'te sifirlanmazsa bir testin enjeksiyonu sonrakileri sessizce bozar
  (bir kez yasandi: sadakat bayragi acik kaldi, 8-paralel pini "0 kazanim" ile kirildi).

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

- **Sprint 6 push `de59f7d` — HER IKI WORKFLOW TAMAMEN YESIL** (run 32380571595 CI +
  32380571560 Security; adim bazinda dogrulandi). `SQL gerektiren testler` SUCCESS,
  `Testler + coverage` SUCCESS (Testcontainers'li `OrderEndpointTests` DAHIL),
  `Coverage raporunu yukle` SUCCESS, codeql/dependency-scan/secret-scan/format-check
  SUCCESS, `TESHIS` skipped, annotation BOS. (Sprint 5 `383f4f2` de tamamen yesildi.)
- **Sprint 7 push `4ee6318` — HER IKI WORKFLOW TAMAMEN YESIL** (run 32386448987 CI + 32386447800 Security).
- **E4a push `fb2b046` — HER IKI WORKFLOW TAMAMEN YESIL** (run 32392181855 CI + 32392181886 Security).
- **E1 (storefront auth + katalog) TAMAMLANDI ve push edildi** — asagidaki bolume bak.
- **Yerel (E1 sonrasi): 150/150 `Category=Sql`, 271/271 tam suit** (Testcontainers'li
  `OrderEndpointTests` HARIC - yerelde Docker kapali, CI'da yesil kosuyor).

## SPRINT 5 - KAPANDI (run yesil)

Sprint 5 (odeme guvenlik dalgasi) push `383f4f2`; her iki workflow tamamen yesil.
16 test (`PaymentCallbackSecurityTests` 11 + `WebhookAndSessionSecurityTests` 5).
On kalemin karsiligi commit mesajinda ve test isimlerinde duruyor. Sprint 5'in iki
"SUPHELI" olcumu Sprint 6'da KAPATILDI (asagi).

## SPRINT 6 - PARA DUZELTMELERI (TAMAMLANDI)

### KOK SEBEP (olculdu, tahmin degil)

Uc gecici teshis testi kosuldu, sonra kaldirildi:

| Teshis | Olcum | Sonuc |
|---|---|---|
| Kilit serilestiriyor mu? | `retrieve=8 maxEszamanli=1 sure=2242ms` (her cagri icerde 250 ms uyutuldu) | Kilit **CALISIYOR** - kritik bolumde hicbir an >1 cagri yok |
| Kilit sonrasi okuma taze mi? | `ilkOkuma=0 ikinciOkuma=0 dbGercek=1 referansAyni=True` | Guard **OLU** |
| Basarisiz odemede fatura? | `kod=400 siparisDurum=5 fatura=1 faturaDurum=1(Sent)` | Yeni SUPHELI (asagi) |

**Kok sebep: `EfEntityRepositoryBase.GetAsync` TRACKED.** `HandleCallback` basinda
odeme satiri Pending olarak okunup DbContext'e izlemeye aliniyordu; kilitten sonraki
"durumu TEKRAR oku" satiri EF identity resolution yuzunden **ayni bayat nesneyi**
donduruyordu. Yani kilidin kapsami dar degildi, kilit bozuk degildi - **kilitten
sonraki tek savunma satiri oluydu**. S5 pininin adi (`KILIT_SERILESTIRMIYOR`) YANLIS
teshisti; S6'da olcumle duzeltildi.

### YAPILAN (uretim)

1. **Bayat okuma kapatildi** - `HandleCallback`'te iki okuma da `GetListNoTrackingAsync`.
2. **Atomik durum gecisi** - `IPaymentDal.TryTransitionStatusAsync(id, from, to)`
   (`ExecuteUpdateAsync`, `EfReturnRequestDal.TryTransitionAsync` kalibi). Basari VE
   basarisizlik dallarinin ikisi de TEK KAZANAN birakiyor. Yan etki hakki artik
   KILIDE degil bu gecise bagli (Redis kopsa/kilit sursesi dolsa da tekil).
3. **Filtreli UNIQUE indeks** - `UX_loyalty_transactions_order_earn` on
   `loyalty_transactions(order_id) WHERE order_id IS NOT NULL AND type = 0`.
   Ikinci savunma hatti; Redeem ayni order_id ile serbest.
4. **`orders.refunded_amount`** + `IOrderDal.TryAddRefundedAmountAsync` (CAS: hem
   beklenen deger hem `+amount <= total_price` WHERE'de) ve `ReleaseRefundedAmountAsync`.
   `RefundToSourceAsync` kalan hakki **saglayici cagrisindan ONCE** rezerve ediyor.
   Saglayici reddederse tahsis geri birakiliyor (hak bloke kalmiyor).
5. **Bayat nesne tuzagi** - `ExecuteUpdateAsync` change-tracker'i atladigi icin
   cagiranin elindeki `Order` bayat kaliyordu; `UpdateAsync(order)` tum kolonlari
   yazdigindan sayaci SIFIRLARDI. `order.refunded_amount += granted` ile bellekteki
   deger de esitleniyor (dis kontrolu: satir kaldirilinca test `found 0.00M` ile kirildi).

Migration: `20260820141003_CumulativeRefundAndLoyaltyEarnUniqueness`. Indeks
kurulmadan ONCE ciftlenmis kazanim var mi diye bakan bir `RAISERROR` on kontrolu var -
**satir SILMIYOR** (silmek `customers.loyalty_points` ile defteri ayirirdi); kirli bir
veritabaninda migration gurultulu duruyor, mutabakat karari operatorun.
`database/mssql/01_schema.sql` de guncellendi (deploy varligi yalan soylemesin).

### PINLER

Kirilan (bilincli, ayni commit'te yenisi geldi):
- `KumulatifIade_ToplamTotalPriceI_ASABILIYOR_PINLENIR`
- `AyniSiparise_SekizParalelCallback_KILIT_SERILESTIRMIYOR_SADAKAT_CIFTLENIYOR_PINLENIR`

Yeni:
- `AyniSiparise_SekizParalelCallback_TEK_ISLENIR_SADAKAT_TEK_SATIR` - retrieve=1,
  `loyalty_transactions` TAM 1 satir, bakiye = tek kazanim
- `ArdisikIkinciCallback_SorguyaULASMAZ_ZatenIslendi_Doner`
- `KumulatifIade_ToplamTotalPriceI_ASAMAZ_IYZICOYA_FAZLA_IADE_GITMEZ`
- `KismiIadeler_UcuncuCagri_KalanHakka_KIRPILIR`
- `SaglayiciIadesi_BASARISIZSA_IadeHakki_BLOKE_KALMAZ`
- `EszamanliIkiTamIade_ToplamTotalPriceI_ASAMAZ`
- `KumulatifSayac_CagiranSiparisiGuncellese_de_KAYBOLMAZ` (RefundMoneyTests)
- `BASARISIZ_ODEMEDE_FATURA_URETILIYOR_PINLENIR` (SUPHELI pini - duzeltilmedi)

## SPRINT 7 - CALLBACK KAPANISI (TAMAMLANDI)

Sprint 6'nin iki SUPHELI maddesi kapatildi. Iki kalem, kapsam buyutulmedi.

### (a) Basarisiz odemede fatura

`ApplyConfirmedSideEffectsAsync` dogrulama BASARISINDAN SONRAYA (commit'in ardina) tasindi;
basarisiz/fraud dali, siparis `Cancelled` olarak kalici olduktan sonra
`ApplyCancelledSideEffectsAsync` cagiriyor. Iptal edilen siparise artik fatura kesilmiyor;
baska bir onay yolundan (COD/havale/magaza kredisi) kesilmis bir fatura varsa iptal ediliyor.

### (b) Gercek transaction - B-MINIMAL (kullanici karari)

Tasarim raporu once verildi, onay sonrasi uygulandi. Iki bolge ayrildi:

- **A bolgesi** (odeme guncelleme, siparis onayi, stok onayi/serbest birakma, kupon kaydi,
  zaman cizelgesi, cuzdan iadesi + defter kaydi) -> `IUnitOfWork.ExecuteInTransactionAsync`
  ile TEK gercek transaction. **Atomik durum gecisi de ICINDE**: kazanma artik KOSULLU,
  commit'te kesinlesiyor. A bolgesi patlarsa rollback gecisi de geri alir, odeme `Pending`'e
  doner, yeniden giris TEMIZ olur.
- **B bolgesi** (fatura, sadakat, referans, kupon sayaci) commit sonrasi kaldi ama artik
  SESSIZ degil: her adim `YanEtkiUygulaAsync` ile ayri sarilir, patlayan adim ADIYLA
  loglanir VE siparis zaman cizelgesine "UYARI: '<adim>' adimi basarisiz" notu dusulur
  (OrderManager'daki "KRITIK: para iadesi BASARISIZ" kalibiyla ayni).

**Manuel `BeginTransaction` DEGIL `ExecuteInTransactionAsync` secildi**: `Program.cs`'te
`EnableRetryOnFailure` yorumda duruyor ve gerekcesi "transaction kullanan manager'lar
`ExecuteInTransactionAsync`'e tasinmali - manuel `BeginTransaction` retry stratejisi
tarafindan REDDEDILIR"; `IyzicoPayment` o listedeydi. Engel kaldirildi.
**Bayragi ACMA karari AYRI ve alinmadi** (defterde - asagi). Iyzico retrieve cagrisi
transaction lambda'sinin DISINDA (her retry saglayiciya yeni sorgu gondermesin).

### PINLER

Kirilan (bilincli, ayni commit'te yenisi geldi):
- `BASARISIZ_ODEMEDE_FATURA_URETILIYOR_PINLENIR`

Yeni:
- `FraudReddi_FaturaBIRAKMAZ_VeCiroyaGIRMEZ` - siparis Cancelled + fatura yok ya da
  Cancelled + gercek `IDashboardService.GetSummary()` ile `total_revenue=0`, `total_orders=1`
- `BasariliOdeme_FaturaKesilir_Sent_VeCiroyaGirer` - regresyon (vakum kirici)
- `ABolgesiOrtasinda_HATA_TAMAMEN_GeriAlinir_YenidenGiris_TEMIZ` - A bolgesinin SON adiminda
  hata; odeme `Pending`'e doner, stok/rezervasyon/zaman cizelgesi bozulmaz, sonra yeniden
  giris temiz tamamlanir (retrieve=2, sadakat TAM 1)
- `BBolgesi_HATASI_OdemeSUCCESS_KALIR_Hata_GORUNUR_ABolgesi_BOZULMAZ` - sadakat adimi
  patlar; odeme Success kalir, fatura yine kesilir, zaman cizelgesinde adim ADIYLA gorunur
- `BasarisizDal_CuzdanIadesi_VeDefterKaydi_ATOMIK_AyrismaOLMAZ` - defter kaydi patlarsa
  bakiye de artmaz; calisan defterle ayni akis gercekten iade yazar

(iii) maddesi icin AYRI pin yazilmadi: `AyniSiparise_SekizParalelCallback_TEK_ISLENIR_SADAKAT_TEK_SATIR`
zaten tam bunu olcuyor ve transaction'li halde de yesil - kopyasi gurultu olurdu.

### DIS KONTROLU

4 assert ters cevrildi -> 3 AYRI test isimli kirmizi verdi (dorduncu flip ayni testte
oldugu icin ilk flip'ten sonra rapor edilmedi - once o kirilir). 5. kontrol (uretim
mutasyonu) `ExecuteInTransactionAsync` -> transaction'siz kosucu ile yapildi ve **S7 oncesi
zarari birebir yeniden uretti**: odeme `Success` kaldi (kalici kismi durum) ve cuzdan
bakiyesi **100.00** artip defterde iz kalmadi (ayrisma). Hepsi geri alindi.

## E4a - ADMIN EKRAN BOSLUKLARI (TAMAMLANDI)

Launch on kosulu kapandi: stok duzeltme ve gorsel yukleme uclari VARDI, arayuz YOKTU.

### YAPILAN

**Yeni admin ucu** `GET /api/Stock/{productId}` -> beden basina `stock_quantity` +
`reserved_quantity` + `available`. AYRI DTO (`ProductStockDetailDto`) acildi; mevcut
`ProductStockDto` ANONIM uclarda donuyor, oraya `reserved_quantity` eklemek "kac kisi
sepetinde tutuyor" bilgisini herkese acardi. `StockController` sinif duzeyinde
`[RequireUserType(Admin)]` oldugu icin yeni uc otomatik korumali.

**api-client.js**: `stock.byProduct` / `stock.adjust` / `productImage.upload` (FormData) /
`productImage.byProduct` + `setPrimary` / `remove`.

**admin.html**: STOK ekrani (urun+beden sec -> fiziksel/rezerve/satilabilir -> delta+sebep
-> uygula) ve GORSEL ekrani (coklu yukleme + mevcut gorseller + birincil yap/sil).

### ROTA VE SEMANTIK DUZELTMELERI (kod okunarak, varsayim degil)

- Gorsel ucu `api/product-image` (TIRELI). `/api/ProductImage/upload` **404** doner.
- `StockAdjustDto.new_quantity` **MUTLAK yeni deger**, delta DEGIL. Panel operatorden
  FARK aliyor, mutlaga cevirip gonderiyor ve sonucu gondermeden ONCE ekranda gosteriyor.
- Gorsel alani `is_primary` (`is_main` degil); birincil ucu `POST /api/product-image/{id}/primary`.

### PINLER (AdminStockAndImageTests - 5 test)

- `StokDetayi_Admin_200_VeUcAlanDaDOGRU` - 10 fiziksel / 3 rezerve / **7 satilabilir**
- `StokDetayi_MusteriTokeni_403_Anonim_401` - cift-anlam kirici
- `AdminDuzeltme_StokDegisir_VeHareketKaydi_OLUSUR` - stok 25, rezerve DEGISMEZ,
  `StockMovement` tipi Adjustment ve miktari **FARK** (15), notta operatorun sebebi
- `MusteriTokeni_StokDuzeltmede_403_VeStok_DEGISMEZ` - 403 kozmetik degil
- `UploadedImage_NosniffVeMagicByte_PINLENIR` - sahte content-type 400 + kayit yok;
  gercek imzali 200; istemci dosya adi URL'de YOK; servis edilen gorselde `nosniff`

### DIS KONTROLU

4 assert ters -> 4 AYRI isimli kirmizi. 5. kontrol: `available = stock - reserved`
ifadesindeki rezerve dususu kaldirildi -> iki pin kirildi (`beklenen 7, bulunan 10` ve
`beklenen 23, bulunan 25`). Hepsi geri alindi.

### ELLE DOGRULAMA (yol raporda; ozet)

`dotnet ef database update` -> `dotnet run --urls http://localhost:5000` -> gercek
register/verify/login ile admin (user_type DB'de 1 yapildi) -> kategori+urun+stok gercek
admin uclarindan -> musteri siparisi ile GERCEK rezervasyon -> `GET /api/Stock/{id}`
10/3/7 -> adjust 25 -> hareket kaydi -> 403/401 yetki matrisi -> sahte/gercek gorsel
yukleme -> statik servis + `nosniff`. Ardindan panel `http://localhost:5173`'te (CORS
`AllowedOrigins` listesinde) surulup STOK ve GORSEL ekranlari elle kullanildi.

## E1 - STOREFRONT AUTH + KATALOG (TAMAMLANDI)

Frontend + config disina CIKILMADI. Backend gerektiren her bulgu SUPHELI'ye yazildi.

### OLCULEN BES SOZLESME HATASI (hepsi istemci tarafinda kapatildi)

| # | Bulgu | Kanit |
|---|---|---|
| 1 | `auth.login` `data.data.access_token` okuyordu; API `data.token` donuyor | Token HIC saklanmiyor, login 200 ama her cagri 401 (E4a'da panele girilemiyordu) |
| 2 | `_tryRefresh` GOVDESIZ POST atiyordu | Uc `[FromBody] RefreshTokenRequestDto` bekliyor -> **415** |
| 3 | Katalog `GET /api/product/getlist` cagiriyordu | O uc `[RequireUserType(Admin)]`; anonim **401** -> kopru her seferinde sessizce MOCK'a dusuyordu |
| 4 | Arama `q` gonderiyordu | Uc `[FromQuery] ProductSearchRequestDto` -> alan adi `query`; `q` HIC baglanmiyor, arama filtresiz |
| 5 | Gorsel URL'leri goreli | Storefront ayri origin'de -> kendi origin'ine cozulup 404 (`api.resolveUrl` ile duzeltildi) |

### REFRESH TOKEN GERCEGI (devir notu duzeltildi)

Onceki devirde "refresh httpOnly cookie" yazilmisti. **Yanlis:** `SetRefreshTokenCookie`
yardimcisi TANIMLI ama HIC CAGRILMIYOR; login refresh token'i GOVDEDE donuyor, `/api/auth/refresh`
de GOVDEDE bekliyor, `AuthManager.RefreshToken` hicbir yerde cookie okumuyor. `Logout` ise
hic yazilmayan cookie'yi okuyor. Bugun calisan sozlesme: **govde**. Istemci buna uyduruldu;
guvenlik notu `setRefreshToken` uzerinde ve SUPHELI'de.

### MOCK YOLLARI KAPANDI

- Katalog `POST /api/product/filter` (anonim) ile geliyor; `sort`/`sizes`/`colors` HER ZAMAN
  gonderiliyor (non-nullable -> eksikse 400 "The sort field is required").
- API 0 urun donerse **mock'a DUSULMEZ**: "Katalog su an bos" durumu cizilir.
- API hata verirse "Urunlere ulasilamadi + Tekrar dene" cizilir.
- Kayit/giris/cikis index.html'in sahte `login(name)` yolundan alinip gercek uclara baglandi.

### LISTE YOLU BOSLUGU ICIN ISTEMCI TELAFISI

`filter` yolu `category_name` / `total_stock` / `sizes` DOLDURMUYOR (ProductProfile ucunu de
Ignore ediyor; admin `GetList` sizes'i sonradan dolduruyor, storefront yolu doldurmuyor).
Ham veriyle vitrin bastan sona "Tukendi" gorunuyordu. Telafi:
- kategori: `category_id` + `/api/category/getlist` (tam cozum, ayrica `T["cat_<slug>"]`
  ceviri tablosuna eklenerek "cat_e4a-kategori" ham anahtar basimi da duzeltildi)
- stok/beden: her urun icin detay ucu (`enrichAll`, 6 esmanli, sayfa boyutu 24)
- **`p._ss` tuzagi**: storefront beden-stok'u `_ss`'te ONBELLEKLIYOR ve ilk cizim stok=0 iken
  yapildigi icin "tum bedenler 0" olarak DONUYORDU; gercek harita `_ss`'e yaziliyor.

### PINLER (StorefrontCatalogContractTests - 4 test)

- `AnonimKatalog_FilterACIK_GetListADMIN_ISTER`
- `Filter_Sort_Sizes_Colors_ZORUNLU_PINLENIR`
- `Filter_ListeYolu_CategoryName_TotalStock_Sizes_DOLDURMUYOR_PINLENIR` (SUPHELI pini)
- `Arama_QueryParametresi_Filtreler_q_Parametresi_FILTRELEMEZ`

### DIS KONTROLU

4 assert ters -> 4 AYRI isimli kirmizi. 5. kontrol istemci mutasyonuyla: login okumasi eski
haline (`access_token`) cevrildi -> access token saklanmadi (yenileme telafi etti); yenileme
yolu da kapatilinca korumali cagri **401** dondu - E4a'da olculen belirti birebir.

### ELLE DOGRULAMA (tarayici)

Storefront `http://localhost:5173`'te (CORS `AllowedOrigins`'te), API `:5000`. Kayit ->
**warn logunda TOKEN YOK** (yalniz alici+konu; token DB'den alindi - devir notundaki
"warn-dali logundan token" varsayimi YANLIS) -> arayuzdeki dogrulama kutusundan verify ->
giris (access+refresh saklandi, `#/hesabim` yonlendirmesi) -> access token BOZULUP korumali
cagri yapildi: sessiz yenileme + tekrar -> BASARILI. Katalog: 2 gercek urun, dogru fiyat,
indirim rozeti, `_ss {M:20,L:4}`, stok 24; bos kategori sayfasi ve bos/hata katalog durumlari
cokmeden cizildi; arama "E4a" -> 1 sonuc.

## SIRA

1. **E1 run raporu** (push edildi, rapor bekleniyor)
2. **E2** sepet+checkout+odeme -> **E3** hesap+siparis takibi
   - E2'nin iki somut isi: `checkout_form_content` gomme + `#/odeme/sonuc` donus sayfasi
     (callback bugun ham JSON donuyor)
   - E3: CMS sanitizasyonu IKI katman (yazma `InputSanitizer` + okuma DOMPurify)
3. **Sema kapanis dalgasi** - kalan tek aday: **gift-card expiry**
   (`refunded_amount` Sprint 6'da kapandi; seller migration DEGIL - `sellers` ve
   `seller_id` zaten `InitialCreate`'te)
4. **E4b** (musteri askiya alma, kategori, CMS ekranlari) - launch sonrasi olabilir

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
- **`EnableRetryOnFailure`: S7'de ACILMADI.** S7 engeli kaldirdi (IyzicoPayment artik
  `ExecuteInTransactionAsync` kullaniyor) ama bayragi acmak AYRI bir karar ve alinmadi.
  Acmadan once `Program.cs` yorumundaki DIGER manager'lar (OrderManager, GiftCard,
  Loyalty, Referral, Return, StoreCredit) da tasinmali - aksi halde onlarin manuel
  `BeginTransaction` cagrilari retry stratejisi tarafindan REDDEDILIR.
- **SPRINT 8 DEFTERE** (simdi is yok, E fazi sonunda karar): `PaymentConfirmed` outbox'a
  tasima (altyapi hazir: `outbox_messages` + `OutboxService` + `OutboxProcessor` atomik
  claim/reclaim + `Cron.Minutely`) **+ kupon `used_count` idempotency** (on kosul).
  Kazanci: B bolgesi hatasi sessiz kalmak yerine yeniden denenir; maliyeti eventual
  tutarlilik (~1 dk) ve at-least-once idempotentlik zorunlulugu.

## SUPHELI DAVRANISLAR - KARAR BEKLEYENLER

Sprint 5'in iki maddesi (kilit/sadakat ciftlenmesi + kumulatif iade siniri) **S6'da**,
Sprint 6'nin iki maddesi (basarisiz odemede fatura + transaction'siz callback) **S7'de**
KAPANDI. Acik kalan / yeni bulunanlar:

1. **Kupon `used_count` artisi IDEMPOTENT DEGIL.** (S7 tasarim calismasinda olculdu)
   `IncrementCouponUsageWithRetry` duz bir sayac artisidir. Bugun zararsiz cunku
   callback tam bir kez calisiyor; ama B bolgesi at-least-once bir mekanizmaya
   (outbox) tasinirsa sayac FAZLA sayar. Sprint 8'in on kosulu - defterde.
   Cozum adaylari: `coupon_usages` satirlarindan turetmek ya da `(coupon_id, order_id)`
   unique indeks + artisi insert basarisina baglamak.
2. **`InvoiceManager.GenerateForOrder` siparis DURUMUNU kontrol etmiyor.** (S7'de
   okundu, dokunulmadi) Var olan herhangi bir siparis id'si icin fatura kesiyor;
   tek koruma cagiranin dogru yerden cagirmasi. S7'de cagri onay dalina tasindigi
   icin bugun sorun yok, ama uc kendi basina korumasiz. Duzeltme karari kullanicinin.
3. **`LocalImageStorage` dosyayi CWD'ye yaziyor, `UseStaticFiles` ContentRoot'tan sunuyor.**
   (E4a'da OLCULDU) `PhysicalRoot = Directory.GetCurrentDirectory()/wwwroot/uploads/products`,
   sunum ise `IWebHostEnvironment.WebRootPath` (= ContentRoot/wwwroot). Ikisi yalniz
   CALISMA DIZINI content root ile AYNI oldugunda ortusur - `dotnet run --project` ve
   normal yayinlarda ortusuyor, ama calisma dizini farkli baslatilan bir servis (systemd
   `WorkingDirectory` verilmemis, Windows Service) yuklemeleri hic servis edilmeyen bir
   dizine yazar: yukleme "basarili" doner, gorsel SONSUZA KADAR 404. Testte bu ayrisma
   birebir gozlendi (dosya test bin'ine yazildi, 404 alindi) ve test host'unda
   `UseContentRoot(CWD)` ile hizalandi. Uretim duzeltmesi `WebRootPath` kullanmak olurdu -
   YAPILMADI, karar kullanicinin.
4. **Storefront liste yolu `category_name` / `total_stock` / `sizes` DOLDURMUYOR.**
   (E1'de olculdu, pinlendi) `ProductProfile` ucunu de `Ignore` ediyor; admin `GetList`
   `sizes`'i sonradan dolduruyor ama `GetListSearchAndFilterWithPaging` (yorumunda
   "storefront" yazan yol) hicbirini doldurmuyor. Ham veriyle vitrindeki HER urun
   "kategorisiz + 0 stok + bedensiz" -> bastan sona "Tukendi" gorunur. E1 istemci
   tarafinda telafi etti (kategori: `category_id`+kategori listesi; stok/beden: urun
   basina detay cagrisi, sayfa boyutu 24). Kalici duzeltme backend'de: liste yolu da
   admin `GetList` gibi doldurmali. Pin: `Filter_ListeYolu_..._DOLDURMUYOR_PINLENIR`.
5. **Refresh token httpOnly cookie ile TASINMIYOR (devir notu YANLISTI).**
   (E1'de olculdu) `AuthController.SetRefreshTokenCookie` TANIMLI ama HIC CAGRILMIYOR;
   login refresh token'i GOVDEDE donuyor, `/api/auth/refresh` `[FromBody]` bekliyor,
   `AuthManager.RefreshToken` hicbir yerde cookie okumuyor. `Logout` ise hic yazilmayan
   cookie'yi okuyor (`Request.Cookies["refresh_token"]` -> null). Yani "access localStorage
   + refresh httpOnly cookie" modeli YARIM: yazma yolu olu. E1 istemciyi bugun CALISAN
   sozlesmeye (govde) uydurdu; refresh token JS'in erisebildigi yerde duruyor ve bu
   httpOnly'den ZAYIF. Duzeltme BACKEND isi (cookie yaz + cookie'den oku + logout'u
   duzelt), karar kullanicinin.

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
