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
- **KULTUR BAGIMLI LITERAL YASAK (E3 run'inda BIR KEZ BEDELI ODENDI).** Testte
  `"549,90"` gibi bicimlenmis sayi/tarih dizgesi ELLE yazilmaz. Yerel makine `tr-TR`,
  GitHub kosucusu invariant kulturde kosar; ayni assert yerelde YESIL, CI'da KIRMIZI olur
  (olculdu: tr-TR `549,90`/`1.049,70` - Invariant `549.90`/`1,049.70`). Beklenen deger,
  uretimin KULLANDIGI bicimle HESAPLANIR: `deger.ToString("N2", CultureInfo.CurrentCulture)`.
  Not: bu kural testin sorunudur; uygulamanin kultur PINLEMEMESI ayri bir bulgudur
  (SUPHELI #13).

## 6b. Rapor bicimi (KALICI)

Her rapor, mesajin SONUNDA ayrica **TEK PARCA DUZ METIN** olarak **TEK kod blogu**
icinde tekrarlanir. Gerekce: raporlar kopyalanip baska yere yapistiriliyor ve zengin
bicim (tablo/kalin/baglanti) kopyada bos dusuyor.

- Tablolar duz satira cevrilir (`Alan: deger` ya da `A | B | C` duz metin).
- Kalin/italik/markdown baglanti isaretleri kullanilmaz; dosya yolu duz yazilir.
- Blok TEK parcadir - ikiye bolunmez, araya aciklama girmez.
- Zengin bicimli anlatim yukarida kalir; kod blogu onun duz metin karsiligidir.

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
- **`dotnet test` kosan HER adim ciktisini AYNI teshis dosyasina yazar** (E3 run'inda
  olculdu). Eskiden yalniz "Testler + coverage" `tee` ediyordu; "SQL gerektiren testler"
  kirildiginda coverage adimi SKIPPED oluyor, `test-output.txt` HIC OLUSMUYOR ve TESHIS
  adimindaki `if [ -f "$F" ]` guard'i yuzunden **tek bir `::error::` bile basilmiyordu**.
  Sonuc: CI job'inda tek failure annotation "Process completed with exit code 1." - hangi
  testin kirildigi ANONIM OKUYUCUYA gorunmuyor. SQL adimi artik `set -o pipefail` +
  `tee -a test-output.txt` kullaniyor.
- **Annotation suzgecinde ONCE test sonucu satirlari, SONRA istisna satirlari.** Tek
  gecisli `grep | head -20` dosya sirasini korur; uygulamanin Serilog ciktisi test kosumu
  SIRASINDA onlarca farkli `...Exception:` satiri yazar, `Failed <test adi>` ise kosumun
  SONUNDA gelir - gurultu ilk 20'yi doldurup asil bilgiyi disarida birakabilir. Olculdu
  (30 farkli istisna satiri + 1 Failed): eski desende ilk 20'de Failed satiri **0**,
  iki gecisli desende **1**.

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
- **E1 push `748c592` — HER IKI WORKFLOW TAMAMEN YESIL** (run 32395415468 CI + 32395415528 Security).
- **E2 (sepet + checkout + odeme) TAMAMLANDI ve push edildi** — asagidaki bolume bak.
- **E2 push `eb449fe` - HER IKI WORKFLOW TAMAMEN YESIL** (run 32410645333 CI + 32410645097 Security).
- **E2b (sandbox dogrulama dalgasi) TAMAMLANDI** - asagidaki bolume bak. Gercek Iyzico
  sandbox'ta uctan uca suruldu: basarili kart, 3DS basarili, 3DS dustu, replay, kismi iade.
- **Format dalgasi push `1d2b43b` - HER IKI WORKFLOW YESIL** (run 32497264566 CI + 32497264657 Security).
  Sertlestirilen iki adim (`whitespace` + `style`, ZORUNLU) SUCCESS; **failure annotation SIFIR**
  (once ayni job "Process completed with exit code 2." tasiyordu).
- **E3 (hesap + siparis takibi + CMS + iki katman sanitizasyon) TAMAMLANDI** - asagidaki
  bolume bak. Icinde E3 elle dogrulamasinda BULUNAN bir uretim hatasinin (SuccessDataResult
  asiri yukleme belirsizligi) KAPSAM SINIRLI duzeltmesi de var; **kok sebep ACIK**, Sprint 8
  madde 11.
- **E3 push `e8b5042` - HER IKI WORKFLOW KIRMIZI.** Adim bazinda okundu: CI
  `build-and-test` -> "SQL gerektiren testler (ATLANMAMALI)" **FAILURE** ("Testler + coverage"
  bu yuzden SKIPPED); Security `tests` -> "Entegrasyon testleri" **FAILURE**. `format-check`
  job'inin IKI ZORUNLU adimi da SUCCESS ve o job'da failure annotation YOK.
  **Kok sebep OLCULDU: kirilan tek test `FaturaHTML_Ucu_DOLU_GOVDE_Doner_ContentLength_SIFIR_DEGIL`
  ve sebep KULTUR BAGIMLI BIR TEST LITERALI** (`Contain("549,90")`). Yerel makine `tr-TR`,
  GitHub kosucusu invariant: olculdu -> tr-TR `549,90` / `1.049,70`, Invariant `549.90` /
  `1,049.70`. Uretim kodu dogru calisiyordu (govde DOLU geldi - `Content-Length` asserti
  GECTI, yalniz `Contain` asserti dustu). **Duzeltme yerelde hazir, push karari kullanicinin.**
- **E3 kirmizisi ve duzeltmesi push `91f8d21` - HER IKI WORKFLOW YESIL**
  (run 32513034626 CI + 32513034566 Security; adim bazinda + annotation duzeyinde dogrulandi:
  `format-check` job'inda ve `build-and-test` job'inda **failure annotation SIFIR**).
  Kirmizinin sebebi: testte KULTUR BAGIMLI bir literal (`Contain("549,90")`). Yerel makine
  tr-TR, GitHub kosucusu invariant. Uretim kodu dogru calisiyordu. Ayrintisi ve iki kalici
  kural (bolum 6 ve bolum 7) ilgili yerlerde.
- **SPRINT 8: ON UC KALEMIN TAMAMI TAMAM.** Madde 9'un 2. turu gercek odemeyle suruldu
  (siparis #34, callback + webhook carpismasi idempotent cikti). Ayrinti Sprint 8 bolumunde.
- **Yerel (madde 3 + madde 9 sonrasi): 198/198 `Category=Sql`, tam suitte 322 basarili /
  325 toplam** - kirilan 3'un UCU DE `OrderEndpointTests` (Testcontainers; yerelde Docker
  kapali, CI'da yesil kosuyor). Baska kirmizi YOK.
- **Yerel (Sprint 8 madde 3/9 ONCESI): 188/188 `Category=Sql`, 312/312 tam suit, Release 0
  hata, format kapilari TEMIZ.**
- **Yerel (E3 sonrasi): 168/168 `Category=Sql`, 289/289 tam suit** (Testcontainers'li
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

## E2 - SEPET + CHECKOUT + ODEME (TAMAMLANDI)

### ONAYLI TEK BACKEND ISTISNASI: callback 302

`PaymentController.Callback` artik `Storefront:BaseUrl` + `#/odeme/sonuc?order=..&status=..`
adresine **302** doner. Sinirlar bilincli:
- `HandleCallback` DEGISMEDI (imza + S2S retrieve + atomik gecis + yan etkiler aynen).
  Yalniz bu action'in YANIT BICIMI degisti.
- **Webhook JSON donmeye DEVAM EDIYOR** - onu tarayici okumuyor.
- `Storefront:BaseUrl` bossa ESKI davranis (JSON) korunur - yapilandirmasi eksik bir
  ortamda callback sessizce bozulmaz.
- Siparis id'si icin `IPaymentService.GetOrderIdByTokenAsync` **eklendi** (salt-okur,
  ayri metot). Mevcut imzalara dokunulmadi.

Yapilandirma: `Storefront:BaseUrl` (prod `https://divisima.com`, dev `http://localhost:5173`)
ve dev'de `Storage:PublicBaseUrl = http://localhost:5000` (gorsellerin storefront'ta 404
olmamasi icin - example.json'a gerekcesiyle yazildi).

### ISTEMCI SOZLESME DUZELTMELERI (olculdu)

- `cart.remove` GOVDE gonderiyordu; uc `Remove(int productId, string size)` ile **sorgu
  dizesi** bagliyor -> parametreler hic baglanmiyordu.
- `cart.add` UPSERT'tir ve adeti **SET** eder (artirmaz) - "adet guncelle" de bu ucu kullanir.
- Eski `divisimaCheckout` calisir bir yol DEGILDI: `payment_type` (dogru ad `payment_method`),
  `coupon_code: null` (non-nullable -> 400), `customer_id` yok (validator > 0), `items` HIC
  gonderilmiyordu. Kaldirildi, yerine gercek checkout paneli geldi.
- Siparis detayi `total` + `order_status` (METIN) doner - `total_price`/`status` DEGIL;
  yanlis alan okununca sonuc sayfasi "0,00 TL / undefined" gosteriyordu.

### SEPET

Yerel `cart` Map ekran icin kaynak olmaya devam ediyor; her mutasyon sunucuya aynalaniyor
(`addToCart` sarmalandi, `renderCart` sonrasi 250 ms'lik tam esitleme). Aynalama hatasi
SESSIZ DEGIL - toast ile bildiriliyor (bedensiz giyim kalemi sunucuda reddediliyor;
checkout da bunu ADIYLA soyleyip erken duruyor).

### CHECKOUT PANELI (mock ekranin yerine)

index.html'in checkout'u MOCK'tu: yerel adres listesi, **yerel kart formu**, yerel kupon
tablosu. Kart bilgisi bize HIC gelmemeli, bu yuzden o ekran gercek panelle degistirildi:
adresler API'den (sec + olustur), kupon API'den, magaza kredisi `/api/Account/summary`'den,
kargo kurali backend ile ayni (>= 2000 bedava, degilse 49.90) ve TAHMIN oldugu yaziyor.
Kart / kapida odeme secimi UI'da.

### PINLER (PaymentCallbackRedirectTests - 4 test)

- `BasariliCallback_302_ile_SonucSayfasina_Yonlendirir` (order + status=success + odeme
  gercekten islenmis)
- `BasarisizCallback_302_status_failed_Doner` (cift-anlam kirici; odeme Pending KALIR)
- `StorefrontAyariYOKSA_EskiDavranis_JSON_Doner`
- `Webhook_JSON_Donmeye_DEVAM_EDER_Yonlendirilmez`

**Kirilan eski pin YOK**: HTTP duzeyinde callback pini hic yoktu; mevcut callback pinleri
(`PaymentCallbackSecurityTests`) servisi DOGRUDAN cagiriyor, o yuzden etkilenmediler.

### DIS KONTROLU

4 assert ters -> 4 AYRI isimli kirmizi. 5. kontrol: `Callback`'ten yonlendirme kaldirildi ->
iki 302 pini kirildi ve **E2 oncesi zarar birebir uretildi** (tarayici ham JSON'da: basarida
200, basarisizlikta 400). Digerleri (JSON dallari) dogru sekilde yesil kaldi.

### ELLE DOGRULAMA (tarayici, uctan uca)

Giris -> sepete ekle (yerel + sunucu `2:2`) -> adet 1'e dusur (`2:1`) -> sil (bos) ->
`#/odeme` paneli (adres olustur, ozet 999,80 + 49,90 = 1.049,70) -> kart ile siparis (#10)
-> Iyzico form gomuldu (mock modda icerik yorum satiri) -> callback POST -> **302** ->
sonuc sayfasi: siparis no, kalemler, toplam 1.049,70, durum Confirmed, sepet temizlendi.
Basarisiz yol: bozuk imza -> 302 `status=failed` -> "Odeme tamamlanamadi", durum **Pending**,
**sepet KORUNDU**, "Tekrar dene". Kapida odeme: siparis #12 dogrudan Confirmed.
Kupon: `%10` kuponu ekran tahmininde 499,81 gosterdi; sunucunun hesapladigi toplam da
**499,81** - istemci tahmini sunucuyla birebir ortusuyor. Uc siparis de `/api/order/my-orders`
listesinde gorunuyor.

Not: ilk kupon denemem `discount_value` alan adiyla olusturuldugu icin indirim 0 cikti;
dogru alan `value`. Backend dogruydu, test verisi yanlisti.
## E2b - SANDBOX DOGRULAMA DALGASI (TAMAMLANDI)

Gercek Iyzico sandbox anahtarlariyla (user-secrets) uctan uca surulen ilk dalga.
**Mock modun goremedigi uretim kusurlari burada ortaya cikti.** Anahtarlar hicbir yere
yazilmadi; teshislerde yalniz `IConfiguration`/`Options` nesnesine verildi, ciktiya HIC basilmadi.

### UC ONAYLI BACKEND ISTISNASI

**(1) `Iyzico:CallbackUrl` config fallback.** `Initialize`'da DTO doluysa MEVCUT davranis aynen
(SSRF guard dahil); BOS ise operator girdisi olan config degeri kullanilir - config kullanici
girdisi olmadigi icin guard'a TABI DEGIL. Engel olculdu: storefront `callback_url` gondermiyor,
manager `?? ""` yaziyordu, gercek Iyzico BOS callbackUrl kabul etmiyor; dev adresi
(`http://localhost:5000/...`) ise guard'dan gecemiyor (yalniz public HTTPS). Dev degeri
`appsettings.Development.json`'a, aciklamali prod yer tutucusu example.json'a.

**(2) CF callback IMZA MODELI.** OLCULDU (tarayici Network > callback > Payload > Form Data):
Iyzico CF callback POST'unda TEK alan var, `token` - `signature` alani YOK. Eski kod imzayi
kosulsuz zorunlu tuttugu icin GERCEK Iyzico ile her gecerli odemenin callback'i reddediliyordu
(olculdu: callback 4 ms'de 400 doner, retrieve HIC calismaz, odeme Pending kalir, para Iyzico'da).
Cozum: `HandleCallback(dto, bool imzaZorunlu = true)` - varsayilan TRUE (fail-closed).
`PaymentController.Callback` TEK yerde `imzaZorunlu: false` veriyor; `Webhook` ACIKCA
`imzaZorunlu: true`. Imza GELIRSE her iki yolda da dogrulanir. Otorite imza degil: sunucu-sunucu
retrieve + token zaman asimi (30 dk) + tutar/para birimi/fraud + "yalniz Pending islenebilir".
`VerifyCallbackSignature`'in KENDISINE dokunulmadi.

**(3) IADE KIMLIGI (B1) + SERBEST BIRAKMA BELLEK ESITLEMESI (B2).**

- **B1:** Iyzico refund `paymentId`'yi DEGIL, odeme KIRILIMININ (itemTransaction)
  `paymentTransactionId`'sini ister. OLCULDU (gercek retrieve yaniti): ayni odemede
  `paymentId=37399936` iken `paymentTransactionId=39316344`, `itemTxSayisi=1`. Kirilim sayisinin
  1 olmasi bizim CF init'imizin sepeti TEK `BasketItem` gondermesinden geliyor -> **tek kolon
  yeterli**: `payments.item_transaction_id` (migration `20260820234946_ItemTransactionIdForRefund`,
  `01_schema.sql` guncellendi). Kismi iade bu tek kirilim uzerinden TUTAR bazli yapilir.
  `itemTxCount != 1` ise `LogError`. Kimlik YOKSA iade SESSIZCE CUZDANA KAYDIRILMAZ - gurultulu
  duser (kartla odenmis siparisin iadesini magaza kredisine cevirmek musteriye parasini
  VERMEMEK demektir).
- **B2:** `ExecuteUpdateAsync` change-tracker'i atladigi icin `ReleaseRefundedAmountAsync` DB'de
  hakki serbest birakiyor ama cagiranin IZLENEN `Order` nesnesi hala `+granted` tasiyordu;
  iadeden sonra kosan herhangi bir `SaveChanges` bayat degeri GERI YAZIYORDU. Gecici teshis
  testiyle olculdu: `serbestBirakma=0,00 bellek=100,00 saveChanges=100,00`. Iki serbest birakma
  dalina `order.refunded_amount -= ...` eklendi. S6 pini bunu goremiyordu cunku her cagriyi AYRI
  DI scope'unda yapiyor ve `order` orada DETACHED.
- **MOCK SERTLESTIRILDI:** mock `RefundAsync` eskiden HER kimlige `Success=true` donuyordu - tip
  karisikliginin hicbir testte gorunmemesinin sebebi buydu. Artik mock retrieve kendi
  `ITX-<guid>`'sini uretip kaydediyor, `RefundAsync` yalniz o kimlikleri kabul ediyor.

### FRONTEND (CSP + SERVICE WORKER)

**CSP - her direktif AYRI olculdu**, tahminle eklenmedi. Uc tur, uc ayri blok:

- `script/style/font/img` -> `sandbox-static` / `static` / `cdn.iyzipay.com` (bundle + kart
  logolari + Inter fontu)
- `connect-src` -> `sandbox-merchantgw` + `sandbox-consumerapigw` (+ prod karsiliklari). Canli
  `iyziInit` yapilandirmasi okunarak bulundu: kart POST'u `merchantGatewayBaseUrl`'e gidiyor,
  engellenince XHR hic tamamlanmiyor ve ODE butonu sonsuza kadar donuyor.
- `form-action` -> `http://localhost:5000` (callback) ve `sandbox-api.iyzipay.com` (3DS
  `/payment/mock/init3ds`). Prod karsiliklari `api.divisima.com` / `api.iyzipay.com`
  **OLCULMEDI isaretiyle** eklendi.

`connect-src`'de bir host bulunmasi `form-action`'i KAPSAMIYOR - bu ders bir tur bedeline
ogrenildi. SENKRON KURALI (`form-action` = `Iyzico:CallbackUrl` origin'i) example.json'a yazildi.

**SERVICE WORKER (SUPHELI #7 - KAPANDI).** Ayrintili kok sebep ve kapanis olcumu SUPHELI #7
maddesinde. Ozet: surumlu CACHE + gercek temizlik + `skipWaiting`/`clients.claim`, ve
navigasyon/`.html`/`.js` NETWORK-FIRST (surum bumpi unutulsa bile duzeltme ulasir). Offline
yedegi artik YALNIZ navigasyona veriliyor. `pwa-register.js`'e `reg.update()`.

**Hesabim > Siparislerim COKMESI** (SUPHELI #6): E2b yalniz yalani ve cokmeyi kaldirdi
(`wireAccountOrders`); gercek liste E3 madde (a).

### SANDBOX TURLARI - OLCULEN SONUCLAR

Basarili kart (#28): callback 469 ms (retrieve kostu), Confirmed, `paid_price=1049.70`,
`fraud=1`, fatura Sent, sadakat TAM 1 satir, stok 17->15, 0 uyari.

Replay (ayni token 2. kez): 302 `success`, 4,4 ms (sorguya ULASMADI), sadakat/fatura/puan/stok
DEGISMEDI.

Bozuk imza: 302 `status=failed`, odeme Pending, rezervasyon bozulmadi.

3DS DUSTU (#30): `paymentStatus=FAILURE`, `mdStatus=0`, `fraudStatus=1`. Basarisiz dal kusursuz:
odeme Failed, siparis Cancelled, **fatura 0**, sadakat 0, stok geri, 0 uyari.
**S7'nin "basarisiz odemede fatura kesilmez" duzeltmesi CANLI dogrulandi.**

3DS BASARILI (#31): Confirmed, `transaction_id=37410742`, **`item_transaction_id=39327281`**
(B1 canli teyidi - #28'de BOS'tu), fatura Sent, sadakat 1 satir.

Kismi iade (#31): GERCEK Iyzico iadesi BASARILI - online 300,00, `refunded_amount` 0 -> 300,00
(DB ve bellek), store credit 0, defter 0. Ayni islem #28'de "odeme kirilim kaydi bulunamadi"
ile dusmustu.

Clamp REDDI (#28): `granted<=0` -> `Success=false`, saglayiciya HIC cagri yok, sayaclar sabit.

**CANLIDA SURULMEYENLER (durust kayit):**

- **Kumulatif clamp KIRPMASI canlida SURULMEDI.** Kirpma kalan hakkin TAMAMINI verdigi icin
  "#31 kismi-iadeli kalsin" ile birlikte saglanamiyordu; kullanici kismi veriyi secti. Pin
  `KismiIadeler_UcuncuCagri_KalanHakka_KIRPILIR` + sertlestirilmis mock + dis kontrolu ile kapali.
- **Fraud reddi dali** (`fraudStatus` 0/-1) sandbox'ta zorlanamadi; S7 pini ile kapali.
- #28'in "hakki tukenmis" satiri Bulgu 2'nin DUZELTME ONCESI hasari; bugunku kodla olusmaz,
  clamp fixture'i olarak kullanildi.

**DERS:** bu sandbox'in 3DS SMS kodu SABIT DEGIL - ekranda gosteriliyor. Talimattaki `123456`
ile denenen tur `mdStatus=0` ile dustu (ve boylece basarisiz dal canli olculdu).

### PINLER

Yeni (7 test):
`DtoBOS_ConfigDOLU_IstemciyeGidenIstek_CONFIG_ADRESINI_TASIR`,
`DtoDOLU_DTO_KAZANIR_GecersizDTO_Configle_KURTARILMAZ`,
`CFCallback_YALNIZ_TOKEN_ile_ISLENIR_GercekIyzicoBicimi`,
`Webhook_ImzaSIZ_REDDEDILIR_CF_Gevsemesi_SIZMAZ`,
`Iade_ODEME_KIRILIMI_Kimligiyle_Gonderilir_PaymentId_ILE_DEGIL`,
`Refund_KirilimKimligi_YOKSA_GURULTULU_DUSER_CuzdanaKAYMAZ`,
`Refund_SaglayiciReddedince_SerbestBirakilanHak_SonrakiSaveChangesTe_GERI_YAZILMAZ`.

Bilincli DEGISTIRILEN: `ImzaYOKSA_Reddedilir` -> `ImzaYOKSA_Reddedilir_KATI_YOL_WEBHOOK`
(kirilmadi; `CallbackAsync` servisi dogrudan cagirdigi ve varsayilan strict oldugu icin yesil
kaldi ama KAPSAMI DARALDI - adi bunu soylemezse pin yalan soyler).
**KIRILAN PIN YOK** - fail-closed varsayilan secildigi icin hicbir S5 pini kirilmadi.

Harnesta guncellenen 6 pin (ASSERT'LER DEGISMEDI, yalniz sahte saglayici olculen sozlesmeye
uyduruldu - `RetrieveOverride`'lar artik `ItemTransactionId` tasiyor):
`KartIadesi_Iyzicoya_DogruTutarla_Gonderilir`, `KumulatifIade_..._IYZICOYA_FAZLA_IADE_GITMEZ`,
`KismiIadeler_UcuncuCagri_KalanHakka_KIRPILIR`, `SaglayiciIadesi_BASARISIZSA_IadeHakki_BLOKE_KALMAZ`,
`EszamanliIkiTamIade_...`, `Refund_KartliSiparis_KartVeCuzdanPayinaBOLUNUR`. Ayrica
`Refund_IyzicoBasarisizsa_...` YESILDI ama YANLIS SEBEPTEN dusuyordu (kimlik yok, saglayici
reddi degil) - cift-anlam kirici olarak duzeltildi.

### DIS KONTROLU (iki dalga)

Imza dalgasi: 3 assert ters -> 3 AYRI isimli kirmizi. 5. kontrol: kosul `if (true)` yapilip E2b
oncesi kosulsuz zorunluluk geri getirildi -> yeni pin `status=failed` ile kirildi, kullanicinin
4. denemesinde gordugu zararin birebir aynisi.

B1+B2 dalgasi: 3 assert ters -> 3 AYRI isimli kirmizi. Iki uretim mutasyonu: **A**
(`RefundAsync(payment.transaction_id, ...)` + kimlik guard kapali) -> saglayiciya `PAY-KIMLIK`
gitti (gercek Iyzico'nun reddettigi kimlik) ve kimliksiz kayit SESSIZCE `Success=True` dondu.
**B** (bellek esitlemesi kaldirildi) -> `refunded_amount` 100.00 bulundu; siparis #28'de canli
olculen zararin birebir aynisi. Hepsi geri alindi.

### ORTAM NOTU

`dotnet run` ve statik sunucu bash arka planindan baslatilinca kabuk oturumu kapaninca
OLUYORLAR (E2b'de ikisi de fark edilmeden oldu; API logu hatasiz kesildi). Ikisi de
`Start-Process` ile AYRIK baslatilmali. Izleyici artik ikisinin sagligini da yokluyor ve
duseni geri kaldiriyor.


## E3 - HESAP + SIPARIS TAKIBI + CMS + IKI KATMAN SANITIZASYON (TAMAMLANDI)

Kapsam: (a) Hesabim ekranlari, (b) CMS `#/sozlesme` sayfalari, (c) iki katman sanitizasyon,
(d) bildirim abonelikleri, (e) elle dogrulama. Backend'e YALNIZ onayli iki istisnayla dokunuldu.

### ONAYLI BACKEND ISTISNALARI (IKI KALEM)

**(1) ICERIK TOHUMLAMA (Secenek A) + YAZMA KATMANI SANITIZASYONU.**
Olculen engel: storefront 10 sozlesme sayfasina link veriyor ama `contents` tablosu BOSTU ve
hicbir yerde tohumlama yoktu; metinler `index.html`'de GOMULUYDU. Gomuluyu kaldirip API'ye
baglamak tohumlama olmadan 10 BOS legal sayfa demekti.
- `Divisima.Bussiness/Seed/ContentSeeder.cs` - 10 icerik, metinler mevcut gomulu i18n'den
  BIREBIR cikarildi. **IDEMPOTENT**: slug varsa DOKUNULMAZ (admin'in CMS duzenlemesi sonraki
  aciliste ezilmez). `AdminSeeder` gibi bayrakla kapatilmadi - bos KVKK/mesafeli satis sayfasi
  yayinlamak kabul edilebilir bir varsayilan degil.
- `ContentManager.Update` artik DORT alani da (basliklar DAHIL) `InputSanitizer.Sanitize`'dan
  geciriyor. Uc `[RequireUserType(Admin)]` korumali ama "yetkili kullanici guvenilir icerik yazar"
  varsayimi stored XSS icin yetersiz; govde storefront'ta innerHTML ile ciziliyor.

**(2) `SuccessDataResult<string>` ASIRI YUKLEME BELIRSIZLIGI - IKI CAGRI DUZELTILDI.**
E3 elle dogrulamasinda BULUNDU, kullanici karariyla E3'e ALINDI (gerekce: Faturalarim E3'un
kendi teslimati ve ucu OLUYDU; dalga bilerek olu sekmeyle kapanmaz).
- **Kok sebep:** `SuccessDataResult<T>` dort kurucuya sahip; `T = string` oldugunda `(T data)`
  ile `(string message)` AYNI IMZAYA duser ve C# generic OLMAYAN adayi secer. Tek argumanli
  `new SuccessDataResult<string>(x)` cagrisinda x MESSAGE'a gider, DATA null kalir - ve
  `Success` yine `true` oldugu icin **hata SESSIZDIR**.
- **Olculen zarar:** `OrderManager.GetInvoiceHtml` -> `GET /api/order/{id}/invoice-html`
  **HTTP 200 + Content-Length: 0** (curl ile sunucu tarafinda olculdu) -> "Faturalarim" ekrani
  HIC CALISMAMISTI. `ReferralManager.GetOrCreateMyCode` -> `GET /api/referral/my-code`
  `{"data":null,"success":true,"message":"REF351E93"}` (canli olculdu).
- **Depo taramasi:** `SuccessDataResult<string>` **4 cagri**, 2'si hatali. Iki argumanli
  cagrilar (`GiftCardManager.cs:43`, `ProductImageManager.cs:83`) `(T data, string message)`
  ile eslestigi icin ETKILENMEZ.
- **Duzeltme KAPSAM SINIRLI:** yalniz o iki cagri `data:` ADLANDIRILMIS ARGUMANA cevrildi.
  **Kurucu SETINE DOKUNULMADI** - belirsizlik dilde duruyor, YENI yazilacak tek argumanli bir
  string cagrisi yine sessizce bozuk olur. Kokten cozum **SPRINT 8 MADDE 11**.
- **Canli teyit (duzeltme sonrasi):** `my-code` -> `{"data":"REF66E826","success":true,"message":null}`;
  `invoice-html` (siparis #32) -> **Content-Length: 1118**, govdede siparis no + kalem +
  `KDV (%20): 174,95 TL` + `Genel Toplam: 1.049,70 TL`.

### (c) IKI KATMAN SANITIZASYON

- **Yazma katmani:** `ContentManager.Update` -> `InputSanitizer.Sanitize` (yukarida).
- **Okuma katmani:** DOMPurify **LOKAL** dosya (CDN degil - CSP'de disa acilan tek istisna
  Google Fonts degil, hicbir yeni host acilmadi).
  - Surum **3.4.14**, kaynak resmi `cure53/DOMPurify` GitHub Release'i.
  - `frontend/vendor/purify.min.js` - **29.204 bayt**,
    SHA-256 `c2f26ea4fc0d88141c9aa430eb515ac86fce59418ceebd85fa475b87a8d6c3e6`.
  - Lisans **Apache-2.0 VE MPL-2.0** (MIT DEGIL - ilk raporda yanlis soylenmisti, duzeltildi).
    Baslik korunarak dahil etmeye uygun, ek islem gerekmiyor. `frontend/vendor/README.txt`
    surum + kaynak + karma bilgisini tasiyor.
  - `guvenliHTML()` / `guvenliYaz()` **FAIL-CLOSED**: DOMPurify yuklenmemisse HTML CIZILMEZ,
    yerine notr bir hata metni yazilir. "Kutuphane yoksa ham bas" davranisi kabul edilmedi.
- **Kanit:** `IcerikGuncelleme_ScriptliGovde_TEMIZLENMIS_Kaydedilir_MesruHTML_KORUNUR` -
  script/onerror/onload/`javascript:`/iframe DEPOYA GIRMIYOR, mesru `<h3>`/`<strong>` KORUNUYOR
  (cift-anlam kirici: "hepsini encode et" cozumu bu asserti gecemez).

### (a) HESABIM EKRANLARI

Yedi sekme gercek uclara baglandi: Ozet (`/api/Account/summary`), Siparislerim
(`/api/order/my-orders` + tembel `timeline` + `get/{id}`), Iadelerim (`/api/returns/my`),
Faturalarim (`/api/invoice/my` + `invoice-html` modali), Adreslerim (`/api/address` +
`upsert` + `remove`), Favorilerim/Kartlar index.html'in kendi cizicilerinde kaldi.

**Iade talebi UI'i** siparis detayinin icinde: kalem + adet + sebep (5 secenek) + tur
(Iade/Degisim) + aciklama. Uygunluk kurali backend ile AYNI (Delivered + 14 gun); uygun
degilse buton CIZILMEZ, yerine SEBEP yazilir.

### (d) BILDIRIM ABONELIKLERI

Stok bildirimi (urun detayindaki "gelince haber ver") ve fiyat uyarisi (favoriler cekmecesindeki
zil) mock akislardan gercek uclara baglandi (`stock-notification/subscribe`, `price-drop/subscribe`).

### OLCULEN VE DUZELTILEN ISTEMCI HATALARI (E3'un kendi yuzeyinde)

| # | Bulgu | Kanit / duzeltme |
|---|---|---|
| 1 | Zaman cizelgesinde `status_text` okunuyordu | Alan YOK; iki DTO da (`OrderStatusHistoryDto`, `ReturnResponseDto`) **`status_name`** kullaniyor. Ekranda `—` ve `1` gorunuyordu. Iade durumlari icin ayri etiket haritasi eklendi. |
| 2 | Hesabim ILK YUKLEME TUZAGI | Sayfa dogrudan `#/hesabim` ile acildiginda MOCK siparisler geri geliyordu (`DVS-20260012`); `router()` biz ezmeden once kosuyor. `wireLegal`'daki yamanin ayni `wireAccount`'a eklendi. |
| 3 | Favorilerim/Kartlar sekmesi KATALOG YARISI | Bu sekmeler index.html'in `cardHTML -> byId` cizicisini kullanir, yani KATALOGA baglidir. Katalog asenkron geldigi icin dogrudan `#/hesabim/favorilerim` acilisinda **MOCK urun** ciziliyordu (olculdu: favori id 2 -> "Yumusak Triko Kazak / 649 TL"; gercek katalogda id 2 = "E4a Test Urun / 499,90 TL"). `wireAccount` yamasi bu yarisi KAPATMIYOR (o, katalogtan ONCE kosuyor). `loadCatalog()` SONRASINA ikinci bir yeniden cizim eklendi. |
| 4 | Fiyat uyarisi giris yapmis kullaniciya da "giris yapmalisin" diyordu | `window.userEmail` okunuyordu; index.html o degiskeni kendi yerel deposundan (`dvs_profile`) dolduruyor ve GERCEK giris o alani DOLDURMUYOR (olculdu: giris yapilmis kullanicida `dvs_profile = {name:"E3 Fix", email:""}`). Dogru kaynak `/api/Account/summary`; bir kez cekilip onbellege aliniyor ve `window.userEmail` de esitleniyor. |

`ReturnResponseDto` **urun adi tasimiyor** (yalniz `product_id`) - katalogdan cozuluyor,
bulunamazsa kimlikle gosteriliyor; uydurma yok.

### PINLER

`ContentSeedAndSanitizeTests` (3):
- `Tohumlama_IDEMPOTENT_AdminDuzenlemesi_SonrakiAciliste_EZILMEZ`
- `TohumGovdeleri_Sanitize_ile_DEGISMEDEN_Gecer` (tohum ile yazma katmani arasinda CELISKI YOK)
- `IcerikGuncelleme_ScriptliGovde_TEMIZLENMIS_Kaydedilir_MesruHTML_KORUNUR`

`ResultOverloadPinTests` (4):
- `FaturaHTML_Ucu_DOLU_GOVDE_Doner_ContentLength_SIFIR_DEGIL` (UC duzeyi; `ContentLength > 0`
  DOGRUDAN pinlendi - olculen belirtinin kendisi)
- `ReferansKodu_Ucu_KODU_data_ALANINDA_Doner_message_te_DEGIL`
- `SuccessDataResultString_IKI_ARGUMAN_DOGRU_CALISIR_DATA_DOLAR` (cift-anlam kirici)
- `SuccessDataResult_StringOLMAYAN_TipTe_TEK_ARGUMAN_DATAYA_GIDER` (karsit kontrol)

**BILINCLI KIRILAN PIN:** `SuccessDataResultString_TEK_ARGUMAN_MESSAGE_a_GIDER_DATA_NULL_KALIR_PINLENIR`.
Bozuk davranisi KABUL EDILMIS gibi sabitliyordu; cagri yerleri duzeltilince yalan soyler hale
gelirdi. Yerini UC DUZEYI iki pin aldi; kurucu duzeyindeki dogru-davranis ve karsit kontrol
pinleri KORUNDU.

### DIS KONTROLU

5 assert ters cevrildi -> **5 AYRI isimli kirmizi** (`Tohumlama_IDEMPOTENT...`,
`TohumGovdeleri_Sanitize...`, `IcerikGuncelleme_ScriptliGovde...`, `FaturaHTML_Ucu...`,
`ReferansKodu_Ucu...`). Hepsi geri alindi.

**5. kontrol (uretim mutasyonu):** iki cagri `data:` adlandirmasindan tek argumanli eski
haline dondurulup temiz build alindi -> uc pinleri E3 oncesi zarari **BIREBIR** uretti:
`Expected resp.Content.Headers.ContentLength to be greater than 0L ... but found 0L` ve
`Expected dataAlani.ValueKind not to be JsonValueKind.Null ... but it is`. Kurucu duzeyindeki
iki pin dogru sekilde YESIL kaldi (onlar cagri yerlerine bagli degil). Mutasyon geri alindi.

### ELLE DOGRULAMA (tarayici, uctan uca)

Faturalarim: iki fatura listelendi (`DIV-2026-000028`, `DIV-2026-000031`), modal gercek fatura
govdesini DOMPurify'dan gecirerek cizdi (`<script>` YOK, 583 bayt DOM). **Not:** faturanin
satir ici `<style>` blogu DOMPurify izin listesinde olmadigi icin sokuluyor - icerik TAM,
bicimlendirme sade. Guvenli taraf bilincli secildi.

Siparislerim: 18 gercek siparis, dogru Turkce durum etiketleri. Detay tembel aciliyor
(kalemler + zaman cizelgesi). Teslim EDILMEMIS siparise iade butonu CIZILMIYOR, sebep yaziliyor.

Iade akisi (uctan uca): siparis #32 gercek admin ucuyla Confirmed -> Preparing -> Shipped ->
Delivered'a tasindi (`delivered_at` doldu) -> detayda "Iade talebi olustur" CIKTI -> form
gonderildi -> `return_requests` tablosunda **1 satir** (order 32, product 2, qty 1, reason 0,
status 0) -> "Iadelerim" sekmesi `Siparis #32 / Beklemede / E4a Test Urun · M · 1 adet` cizdi.

Adreslerim: UI'dan adres olusturuldu ("Ofis") ve silindi; liste her iki islemden sonra tazelendi.

Ozet: sadakat 104, magaza kredisi 0,00 TL, referans kodu, hesap bilgileri + dogrulama durumu.

Bildirimler: urun 1 beden L (DB'de stok 0, UI'da `size-chip out`) -> "gelince haber ver" ->
`stock_notification_requests` **1 satir** (product 1, size L, `is_notified=0`). Favoriler
cekmecesindeki zil -> `price_drop_subscriptions` **1 satir** (product 2, giris yapmis
kullanicinin GERCEK e-postasi, `subscribed_price=499.90`).

SW surumu `2026-08-21-e3`'e cekildi (kod tasiyan dosyalar zaten network-first; surum bumpi
eski onbellegi temizlemek icin).

## SPRINT 8 - LAUNCH ONCESI ZORUNLU DALGA (11/13 TAMAM, 2 KALEM KULLANICIDA)

Kalem sirasi ve UC COMMIT bolunmesi kullanici karari (bkz. Sprint 8 defteri basligi).
**KOD YAZILDI, COMMIT ATILMADI**: guvenlik commit'i madde 9'a, dogruluk commit'i madde 3
kararina bagli. Asagidakilerin tamami YERELDE yesil ve dis kontrolunden gecti.

### DURUM TABLOSU

**ON UC KALEMIN TAMAMI TAMAM: 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13.**
Madde 9'un 2. turu tunel uzerinden gercek odemeyle suruldu ve tam yesil (siparis #34);
gecici teshis middleware'i ve gecici CSP satiri KALDIRILDI.

### MADDE 6 - REFRESH TOKEN httpOnly COOKIE (ONCELIKLI, GUVENLIK)

Kapsam OLCUMLE GENISLEDI ve kullanici onayladi: **uc parca ayrilamaz.**
- `AntiforgeryMiddleware` yalniz "refresh_token cookie'si VAR + Bearer YOK" durumunda
  devreye giriyor. Cookie hic yazilmadigi icin middleware BUGUNE KADAR HIC CALISMADI.
- `csrf_token` cookie'sini depoda **hicbir yer YAZMIYORDU** (tarandi; tek gecen yer
  middleware'in kendi okumasi). Yani cookie yazilmaya baslandigi an `/api/auth/refresh`
  kalici **403** verirdi.
- Ucuncu uyusmazlik: istemci `XSRF-TOKEN` cookie'sini okuyup `X-XSRF-TOKEN` gonderiyordu;
  middleware `csrf_token` / `X-CSRF-Token` bekliyor. **UC YONLU AD UYUSMAZLIGI** - iki taraf
  da kendi adiyla calisiyordu, hicbir zaman eslesmedi. Istemci backend'in adlarina hizalandi.

YAPILAN: login / refresh / verify-2fa sonrasi `OturumCerezleriniYaz` - refresh_token httpOnly
cookie'ye yazilir, csrf_token cookie'si yazilir, **refresh token YANIT GOVDESINDEN SILINIR**.
`Refresh` action'i govde ALMIYOR (parametre kaldirildi - kalsaydi istemci cookie modelini
sessizce bypass edebilirdi). `Logout` cookie'yi okuyor ve siliyor - o satir artik GERCEKTEN
calisiyor. Istemci refresh token'i ne goruyor ne sakliyor; eski surumden kalan localStorage
kalintisi ilk kurulumda TEMIZLENIYOR.

**OLCUMLE ALINAN UC KARAR:**
1. **`Secure = true` SABIT, ortam guard'i YOK.** "Development'ta kapatmak gerekir mi?" sorusu
   TARAYICIDA olculdu: giris -> `document.cookie` csrf_token'i GORDU; access token bilerek
   bozuldu -> sessiz yenileme BASARILI (yani httpOnly+Secure refresh cookie duz HTTP uzerinde
   de saklanmis VE geri gonderilmis). Tarayicilar `localhost`u guvenilir origin sayiyor.
   Guard eklenseydi kod, hicbir sey kazandirmadan uretim disinda Secure'u kapatan bir yol
   tasiyacakti.
2. **Cookie YOLLARI FARKLI.** `refresh_token` -> `/api/auth` (dar; her istekte tasinmasin).
   `csrf_token` -> `/`. ZORUNLU: `document.cookie` yalnizca GECERLI SAYFA YOLUYLA eslesen
   cerezleri dondurur. Ilk yazimda ikisi de `/api/auth` idi ve TARAYICIDA OLCULDU: giristen
   sonra `document.cookie` BOS dondu - istemci basligi dolduramaz, yenileme kalici 403 olurdu.
3. **CSRF degeri HEX, base64 DEGIL.** Base64'un `+`, `/`, `=` karakterleri Cookie basliginda
   bozulup karsilastirmayi sessizce dusuruyordu (ilk kosumda birebir bu 403 alindi).

`Cookies:Domain` ayari eklendi (dev BOS, uretimde `.divisima.com`): storefront (divisima.com)
ile API (api.divisima.com) FARKLI HOSTLAR; host-only bir cerezi storefront JS'i OKUYAMAZ ve
double-submit yarim kalir. example.json'a olcum gerekcesiyle yazildi.

**KIRILAN PIN YOK - DURUST KAYIT.** Kullanicinin beklentisi "govde-tabanli eski pinler bilincli
kirilir" idi; TARANDI ve HTTP duzeyinde `/api/auth/refresh` pini HIC YOKTU. Var olan iki pin
(`Refresh_YeniCiftUretir_ESKI_RefreshToken_REDDEDILIR`, `PasifHesabin_RefreshToken_i_Reddedilir`)
`IAuthService`'i DOGRUDAN cagiriyor; servis imzasi degismedigi icin ikisi de SAG KALDI.
Uydurma bir "kirilan pin" raporlanmadi.

PINLER (`RefreshCookieContractTests`, 4): cookie yazilir + govdede refresh token YOK ·
cookie'siz 401 **ve gecerli token GOVDEDE bile 401** (eski yol gercekten kapali) · CSRF
basligi yoksa/yanlissa 403, dogruysa **200** (vakum kirici) · `Secure` her ortamda isaretli.

### MADDE 7 - Iyzico:CallbackUrl URETIM FAIL-FAST

`Program.cs` fail-fast blogunda (ConnectionStrings / SecurityKey / Encryption:Key /
MailSettings:Host kalibi). Bos ya da HTTPS olmayan degerle uretim host'u ACILMAZ. Mesaj CSP
`form-action` senkron kuralini da hatirlatiyor. example.json'daki `//Iyzico` aciklamasina
fail-fast notu eklendi.

PINLER (`ConfigFailFastTests`, 3): bos -> acilmaz (mesajda hem ayar adi hem `form-action`
aranir) · HTTPS degil -> acilmaz · **gecerli deger -> ACILIR** (vakum kirici; bu olmadan
"uretim host'u zaten hic acilmiyor" durumunda da iki pin yesil kalirdi).

### MADDE 11 - SuccessDataResult BELIRSIZLIGININ KOK COZUMU

Depo TARANDI (olcum, tahmin degil):
  SuccessDataResult<T>(T data, string message) -> 27 cagri
  SuccessDataResult<T>(T data)                 -> 16 cagri (hepsi veri niyetli)
  SuccessDataResult<T>(string message)         -> **0 cagri**
  ErrorDataResult<T>(T data, ...) / (T data)   -> **0 cagri**
  ErrorDataResult<T>(string message)           -> 23 cagri (hepsi `Messages.X`)
  Parametresiz kurucular                       -> 0 cagri

Yani belirsizligi URETEN kurucular ZATEN OLUYDU. Analyzer/kural yazmak yerine KALDIRILDILAR:
`SuccessDataResult<T>` -> yalniz `(T data, string message)` + `(T data)`; tek arguman HER ZAMAN
veri. `ErrorDataResult<T>` -> yalniz `(string message)`; tek arguman HER ZAMAN mesaj.
Hicbir `T` icin cakisan iki kurucu kalmadi. **Build 0 hata, TEK BIR cagri yeri degismedi** -
bu da kaldirilanlarin gercekten olu oldugunun kaniti.
Ileride veri tasiyan hata sonucu gerekirse kurucu GERI EKLENMEZ, ayirt edilebilir bir fabrika
(`ErrorDataResult<T>.WithData`) eklenir; gerekce koda yazildi.

E3'un `data:` adlandirmalari BIRAKILDI (artik gerekli degil ama niyeti acik tutuyor); yorumlari
durustlestirildi. Pin seti E3'un TERSINI sabitleyen yeni pinle genisledi
(`SuccessDataResultString_TEK_ARGUMAN_ARTIK_DATAYA_GIDER_BELIRSIZLIK_KALKTI`) + `ErrorDataResult`
icin cift-anlam kirici.

### MADDE 1 - KUPON SAYACI IDEMPOTENT

`used_count += 1` yerine `coupon_usages` satirlarindan **TURETME** (tek SQL ifadesi,
`ExecuteUpdateAsync`). Turetme TANIMI GEREGI idempotenttir.
**DURUST DUZELTME:** ilk yorumumda "oku-degistir-yaz yarisi da vardi" yazmistim - YANLISTI.
`coupons.row_version` DbContext'te `IsRowVersion()` ile yapilandirilmis GERCEK bir concurrency
token; kayip guncelleme istisnaya donusuyor ve retry onu yakaliyordu. **Tek gercek sorun
IDEMPOTENCY'di** ve yeniden deneme onu KURTARAMAZ (ikinci artis hata degil, basarili yazma).

Ikinci savunma hatti: `UX_coupon_usages_coupon_order` UNIQUE indeksi. Migration Sprint 6
kalibiyla - kirli veride **satir SILMEDEN** `RAISERROR` ile gurultulu duser.
`database/mssql/01_schema.sql` guncellendi.

PINLER (`CouponCounterAndInvoiceGuardTests`): sayac uc kez kossa da 1 kalir (once GERCEKTEN 1
oldugu dogrulanir - vakum kirici) · ayni siparise ikinci kullanim satiri veritabaninda ENGELLENIR.

### MADDE 2 - FATURA DURUM GUARD'I

`InvoiceManager.GenerateForOrder` artik `Pending` ve `Cancelled` siparisleri reddediyor
(`InvoiceOrderNotBillable`). Fatura MALI BIR BEYANDIR: iptal edilmise kesmek ciroyu sisirir,
odenmemise kesmek musteriye olmayan bir borc gonderir.

PINLER: iptal edilmise kesilmez (400 + satir YOK; cift-anlam kirici mesaj kontrolu) · Pending'e
kesilmez · **onayliya KESILIR** (vakum + cift-anlam kirici; guard'in DAR oldugunu bu kanitliyor).

### MADDE 13 - KULTUR PINLEME

`Program.cs`'te TEK NOKTA `tr-TR` pinlemesi (`DefaultThreadCurrentCulture` +
`DefaultThreadCurrentUICulture`).

**`RequestLocalization` SECILMEDI - iki olculmus gerekce:** (a) magaza tek pazarli, bicimin
istemcinin `Accept-Language`'ine gore degismesi Ingilizce tarayicidan siparis verene NOKTA
ayracli Turk faturasi cikarirdi; (b) fatura / fiyat-dususu e-postasi / outbox ARKA PLAN
islerinde de uretiliyor, orada istek hatti YOK - middleware o yolu hic kapsamazdi.

ETKILENEN YUZEY (tarandi): `OrderManager` (fatura HTML'i - 11 tutar + 1 tarih),
`PriceDropManager` (2 tutar). `IyzicoClient` ZATEN acikca `InvariantCulture` kullaniyor -
saglayiciya giden tutarlar ETKILENMEZ. `{Guid:N}` kullanimlari sayi bicimi DEGIL Guid bicimi.

PINLER (`CulturePinTests`, 2): surec kulturu `tr-TR` · fatura govdesi KOSUCU KULTURUNDEN
BAGIMSIZ `1.049,70` tasir (assert ACIKCA `tr-TR` ile hesaplanir, `CurrentCulture` ile DEGIL -
invariant kosucuda da ayni degeri bekler) + invariant bicimin govdede BULUNMADIGI (cift-anlam).

5. KONTROL: pinleme kaldirildi + kosucu invariant'a cekildi -> IKI PIN DE KIRILDI
(`DefaultThreadCurrentCulture` `""`, fatura tr bicimi tasimiyor). E3'teki CI kirmizisi birebir
yeniden uretildi. Geri alindi.

### MADDE 4 - LocalImageStorage WebRootPath

`Directory.GetCurrentDirectory()/wwwroot/...` yerine `IWebHostEnvironment.WebRootPath`
(yoksa `ContentRootPath/wwwroot`).

ASIL KANIT TESTTE: `AdminStockAndImageTests`'teki **`UseContentRoot(CWD)` hizalamasi KALDIRILDI**.
O ayar uretimdeki gercek ayrismayi test icinde GIZLIYORDU - ve o ayrisma E2b'de canli
gerceklesti (DB'de 3 gorsel satiri, `Divisima.API/wwwroot/uploads/products` BOS, dosyalar test
bin'inde). Hizalama olmadan 5/5 yesil: yazma ile statik sunum artik FARKLI calisma dizininde
bile ortusuyor. Ayar geri konursa pin anlamini yitirir; gerekce koda yazildi.

### MADDE 5 - DTO ZENGINLESTIRME + ISTEMCI TELAFISININ KALDIRILMASI

`ListeyiZenginlestirAsync` TEK yardimci olarak eklendi ve **hem admin `GetList` hem storefront
`filter`** yoluna baglandi - onceden yalniz admin yolu `sizes` dolduruyordu, iki yol
ayrisiyordu. N+1 YOK: kategoriler ve stoklar tek sorguda.

TASARIM: `total_stock` ve `sizes` **`available`** uzerinden (`stock_quantity - reserved_quantity`).
Bir bedenin tamami baskalarinin sepetinde rezerveyse o beden SATILABILIR DEGILDIR ve vitrinde
"var" gorunmemeli.

`ReturnResponseDto`'ya `product_name` + `order_number` eklendi. Bu yalniz fazladan is degil
YANLISLIK da duzeltti: pasiflenmis / katalogdan cikmis urunun iadesi "Urun #12" gorunuyordu.
Iade kaydi GECMISE ait bir belgedir; adi kaydin kendisi tasimali.

**`my-orders` icin DURUST BULGU: zenginlestirilecek bir sey YOK.** Olculdu - istemci liste icin
TEK cagri yapiyor, detayi kullanici siparis actiginda tembel cekiyor. Bu bir N+1 degil, mesru
lazy loading. Olculen N+1 KATALOG yolundaydi ve o kapandi. Uydurma is cikarilmadi.

ISTEMCI TELAFISI KALDIRILDI: `enrichAll` (6 eszamanli, urun basina detay cagrisi) silindi.
Detay zenginlestirmesi TEMBEL kaldi (`wireProductDetail` - kullanici urunu actiginda).
TARAYICIDA OLCULDU: **1 filter cagrisi, 0 detay cagrisi** (once 1 + 24). Kategori adlari cozulu,
stoklar 16/15 (rezerve dusulmus), urun 1'in stoksuz `L` bedeni listede YOK.

**BILINCLI KIRILAN PIN:** `Filter_ListeYolu_..._DOLDURMUYOR_PINLENIR` -> `..._DOLDURUR`.
Eski pin E1'de olculen SUPHELI davranisi sabitliyordu; backend duzelince YANLIS bir sozlesmeyi
savunur hale geldi. Yeni pin cift-anlam kirici: rezerve edilmis bedenin GELMEDIGI de assert
ediliyor.

### MADDE 10 - BILDIRIM ABONELIKLERI (unsubscribe + "aboneliklerim")

Backend'de YALNIZ `subscribe` vardi. Eklenen: `GET /my`, `DELETE /{id}`,
`GET /unsubscribe?token=` - her iki tur icin (stok bildirimi + fiyat dususu).

**TASARIM KARARI (olcume dayali):** abonelik ANONIM kurulabiliyor, dolayisiyla cikma yolu
kimlik dogrulamasi ISTEYEMEZ - yoksa uye olmayan abone verdigi izni geri alamaz.
"E-posta + urun ile cik" SECILMEDI: herkes herkesi cikarabilirdi ve uc "bu e-posta abone mi?"
sorusuna yanit veren bir SIZINTI KANALI olurdu. Cozum: satir basina TAHMIN EDILEMEZ jeton
(32 bayt HEX - base64'un `+/=` karakterleri URL'de bozuluyor), UNIQUE indeksli, e-postadaki
baglantida tasiniyor.

Giris yapmis kullanici tarafinda sahiplik **JWT'deki e-posta** ile dogrulaniyor
(`ICurrentUserService.GetRequiredEmail()` eklendi), istemci girdisiyle DEGIL. Baskasinin
aboneligine **404** doner - 403 demek varligi sizdirirdi.

MIGRATION: `AddColumn` tum mevcut satirlara AYNI varsayilani yazdigi icin UNIQUE indeks 2+
satirda kurulamazdi; `NEWID()` ile SATIR BASINA geri doldurma eklendi.

`Api:PublicBaseUrl` ayari (bos ise `Storage:PublicBaseUrl`'e duser - gorseller de API'nin
wwwroot'undan servis ediliyor, ayni origin). Ikisi de bossa e-postaya baglanti YERINE
"Hesabim > Bildirimlerim" yonlendirmesi yazilir - sessizce bos birakilmaz.

Hesabim'a **"Bildirimlerim"** sekmesi geldi; iki tur TEK listede, "Kaldir" butonuyla.

YAN ETKI (5 test kirildi, duzeltildi): `unsubscribe_token` NOT NULL olunca dogrudan DbContext
ile satir ekleyen 4 test kurgusu `Cannot insert the value NULL` ile kirildi. Kolonu opsiyonel
yapmak yerine KURGULAR uretimle ayni sozlesmeye uyduruldu - token'siz bir satir hicbir zaman
abonelikten cikarilamaz, yani kolon gercekten zorunlu olmali. `ClaimBeforeSendTests`'teki
fabrika HER CAGRIDA kendi jetonunu uretiyor: sabit deger verilseydi ikinci satir, testin
OLCTUGU filtreli-unique yerine JETON unique'ine takilir ve test yanlis sebepten kirilirdi.

PINLER (`NotificationSubscriptionTests`, 5): liste yalniz kendi e-postasini doner (baskasininkinin
listede OLMADIGI da assert) · baskasininki silinemez, satir KALIR · kendi satiri silinir ·
yanlis jeton reddedilir + dogru jeton ANONIM calisir · fiyat uyarisi tarafi da ayni sozlesmeyi tasir.

### MADDE 12 - PAYLASIM BAGLANTILARI (TESHIS DUZELTMESI)

**E3'teki teshisim YANLISTI ve duzeltildi** - ayrinti SUPHELI #10'da. Router `#/urun` yolunu
TANIYOR (`index.html:2077`); olculdu: gorunen view `home`, `detailOpenId` 1, yani urun detayi
GERCEKTEN aciliyor. "Sayfa Bulunamadi" bir 404 SAYFASI DEGIL, SAYFA BASLIGIYDI.

GERCEK KUSUR IKI TANE: (a) `setDocTitle()`in `urun` dali yok ve router onu `openDetail`DEN
SONRA cagirdigi icin dogru baslik eziliyor; (b) katalog yarisi - acilistaki router mock
PRODUCTS ile kosuyor, katalog sonrasi yeniden yonlendirme yalniz `#/kategori` icin yapiliyordu
(Favorilerim'de bu oturumda olculen yarisin aynisi).

Duzeltme `api-bridge.js`'te: `setDocTitle` sarmalandi + katalog sonrasi `urunRotasiniTazele()`.
OLCULEN SONUC: baslik "Sayfa Bulunamadi · Divisima" -> **"Siyah Midi Elbise · Divisima"**.

### MADDE 8 - E-POSTA VALIDATORU INCELEMESI + AYIRT EDILEBILIR MESAJ

INCELEME SONUCU: kayit validatoru FluentValidation'in permisif `.EmailAddress()` kuralini
kullaniyor ve RFC 2606'da TEST/OZEL kullanim icin AYRILMIS ust alan adlarini (`.test`,
`.example`, `.invalid`, `.localhost`) KABUL EDIYOR. Gercek Iyzico reddediyor (E2b'de olculdu).
Yani bizim kabul ettigimiz bir e-posta ile uye olan musteri HIC kart odemesi yapamiyor.

YAPILAN: init hatasinda sebep KENDIMIZ tespit ediliyor. Saglayicinin ham hata metni ne
musteriye yansitiliyor ne de METIN ESLESTIRMESI yapiliyor (yabanci bir API'nin dizgesine
bagimli olmak kirilgan). Teslim edilemez ust alan adi varsa ayirt edilebilir mesaj; DIGER TUM
init hatalarinda eski genel mesaj KORUNUYOR.

**YAPILMADI - SUPHELI, KARAR KULLANICININ:** kayit validatoru sikilastirilmadi. `.test` gibi
adresleri kayitta reddetmek ayri bir URUN karari; gecerli ama alisilmadik adresleri kapida
cevirmek gercek musteri kaybettirebilir.

PINLER (`PaymentInitMessageTests`, 2): teslim edilemez adreste ayirt edilebilir mesaj (ve ham
saglayici metninin SIZMADIGI) · gecerli adreste **genel mesaj korunur** - bu cift-anlam kirici
olmadan "her hatada e-posta mesaji donen" yanlis teshisli bir uygulama da testi gecerdi.

### MADDE 3 - PaymentConfirmed OUTBOX'A (SECENEK A - DORT ADIM DA)

Kullanici karari: **A** (tek `PaymentConfirmed` mesaji, dort adim da outbox'ta). Gerekce:
kaybedilen yan etki SESSIZ ve KALICI, gecikme GORUNUR ve GECICI; kupon sayacini inline
birakmak (B) gerekcesiz bir ikilik yaratirdi.

Yeni: `Events/PaymentConfirmedEvent.cs`, `IPaymentConfirmedSideEffects.cs`,
`PaymentConfirmedSideEffects.cs` (fatura -> sadakat -> referans odulu -> kupon sayaci).
Mesaj **A bolgesi transaction'inin ICINDE** yaziliyor; commit sonrasi B bolgesi kalmadi.

**EK UNIQUE KISIT (kullanici karari):** `UX_store_credit_referee_reward` on
`store_credit_transactions(customer_id)` filtreli - "davet edilen odulu" satiri musteri basina
TEK. Boylece idempotentlik tablosundaki son "kosullu" satir (oku-sonra-davran guard'i) DB
duzeyine indi. Migration `20260821202442_RefereeRewardUniquenessSprint8`, Sprint 6 kalibiyla:
kirli veride **satir SILMEDEN** `RAISERROR`. `database/mssql/01_schema.sql` guncellendi.

**OLCUMLE BULUNAN IKI GERCEK KUSUR** (ikisi de tahminle degil, teshisle):

1. **OUTBOX DONGU ZEHIRLENMESI.** Isleyici ve outbox'in kendi defter yazimi AYNI DbContext'te
   kosuyordu. Bir yan etki adimi `SaveChanges` sirasinda patlayinca (or.
   `UX_loyalty_transactions_order_earn` ihlali - at-least-once'ta BEKLENEN durum) basarisiz
   varlik change tracker'da **"Added" halinde KALIYOR**; hemen ardindaki `_outboxDal.UpdateAsync(msg)`
   ayni context'te `SaveChanges` yapinca o bekleyen varligi TEKRAR yazmaya calisip AYNI hatayla
   patliyor - bu kez OUTBOX'IN KENDI KAYDINDA. Zarar: istisna dongunun DISINA cikiyor,
   `retry_count` HIC kaydedilmiyor, ayni parti sonsuza kadar yeniden isleniyor ve ayni turdaki
   DIGER mesajlar hic islenmiyor. **Cozum: mesaj basina AYRI DI scope** - zehirlenen context o
   scope ile atiliyor.
2. **SADAKAT "KAZAYLA" IDEMPOTENTTI.** `EarnPoints` duplicate-key istisnasini YUTUP 500
   donuyordu ve isleyici sonucu KONTROL ETMIYORDU - yani gercek bir hata da "basarili" sayilirdi.
   Iki taraf da duzeltildi: `EarnFromOrder` basta ACIKCA "bu siparis icin kazanim var mi" diye
   soruyor, isleyici de `Result.Success`'i kontrol edip basarisizlikta ISTISNA firlatiyor.

Musteri gorunurlugu (sart iii): sonuc sayfasi metni yalnizca "Siparisin onaylandi ve
hazirlanmaya basliyor" diyor - fatura/puan hakkinda SOZ VERMIYOR, bu yuzden DOKUNULMADI.

PINLER (`PaymentConfirmedOutboxTests`, 2): ayni mesajin IKINCI teslimati dort adimin
HICBIRINDE fazla etki uretmez (fatura tek, puan tek, odul tek, sayac dogru) · islem yarida
cokerse retry'da TAMAMLANIR. 5 denemede Failed olan mesaj H53 kalibiyla GURULTULU kaliyor
(log + zaman cizelgesine "KRITIK" notu).

YAN ETKI: S6/S7'nin 5 pini outbox'a gecisle kirildi ve `OutboxBosaltAsync()` bosaltmalariyla
onarildi. `BBolgesi_HATASI_...` BILINCLI olarak
`YanEtkiHatasi_OdemeSUCCESS_KALIR_Mesaj_YENIDEN_DENENIR_ve_TAMAMLANIR` olarak yeniden yazildi -
eski adi artik var olmayan bir mimariyi (commit sonrasi B bolgesi) tarif ediyordu.

### MADDE 9 - WEBHOOK TUNEL DOGRULAMASI (1. TUR TAMAM, 2. TUR BEKLIYOR)

Kullanici public bir Cloudflare tuneli acti ve Iyzico panelinde "Isyeri Bildirimleri Url"
alanina `<tunel>/api/payment/webhook` girdi. `Iyzico:CallbackUrl` user-secrets'te tunel
adresine cekildi (depoya GIRMEDI). 1. tur TASARIMI: CSP `form-action`'a tunel **BILEREK**
eklenmedi -> callback engellenir, siparis Pending kalir, WEBHOOK'un kurtarmasi olculur.

**1. TUR SONUCU: KURTARMA YOLU CALISMIYORDU.** Kullanici 1.049,70 odedi (3DS kapali).
Callback CSP tarafindan engellendi (beklendigi gibi). Webhook GELDI - ve bizim ucumuz
**400** dondu.

Gercek bildirim (teshis gunlugu, User-Agent `Apache-HttpClient/5.2.3 (Java/17.0.15)`):

```
govde : {"paymentConversationId":"e160a135...","merchantId":3432888,"status":"SUCCESS",
         "token":"76ee5138-cc90-481d-884f-90a9978a58a4","iyziReferenceCode":"8fe79c9a-...",
         "iyziEventType":"CHECKOUT_FORM_AUTH","iyziEventTime":1787347437752,
         "iyziPaymentId":37415135}
baslik: X-Api-Version=V1 | X-Iyz-Signature=   (VAR ama DEGERI BOS)
```

**IMZA GERCEGI:** govdede `signature` alani YOK; baslik adi `X-Iyz-Signature`
(dokumanlardaki `X-IYZ-SIGNATURE-V3` DEGIL) ve BOS geliyor. Devir notundaki hipotez
("imza V3 basliginda olabilir") kismen dogruydu - baslik var ama DOLU DEGIL.

**IKI BAGIMSIZ ENGEL** (tunel uzerinden uc kontrollu istekle IZOLE EDILDI):

| Deneme | Sonuc |
|---|---|
| Iyzico'nun gonderdigi gibi (`X-Api-Version: V1` + bos imza) | **400, govde BOS** |
| Surum basligi yok, imza yok | 400 `"Ödeme imzası doğrulanamadı"` |
| Surum basligi yok, govdede signature | 400 `"Ödeme imzası doğrulanamadı"` |

**CANLI ZARAR:** siparis **#33 `DVS20260822-02477199B6`** - Iyzico'da odeme SUCCESS
(`iyziPaymentId 37415135`), bizde `status=0` / `payment_status=0` / `transaction_id=NULL`,
`outbox_messages` BOS. Yani **"para gitti, siparis yok"** birebir uretildi.

#### ENGEL 1 - `X-Api-Version: V1`

`HeaderApiVersionReader("X-Api-Version")` bu degeri ayristiramiyor; istek CONTROLLER'A HIC
ULASMADAN bos govdeli 400 yiyor (log: `Request contained the API version 'V1', which is not valid`).

**KULLANICININ ONERDIGI COZUM (`[ApiVersionNeutral]`) OLCULDU VE YETMEDI - UC KEZ:**

| Deneme | Sonuc |
|---|---|
| `[ApiVersionNeutral]` **action** duzeyinde | HALA 400 (bos govde) |
| `[ApiVersionNeutral]` **controller** duzeyinde | HALA 400 (bos govde) |
| Boru hattinin basinda basligi silen `app.Use(...)` | HALA 400 (bos govde) |

Sebep OLCULDU: uygulama `app.UseRouting()`'i ACIKCA cagirmiyor, bu yuzden yonlendirme (ve
`ApiVersionMatcherPolicy`) boru hattinin BASINA ekleniyor - kullanici middleware'lerinden ONCE
kosuyor. Ayrica reddi yapan katman endpoint'in versiyon-NOTRLUGUNE bakmiyor.

**UYGULANAN COZUM: `Divisima.API/Versioning/WebhookExemptHeaderApiVersionReader.cs`** -
`HeaderApiVersionReader`'i sarmalar, YALNIZ `/api/payment/webhook` yolunda basligi yok sayar.
O yolda hicbir okuyucu deger uretmedigi icin `AssumeDefaultVersionWhenUnspecified` devreye
girip 1.0 seciliyor. `[ApiVersionNeutral]` action uzerinde BIRAKILDI (niyeti dogru ifade
ediyor) ama 400'u COZEN SEY O DEGIL - iki tarafta da yorumla capraz referans verildi.

#### ENGEL 2 - IMZA (BILINCLI GEVSEME)

`Webhook` action'i artik `imzaZorunlu: false`. Otorite E2b'deki CF callback modelinin AYNISI:
token opak + sunucu-sunucu retrieve + 30 dk zaman asimi + tutar/para birimi/fraud + "yalniz
Pending islenir". **Gevseme "imzayi yok say" DEGIL:** imza gelirse (govdede ya da
`X-Iyz-Signature` basliginda) AYNEN dogrulanir ve tutmazsa 400 doner.

**BILEREK YAPILMADI:** `X-IYZ-SIGNATURE-V3` basligi verifier'a BAGLANMADI. Bizim
`VerifyCallbackSignature` HMAC-SHA256(secretKey, token) hesaplar; V3 imzasi FARKLI bir govde
uzerinden uretilir. Olculmemis bir esleme yazmak, o baslik dolmaya basladigi gun HER GERCEK
bildirimi reddederdi - bugun duzelttigimiz kesintinin BIREBIR aynisi. Imza dogrulamasi
basarisiz oldugunda artik ADIYLA `LogWarning` dusuluyor ki bicim degisirse sessiz kalmasin.

#### BEDEL (AMPLIFIKASYON) - OLCULDU, ENDISEDEN DAR CIKTI

"Her sahte istek bir retrieve" endisesi olculdu ve **yanlis cikti**: `HandleCallback`
retrieve'e gelmeden ONCE token'i BIZIM tablomuzda ariyor. Bizim olmayan token **404** ile
duser ve **disari HIC cikilmaz** (pin: `RetrieveCallCount == 0`). Retrieve'e ancak (a) bizim
urettigimiz, (b) hala Pending, (c) 30 dk'dan yeni bir token ulasabilir.

Rate limit kapsami olculdu ve **IKI YOL AYRISIYORDU**:
- Redis yolu (`RedisRateLimitMiddleware`): path eslesmesi `/payment/` -> **10/dk**, webhook DAHIL.
- Yerlesik yol (varsayilan; `Redis:Enabled=false`): webhook yalniz GlobalLimiter'in **100/dk**'sinda.

Webhook action'ina `[EnableRateLimiting("payment")]` eklendi. **YENI BIR SAYI DEGIL** - iki yolu
Program.cs'teki policy tanimin ACIK NIYETINE ("Redis middleware'indeki payment scope (10/dk) ile
tutarli") hizaliyor.

#### PINLER (`WebhookContractTests`, 8)

- `WebhookV1SurumBasligiyla_VERSIYONLAMAYA_TAKILMAZ_ve_ISLENIR` (200 + odeme GERCEKTEN islendi
  + govde BOS DEGIL - versiyonlama reddi bos govdeliydi)
- `AyniV1Basligi_BASKA_BIR_UCTA_HALA_400_KAPSAM_DAR_KALDI` (cift-anlam kirici: ayni uc
  BASLIKSIZ 200 doner)
- `ImzasizGercekBildirim_RETRIEVE_OTORITESIYLE_Islenir` (retrieve TAM 1)
- `BizimOlmayanToken_404_ve_IYZICOYA_HIC_CIKILMAZ_AmplifikasyonDAR` (retrieve 0)
- `TokenBIZIM_ama_RETRIEVE_DUSERSE_YanEtkiSIZ_Reddedilir` (fatura 0, puan 0, outbox mesaji 0)
- `AyniTokenTekrari_ZATEN_ISLENDI_RetrieveARTMAZ_YanEtkiYOK` (retrieve 1'de kalir, mesaj 1'de kalir)
- `ImzaGELIRSE_DOGRULANIR_Govde_ve_BASLIK_YanlisImzayi_REDDEDER` (uc dal: govde yanlis, BASLIK
  yanlis, DOGRU imza -> sonuncusu vakum kirici)
- `Webhook_PAYMENT_KOVASINDA_OnBirinci_Istek_429` (AYRI host, uretim varsayilani; ilk on istek
  404 aliyor - yani uygulamaya ULASIYORLAR)

**BILINCLI KIRILAN PIN (kullanici ACIKCA yetkilendirdi):**
`Webhook_ImzaSIZ_REDDEDILIR_CF_Gevsemesi_SIZMAZ` -> `Webhook_YONLENDIRILMEZ_JSON_Doner`.
Eski pin E2b'de DOGRU bir seyi sabitliyordu ama dayandigi VARSAYIM ("webhook'ta imza gelir")
gercek bildirimle CURUTULDU; pin, gercek bildirimi reddeden davranisi savunur hale gelmisti.
Imza asserti kaldirildi, E2'nin kendi iddiasi (yonlendirme YOK + JSON) kaldi ve VAKUM KIRICI
eklendi (imzasiz bildirim GERCEKTEN islenmis olmali).

#### DIS KONTROLU

5 assert ters -> **5 AYRI ISIMLI KIRMIZI**. Hepsi geri alindi.

#### 5. KONTROL (URETIM MUTASYONU) - IKI DALGA

- **M1** (okuyucu muafiyeti geri alindi): yalniz `WebhookV1SurumBasligiyla_...` kirildi ->
  400. Diger 7 pin YESIL kaldi - muafiyetin gercekten DAR oldugunun kaniti.
- **M2** (`imzaZorunlu: true` geri getirildi): **7 AYRI ISIMLI KIRMIZI**, iki farkli sinifta.
  En onemlisi `Webhook_YONLENDIRILMEZ_JSON_Doner` -> `payment_status` `0x00` (Pending) bulundu:
  **siparis #33'un canli durumunun BIREBIR aynisi**. Hepsi geri alindi.

#### TUNEL UZERINDEN UCTAN UCA TEYIT (yan etkisiz)

Iyzico'nun GERCEK baslik setiyle (`X-Api-Version: V1` + bos `X-Iyz-Signature` +
`Apache-HttpClient` UA) tunele POST -> **404 + bizim JSON govdemiz**. Yani istek artik
controller'a ULASIYOR (once ciplak 400 idi). Uydurma token kullanildi - #33 kanitina
DOKUNULMADI.

#### SIPARIS #33: KURTARILAMADI - DURUST KAYIT

Kurtarma plani (ayni token'la webhook'u tekrar tetiklemek) **OLCULDU VE CALISMIYOR**:
`payments.id=20` `created_at = 2026-08-22 00:23:00`, olcum ani `01:21:29` -> **58 dakika**.
`HandleCallback`'in 30 dk token zaman asimi guard'i devreye girer ve odemeyi **Failed**
yapardi - yani kurtarmak yerine kanit da bozulurdu. Tekrar TETIKLENMEDI. Karar kullanicida
(bkz. SUPHELI #15 - bu guard'in webhook kurtarma yolunu da sinirlamasi AYRI bir bulgudur).

#### 2. TUR - TAMAMLANDI, TAM YESIL (siparis #34)

CSP `form-action` tunel origin'i ile senkronlandi (gecici satir; tur bitince GERI ALINDI).
Kullanici gercek bir odeme yapti: **`DVS20260822-174E953852`**, E4a M x3, **1.549,60**.
Sonuc sayfasi GELDI, durum Confirmed.

**(a) NORMAL YOL** - `HTTP POST /api/payment/callback responded 302 in 223.7 ms`
(223 ms = retrieve GERCEKTEN kostu). Storefront sonuc sayfasi cizildi.

**(b) CALLBACK + WEBHOOK CARPISMASI - IDEMPOTENTLIK CANLI KANITLANDI.** Iyzico'nun bant-disi
bildirimi callback'ten **14,6 saniye SONRA** ayni odeme icin geldi:

```
01:53:48.500  POST /api/payment/callback  -> 302  in 223.7 ms   (retrieve KOSTU)
01:54:03.149  POST /api/payment/webhook   -> 200  in  15.0 ms   (SORGUYA ULASMADI)
```

Ayni odeme oldugu KANITLI: webhook govdesindeki `token` =
`f492bf0f-06b1-476b-8779-95e1ad11e2dc` = `payments.token`; `iyziPaymentId 37416082` =
`payments.transaction_id`. 15 ms'lik sure "zaten islendi" dalinin kaniti (E2b'de olculen
replay suresiyle ayni buyukluk; gercek retrieve 223 ms).

Yan etki sayilari (tam olarak BIRER):

```
orders   #34  status=1 (Confirmed)  total=1549.60  is_online_payment_done=1
payments #21  payment_status=1      transaction_id=37416082  item_transaction_id=39332690
outbox        PaymentConfirmed x1   status=1 (Processed)  retry_count=0   (tabloda TEK mesaj)
invoices      1 satir  DIV-2026-000034  status=1 (Sent)
loyalty       1 satir  154 puan
timeline      2 satir  ("Sipariş oluşturuldu", "Ödeme onaylandı")  UYARI/KRITIK notu: 0
stock    M    stock_quantity=10  reserved_quantity=0   (3 adet satildi, rezervasyon kapandi)
```

`item_transaction_id` dolu geldi - E2b'nin B1 duzeltmesi bu turda da canli teyit edildi.

**TEMIZLIK YAPILDI:** gecici CSP satiri geri alindi, `WebhookDiagnosticMiddleware.cs` SILINDI
ve `Program.cs`'teki kaydi kaldirildi. Depo tarandi: `WebhookDiagnostic`, `trycloudflare`,
`GECICI TESHIS` - kod/yapilandirma dosyalarinda SIFIR kalinti.

**ACIK KALAN (bloke etmiyor):** Iyzico panelinde webhook icin ayri bir imza anahtari/secret
olup olmadigi. Kullanici Isyeri Bildirimleri karti, IP/Back URL Yonetimi ve Eklentiler
sayfalarina baktigini belirtti ama yanit sablonu doldurulmadan geldi - **KESIN CEVAP YOK**.
Bulunursa `X-Iyz-Signature` dolmaya baslar; o zaman imza BICIMI olculmelidir (bkz. V3 notu -
bicim varsayimiyla baglamak kesintiyi geri getirir).

### DIS KONTROLU (SPRINT 8)

7 assert ters cevrildi -> **7 AYRI ISIMLI KIRMIZI**, her biri farkli test sinifindan:
`ConfigFailFastTests.Uretimde_IyzicoCallbackUrl_BOSSA_UYGULAMA_ACILMAZ`,
`PaymentInitMessageTests.InitHatasi_TESLIM_EDILEMEZ_EPOSTADA_AYIRT_EDILEBILIR_MESAJ_Doner`,
`CulturePinTests.FaturaGovdesi_KOSUCU_KULTURUNDEN_BAGIMSIZ_tr_BICIMI_Tasir`,
`RefreshCookieContractTests.Login_RefreshTokenI_HTTPONLY_COOKIEYE_YAZAR_GOVDEDE_BIRAKMAZ`,
`NotificationSubscriptionTests.Aboneliklerim_YALNIZ_KENDI_EPOSTASININ_Aboneliklerini_Doner`,
`StorefrontCatalogContractTests.Filter_ListeYolu_CategoryName_TotalStock_Sizes_DOLDURUR`,
`CouponCounterAndInvoiceGuardTests.KuponSayaci_TURETILIR_AyniAdim_IKI_KEZ_Kossa_da_FAZLA_SAYMAZ`.
Hepsi geri alindi. Ayrica madde 13 icin AYRI bir 5. kontrol (uretim mutasyonu) yapildi.

### YEREL DOGRULAMA (Sprint 8 sonrasi)

312/312 tam suit · 188/188 `Category=Sql` · Release 0 hata ·
`dotnet format` whitespace + style `--verify-no-changes` TEMIZ.

### SURECTE YASANAN (kayit)

- Bir `sed` satir-numarali duzenlemesi kaydi ve `frontend/api-bridge.js`'in ILK 10 SATIRINI
  bozdu. Fark edildi, hasar OLCULDU (yalniz 1-10 arasi), onarildi ve tarayicida sozdizimi
  hatasi olmadigi dogrulandi. **DERS: bu dosyada satir-numarali `sed` KULLANILMAZ** - desen
  tabanli duzenleme ya da Edit araci kullanilir.
- Biçim kapisi iki kez is gordu: EF'in urettigi migration dosyalari CRLF+BOM ile geliyor ve
  elle eklenen `using` satirlari siralamayi bozabiliyor. **Migration uretildikten sonra
  `dotnet format whitespace --include <migration dosyalari>` kosulur.**

## SIRA

1. **KARAR BEKLEYEN - SIPARIS #33** (para alindi, Pending). Token 30 dk guard'ini asti;
   tekrar tetiklemek onu Failed yapardi. Sandbox siparisi oldugu icin aciliyeti yok ama
   ARDINDAKI BULGU launch'i ilgilendiriyor: **SUPHELI #15**.
2. **YENI SUPHELILER - KARAR KULLANICIDA:** #14 (surum okuyucusu kirilganligi, genel),
   #15 (30 dk zaman asimi webhook kurtarma yolunu da siniriyor), #16 (`Webhook:AllowedIps`
   bos - yalniz yapilandirma isi, imzasiz uctaki en guclu ek katman), #17 (callback rate
   limit policy'si disinda).
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
  **YENI KALEM (Sprint 8 madde 8 eki - kullanici karari): RFC 2606 ust alan adlarini KAYITTA
  reddetme.** Kayit validatoru FluentValidation'in permisif `.EmailAddress()` kuralini kullaniyor
  ve `.test` / `.example` / `.invalid` / `.localhost` adreslerini KABUL EDIYOR; gercek Iyzico
  reddediyor (E2b'de olculdu), yani o adresle uye olan musteri HIC kart odemesi yapamiyor.
  Sprint 8'de AYIRT EDILEBILIR MESAJ eklendi (init hatasinda sebep soyleniyor, saglayicinin ham
  metni sizdirilmiyor) ve bu YETERLI goruldu. Validatoru sikilastirmak ayri bir URUN karari:
  gecerli ama alisilmadik adresleri kapida cevirmek gercek musteri kaybettirebilir.
  **Sprint 8'e GIRMEZ.**
- **Iyzico'nun TELEMETRI alan adlari CSP'de ACILMAZ (kalici karar).** `countly.iyzico.com`
  ve `*.ingest.tr.sentry.io` (o120955...). Iyzico checkout formu kendi
  Countly analitigine baglaniyor (`campaign_banner_enabled`, `checkout_radio_button_layout_updated`
  gibi A/B bayraklari) ve Sentry hata toplamaya. Ucuncu taraf izleme; engellendiklerinde
  form yine ciziliyor ve odeme akisi calisiyor. Resmi Iyzico ALAN ADLARI (static / api /
  cdn / merchantgw / consumerapigw.iyzipay.com) E2b de OLCULEREK acildi - tahminle degil,
  her tur konsoldaki ihlal ve canli iyziInit yapilandirmasi okunarak.
- **Auth modeli**: mevcut hibrit korunuyor (access localStorage + refresh httpOnly
  cookie + kosullu CSRF). Backend ile uyumlu oldugu dogrulandi.
- **`EnableRetryOnFailure`: S7'de ACILMADI.** S7 engeli kaldirdi (IyzicoPayment artik
  `ExecuteInTransactionAsync` kullaniyor) ama bayragi acmak AYRI bir karar ve alinmadi.
  Acmadan once `Program.cs` yorumundaki DIGER manager'lar (OrderManager, GiftCard,
  Loyalty, Referral, Return, StoreCredit) da tasinmali - aksi halde onlarin manuel
  `BeginTransaction` cagrilari retry stratejisi tarafindan REDDEDILIR.
- **SPRINT 8 = E FAZI SONRASI LAUNCH-ONCESI ZORUNLU DALGA (ON UC KALEM).**
  **COMMIT BOLUNMESI ONAYLI (kullanici karari): UC COMMIT** - guvenlik (6+7+9),
  dogruluk (1+2+3+4+11+13), yuzey (5+10+12+8). Hepsi **TEK PUSH, TEK RUN**.
  Gerekce: madde 6 pinleri BILINCLI kiriyor; tek dev commit'te bir regresyon `git bisect`
  ile ayristirilamazdi, ayrica onlarca dosyalik tek commit okunamazdi.
  **KALEM SIRASI (onayli):** 9-kurulum -> 6 -> 7 -> 11 -> 1 -> 2 -> 3 -> 13 -> 4 ->
  5 -> 10 -> 12 -> 8 -> 9-dogrulama.
  Simdi is yok; E fazi bitince kosulur. Sira onceligi (6) guvenlik oldugu icin ustte.

  1. **Kupon `used_count` idempotency** (outbox'in on kosulu). `IncrementCouponUsageWithRetry`
     duz sayac artisi; at-least-once bir mekanizmada FAZLA sayar. Cozum adaylari:
     `coupon_usages` satirlarindan turetmek ya da `(coupon_id, order_id)` unique indeks +
     artisi insert basarisina baglamak.
  2. **`InvoiceManager.GenerateForOrder` siparis DURUMU guard'i** - Cancelled/Pending
     siparise fatura kesilmez + pinler.
  3. **`PaymentConfirmed` outbox'a tasima** (altyapi hazir: `outbox_messages` +
     `OutboxService` + `OutboxProcessor` atomik claim/reclaim + `Cron.Minutely`).
     Kazanci: B bolgesi hatasi sessiz kalmak yerine yeniden denenir; maliyeti eventual
     tutarlilik (~1 dk) ve at-least-once idempotentlik zorunlulugu. **Outbox karari o gun.**
  4. **`LocalImageStorage`: CWD yerine `WebRootPath`.** Pin: yazma ile statik servis FARKLI
     calisma dizininde bile ortusur (test host'undaki `UseContentRoot` hizalamasina gerek
     kalmadan yesil).
  5. **Storefront `filter` yolu `category_name` + `total_stock` + `sizes` DOLDURUR**
     (DTO zenginlestirme). Duzeltme sonrasi istemcideki 6-esmanli detay telafisi
     (`api-bridge.js enrichAll`) KALDIRILIR ve pinler guncellenir.
     **KAPSAMA EKLENDI (kullanici karari): `my-orders` DTO zenginlestirme.** Ayni kok
     eksiklik: liste yolu ince DTO donduruyor, istemci her satir icin ek cagri yapiyor.
     E3'te `ReturnResponseDto`'nun **urun adi tasimadigi** da olculdu (yalniz `product_id`);
     iade listesi urun adini KATALOGDAN cozmek zorunda kaliyor. O da bu kalemin icinde.
  6. **ONCELIKLI (GUVENLIK): refresh token gercekten httpOnly cookie'ye tasinir.**
     `SetRefreshTokenCookie` GERCEKTEN kullanilir - login/refresh cookie YAZAR, refresh ucu
     cookie'den OKUR, logout siler; istemci uyarlanir; CSRF double-submit devreye girer.
     Eski govde-tabanli sozlesme pinleri BILINCLI kirilir, yenileri ayni commit'te gelir.
  7. **`Iyzico:CallbackUrl` uretim FAIL-FAST listesine eklenir.** (E2b'de olculdu)
     `Program.cs` satir 43-84'teki blok ConnectionStrings / TokenOptions:SecurityKey /
     Encryption:Key / MailSettings:Host kontrol ediyor; `Iyzico:CallbackUrl` YOK. Uretimde
     bos kalirsa HER kart odemesi init'te 400 ile duser ve musteri yalniz "Odeme
     baslatilamadi." goruru - E2b'de bu belirti birebir olculdu. Tam fail-fast konusu.
  8. **Kayit e-posta validatorunun Iyzico kabul kurallariyla uyumu INCELENIR (rapor).**
     (E2b'de olculdu) Gercek Iyzico `@divisima.test` adresini "email hatali format ile
     gonderilmistir" ile REDDEDIYOR; ayni musteri example.com ile 200 aliyor. Yani bizim
     kabul ettigimiz bir e-posta ile uye olan musteri HIC odeme yapamaz. Ayrica init-400
     dalinda kullaniciya AYIRT EDILEBILIR mesaj verilmesi degerlendirilir (bugun yalniz
     "Odeme baslatilamadi." goruluyor). Duzeltme YAPILMADI - turlar example.com hesabiyla suruldu.
  9. **WEBHOOK TUNEL DOGRULAMASI - LAUNCH ONCESI ZORUNLU** (E2b'de statusu YUKSELTILDI).
     Onceden "ayri bir dogrulama" olarak deftere yazilmisti; E2b bunun GERCEK bir senaryo
     oldugunu OLCTU. Siparis `DVS20260821-6958D22788`: odeme Iyzico sandbox'ta ALINDI,
     sonucu tasiyan form POST'u storefront CSP'si (`form-action 'self'`) tarafindan
     ENGELLENDI, callback HIC ATESLENMEDI, siparis PENDING kaldi. Uretimde bu "para gitti,
     siparis yok" demektir. Tasarimda tek telafi `POST /api/payment/webhook` (bant-disi
     bildirim, ayni HandleCallback mantigi, idempotent) - ama disaridan erisilebilir bir
     tunel olmadan HIC dogrulanmadi. Kapsam: public tunel -> Iyzico panelinde webhook
     adresi -> kaybolan callback senaryosu -> webhook'un siparisi Confirmed'a tasidigi
     OLCULUR. CSP senkron kurali (form-action = Iyzico:CallbackUrl origin'i)
     `appsettings.Development.example.json` icindeki `//Iyzico` aciklamasina yazildi.
  10. **BILDIRIM ABONELIKLERI: `unsubscribe` + "aboneliklerim" uclari.** (E3'te olculdu,
     kullanici karariyla deftere alindi) Backend'de YALNIZ `subscribe` var; tum controller'lar
     tarandi, abonelikten CIKMA ve "hangi aboneliklerim var" uclari YOK. Sonuc: kullanici
     kurdugu stok/fiyat bildirimini goremiyor ve KAPATAMIYOR. E3 istemcisi bunu gizlemiyor -
     abonelik TEK YONLU kuruluyor ve ekranda geri alma sozu verilmiyor; kalici cozum backend.
     Kapsam: `stock-notification/unsubscribe`, `price-drop/unsubscribe`, ikisi icin "benim
     aboneliklerim" listesi + Hesabim'da bir sekme.
  11. **`SuccessDataResult<string>` BELIRSIZLIGININ KOKTEN COZUMU.** (E3'te olculdu; iki
     cagri E3'te duzeltildi, KOK SEBEP ACIK) `T = string` oldugunda `(T data)` ile
     `(string message)` ayni imzaya duser ve C# generic OLMAYAN adayi secer; tek argumanli
     cagri veriyi MESSAGE'a yazar, `Data` null kalir ve `Success` true oldugu icin hata
     SESSIZ olur. E3 yalniz iki cagri yerini `data:` adlandirilmis argumana cevirdi -
     **YENI yazilacak tek argumanli bir string cagrisi yine sessizce bozuk olur.**
     Aday cozumler: (i) kurucu setini yeniden tasarlamak (`(string message)` kurucusunu
     kaldirip yerine `SuccessDataResult<T>.WithMessage(...)` gibi ayirt edilebilir bir
     fabrika koymak), (ii) tek-argumanli-string kullanimini yasaklayan bir analyzer/kural.
     Depo taramasi (E3, referans): `SuccessDataResult<string>` **4 cagri** -
     `OrderManager.cs`, `ReferralManager.cs` (ikisi de duzeltildi),
     `GiftCardManager.cs:43`, `ProductImageManager.cs:83` (iki argumanli, ETKILENMEZ).
  12. **PAYLASIM BAGLANTILARININ BASLIGI (kapsam OLCUMLE DARALDI).**
     **DUZELTME: "router'a rota eklenmesi" GEREKMEDI - rota ZATEN VARDI** (`index.html:2077`).
     E3'teki teshis yanlisti; ayrinti SUPHELI #10'da. Gercek is iki kalemdi ve yapildi:
     (a) `setDocTitle()`in `urun` dali olmadigi icin baslik "Sayfa Bulunamadi" kaliyordu -
         ustelik router bu fonksiyonu `openDetail`den SONRA cagirip dogru basligi eziyordu;
     (b) katalog yarisi - acilistaki router mock PRODUCTS ile kosuyordu.
     Olculen sonuc: baslik "Sayfa Bulunamadi · Divisima" -> "Siyah Midi Elbise · Divisima".

     Eski (YANLIS) kapsam metni, kayit icin: (E3'te olculdu,
     kullanici karariyla LAUNCH ONCESINE alindi - "paylasilan linklerin 404'u launch'a
     tasinmaz") `index.html:2154` `shareUrl(id)` -> `#/urun/<id>` uretiyor ve urun
     kartindaki WhatsApp / Facebook / X / Pinterest / "baglantiyi kopyala" secenekleri bu
     adresi paylasiyor; ama urun detayi bir ROTA DEGIL, `openDetail(id)` ile acilan bir
     MODAL ve router `#/urun` yolunu TANIMIYOR. Olculdu: `location.hash = "#/urun/1"` ->
     sayfa basligi **"Sayfa Bulunamadi · Divisima"**. Kapsam: router'a `#/urun/:id` yolu
     eklenir ve **katalog yuklendikten SONRA** `openDetail(id)` cagrilir (E3'te olculen
     katalog yarisi burada da gecerli - erken cagri MOCK urunu acardi), ardindan ELLE
     DOGRULAMA: paylasilan bir baglantiyi temiz sekmede acmak dogru urunu acmali.
     Bkz. SUPHELI #10.
  13. **KULTUR PINLEME.** (E3 run'inda CANLI ORTAMDA kanitlandi - bkz. SUPHELI #13;
     kullanici karariyla SUPHELI'den KALEME yukseltildi, DOGRULUK commit'ine girer)
     Uygulama hicbir yerde kultur pinlemiyor; para/tarih bicimlendirmesi kostugu kabin
     yereline gore degisiyor. GitHub kosucusu (invariant) fatura tutarini `1,049.70`
     olarak bastigi icin bu davranis ORTAMDA gorunur oldu.
     **MAGAZA TEK PAZARLI (TR / TRY) - tasarim buna gore.**
     Kapsam: (a) tasarim OLCEREK kurulur - aday `Program.cs`'te TEK NOKTA `tr-TR`
     pinlemesi (`CultureInfo.DefaultThreadCurrentCulture` + `DefaultThreadCurrentUICulture`);
     `RequestLocalization` alternatifi de olculur ve secim gerekcesiyle yazilir.
     (b) TUM `:N2` / `:C` / tarih bicimlendirme yuzeyi taranir (fatura HTML'i tek yer
     degil - e-posta sablonlari, PDF/e-fatura alanlari, log satirlari dahil).
     **PIN: fatura govdesi KOSUCU KULTURUNDEN BAGIMSIZ olarak `tr` bicimiyle cikar** -
     test kendi thread kulturunu invariant'a cekip yine `1.049,70` gormeli, yani pin
     CI'da da (invariant kosucuda) gecerli olmali. Dis kontrolu: pinleme kaldirilinca
     pin KIRILMALI.

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
   **E2b: ARTIK TEORIK DEGIL - CANLI ORTAMDA GERCEKLESTI.** Storefront urun 2 gorselleri
   icin 404 aliyor. Olculdu: `product_images` tablosunda 3 satir var
   (`/uploads/products/3088...png` dahil, `is_primary=1`), ama
   `Divisima.API/wwwroot/uploads/products/` **BOS** ve dosya adi repo genelinde
   HICBIR YERDE yok. Dosyalari iceren TEK dizin
   `Divisima.IntegrationTests/bin/Release/net8.0/wwwroot/uploads/products` (test
   yuklemeleri). Yani E4a'da yuklenen gercek gorseller, o anki CALISMA DIZININE yazilmis
   ve orasi sunulan dizin DEGIL; sonucta veritabani "gorsel var" diyor, dosya yok,
   vitrin SONSUZA KADAR 404. Tam olarak yukarida ongorulen zarar. Sprint 8 madde 4.
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
6. **[KAPANDI - E3] Hesabim > Siparislerim ekrani MOCK siparis listesi ciziyordu ve COKUYORDU.**
   (E2b'de olculdu) `index.html` satir 2524'teki `accOrders()` `MOCK_ORDERS` uzerinde
   donuyor ve her kalem icin `byId(id).price` okuyor. E1 katalogu gercek API'ye
   bagladigi icin `byId` artik yalniz GERCEK urunleri biliyor; mock siparislerin kalem
   id'leri (olculdu: 1, 8, 5, 13, 18, 3) gercek katalogda (olculdu: 2, 1) karsilik
   BULMUYOR -> `byId(8)` undefined -> "Uncaught TypeError: Cannot read properties of
   undefined (reading 'price')" ve tum `renderAccount` render'i cokuyor (yakalandi:
   `router()` cagrisi bu istisnayla duruyor, `accountView` BOS kaliyor).
   E2b SADECE yalani ve cokmeyi kaldirdi (`api-bridge.js` -> `wireAccountOrders`,
   `window.accOrders` ezilir, notr durum cizilir). GERCEK listeyi
   (`/api/order/my-orders` + zaman cizelgesi) baglamak **E3 madde (a)**; oraya kadar
   ekran gercek siparisleri GOSTERMIYOR.
   **KAPANIS (E3):** yedi sekmenin tamami gercek uclara baglandi; elle dogrulamada 18 gercek
   siparis, tembel acilan kalem + zaman cizelgesi, iade talebi ve iade listesi uctan uca
   suruldu. `wireAccountOrders` gecici yamasi kaldirildi.
7. **[KAPANDI - E2b] SERVICE WORKER SURUMLEME YOK - YAYINLANAN DUZELTME KULLANICIYA ULASMIYORDU.**
   (E2b'de olculdu; kullanicinin suphesi dogru cikti) `frontend/service-worker.js`:
   - `const CACHE = "divisima-v1"` **SABIT** - hicbir surumleme/hash yok.
   - `SHELL = ["/", "/index.html", "/manifest.json", "/api-client.js"]` -> `index.html`
     (yani **CSP meta etiketi**) install aninda onbellege aliniyor.
   - API disi her GET **cache-first**: `caches.match(req).then(cached => cached || fetch(...))`.
     Onbellekte varsa aga HIC cikilmaz.
   - Fetch handler her GET yanitini onbellege YAZIYOR -> `api-bridge.js` de ilk yuklemede
     girip sonsuza kadar oradan servis ediliyor.
   - `activate` yalniz `k !== CACHE` olanlari siliyor; CACHE hic degismedigi icin
     **hicbir sey silinmiyor**.
   - SW dosyasi kendisi degismedigi icin tarayici YENI SW kurmuyor; `skipWaiting()` /
     `clients.claim()` hic devreye girmiyor.
   Sonuc: ilk ziyaretten sonra `index.html` ve `api-bridge.js` kullanicinin tarayicisinda
   DONMUS. E2b'de CSP duzeltmeleri ancak Ctrl+Shift+R (navigasyonda SW atlanir) ile
   ulasti, normal yenileme/yeni sekmede ESKI surum geri geldi - teshis bu yuzden
   tutarsiz gorunuyordu. URETIMDE ANLAMI: yayinlanan hicbir duzeltme (guvenlik yamasi
   dahil) mevcut kullanicilara ULASMAZ. Aday duzeltme: CACHE adini her dagitimda degisen
   bir surume baglamak + navigasyon/`index.html` icin network-first. YAPILMADI -
   kullanici acikca "olc ve adim adim talimat ver" dedi, kod degisikligi istemedi.
   **EK KANIT (E2b, 2. olay): ORIGIN ERISILEMEZKEN DE ESKI SURUM SERVIS EDILIYOR.**
   Statik sunucu (:5173) fark edilmeden olmustu (`curl` -> `http=000`). Tarayici yine de
   sayfayi ACTI: SW'nin fetch handler'indaki `.catch(() => caches.match("/index.html"))`
   dali devreye girip ONBELLEKTEKI ESKI index.html'i servis etti - `?v=2` cache-buster'i
   dahil. Kullanici "duzeltme uygulanmadi" sanirken aslinda SUNUCU KAPALIYDI ve SW bunu
   GIZLEDI. Uretimdeki karsiligi: origin coktugunde kullanici hicbir hata gormez, aylar
   once onbellege alinmis bir surumu kullanmaya devam eder ve operasyon kesintiyi
   musteri tarafinda GOREMEZ. Ayni duzeltme (surumlu CACHE + navigasyonda network-first)
   bunu da kapatir.
   **KAPANIS (E2b - kullanicinin KENDI tarayicisinda olculdu).** Duzeltme iki ayak uzerine
   kuruldu: (a) `VERSION` sabiti -> `CACHE = "divisima-" + VERSION`, `activate` artik
   `k !== CACHE` olan HER onbellegi gercekten siliyor, `skipWaiting` + `clients.claim`
   devrede; (b) navigasyon + `.html` + `.js` NETWORK-FIRST, yani VERSION bumpi UNUTULSA
   BILE yayinlanan duzeltme ulasir - surumleme temizlik icin, tek dayanak degil. Offline
   yedegi YALNIZ navigasyona veriliyor (bir `.js` istegine HTML donmek "sunucu oldu"
   durumunu gizliyordu). `pwa-register.js`'e `reg.update()` eklendi - statik sunucu cache
   basligi gondermedigi icin tarayici SW betigini gec fark edebiliyordu.
   OLCUM (Bypass KAPALI, elle temizlik YOK, 2 x normal F5): dortlu CSP kontrolu TRUE,
   `caches.keys()` -> `['divisima-2026-08-21-e2b']` (**`divisima-v1` YOK** - activate sildi),
   SW kaydi 1. Yani guncelleme kullaniciya ELLE MUDAHALE OLMADAN ulasti.


8. **`SuccessDataResult<string>` ASIRI YUKLEME BELIRSIZLIGI - KOK SEBEP ACIK.**
   (E3'te olculdu; iki cagri yeri E3'te DUZELTILDI, kok sebep DURUYOR) `T = string` oldugunda
   `(T data)` ile `(string message)` AYNI imzaya duser; C# generic OLMAYAN adayi secer. Tek
   argumanli `new SuccessDataResult<string>(x)` veriyi MESSAGE'a yazar, `Data` null kalir ve
   `Success` true oldugu icin **hata SESSIZDIR**. Olculen zarar: `invoice-html` **200 +
   Content-Length: 0** (Faturalarim ekrani hic calismamisti) ve `referral/my-code`
   `{"data":null,...,"message":"REF..."}`. E3 yalniz iki cagriyi `data:` adlandirilmis
   argumana cevirdi; **kurucu setine dokunulmadi**, yani yeni yazilacak tek argumanli bir
   string cagrisi yine sessizce bozuk olur. Kokten cozum karari kullanicinin -
   **SPRINT 8 MADDE 11**. Bugunku davranis uc duzeyinde pinli (`ResultOverloadPinTests`).

9. **Bildirim aboneliklerinde `unsubscribe` ve "aboneliklerim" UCU YOK.** (E3'te olculdu)
   Tum controller'lar tarandi: yalniz `subscribe` var. Kullanici kurdugu stok/fiyat
   bildirimini ne GOREBILIYOR ne KAPATABILIYOR. E3 istemcisi bunu gizlemiyor (abonelik TEK
   YONLU kuruluyor, geri alma sozu verilmiyor) ama kalici cozum backend isi.
   **SPRINT 8 MADDE 10.**

10. **[KAPANDI - SPRINT 8 MADDE 12] `#/urun/{id}` PAYLASIM BAGLANTILARI.**
   **ONEMLI DUZELTME: bu maddenin E3'teki TESHISI YANLISTI.** Asagidaki eski metin
   "router `#/urun` yolunu TANIMIYOR" diyordu; Sprint 8'de kaynak okunup TEKRAR olculdu ve
   yol `index.html:2077`'de MEVCUT cikti:
   `else if(top==='urun'){ showHome(); var _pid=+h[1]; if(byId(_pid)) openDetail(_pid); }`
   Olcum: `#/urun/1` ile acilan sayfada gorunen view **"home"**, `detailOpenId` **1** - yani
   urun detayi GERCEKTEN aciliyor. Gordugum "Sayfa Bulunamadi" bir 404 SAYFASI DEGIL, SAYFA
   BASLIGIYDI; ilk raporda bu ikisi karistirilmisti.
   **GERCEK KUSUR IKI TANEYDI (ikisi de duzeltildi):**
   (a) `setDocTitle()` icinde `urun` dali YOK - bilinmeyen yol dalina duser. Ustelik router
       onu `openDetail`DEN SONRA cagiriyor, yani `setProductSchema`'nin koydugu dogru baslik
       hemen EZILIYOR. Paylasilan her urun baglantisi sekmede ve sosyal onizlemede
       "Sayfa Bulunamadi" gorunuyordu.
   (b) Katalog yarisi: acilista router PRODUCTS'in O ANDAKI (mock) icerigiyle calisiyor,
       gercek katalog asenkron geliyor ve `loadCatalog` sonrasi yeniden yonlendirme YALNIZ
       `#/kategori` icin yapiliyordu (Favorilerim'de bu oturumda olculen yarisin aynisi).
   Duzeltme `api-bridge.js`'te: `setDocTitle` sarmalandi + katalog sonrasi `urunRotasiniTazele()`.
   OLCULEN SONUC: baslik "Sayfa Bulunamadi · Divisima" -> **"Siyah Midi Elbise · Divisima"**.

   Eski (YANLIS) teshis, kayit icin: (E3'te olculdu)
   `index.html:2154` `shareUrl(id)` -> `#/urun/<id>` uretiyor ve urun kartindaki WhatsApp /
   Facebook / X / Pinterest / "baglantiyi kopyala" secenekleri bu adresi paylasiyor. Ancak
   urun detayi bir ROTA DEGIL, `openDetail(id)` ile acilan bir MODAL; router `#/urun` yolunu
   TANIMIYOR. Olculdu: `location.hash = "#/urun/1"` -> sayfa basligi **"Sayfa Bulunamadi ·
   Divisima"**. Uretimdeki anlami: paylasilan her urun baglantisi 404 sayfasina dusuyor -
   sosyal trafik ve SEO tarafinda dogrudan kayip. E3 KAPSAMI DISI (E3 hesap/CMS/bildirim
   yuzeyi), duzeltilmedi. Duzeltme adayi: router'a `#/urun/:id` yolu eklemek ve o yolda
   katalog yuklendikten sonra `openDetail(id)` cagirmak.

11. **`dvs_profile.email` GERCEK GIRISTE DOLDURULMUYOR.** (E3'te olculdu) index.html kendi
   yerel profil deposunu (`dvs_profile`) ve ondan tureyen `window.userEmail` degiskenini
   kullaniyor; E1 girisi gercek uclara bagladi ama e-posta alanini DOLDURMUYOR. Olculdu:
   giris yapilmis kullanicida `dvs_profile = {"name":"E3 Fix","email":""}`. E3 bunu KENDI
   tuketicisi icin kapatti (fiyat uyarisi artik `/api/Account/summary`'den okuyor ve
   `window.userEmail`'i de esitliyor), ama index.html'in o degiskeni okuyan DIGER yerleri
   hala bos gorebilir. Genel duzeltme (girisin profil deposunu gercek ozetle doldurmasi)
   yapilmadi - karar kullanicinin.

12. **Fatura HTML'inin satir ici `<style>` blogu okuma katmaninda SOKULUYOR.** (E3'te olculdu,
   BILINCLI) `OrderManager.GetInvoiceHtml` govdeyi satir ici `<style>` ile uretiyor; okuma
   katmanindaki DOMPurify izin listesinde `style` etiketi YOK, bu yuzden modal faturayi
   BICIMSIZ (sade tablo) ciziyor. Icerik TAM - siparis no, kalemler, matrah/KDV, genel toplam
   hepsi var. Guvenli taraf bilincli secildi (`style` etiketini acmak CSS enjeksiyonu yuzeyi
   getirir). Kalici cozum adaylari: faturayi `sandbox`'li bir iframe'de servis etmek ya da
   bicimlendirmeyi storefront'un kendi CSS'ine tasimak. Duzeltme YAPILMADI.

13. **UYGULAMA KULTUR PINLEMIYOR - PARA BICIMLENDIRMESI ORTAMA GORE DEGISIYOR.**
   (E3 run'inda CANLI ORTAMDA kanitlandi) `Program.cs`'te ne `RequestLocalization` ne
   `CultureInfo.DefaultThreadCurrentCulture` var; `csproj`'de `InvariantGlobalization`
   ayari da yok (tum cozum tarandi). `OrderManager.GetInvoiceHtml` tutarlari
   `{order.total_price:N2}` ile, yani **AMBIENT kulturle** basiyor.
   OLCUM: `tr-TR` -> `549,90` / `1.049,70`;  Invariant -> `549.90` / `1,049.70`.
   GitHub kosucusu (Linux, LANG=C.UTF-8) invariant kulturde kostugu icin fatura govdesi
   orada NOKTA ayracli cikti - bu, testin kultur bagimli literalini kirdi ve boylece
   davranis ORTAMDA GORULDU (teori degil). Uretimdeki anlami: Turk musteriye kesilen
   faturanin tutari, uygulamanin kostugu kabin/konteyner yerelinden etkileniyor;
   `LANG` verilmemis bir Linux dagitiminda `1,049.70 TL` yazar.
   Ayni risk fatura disindaki her `:N2` / `:C` / tarih bicimlendirmesi icin gecerli.
   **KARAR VERILDI (kullanici): SPRINT 8 MADDE 13'e yukseltildi, DOGRULUK commit'ine
   girer.** Magaza TEK PAZARLI (TR / TRY); tasarim olcerek kurulacak ve fatura govdesinin
   kosucu kulturunden BAGIMSIZ `tr` bicimiyle ciktigi pinlenecek.

14. **`X-Api-Version` BASLIGI AYRISTIRILAMAZSA TUM API BLANKET 400 VERIYOR.** (Sprint 8
   madde 9'da olculdu) `HeaderApiVersionReader("X-Api-Version")` ayristiramadigi bir degerle
   karsilasinca istegi **hangi uca giderse gitsin** bos govdeli 400 ile dusuruyor - ve bunu
   endpoint'in versiyon-NOTRLUGUNE bakmadan yapiyor (`[ApiVersionNeutral]` action ve controller
   duzeyinde AYRI AYRI denendi, ikisi de ENGELLEMEDI). Yani basligi "V1", "v1.0-beta", "latest"
   gibi bir degerle gonderen HERHANGI bir ucuncu taraf entegrasyonu, uctan bagimsiz olarak
   erisemez hale gelir. Ustelik yanit govdesi BOS oldugu icin karsi taraf sebebi goremez -
   Iyzico entegrasyonunda tam olarak bu yasandi ve teshis ancak sunucu logundan yapilabildi.
   Sprint 8 madde 9 YALNIZ `/api/payment/webhook` yolunu muaf tutti (kapsam bilerek dar,
   pinli). **GENEL COZUM KARARI KULLANICININ:** aday (i) ayristirilamayan degeri YOK SAYAN
   tolere edici bir okuyucu (mevcut istemciler etkilenmez, bozuk baslik sessizce varsayilana
   duser), (ii) 400'u KORUYUP govdeye acik bir hata mesaji koymak (teshis edilebilir olur ama
   entegrasyon yine kirilir). Bugunku davranis DIGER uclar icin pinli
   (`AyniV1Basligi_BASKA_BIR_UCTA_HALA_400_KAPSAM_DAR_KALDI`).

15. **30 DK TOKEN ZAMAN ASIMI WEBHOOK KURTARMA YOLUNU DA SINIRLIYOR.** (Sprint 8 madde 9'da
   olculdu) `HandleCallback`'teki `payment.created_at.AddMinutes(30) < DateTime.Now` guard'i
   TARAYICI callback replay'i icin dogru bir savunmadir; ama webhook AYNI kodu kullaniyor ve
   webhook FARKLI zamanlama karakteristigine sahip bir kanaldir (saglayici bildirimi
   geciktirebilir ya da saatler sonra yeniden deneyebilir). Bugunku davranis: 30 dakikadan
   eski bir GERCEK bildirim geldiginde odeme **Failed** isaretleniyor ve 400 donuyor - yani
   parasi ALINMIS bir odeme "basarisiz" olarak defterlenip mutabakat kaybediliyor. Siparis #33
   canli ornek: kurtarma denenemedi cunku token 58 dakikalikti ve tetiklenseydi kanit da
   bozulurdu. Bugun siparis Pending kaliyor (Failed'dan daha durust bir hal), ama bu SANS
   eseri - guard tetiklenseydi Failed olurdu. Aday cozumler: (i) webhook yolunda zaman asimini
   uygulamamak (otorite zaten retrieve - saglayici odemenin gercek durumunu soyluyor),
   (ii) zaman asimini gecen ama retrieve'i SUCCESS donen odemeler icin "elle mutabakat"
   kuyrugu acmak. Duzeltme YAPILMADI, karar kullanicinin.

16. **`Webhook:AllowedIps` ALLOWLIST'I VAR AMA BOS - VE PROXY ARKASINDA CALISMAZ.**
   (Sprint 8 madde 9'da bulundu) `WebhookIpAllowlistMiddleware` `/api/payment/webhook` yolunu
   saglayici IP araliklarina kapatabiliyor, ama `Webhook:AllowedIps` listesi depoda HICBIR
   YERDE doldurulmamis; bos oldugu icin middleware TAMAMEN atlaniyor. Bu, imza olmayan bir
   ucta mevcut EN GUCLU ek savunma katmani ve yalniz YAPILANDIRMA isi - kod degisikligi
   gerektirmiyor. Sprint 8'de olculen gercek Iyzico bildirimi
   `Cf-Connecting-Ip=213.226.118.95` tasiyordu (tunel uzerinden gorulen kaynak).
   **IKI UYARI:** (a) saglayici IP'leri degisebilir - liste bayatlarsa GERCEK bildirimler 403
   yer ve kurtarma yolu yine olur, yani liste ancak izlenirse guvenlidir; (b) middleware
   `RemoteIpAddress` okuyor - ters proxy/LB arkasinda `ForwardedHeaders:KnownProxies`
   DOLDURULMAZSA bu deger proxy'nin IP'sidir ve allowlist ya herkesi gecirir ya herkesi
   reddeder. Iki ayar birlikte anlamlidir (ayni not rate limit bolumunde de var).
   Duzeltme YAPILMADI, karar kullanicinin.

17. **`/api/payment/callback` RATE LIMIT POLICY'SI DISINDA.** (Sprint 8 madde 9'da olculdu)
   `[EnableRateLimiting("payment")]` yalniz `Initialize` uzerindeydi; madde 9'da `Webhook`'a da
   eklendi. `Callback` HALA yalniz GlobalLimiter'in 100/dk'sinda (yerlesik yolda). Redis yolu
   path eslesmesiyle (`/payment/`) onu da 10/dk'ya bagliyor - yani IKI YAPILANDIRMA HALA
   AYRISIYOR, sadece webhook icin hizalandi. Callback bir TARAYICI ucu oldugu icin farkli
   degerlendirilebilir (musteri basina dogal olarak seyrek), ama ayrisma bilincli bir karar
   degil, sadece kapsam disinda kalmis bir bosluk. Duzeltme YAPILMADI, karar kullanicinin.

## SUREC (degismez)

- **Tek push -> tek run -> tek rapor.** Commit/push karari HER ZAMAN kullanicidan gelir.
- **Push on-onayinin dort kosulu**: (a) `Category=Sql` yerel komut yesil,
  (b) tam suit yesil, (c) Release build 0 hata, (d) o sprintin pinlerinde dis kontrolu
  (>=3 assert ters cevir -> isimli kirmizi gozle -> geri al).
- **Test sayilari CI'dan OKUNAMAZ.** Job log'u anonim erisime 403, Summary imza istiyor,
  annotation yalniz `Failed` satirlari tasiyor, check-run `output` bos (dordu de denendi).
  Kanit = **adimin SUCCESS olmasi** + yerelde `ci.yml`'dan cikarilan komutun verdigi sayi.
- **`format-check` JOB SONUCUNDAN DEGIL ANNOTATION'DAN OKUNUR (kalici kural).** Adim
  `continue-on-error` altindaysa job YESIL, adim sonucu da API'de `success` gorunur; tek
  durust sinyal `check-runs/{job_id}/annotations` icindeki `annotation_level: failure`
  satiridir. E2b run raporunda bu ortaya cikti: format adimi en az E2'den beri exit 2
  veriyordu ve onceki raporlarda "SUCCESS" olarak gecmisti (job duzeyinde dogru, adim
  duzeyinde yaniltici). Format dalgasinda kapi sertlestirildi (`continue-on-error` kaldirildi),
  ama kural genel: **`continue-on-error` tasiyan HER adim annotation'dan okunur.**
- **Sunucular `Start-Process` ile AYRIK baslatilir.** `dotnet run` ve statik sunucu bash arka
  planindan baslatilirsa kabuk oturumu kapaninca SESSIZCE olurler (E2b'de ikisi de yasandi;
  API logu hatasiz kesildi, storefront'ta SW eski sayfayi servis edip kesintiyi gizledi).
  Uzun sureli izleyici ikisinin sagligini da yoklamali.
- **`--no-build` ile kosulan test, DEGISTIRILEN kodu DOGRULAMAZ.** Format dalgasinda bir kez
  yasandi: `dotnet format` 116 dosyayi degistirdi, `dotnet build` calisan API yuzunden dosya
  kilidiyle 8 hata verdi, ama `--no-build` testler ESKI ikililerden gecip yesil gorundu.
  Kod degistiyse ONCE temiz build, SONRA test.
- **Izleyici adabi**: nabiz >= 300 sn, tur basina TEK konsolide cagri, kota yandiysa bekle.
  Dependabot run'i beklenmez - asil iki workflow (CI + Security) yeter.
- **PAT veya tarayici eklentisi ASLA istenmez.**
- **Yerel SQL**: `DIVISIMA_TEST_SQL` her zaman set edilir (skip modu kullanilmaz);
  dizgede `Database=` bulunmalidir. LocalDB cokmus durumda ve **`sqllocaldb delete`
  YASAK** (ayni ornekte baska bir projenin `GarajimDb` veritabani var). Tam ornek
  (`Server=localhost`) kullaniliyor.
- **Uretim kodu**: yalniz kullanicinin acikca izin verdigi kalemlerde. Kapsam disi
  bulgular duzeltilmez, **SUPHELI DAVRANISLAR** basligiyla raporlanir.
