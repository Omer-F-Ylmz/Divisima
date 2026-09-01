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
- **Sprint 8 push `19d101f` - CI YESIL, SECURITY KIRMIZI (tek job).** Adim bazinda okundu:
  CI `format-check` iki ZORUNLU adim SUCCESS, `build-and-test`in TUM adimlari SUCCESS
  ("SQL gerektiren testler" + "Testler + coverage" + "Coverage raporunu yukle"), TESHIS
  skipped, **failure annotation SIFIR**. Security `dependency-scan` / `tests` / `codeql`
  SUCCESS; **`secret-scan` -> "Gitleaks (secret taramasi)" FAILURE**, annotation:
  "Leaks detected, see job summary for details".
  **KOK SEBEP OLCULDU (uretim kodu DEGIL, BENIM RAPOR ALISKANLIGIM):** madde 9 kanitini
  CLAUDE.md'ye yapistirirken gercek Iyzico odeme jetonunu BIREBIR yazdim -
  `"token":"<tam GUID>"`. Push edilen diff tarandi; anahtar-adi + yuksek entropili deger
  AYNI SATIRDA olan **TEK** yer buydu (gitleaks `generic-api-key` deseni).
  **TARAMA KAPSAMI OLCULDU** (`gitleaks-action` kaynagi + `security.yml` okundu):
  ayni dala push'ta `--log-opts=-1` -> **yalniz SON COMMIT**; `schedule`/`workflow_dispatch`'te
  **hicbir `--log-opts` yok** -> `gitleaks detect` varsayilaniyla **TUM GECMIS**. Depoda
  haftalik cron VAR (`0 6 * * 1`) ve checkout `fetch-depth: 0`. **Sonuc: kirpma TEK BASINA
  YETMEZ** - push run'i yesillenir ama haftalik tarama kirmizi kalirdi.
  DUZELTME (tek commit): iki jeton ilk 8 karaktere kirpildi + **DAR KAPSAMLI**
  `.gitleaksignore` (YALNIZ iki fingerprint, gerekcesi dosyanin basinda) + kalici maskeleme
  kurali bolum 1'e + `secret-scan` okuma kurali bolum 7'ye + force-push yasagi SUREC'e.
  **NOT: jetonlar `19d101f` GECMISINDE KALIYOR** - force-push YASAK (gerekce SUREC'te).
  Bunlar SANDBOX, TEK KULLANIMLIK, suresi DOLMUS checkout-form jetonlaridir; kimlik bilgisi
  DEGILDIR.
  **DOGRULAMA BOSLUGU (durust kayit):** `.gitleaksignore`'un ise yaradigi bir sonraki PUSH
  run'inda GORULEMEZ (push yalniz son commit'i tarar, orada bulgu zaten yok). Kanit ancak
  TUM GECMISI tarayan bir kosumdan gelir - Pazartesi cron'u ya da elle `workflow_dispatch`
  (bugun workflow'da dispatch tetigi YOK; eklemek ayri bir karar).
- **Duzeltme push `dd3b6b0` - HER IKI WORKFLOW TAMAMEN YESIL** (run 32538058539 CI +
  32538058556 Security; adim bazinda + annotation duzeyinde dogrulandi). `secret-scan` ->
  `Gitleaks (secret taramasi)` **SUCCESS**; CI `build-and-test`in TUM adimlari SUCCESS
  ("SQL gerektiren testler" + "Testler + coverage" + "Coverage raporunu yukle"),
  `format-check`in iki ZORUNLU adimi SUCCESS, TESHIS adimlari skipped, **alti job'in
  hicbirinde failure seviyeli annotation YOK** ve "Leaks detected" satiri KAYBOLDU.
  **AMA: bu yesil `.gitleaksignore`'u KANITLAMAZ** - push yalniz son commit'i (`dd3b6b0`)
  tarar ve orada bulgu zaten yoktu. Kanitlanan sey MASKELEME commit'inin kendisinin temiz
  oldugu. Fingerprint'lerin gercekten tuttugu ancak TUM GECMISI tarayan bir kosumda
  (Pazartesi cron'u) gorulur - **ilk Pazartesi kosumu IZLENMELI**; kirmizi kalirsa
  fingerprint'in kural-id'si ya da satir numarasi tutmamis demektir ve o noktada gitleaks
  yerele indirilip birebir yeniden uretilir.
- **MINI DALGA TAMAMLANDI** (workflow_dispatch + SUPHELI #15 duzeltmesi + siparis #33
  kurtarmasi + SUPHELI #17 duzeltmesi + #16 bilincli bos). Ayrinti asagidaki MINI DALGA
  bolumunde. **YENI BULGU: SUPHELI #18** - canli kurtarmada olculdu, envanter sessiz sapmasi.
- **Mini dalga push `98bbe3e` - HER IKI WORKFLOW TAMAMEN YESIL** (run 32540574944 CI +
  32540574929 Security; adim bazinda + annotation duzeyinde dogrulandi). `SQL gerektiren
  testler` / `Testler + coverage` / `Coverage raporunu yukle` / iki ZORUNLU format adimi /
  `Entegrasyon testleri` / `Gitleaks (secret taramasi)` hepsi SUCCESS, TESHIS adimlari
  skipped, **yedi job'in hicbirinde failure seviyeli annotation YOK**.
  NOT: ayni SHA'da UCUNCU bir run daha gorunuyor (`event=dynamic`, Dependabot guncelleme
  run'i) - o da success, ama izleyici kurali geregi asil iki workflow yeterlidir.
- **`.gitleaksignore` KESIN OLARAK KANITLANDI.** Kullanici `workflow_dispatch`'i elle tetikledi:
  **run 32540908505, conclusion SUCCESS**; `secret-scan` -> `Gitleaks (secret taramasi)`
  **SUCCESS**, dort job'da da failure seviyeli annotation YOK. Bu kosum `--log-opts` ALMADIGI
  icin **TUM GIT GECMISINI** taradi - yani jetonlarin durdugu `19d101f` commit'i DAHIL.
  Fingerprint'ler (kural-id `generic-api-key`, satir 1137/1277) TUTTU. Dogrulama boslugu KAPANDI.
- **Mini dalga 2 push `d4b4d01` - HER IKI WORKFLOW TAMAMEN YESIL** (adim bazinda + annotation
  duzeyinde dogrulandi). `SQL gerektiren testler` / `Testler + coverage` / `Coverage raporunu
  yukle` / iki ZORUNLU format adimi / `Entegrasyon testleri` / `Gitleaks (secret taramasi)` /
  `Acik bagimlilik KAPISI` / CodeQL hepsi SUCCESS; TESHIS adimlari skipped; **alti job'in
  hicbirinde failure seviyeli annotation YOK**. Yerelde bir kez gorulen ISIMSIZ 4. kirmizi
  CI'da TEKRAR ETMEDI. **[ACIKLANDI - Dalga D: Hangfire yarisi; bkz. MINI DALGA 2 kaydi.]**
- **SIPARIS #33'UN ENVANTER SAPMASI GIDERILDI** (kullanici karari: secenek B). Duzeltilmis
  uretim yolu bir kez kosturuldu: stok 10 -> 8, rezervasyon Expired -> Confirmed, denetim izli
  TEK hareket satiri. Ikinci cagri NO-OP (canli teyit). Elle SQL YOK. Ayrinti MINI DALGA 2
  bolumunun sonunda.
- **MINI DALGA 2 TAMAMLANDI** - SUPHELI #18 duzeltildi (ayrinti MINI DALGA 2 bolumunde).
  **Yerel: 204/204 `Category=Sql`, tam suitte 328 basarili / 331** (kirilan 3'un UCU DE
  Docker'li `OrderEndpointTests`; UC ARDISIK kosumda ayni sonuc). Release 0 hata, format TEMIZ.
  **DURUST KAYIT - ISIMSIZ FLAKE [ACIKLANDI - kayit tarihsel iz olarak DURUYOR]:** bicim
  duzeltmesinden hemen sonraki TEK bir kosumda 4 kirmizi gorundu; adlari YAKALANMADI.
  Ardindan UC kosum ust uste 3 kirmizi (yalnizca Docker) verdi. Dorduncusunun ne oldugu
  o gun BILINMIYORDU.
  **KOK SEBEP SONRADAN OLCULDU (Dalga D / `cd51a52` CI kirmizisi):** her test host'u kosulsuz
  bir Hangfire sunucusu calistirip `outbox-processor` isini DAKIKADA BIR kosuyordu ve testlerin
  KENDI drenajiyla yarisiyordu. Yaris bu satirlar yazildiginda ZATEN VARDI; dakikalik bir is
  ancak host yeterince uzun yasarsa atesledigi icin yalnizca ARADA BIR gorunuyordu - "bir
  kosumda cikip UC kosumda tekrar etmemesi" tam da bu desendir. Duzeltme: `BackgroundJobs:Enabled`
  (bkz. "CI KIRMIZISI cd51a52 ve DUZELTMESI").
  **SINIR (durust kayit):** adlar o gun yakalanmadigi icin BIREBIR esleme yapilamaz; bu, adi
  bilinen bir mekanizmanin adi bilinmeyen bir belirtiye EN OLASI aciklamasidir, ispat degil.
- **Yerel (mini dalga sonrasi): 203/203 `Category=Sql`, tam suitte 327 basarili / 330
  toplam** - kirilan 3'un UCU DE `OrderEndpointTests` (Testcontainers; yerelde Docker kapali,
  CI'da yesil kosuyor). Release 0 hata, format kapilari TEMIZ.
- **Yerel (madde 3 + madde 9 sonrasi): 198/198 `Category=Sql`, tam suitte 322 basarili /
  325 toplam** - kirilan 3'un UCU DE `OrderEndpointTests`.
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

## SPRINT 8 - LAUNCH ONCESI ZORUNLU DALGA (13/13 TAMAM - KAPANDI)

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
         "token":"76ee5138-...","iyziReferenceCode":"8fe79c9a-...",
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
yapardi - yani kurtarmak yerine kanit da bozulurdu. O AN tekrar TETIKLENMEDI.
**SONRADAN COZULDU:** guard'in webhook yolunda gevsetilmesi (SUPHELI #15) mini dalgada
yapildi ve siparis #33 o duzeltmeden sonra GERCEKTEN kurtarildi - odeme/siparis/fatura/puan
tarafi mini dalgada, envanter tarafi mini dalga 2'de. Ayrinti ilgili bolumlerde.

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
`f492bf0f-...` = `payments.token`; `iyziPaymentId 37416082` =
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

## KALITE SUPURMESI - DALGA 2 (MANTIK/INVARIANT DENETIMI) ve DALGA-2-FIX

Dalga 2 YALNIZ olcumdu: gercek dev veritabaninda 32 kimlik sorgusu (34 siparis, 21 odeme,
15 fatura, 29 stok hareketi, 35 rezervasyon). Duzeltmeler ayri commit'te geldi.

### DALGA 2 BULGULARI

| # | Sinif | Bulgu | Durum |
|---|---|---|---|
| B10 | VERI-BOZAN | Kart DISI onay yollarinda dort yan etkiden UCU hic calismiyor | **KAPANDI** |
| B11 | VERI-BOZAN | `stock_movements` Adjustment satirlari ISARETI kaybediyor | **KAPANDI** |
| B12 | ISLEV-KIRAN | Tam iptalde `shipping_cost` sifirlanmiyor - muhasebe kimligi kirik | **KAPANDI** |
| B13 | UX | Terk edilmis Pending siparisler hic kapatilmiyor (17 adet, >24 saat) | ERTELENDI (launch sonrasi defteri) |
| B14 | KOZMETIK/gizli | `DashboardManager` `PaidOrderSpec`i kullanmiyor, kurali kopyaliyor | **KAPANDI** |

**TEMIZ CIKANLAR (0 ihlal):** siparis toplami = kalemler · kargo kurali · `payments.amount` =
toplam - magaza kredisi · sadakat defteri = bakiye · magaza kredisi defteri = bakiye ·
rezervasyon <-> hareket <-> siparis (dort yon) · `reserved_quantity` = aktif rezervasyonlar ·
fatura 1:1 · KDV kimligi (15/15) · fatura kalem toplami · mukerrer basarili odeme · yetim
satirlar (4 tablo) · negatif degerler · mukerrer siparis/fatura no · zaman cizelgesi kapsamasi ·
`paid_price` = `amount` · `is_online_payment_done` · kazanim orani `floor(total/10)` ·
#33/#34 outbox dort yan etki.

**OLCUM HATASI (kayit):** ilk KDV sorgum `invoices.tax_rate`i YUZDE sandi; alan KESIR (0.2000)
sakliyor ve 15 faturanin TAMAMI ihlal gorundu. Duzeltilmis formulle 0 ihlal. Fatura HTML'i
"KDV (%20)" bastigi icin (goruntu carpiyor) ayni yanilgi tekrarlanabilir.

### DALGA-2-FIX - YAPILANLAR

**B10 - ONAY YAN ETKILERININ TEK GIRIS NOKTASI.**
Kok sebep: `ApplyConfirmedSideEffectsAsync` YALNIZ faturayi kesiyor; sadakat + referans odulu +
kupon defteri `PaymentConfirmedSideEffects`te ve oraya tek giris `IyzicoPaymentManager`in
yazdigi outbox mesaji. Kart disi UC onay yolu (`OrderManager` 363 kapida odeme / 515 havale /
568 admin durum) o mesaji HIC yazmiyordu.
- Uc yolun ucu de olayi KENDI TRANSACTION'I ICINDE yaziyor (madde 3 kalibi). `ChangeOrderStatus`
  yolunda transaction HIC YOKTU - durum yazimi + zaman cizelgesi + olay icin DAR bir transaction
  eklendi; iptal dalinin isleri `HandleStatusSideEffects` icinde, commit sonrasinda, AYNEN kaldi.
- **KUPON KULLANIM SATIRI isleyiciye tasindi** (TEK YAZICI). Onceden satiri odeme transaction'i
  yaziyordu, sayac ondan turetiliyordu; kart disi yollarda satir olusmadigi icin sayac kalici 0
  kaliyordu. Olay `discount_amount` tasiyor (snapshot semantigi).
- **FATURA SENKRON KALDI - OLCUME DAYALI GERI ADIM.** Ilk denemede faturayi da outbox'a
  birakmistim; `AuthorizationIdorTests`in IKI fatura pini bunu YAKALADI ("Sequence contains no
  elements") - kart disi yollarda fatura BUGUNE KADAR ANINDA kesiliyordu ve onu ~1 dakikaya
  yaymak ISTENMEYEN bir davranis degisikligiydi. B10'un kusuru fatura DEGIL, eksik olan diger
  uc yan etkiydi. Cakisma yok: isleyicinin 1. adimi NO-OP doner (olculdu, pinli).
  **YAN KAZANC:** fatura artik kart disi yollarda da YENIDEN DENENEBILIR.
- **"Confirmed" DOGRU TETIK NOKTASI (sart iii):** kapida odemede `Confirmed` magazanin kabulu;
  fatura zaten TAM O NOKTADA kesiliyordu, diger ucunu ayni noktaya baglamak yeni kapi acmiyor.
  Havalede `is_online_payment_done = true` orada yaziliyor. Iptalde puan ZATEN geri aliniyor.
- OLU METOT `IyzicoPaymentManager.SyncCouponUsageCountAsync` kaldirildi: hicbir cagrisi yoktu
  (Sprint 8 madde 3'ten kalma) ve yorumu bu degisiklikle YANLIS hale gelecekti.

**B11 - STOK HAREKET DEFTERINDE ISARET.**
`quantity = Math.Abs(delta)` -> `quantity = delta`. Yon YALNIZ Adjustment'ta isaretle yasar;
In/Out'un yonu `movement_type`tan gelir (onlari da isaretlemek iki mevcut pini gerekcesiz
kirardi). **Tuketici riski olculdu: `stock_movements` tablosunu OKUYAN uretim kodu YOK** -
salt-yazilir denetim izi.
MUTABAKAT FORMULU: `SUM(CASE movement_type WHEN 2 THEN -quantity ELSE quantity END)`.
Migration `20260822134317_StokHareketiIsaretliDuzeltme` (Sprint 6 kalibi): isaret YALNIZ notun
URETILMIS bicimi ("... (-N)" / "(+N)") uzerinden okunur; desene uymayan bir satir varsa
**HICBIR SATIR YAZILMADAN** RAISERROR - tahminle onarim YOK. Desen eslesmesi
`COLLATE Latin1_General_BIN2` ile (bolum 6c kurali). `01_schema.sql`e isaret sozlesmesi yazildi.
CANLI ONARIM (dev): satir 20 `5` -> `-5`; urun2/M `10 + (-2) = 8 = tablo`; bes urun-bedenin
tamami mutabik.

**B12 - TAM IPTALDE KARGO.** `order.shipping_cost = 0m`, `order.total_price = 0m` ile ayni
yerde. **SIRA KRITIK:** `leftoverRefund` HESAPLANDIKTAN SONRA - iade tutari `total_price`ten
turer ve kargoyu ICERIR; once sifirlamak musteriye kargo bedelini VERMEMEK olurdu. Para yolu
ayrica pinlendi.

**B14 - CIRO KURALI MERKEZDEN.** `IsRevenueOrder` (DISLAMA ile yazilmis kopya) kaldirildi,
`PaidOrderSpec.IsPaidStatus` kullaniliyor. Ayni sinifin diger UC sorgusu zaten spec kullaniyordu.

**VERI ARTIGI INVARIANTI.** Iptal edilmis 7 siparisin faturasi hala `Sent` (22-23 Temmuz
artiklari; bugunku kod uc iptal yolunda da faturayi iptal ediyor). GERCEK BOSLUK:
`ApplyCancelledSideEffectsAsync` BEST-EFFORT - `CancelForOrder` basarisiz donerse (saglayici GIB
iptalini reddederse) fatura Sent kalir ve bunu goren TEK sey bir LOG SATIRI. Artik siparis ZAMAN
CIZELGESINE de "KRITIK" notu dusuluyor (H53 kalibi). **Veri temizligi AYRI is - karar bekliyor.**

### PINLER

`SideEffectSingleEntryTests` (7): kapida odeme / havale / admin durum -> dort yan etki de
uygulanir · ayni siparis iki kez islenirse yan etki BIRER kalir · fatura onay aninda kesilir ve
outbox IKINCI fatura URETMEZ · onay TEK mesaj yazar, sonraki durum gecisleri MUKERRER mesaj
uretmez · iptal edilen siparisin faturasi Sent KALAMAZ (iki yol, Theory).
`LedgerAndRevenueSpecTests` (4): azalis NEGATIF yazilir · defter mutabakati = tablo · artis
POZITIF kalir (cift-anlam kirici) · ciro TANIMLI HER durum icin `PaidOrderSpec`i izler.
`OrderCancellationMoneyTests` (+2): tam iptalde kargo sifirlanir + muhasebe kimligi korunur ·
odenmis sipariste KARGO IADESI DEGISMEDI (para yolu pini).
`InvoiceCancellationTests` (+2): fatura iptali basarisizsa zaman cizelgesine KRITIK notu duser ·
basariliysa DUSMEZ (vakum kirici).

**KIRILAN PIN YOK.** Iki pin metni DUZELTILDI (assert degerleri AYNEN):
`StockReservationTests.AdjustStock_...` gerekcesindeki "mutlak fark" ifadesi "ISARETLI fark"
oldu; `PaymentConfirmedOutboxTests` KURGUSUNDAN elle eklenen `CouponUsage` satiri KALDIRILDI -
o satir birakilsaydi isleyicinin yazma adimi HIC KOSMAZ ve pin olcmesi gereken seyi olcmezdi.

### DIS KONTROLU + 5. KONTROL

5 assert ters, BES AYRI test, UC ayri sinif -> **5 AYRI ISIMLI KIRMIZI** (geri alindi).
5. kontrol, iki uretim mutasyonu (farkli testleri vurduklari icin ayristirilabilir):
- COD yolundaki onay olayi yazimi kaldirildi -> `KapidaOdemeOnayi_...` **SadakatKazanim = 0**
  buldu: Dalga 2'de olculen CANLI TABLONUN BIREBIR AYNISI. Havale ve admin-durum pinleri YESIL
  kaldi (mutasyon lokalize).
- `quantity = Math.Abs(delta)` geri getirildi -> `AzalisYonundekiDuzeltme` 5 buldu ve
  `DefterMutabakati` **28 vs 18** verdi: **tam 10 birimlik hayali fark** - urun2/M'de olculen
  sapmanin (18 vs 8) BIREBIR aynisi.
Ikisi de geri alindi.

### YEREL DOGRULAMA

227/227 `Category=Sql` · tam suitte 357 basarili / 360 (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`) · Release 0 hata · whitespace + style TEMIZ.

## VERI TEMIZLIGI - 7 IPTAL FATURASI (TAMAMLANDI)

Dalga 2'nin "VERI ARTIGI" kalemi kullanici karariyla temizlendi. **URETIM YOLUYLA, ELLE SQL YOK:**
`ApplyCancelledSideEffectsAsync` - uc iptal yolunun cagirdigi metodun ta kendisi. Kosucu DEPO
DISINDA tutuldu, is bitince SILINDI (`git status` temiz).

SAGLAYICI RISKI ONCE OLCULDU: `EInvoice:Enabled=false` -> `CancelInvoiceAsync` aninda Success
doner, disariya HIC cikilmaz. Kosucuya ayrica kilit konuldu (HttpClient istenirse gurultulu
patlar) - hic tetiklenmedi.

SONUC: 7/7 fatura `Sent(1) -> Cancelled(3)`. **IKINCI KOSUM NO-OP:** durum ayni VE satir
sayilari ayni (timeline 14->14, faturaKalemi 0->0, fatura 7->7) - "ayni son durum" tek basina
yetmez, hicbir tabloya SATIR YAZILMADIGI da olculdu. KRITIK notu 0 (saglayici reddi yok).
Iptal siparis + Sent/Approved fatura kalan satir: **0**.

DURUST SINIR: `invoice_items` 0'DA KALDI ve KALAMAZ DA - kalemler fatura URETILIRKEN yazilir;
yeniden uretmek iptal edilmis siparise fatura kesmek olurdu ve Sprint 8 madde 2 guard'i bunu
DOGRU sekilde reddediyor.

## KALITE SUPURMESI - DALGA 3 (PERFORMANS) ve DALGA-3-FIX

Dalga 3 YALNIZ olcumdu. Olcum icin veri olceklendi (2 -> 62 urun, 1 -> 41 siparis), olculdu,
sonra seed TAMAMEN SILINDI.

### DALGA 3 BULGULARI

| # | Onem | Bulgu | Durum |
|---|---|---|---|
| P1 | YUKSEK | CORS preflight onbellegi yok - trafigin %44'u OPTIONS | **KAPANDI** |
| P2 | ORTA | index.html 883 KB, 5 render-bloklayan kaynak | **KISMEN** (a+b yapildi, inline bolme ERTELENDI) |
| P3 | ORTA | Admin urun listesi sayfalanmiyor | **KAPANDI** |
| P4 | DUSUK | Istemci tarafi onbellek yok | ERTELENDI (launch sonrasi) |
| P5 | DUSUK | Her ana sayfa yuklemesinde konsol hatasi (`catGrid` guard'siz) | **KAPANDI** |

**N+1 YOK - YAPISAL KANIT:** sorgu sayisi satir sayisindan BAGIMSIZ.
`product/filter` size=1/24/60 -> **4/4/4 sorgu**; `my-orders` 1 sipariste de 41 sipariste de
**1 sorgu**; `order/get` 3 sorgu. Konsol teyidi: "24 urun API'den yuklendi (tek istek)".
**Eksik indeks onerisi: SIFIR** (sinir: DMV gercek planlardan beslenir, 62 urunluk veride
SQL Server hicbir indeksi onermeye deger bulmamis olabilir).

**B7 TEKRAR ETMEDI:** yedi akisin hicbirinde cift istek yok (her hesap sekmesi 1 istek,
siparis detayi 2 = get + timeline).
**SW ISABET ORANI OLCULEMEDI:** tarayici sanal alani SW kaydini engelledi. Dosya sunucudan
DOGRU servis ediliyor (200, application/javascript, 6531 bayt) - URUN KUSURU DEGIL, olcum
ortami siniri.

**KENDI OLCUM HATAM (kayit):** filtre govdesine `page_size` yazip "sayfa boyutu yok sayiliyor"
sandim. DTO'daki ad `size` ve ISTEMCI ZATEN DOGRUSUNU GONDERIYOR. Hatali olan BENIM GOVDEM'di.

### DALGA-3-FIX - YAPILANLAR (her kalem ONCE sayi -> degisiklik -> SONRA sayi)

**P1 - CORS `SetPreflightMaxAge(10 dk)`.**
  ONCE : 12 kimlikli istek / 24 sn -> **4 OPTIONS** (Chrome kendi kisa varsayilanina duser)
  SONRA: ayni akis -> **1 OPTIONS**; preflight yaniti `Access-Control-Max-Age: 600` tasiyor
  NEDEN 600 sn: tarayicilar bu degeri KENDI TAVANLARINA kirpar ve tavanlar farklidir
  (WebKit/Safari en dusuk). 600, yaygin tarayicilarin TAM uyguladigi en buyuk ortak deger;
  daha buyugu Safari'de sessizce kirpilir. Ayrica CORS politikasi degisirse eski izin en fazla
  10 dk yasar. **TARAYICI TAVANLARI BU DALGADA OLCULMEDI** - olculen sey Chrome'da 4 -> 1.

**P5 - `renderCatGrid` guard'i.** `document.getElementById('catGrid').innerHTML` GUARD'SIZDI;
  kardes aramalar (`if(lm)`, `if(cc)`) guard'liydi. Ana sayfada `catGrid` yok -> her katalog
  yuklemesinde TypeError. ONCE: konsolda `renderCatGrid cizilemedi` uyarisi VAR.
  SONRA: **YOK** (ayni yukleme, ayni akis).

**P3 - Admin listesi sayfali.**
  ONCE : ciplak DIZI, 62 urunun TAMAMI (17.094 bayt), `?page=1&size=1` YOK SAYILIYOR
  SONRA: storefront deseniyle AYNI zarf (`items + total_count + page + size + total_pages`);
         `?size=1` -> 1 kalem/total 2; `?size=9999` -> 200'e kirpilir; `?page=0&size=0` -> 1/100
  VARSAYILAN 100 = storefront yolunun UST SINIRI (tutarlilik). Ust sinir 200.
  **GERIYE DONUK UYUM (kullanici sarti) - CANLI DOGRULANDI:** admin paneli (admin.html
  189/345/440) DEGISTIRILMEDI; uyumu ISTEMCI ADAPTORU sagliyor - `api-client.js`'teki `list()`
  zarfi acip DIZI donduruyor. Tarayicida olculdu: `list()` -> dizi (2 kalem),
  `listPaged({size:1})` -> zarf (total_count=2, size=1).
  **KIRPILMA SESSIZ DEGIL:** `total_count > items.length` ise adaptor KONSOLA UYARI yazar.

**P2 SINIRLI (a+b).**
  (a) dort harici script'e `defer` - BAGIMLILIK KONTROL EDILDI: index.html'in satir ici kodu
      `DivisimaAPI`/`divisimaApi`'ye HIC dokunmuyor ve etiketlerden SONRAKI tek satir ici blok
      (analytics) da onlara bagli degil. `defer` SIRAYI korur.
  (b) Google Fonts `media="print"` + `onload` ile render-bloklamiyor; `<noscript>` yedegi var.
      CSP UYUMLU (sayfanin `script-src`'sinde `unsafe-inline` + `unsafe-hashes` var - olculdu:
      `media` attr calisma aninda `all` oldu, yani onload atesledi). Font UYGULANDI:
      `"Playfair Display", Georgia, serif`, 14 font yuzu yuklendi.
  ONCE : RENDER-BLOKLAYAN KAYNAK **5** (4 script + 1 stylesheet)
  SONRA: RENDER-BLOKLAYAN KAYNAK **0**
  Zamanlama (sicak, GURULTULU - kayit icin): domInteractive 185 -> 97 ms, load 204 -> 113 ms.
  Ayni sayfanin SOGUK olcumu 1384/1503 ms cikmisti; bu yuzden SURE PINI KONULMADI.
  **INLINE 704 KB script / 142 KB style BOLUNMEDI** - ayri bir is, launch sonrasi defterinde.

### PINLER (`PreflightAndAdminPagingTests`, 7)

SURE PINI YOK - YAPI PINI VAR (kullanicinin kurali).
- `Preflight_Yaniti_ACCESS_CONTROL_MAX_AGE_Tasir` - vakum kirici: once CORS'un preflight'i
  GERCEKTEN degerlendirdigi (`Allow-Origin`) dogrulanir, yoksa assert yanlis sebepten kirmizi
  olurdu. Deger sabitlenmez, MAKUL ARALIK pinlenir (60..86400).
- `AdminListesi_SAYFALI_ZARF_Doner_TOPLAM_SAYIYI_Bildirir`
- `AdminListesi_SAYFA_PARAMETRELERI_ISLER_ve_TOPLAM_DEGISMEZ` - cift-anlam kirici: farkli
  sayfalar FARKLI urunler dondurmeli (her sayfa ilk N'i donduren bir uygulama da digerlerini gecerdi)
- `AdminListesi_SINIR_DEGERLERI_CLAMP_Edilir` (Theory, 3 vaka)
- `AdminListesi_ZENGINLESTIRME_URUN_SAYISINDAN_BAGIMSIZ_Calisir` - "liste ucu kalem basina ek
  sorgu atmaz" sozlesmesinin yapi pini: 1 urunle de 30 urunle de AYNI alanlar dolu.

**KIRILAN PIN YOK.**

### DIS KONTROLU + 5. KONTROL

5 assert ters -> **4 AYRI ISIMLI KIRMIZI** (iki flip ayni testte oldugu icin 4; >=3 sarti saglandi).
5. kontrol, iki uretim mutasyonu:
- `SetPreflightMaxAge` kaldirildi -> `Access-Control-Max-Age` basligi **YOK** (olculen
  once-durumun ta kendisi: 12 istek -> 4 OPTIONS).
- Sayfalama (`Skip/Take`) kaldirildi -> `?size=3` **7 kalem** dondu, yani parametre YOK SAYILDI -
  Dalga 3'te olculen `?page=1&size=1` davranisinin BIREBIR aynisi.
`AdminListesi_SAYFALI_ZARF_...` mutasyonda YESIL kaldi (zarf duruyor, yalniz kirpma yok) -
mutasyonun lokalize oldugunun kaniti. Ikisi de geri alindi.

### YEREL DOGRULAMA

234/234 `Category=Sql` · tam suitte 364 basarili / 367 (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`) · Release 0 hata · whitespace + style TEMIZ.


## (C) GUVENLIK DALGASI (OLCUM) ve GUVENLIK-FIX

Dalga YALNIZ olcumdu: iki gercek musteri (A=22, B=23) + admin (24), hepsi GERCEK
register/verify/login zincirinden. Uydurma JWT yok, yikici deneme yok, gercek Iyzico'ya
HIC gidilmedi. **KRITIK ve YUKSEK sinifta bulgu YOK.**

### OLCUM SONUCU - DENENIP TUTMAYANLAR (sonuc olarak kayda gecer)

- **S1 IDOR:** B'nin A'ya 10 capraz denemesi -> hepsi 404/403; 9 yetki yukseltme -> 403;
  4 anonim -> 401. **Tek IDOR yok.**
- **S2 TUTAR:** `unit_price/subtotal/total_price/discount_amount` enjekte edildi -> YOK
  SAYILDI (DB: `899.90|0.00|49.90|949.80`, sunucu hesabi). `customer_id=A` gonderildi ->
  siparis **B'ye** yazildi. Baskasinin adresi 403, baskasinin siparisine odeme init 403.
  Adet -5/0/101/100000 -> 400. `PaymentInitRequestDto` YALNIZ `order_id` tasiyor - tutar
  istemciden HIC gelmiyor.
- **S4 MASS ASSIGNMENT:** kayitta `user_type=1/is_admin/loyalty_points=999999/
  store_credit=999999/email_verified` -> DB'de `2|0|0.00|0`. Hepsi yok sayildi.
- **S6 ENJEKSIYON:** uretimde **ham SQL SIFIR**; yuklemede magic-byte dogrulamasi;
  `callback_url` SSRF guard'i; kullanici kontrollu dis cagri yok.
- **S7 SIZINTI:** yanit DTO'larinda hash/salt/secret yok; giris govdesindeki `refresh_token`
  alani NULL (Sprint 8 cerez sozlesmesi tutuyor); 500 govdesi RFC 7807, **yigin izi yok**.
- **S8 BASLIK:** HSTS non-Development dalinda bagli; nosniff / Referrer-Policy / CSP /
  `X-Frame-Options: DENY` / Permissions-Policy var; cerezler httpOnly+Secure+SameSite=Strict;
  **CORS echo-origin riski YOK** (bilinmeyen origin -> Allow-Origin hic yok).
- **S9 YARIS:** 8 eszamanli kredi harcamasi -> 500.00 -> 100.00 = 4 x 100, defter mutabik,
  negatif bakiye yok. Cift harcama yok.

### BULGULAR (G1..G9) ve DUZELTMELERI

| # | Onem | Bulgu | ONCE (olculdu) | SONRA (olculdu) |
|---|---|---|---|---|
| G1 | ORTA | Refresh token yeniden kullanimi tespit edilmiyor | dondurulmus jeton 401, ama YENI jeton **200**; aktif oturum 11 | YENI jeton **401**; aktif oturum **0**; `RefreshTokenReuse` Critical olayi 1 |
| G2 | ORTA | Kayit ucunda e-posta enumeration | var olan 400 "zaten kayitli" / yeni 201 | ikisi de **201 + AYNI govde** |
| G2b | ORTA | `resend-verification` UC ayri yanit veriyordu | 404 / 200 "zaten dogrulanmis" / 200 "gonderildi" | **TEK** yanit |
| G3 | ORTA | Anonim uctan >=4000 karakterlik arama -> 500 | 3998 -> 200, 4000/5000 -> **500**; 9 istek: 6 ERROR satiri, 66 SQL yigin satiri, 17.655 bayt log | 201+ -> **400**; 9 istek: **0 ERROR, 0 yigin, 855 bayt** |
| G3b | DUSUK | Ayni hata admin musteri aramasinda da vardi | 4000 karakter -> **500** | **400** |
| G4 | ORTA (bugun ERISILEMEZ) | Satici girisi refresh token'i GOVDEDE donuyor | `SellerAuthManager:101`; kayit kapali (403), `sellers` 0 satir | **DOKUNULMADI** - satici modulu on kosulu (KARARLAR) |
| G5 | DUSUK | Yetki varsayilani ACIK | oznitelik olmayan uc herkese acik | oznitelisiz uc **401**; acik uclar aynen 200 |
| G6 | DUSUK | Kargo ucu varlik sizdiriyor | baskasinin siparisi **403** "Bu kargo size ait degil", olmayan 404 | ikisi de **404 + ayni govde** |
| G7 | DUSUK | Satici kaydinda dogrulama kapidan ONCE | eksik govde **400** "The email field is required." | **403** |
| G8 | DUSUK | `Server: Kestrel` | var | **yok** |
| G9 | DUSUK | Negatif `use_store_credit` sessizce kabul | **201** (bakiye degismedi) | **400**, siparis olusmaz |

**LAUNCH'I BLOKE EDEN BULGU YOKTU.**

### KAPSAM EKLERI (rapor edildi, gerekcesiyle)

- **G2b ve askiya-alinmis-hesap 500'u (C) dalgasinda OLCULMEMISTI**, G2 duzeltilirken bulundu.
  Ikisi de G2'nin AYNI kapisidir: `resend-verification` acik kalsaydi saldirgan ayni soruyu
  bir uc oteden sorardi; askidaki hesabin 500'u ise 201-vs-500 farkiyla sizintiyi surdururdu.
  Bu yuzden G2 ile birlikte kapatildilar.
- **G3b (admin arama)** ayni hata sinifinin ikinci ve son yuzeyi (depoda serbest metinli LIKE
  aramasi TAM IKI yerde). Sinir "sema kaynakli" gerekceyle konuldugu icin birini birakmak
  gerekceyle celisirdi.

### G2 TASARIMI (UX etkisi tasidigi icin ayrica yazildi)

**Yanit HER ZAMAN ayni (201 + notr metin); ne oldugunu gercek kullanici E-POSTADAN ogrenir -
dort durum, dort farkli e-posta, tek yanit.**

| Durum | E-posta | Not |
|---|---|---|
| adres bos | dogrulama jetonu | bugunku davranis |
| hesap var, DOGRULANMIS | "zaten hesabin var, giris yap / sifreni sifirla" | |
| hesap var, DOGRULANMAMIS | **YENI** dogrulama jetonu | bugunkunden IYI: onceden 400 yiyip sikisiyordu |
| hesap var, ASKIDA | "hesabin askida, destek ile iletisim" | 500 yerine |

`resend-verification` ayni kalibi izler (`PasswordResetMailSent` deseni).
Mesaj sabiti `RegisterSuccess` -> `RegisterSubmitted` olarak DEGISTI: "Kaydiniz olusturuldu"
var olan hesapta YALAN olurdu; yeni metin dort durumda da DOGRU.

**DURUST SINIR:** yanit esitligi ZAMANLAMAYI esitlemez (400 yolu 9 ms, 201 yolu 14 ms olculdu).
Sabit-zamanli kayit ayri bir istir. Ayrica KILITLENME kanali aciktir: 5 basarisiz giristen
sonra kayitli adres 403 "hesap kilitlendi", kayitsiz adres 401 doner - kapatmak "gercek
kullaniciya hesabinin kilitli oldugunu soyleme" ile celisir; **karar kullanicinin**.

### G5 - FallbackPolicy YERINE `MapControllers().RequireAuthorization()` (OLCUMLE SAPMA)

Istenen `options.FallbackPolicy` idi; once o uygulandi ve **MEVCUT BIR PINI KIRDI**:
`WebhookContractTests.AyniV1Basligi_BASKA_BIR_UCTA_HALA_400_KAPSAM_DAR_KALDI` 400 yerine
**401** buldu. Sebep olculdu: `X-Api-Version` ayristirilamayinca Asp.Versioning gercek
endpoint yerine **metadata'siz bir HATA endpoint'i** koyuyor; FallbackPolicy onu da kapsayinca
400'u yazan kod HIC calismiyor. Bu, SUPHELI #14'te belgelenen sorunu DAHA KOTU yapardi -
entegratore 401 demek onu kimlik hatasi aramaya yonlendirir.
Kapsam controller'lara daraltilinca ayni guvence yan etkisiz saglandi (olculdu: oznitelisiz
uc 401, `X-Api-Version` pini yesil).
**KALAN BOSLUK (durust kayit):** ileride eklenecek bir minimal-API ucu ya da yeni bir hub
yine varsayilan acik olur. O bosluk RUNTIME'da degil TEST'te kapatildi - pin her uretim
ucunun ACIKCA isaretli oldugunu tarar (oznitelikler yansimayla okunur; `EndpointMetadata`
okunsaydi konvansiyonun ekledigi `AuthorizeAttribute` yuzunden tarama VAKUM olurdu).

### PINLER (`SecurityHardeningTests`, 15)

G3: uzun terim 400 + **hata seviyeli log SIFIR** · sinir icinde 200 ve GERCEKTEN eslesir
(+ tam sinir 200) · admin aramasi da 400 (once normal aramanin 200 oldugu dogrulanir).
G2: var olan ve yeni adres AYNI kod + AYNI govde (+ yeni adres icin hesap GERCEKTEN acilir,
+ ayni adres IKINCI satir uretmez) · askidaki adres 500 URETMEZ ve ayirt edilemez.
G2b: uc durumda tek imza (+ dogrulanmamis hesaba jeton GERCEKTEN uretilir).
G1: dondurulmus jeton sunulunca YENI jeton da 401, aktif oturum 0, guvenlik defterinde TAM 1
kayit, **tekrar denemeler YENI alarm URETMEZ** · sinyal YOKKEN 3 ardisik yenileme calisir ve
alarm 0 (vakum + cift-anlam kirici).
G6: baskasinin kargosu 404 ve olmayanla AYNI (+ sahibi FARKLI cevap alir).
G5: oznitelisiz sonda ucu 401, kardes `[AllowAnonymous]` uc 200 · health uclari 200, anonim
katalog/arama 200, korumali uc 401, her uretim ucu acikca isaretli (+ sonda ucunun
ISARETSIZ gorulmesi cift-anlam kiricisi).
G9: negatif kredi 400 + siparis olusmaz. G8: Server yok + diger guvenlik basliklari var.
G7: kapali kapida eksik govde 403 ve "required" SIZMAZ · **kapi acikken 400** (cift-anlam kirici).

**BILINCLI DEGISTIRILEN PIN (1):**
`KimlikDizgesiSozlesmeTests.AyniAdresinFarkliCasingi_IKINCI_KAYITTA_REDDEDILIR`
-> `..._IKINCI_HESAP_ACMAZ`. Eski assert (`NotBe(Created)`) B1'in gercek invariantini degil,
o gunku YAN ETKISINI (400) sabitliyordu; G2'den sonra DOGRU davranisi kirardi. Asil assert
(satir sayisi 1 + kanonik deger) **DEGISMEDI**.

**OLU HALE GELEN IKI SABIT KALDIRILDI** (Sprint 8 madde 11 kalibi - build kanit):
`Messages.RegisterSuccess`, `Messages.ShipmentNotYours`. Derleme 0 hata -> gercekten oluydular.

### DIS KONTROLU + 5. KONTROL

5 assert ters (BES ayri test) -> **5 AYRI ISIMLI KIRMIZI** (geri alindi).
5. kontrol, IKI uretim mutasyonu:
- **M1** (`ProductSearchRequestValidator`'daki uzunluk kurali kaldirildi) -> pin 201'de kirildi;
  ayrica canli sunucuda **olculen once-durum BIREBIR uretildi**: 3998 -> 200, 4000 -> **500**,
  5000 -> **500** ve sunucu logunda 12 satir `truncated`/`8152`.
- **M2** (`GetByRefreshTokenAnyStateAsync` -> `GetByRefreshTokenAsync`, yani `is_active`
  filtreli hale dondu) -> `DondurulmusRefreshToken_...` **200 buldu** - olculen once-durumun
  ta kendisi. Diger 13 pin YESIL kaldi (mutasyonlar lokalize).
Ikisi de geri alindi.

### ELLE DOGRULAMA (tarayici)

Storefront :5173 (minik HttpListener sunucusu, depo disinda), API :5000.
Ayni adresle IKI kez kayit -> **iki istek de 201**, ekranda IKI durumda da ayni notr metin
("... adresine bir e-posta gonderdik. Yeni hesap acildiysa ..."), **hata alani BOS**
(onceden ikinci denemede "Bu e-posta adresi zaten kayitli." hatasi cikiyordu).
DB: **1 satir**, `email_verified=0`, dogrulama jetonu 43 karakter (yani ikinci kayit gercekten
YENI jeton uretti). Konsolda uygulama hatasi yok; iki hata satiri **service worker kaydi**
(tarayici sanal alani SW'yi engelliyor - Dalga 3'te de olculdu, urun kusuru degil).
YAN GOZLEM: iki POST icin **1 OPTIONS** - Dalga-3'un preflight onbellegi calisiyor.

### YEREL DOGRULAMA

249/249 `Category=Sql` · tam suitte 379 basarili / 382 (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`) · Release 0 hata · whitespace + style TEMIZ (exit 0).

**DURUST KAYIT - ADI OLAN FLAKE:** tam suit DORT kez kosuldu. Bir kosumda DORDUNCU bir
kirmizi gorundu: `RefreshCookieContractTests.Cerez_Secure_HER_ORTAMDA_ISARETLI_OrtamGuardi_YOK`.
Sonraki UC tam suit kosumunda ve sinif TEK BASINA kosuldugunda (4/4 yesil) TEKRAR ETMEDI.
**Hata mesaji YAKALANAMADI** - o kosumda ciktiyi suzen grep deseni mesaji disarida birakti;
uydurma bir aciklama yazilmiyor. Bu test PRODUCTION ortamli IKINCI bir host aciyor (sinifin
kendi host'u zaten ayakta), yani en olasi sinif ana-host ile ikinci host arasindaki bir yaris -
ama bu OLCULMEDI, tahmindir. Onceki dalgalardaki ISIMSIZ flake'ten farki: bu sefer AD BELLI.
CI'da tekrar ederse SUPHELI olarak ACILIR.

**SONRADAN EKLENEN ADAY ACIKLAMA (Dalga D - ACIK KALIYOR, kapatilmadi):** Dalga D'de olculen
Hangfire yarisi (her test host'u kosulsuz bir arka plan sunucusu aciyordu) bu kayit icin de
bir adaydir - cunku bu test IKINCI bir host aciyor ve o host da kendi Hangfire sunucusunu
kaldirip AYNI depolamaya (`ConnectionStrings:DivisimaDb`) baglaniyordu. Belirtinin
"assert satiri suzgecine takilmamasi" da bununla tutarli: host KURULUMUNDA patlayan bir
istisna assert mesaji URETMEZ. **AMA BU OLCULMEDI ve belirtinin kendisi (cerez `Secure`
bayragi) outbox mekanizmasiyla dogrudan ilgisiz** - bu yuzden ISIMSIZ flake'lerden farkli
olarak bu kayit **ACIKLANMIS SAYILMIYOR**. CI'da tekrar ederse SUPHELI olarak acilir.

**IKINCI ADAY ACIKLAMA (Dalga D - YINE ADAY, KAPATILMADI): `model` KILIDI.** CI kirmizisi
10d794d'de olculdu ki `CREATE DATABASE` / `DROP DATABASE` islemleri `model` uzerinden
serilesiyor ve cakisma **`SqlException 1807`** uretiyor. Bu kayit icin neden ADAY: (a) bu
test IKINCI bir host aciyor, yani ekstra kurulum yuku tasiyor; (b) 1807 host/kurulum
asamasinda patlar ve **assert mesaji URETMEZ** - "hata mesaji YAKALANAMADI, ciktiyi suzen
grep deseni mesaji disarida birakti" gozlemiyle BIREBIR tutarli; (c) belirtinin
"bir kosumda cikip UC kosumda tekrar etmemesi" contention desenine uyar.
**AMA OLCULMEDI:** o kosumun hata metni elde YOK, dolayisiyla 1807 oldugu KANITLANAMAZ.
Bu bir ADAYDIR - "kapandi" DEGIL. 1807 tarafi artik yeniden denemeyle azaltildi
(`TestDbKurulum`), yani bu aday dogruysa belirti kendiliginden seyrelir; **yine de kayit
ACIK kalir** ve CI'da tekrar ederse SUPHELI olarak acilir.
Not: iki aday BIRBIRINI DISLAMAZ - Hangfire yarisi ve `model` kilidi ayri mekanizmalardir
ve ikisi de o kosumda yururlukteydi.

**[KAPANDI - FLAKE-FIX] KOK SEBEP OLCULDU; IKI ADAY DA TAM ISABET DEGILDI.**
**COZUM (FLAKE-FIX):** `BackgroundJobs:Enabled=false` iken Hangfire DEPOLAMA yapilandirmasi
da atlaniyor - bayrak false host'unda Hangfire'a ait HICBIR DI kaydi yok, dolayisiyla
`IGlobalConfiguration` AKTIVE EDILEMEZ ve bu istisna YAPISAL OLARAK olusamaz. Ardisik UC tam
suit temiz (3'te-1 tabanina karsi). Ayrinti: FLAKE-FIX bolumu. Asagidaki metin TESHIS kaydidir.

Bu dalgada hata
mesaji ILK KEZ yakalandi: `Autofac ... activating λ:Hangfire.IGlobalConfiguration` ->
`Timeout expired ... max pool size was reached`. Yani dogru aile **Hangfire**'di (birinci
aday) ama mekanizma **yaris DEGIL BAGLANTI HAVUZU TUKENMESI**; `model` kilidi (ikinci aday)
ise ILGISIZ cikti (o kosumda 1807 hic ateslemedi). `BackgroundJobs:Enabled=false` yalniz
`AddHangfireServer()`i kapatiyor - Hangfire'in DEPOLAMA yapilandirmasi hala SQL'e baglaniyor
ve bu test IKINCI bir host actigi icin ekstra baglanti kumesi aciliyor. Ayrinti, siklik
olcumu (tam suit 3 kosum -> 1 kirmizi) ve aday cozumler GUVENLIK-FIX-4 bolumunde.
**DUZELTILMEDI - karar kullanicinin.**


## GUVENLIK-FIX-2 - SUPHELI #19 (KILIT ENUMERATION) KAPANDI

Kullanici karari: **secenek (iii)** - kilit bilgisi YALNIZ SIFRE DOGRUYSA bildirilir.
AYRI commit (bu is GUVENLIK-FIX commit'ine BINMEDI), ayni push.

### KOK SEBEP: SIRA (kod degil)

`AuthManager.Login` kilit kontrolunu SIFRE DOGRULAMASINDAN ONCE yapiyordu. Bes basarisiz
denemeden sonra:

```
ONCE   kayitli adres  + yanlis sifre -> 403 "Cok fazla basarisiz deneme. Hesabiniz ... kilitlendi"
       kayitsiz adres + yanlis sifre -> 401 "E-posta veya sifre hatali."
SONRA  kayitli adres  + yanlis sifre -> 401  (kayitsiz adresle BIREBIR AYNI govde)
       kayitli adres  + DOGRU  sifre -> 403  kilit mesaji (kullanici NEDEN giremedigini ogrenir)
```

Kaybeden yalniz oracle: gercek kullanici sifresini dogru yazdiginda kilit bilgisini
ALMAYA DEVAM EDIYOR.

### KILIT UZATMA (DoS) GUARD'I - SIRA DEGISIKLIGININ ACTIGI KAPI KAPATILDI

Eski kodda kilitliyken `VerifyPasswordHash` HIC calismiyordu, dolayisiyla basarisiz sayaci
da artmiyordu. Yeni sirada dogrulama HER ZAMAN kosuyor; sayac kosulsuz artsaydi saldirgan
kilitli bir hesabi surekli yanlis sifreyle doverek kilidi SONSUZA KADAR uzatabilirdi
(`LockAccountAsync` sayaci sifirliyor - sayac yeniden 5'e ulasir ve YENI bir 15 dakika yazilir).
Bu yuzden: **kilitliyken yanlis sifre sayaci ARTIRMAZ, olay YAZMAZ, kilidi UZATMAZ.**
Eski davranisin bu ozelligi KORUNDU; pin `lockout_end`'in DEGISMEDIGINI de assert ediyor.

**TIMING:** degisiklik zamanlamayi KOTULESTIRMIYOR, iyilestiriyor - kilitli ve kilitsiz
yollar artik AYNI isi yapiyor (ikisi de bir hash dogrulamasi kosuyor).

### SATICI TARAFI (dokunulmadi, kayit)

`SellerAuthManager.Login` AYNI siraya sahip (kilit kontrolu dogrulamadan once). Bugun
ORACLE OLUSTURAMAZ: `sellers` tablosu 0 satir ve kayit kapali (403), yani her giris
`seller == null` dalina duser. Satici modulu acilirken bu sira da musteri tarafiyla
hizalanmali - **G4 ile ayni on kosul listesinde** (bkz. KARARLAR).

### PINLER (`SecurityHardeningTests`, +3 -> toplam 18)

- `KilitliHesap_YANLIS_SIFREYLE_KAYITSIZ_ADRESLE_AYNI_YANITI_Doner` - ayni kod + ayni govde,
  "kilit" kelimesi yanitta GECMEZ, ve `lockout_end` DEGISMEZ (DoS guard'i).
- `KilitliHesap_DOGRU_SIFREYLE_403_KILIT_MESAJI_Alir` - vakum kirici: esitlik "her seye 401
  don" ile saglanmadi, gercek kullanici bilgiyi ALIYOR.
- `KILITSIZ_Hesapta_Giris_AYNEN_Calisir` - vakum kirici: yanlis sifre 401, DOGRU sifre **200**
  (giris tumden bozulmadi) + basarili giris sayaci sifirliyor.

ON KOSUL GERCEK YOLDAN: hesap DB'ye elle `lockout_end` yazilarak degil, GERCEK uctan bes
yanlis giris yapilarak kilitleniyor.

### DIS KONTROLU + 5. KONTROL

3 assert ters (UC ayri test) -> **3 AYRI ISIMLI KIRMIZI** (geri alindi).
5. kontrol: kilit kontrolu SIFRE DOGRULAMASINDAN ONCEYE geri alindi ->
`KilitliHesap_YANLIS_SIFREYLE_...` **403 buldu** - olculen once-durumun (oracle) ta kendisi.
Diger 17 pin YESIL kaldi (mutasyon lokalize). Geri alindi.

### YEREL DOGRULAMA (iki commit birlikte)

252/252 `Category=Sql` · tam suitte 382 basarili / 385 (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`) · Release 0 hata · whitespace + style TEMIZ (exit 0).


## GUVENLIK-FIX + GUVENLIK-FIX-2 PUSH RAPORU (147a95d)

**Push `b7e9279..147a95d` - IKI COMMIT, TEK PUSH** (Sprint 8 kalibi):
`a8fb34b` GUVENLIK-FIX (G1..G9) + `147a95d` GUVENLIK-FIX-2 (SUPHELI #19).

### ADIM BAZINDA SONUC

- **CI - Build & Test (run 32593535275) - TAMAMEN YESIL.**
- **Security CI (run 32593535166) - KIRMIZI, TEK JOB.** Adim bazinda okundu:
  `codeql` SUCCESS · `dependency-scan` (3 adim: RAPOR / KAPI / kullanimdan kalkmis paket)
  SUCCESS · `tests` -> `Is mantigi guvenlik simulasyonu` SUCCESS, `SQL Server hazir mi`
  SUCCESS, **`Entegrasyon testleri` SUCCESS**, TESHIS adimi skipped.
  **`secret-scan` -> `Gitleaks (secret taramasi)` FAILURE.**

### KOK SEBEP - KANIT KANALINDAN DEGIL DEPO TARAMASIYLA (bolum 7 kurali dogrulandi)

Annotation ANONIM okundu: yalniz iki WARNING var - Node 20 deprecation ve
"Leaks detected, see job summary for details". **Dosya/satir/kural TASIMIYOR.**
Yani kural bir kez daha dogrulandi: `secret-scan` annotation'dan degil ADIM SONUCUNDAN
okunur, kok sebep DEPO TARAMASIYLA bulunur.

Push run'i `--log-opts=-1` ile YALNIZ SON COMMIT'i (`147a95d`) tarar. O commit'in eklenen
satirlari tarandi; sifre/anahtar bicimli TAM DORT satir bulundu, hepsi yeni test dosyasinda.
Hangisinin tetikledigi ENTROPIYLE ayrildi (`generic-api-key`: anahtar kelime + entropi >= 3.5;
esik degeri deponun kendi `.gitleaksignore` notunda zaten yaziliydi):

```
Shannon entropisi (karakter basina)
  satir 731 / 806 degerleri (16 krkt, tireli)  -> 3.750   ESIGIN USTUNDE  <- BULGU
  ucuncu deger (15 krkt, tireli)               -> 3.374   esigin ALTINDA
  TestAuthHelper.TestPassword                  -> 3.027   esigin ALTINDA
  AuthRateLimitPinTests'teki yanlis parola     -> 3.457   esigin ALTINDA
```

**MODEL BILINEN-YESIL BIR KOSUMLA DOGRULANDI:** son iki deger depoda ZATEN VAR ve TUM
GECMISI tarayan `workflow_dispatch` kosumu (run 32540908505) SUCCESS'ti. Yani 3.5 esigi
teoriden degil, gecmiste yesil kalmis gercek bir tam-gecmis taramasindan dogrulandi.
`a8fb34b` (birinci commit) ayni taramadan gecti: **sifre/anahtar literal'i SIFIR**.

### DUZELTME (yerelde hazir)

1. **Ileriye donuk:** dort literal de TEK bir DUSUK ENTROPILI sabite (`YanlisSifre`, 3.096)
   cevrildi. Uc bagimsiz sebeple guvenli: deger esigin altinda · kullanim satirinda TIRNAKLI
   deger yok (sadece tanimlayici) · tanim satirinda anahtar kelime GECMIYOR.
   Yorumdaki ornek degerler de CLAUDE.md bolum 1 kuralina uyarak KIRPILDI - kanit degeri
   entropi sayisinda, dizgenin kendisinde degil.
2. **Gecmis icin:** `.gitleaksignore`'a DAR KAPSAMLI iki fingerprint + gerekcesi
   (`147a95d:...SecurityHardeningTests.cs:generic-api-key:731` ve `:806`).
   Force-push YASAK oldugu icin `147a95d`'nin gecmisteki hali ancak boyle susturulur.
   Susturulan sey KIMLIK BILGISI DEGIL: bir testin BILEREK YANLIS yazdigi, hicbir hesaba
   ait olmayan sifre denemeleri.

**DOGRULAMA BOSLUGU (Sprint 8'dekiyle AYNI, durust kayit):** fingerprint'lerin tuttugu bir
sonraki PUSH run'inda GORULEMEZ (push yalniz son commit'i tarar, orada bulgu zaten olmayacak).
Kanit ancak TUM GECMISI tarayan bir kosumdan gelir - Pazartesi cron'u ya da ELLE
`workflow_dispatch` (tetik depoda VAR, mini dalgada eklendi). Kural-id ya da satir numarasi
tutmazsa satirlar sadece ETKISIZ kalir, zarar vermez.

### YEREL DOGRULAMA (duzeltme sonrasi)

252/252 `Category=Sql` · tam suitte 382 basarili / 385 (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`) · Release 0 hata · whitespace + style TEMIZ (exit 0).
Dis kontrolu ve 5. kontrol YENIDEN KOSULMADI - degisiklik yalnizca sabit degeri (literal ->
`YanlisSifre`); pinlerin OLCTUGU sey ve assert'ler DEGISMEDI.


## SECRET-SCAN DUZELTMESI PUSH RAPORU (d40be2f) - HER IKI WORKFLOW YESIL

**Push `147a95d..d40be2f`.** Adim bazinda + annotation duzeyinde dogrulandi.

### CI - Build & Test (run 32596590781) - TAMAMEN YESIL
`build-and-test`: .NET kurulumu · geri yukleme · **Derle (Release)** · SQL Server hazir mi ·
**SQL gerektiren testler (ATLANMAMALI)** · **Testler + coverage** · **Coverage raporunu
yukle** hepsi SUCCESS; TESHIS skipped.
`format-check`: **iki ZORUNLU adim** (whitespace + style) SUCCESS.

### Security CI (run 32596590765) - TAMAMEN YESIL
`codeql` SUCCESS · `dependency-scan` (RAPOR / KAPI / kullanimdan kalkmis paket) SUCCESS ·
`tests` (Is mantigi simulasyonu / SQL hazir mi / **Entegrasyon testleri**) SUCCESS, TESHIS
skipped · **`secret-scan` -> `Gitleaks (secret taramasi)` SUCCESS.**

**ALTI JOB'IN HICBIRINDE failure seviyeli annotation YOK** (tek tek tarandi) ve "Leaks
detected" satiri KAYBOLDU.

### DISPATCH KOSUMU BIR YARISA DENK GELDI (durust kayit)

Kullanici `workflow_dispatch`'i tetikledi -> **run 32596588688**. AMA o kosum
`head_sha = 147a95d` uzerinde kostu, yani DUZELTMEDEN ONCEKI commit'te:

```
dispatch olusturuldu : 2026-08-22T20:24:32Z
izleyicinin 1. turu  : 23:24:50 (+03)  = 20:24:50Z
```

Yani tetikleme ile push **18 saniye** arayla oldu; GitHub dispatch'i o anki main HEAD'ine
(`147a95d`) bagladi. `.gitleaksignore`'daki iki fingerprint `d40be2f`'te oldugu icin o
kosum onlari HIC GORMEDI. Kullanici hatasi degil, ZAMANLAMA.

**YINE DE DEGERLI BIR OLCUM:** o kosum TUM GECMISI taradi ve `secret-scan` FAILURE dondu -
yani bir tam-gecmis taramasinin `147a95d`'deki literal'leri GERCEKTEN buldugu KANITLANDI.
Bu, iki seyi birden dogruluyor: (a) kok sebep teshisi gecmis duzeyinde de dogru,
(b) fingerprint'ler GERCEKTEN gerekli - yalniz ileriye donuk kirpma yetmezdi.

### FINGERPRINT'LER KESIN OLARAK KANITLANDI - KONTROLLU A/B

Kullanici dispatch'i `d40be2f` HEAD iken TEKRAR tetikledi -> **run 32600833891**.
Bu kosum `--log-opts` ALMADIGI icin **TUM GIT GECMISINI** taradi.

```
AYNI EVENT (workflow_dispatch), AYNI KAPSAM (tum gecmis), TEK FARK .gitleaksignore:

  run 32596588688   head_sha 147a95d   (fingerprint YOK)   secret-scan -> FAILURE
  run 32600833891   head_sha d40be2f   (fingerprint VAR)   secret-scan -> SUCCESS
```

`32600833891` adim bazinda: `tests` / `dependency-scan` / `codeql` SUCCESS ·
**`secret-scan` -> `Gitleaks (secret taramasi)` SUCCESS** · dort job'da da failure
seviyeli annotation **0** ve "Leaks detected" satiri **0**.

Yani iki fingerprint (kural-id `generic-api-key`, satir 731/806) GERCEKTEN TUTTU.
Sprint 8'de yalnizca "tutuyor gorunuyor" diyebildigimiz sey burada KONTROLLU BIR
KARSILASTIRMAYLA kanitlandi: ayni tarama, ayni kapsam, tek degisken.

## KALITE SUPURMESI - DALGA 4 (MOBIL + CAPRAZ CIHAZ) ve M10/M11-FIX

Kalite supurmesinin SON dalgasi. Olcum uc katmanda yapildi: statik kaynak taramasi,
390x844 / 360x800 emulasyonu ve **GERCEK CIHAZ TURU** (kullanicinin Android telefonu,
Opera, 384x638 kullanilabilir alan). Duzeltmeler kullanici karariyla ayni commit'te geldi.

### BULGU TABLOSU

| # | Sinif | Bulgu | Bloke | Durum |
|---|---|---|---|---|
| M10 | ISLEV-KIRAN | "Sepeti Onayla" mobilde HIC calismiyor - satin alma kapali | **EVET** | **KAPANDI** |
| M1 | ISLEV-KIRAN | Storefront API adresi `localhost:5000` SABIT gomulu | **EVET** | ACIK (dagitim kalemi) |
| M11 | ISLEV-KIRAN | Cerez bari odeme sayfasinin TEK eylem dugmesini ortuyor | **EVET** | **KAPANDI** |
| M3 | UX | Cerez bari alt navigasyonun dort ogesini ortuyor | hayir | **KAPANDI** (M11 ile ayni duzeltme) |
| M2 | UX | 376 px altinda header aksiyon kumesi tasiyor, tasan kisim kaydirilamiyor | hayir | ACIK |
| M4 | UX | Dokunma hedefleri 44x44 altinda (Kaldir 32x15, x 17x25, .cdot 15x15) | hayir | ACIK |
| M5 | UX | Giris/kayit formlarinda `autocomplete` yok ve `<form>` elementi HIC yok | hayir | ACIK (onemi DUSURULDU) |
| M6 | KOZMETIK | `safe-area-inset-top` hic kullanilmiyor (notch) | hayir | ACIK |
| M7 | KOZMETIK | `manifest.json theme_color=#111111` vs HTML meta `#2b2724` | hayir | ACIK |
| M8 | KOZMETIK | Service worker `VERSION = "2026-08-21-e3"` - E3'ten beri bumplanmadi | hayir | ACIK |
| M9 | KOZMETIK | Alt navigasyon etiketleri 9.5 px | hayir | ACIK |

### GERCEK CIHAZ TURUNUN GETIRDIGI (emulasyonun GOREMEDIGI) - DURUST KAYIT

- **M10 emulasyonda CURUK GORUNDU.** Sentetik `.click()` DOGRUDAN butona gonderilir; o an
  ripple ink YOKTUR. Gercek dokunusta ink DOM'a girer ve click hedefi O OLUR. Yani
  "gercek cihaz turu neden sart" sorusunun kaniti bu maddedir.
- **M2 gercek cihazda DOGRULANMADI** (ekran ~412 px; arama ikonu kirpik degil). 360/320
  olcumu EMULASYON kaniti olarak gecerli, gercek-cihaz kaniti YOK.
- **M4 kismen curudu**: sepette `-`/`+` gercek cihazda rahat kullanildi. Diger kucuk
  hedefler (Kaldir / x / .cdot) cihazda AYRICA denenmedi.
- **M5 kismen curudu**: telefon "Parola kaydedilsin mi?" onerdi ve klavyede "Git" tusu VAR.
  `<form>` yoklugunun etkisi tahmin edilenden AZ; `autocomplete` eksikligi duruyor ama
  onem derecesi DUSURULDU.
- **M3 gercek cihazda DOGRULANDI** (cerez bari alt navigasyonu ortuyor).
- **M6/M7 (PWA standalone) ve M8 (SW/offline) HENUZ TEST EDILMEDI.**

### M10 - "SEPETI ONAYLA" MOBILDE CALISMIYORDU (LAUNCH BLOKE) - KAPANDI

**GERCEK CIHAZ KANITI** (tani katmani, Android/Opera 384x638):

```
pointerdown -> button#checkoutBtn.btn            trusted=true  idEsit=true
touchstart  -> button#checkoutBtn.btn.rippling   trusted=true
pointerup   -> button#checkoutBtn.btn.rippling   trusted=true
touchend    -> button#checkoutBtn.btn.rippling   trusted=true
click       -> span.ripple-ink                   trusted=true  idEsit=false
   hash: #/ -> #/   *** DEGISMEDI ***
```

**KOK SEBEP ZINCIRI (her halkasi OLCULDU, tahmin YOK):**

1. `pointerdown` dinleyicisi (index.html) sinifi `btn|card-add|...` desenine uyan HER
   butonun ICINE `<span class="ripple-ink">` ekliyor.
2. `.ripple-ink{pointer-events:none}` kurali VARDI - **ama CEKMECE ICINDE ETKISIZDI.**
   CSSOM ile olculdu: cekmecedeki ink'in hesaplanan `pointer-events` degeri **`auto`**.
   Ezen kural: `.filter-side.open *,.cart.on *,.search.on *{pointer-events:auto}` -
   ozgullugu **(0,2,0)**, `.ripple-ink` ise **(0,1,0)**.
3. Ink isabet edilebilir oldugu icin gercek dokunusta click hedefi O oluyor.
4. `cartFoot` handler'i `e.target.id==='checkoutBtn'` diye KATI karsilastirma yapiyordu ->
   kosul dusuyor, `closeCart()` ve `location.hash='#/odeme'` HIC calismiyor.
5. Kullanicinin gordugu: butona basiliyor, hicbir sey olmuyor. **Mobilde satin alma KAPALI.**

**DEPO GENELI TARAMA (kullanici sarti) - kati hedef karsilastirmasi yapan TUM handler'lar:**

```
index.html'de 113 `.target` kullanimi var; 69'u ZATEN `closest` kullaniyor.
Kalanlar:  .target.id (4) · .target.classList (5) · .target.checked (3)
           .target.tagName (1) · .target.getAttribute (1) · .target === (8)
api-bridge.js ve admin.html: delege handler'larin TAMAMI zaten `closest` kullaniyor.
```

| Konum | Desen | Hedef | Ripple? | Sinif |
|---|---|---|---|---|
| index.html cartFoot | `e.target.id==='checkoutBtn'` | `<button class="btn">` | EVET | **BUGUN KIRIK** (cihazda olculdu) |
| index.html favFoot | `e.target.id==='favAll'` | `<button class="btn">` | EVET | **BUGUN KIRIK** (deterministik olculdu) |
| index.html giftChk | `e.target.id==='giftChk'` | `<input type=checkbox>`, `change` | hayir | SAGLAM (yapisal) |
| index.html cmpDiffChk | `e.target.id==='cmpDiffChk'` | `<input type=checkbox>`, `change` | hayir | SAGLAM (yapisal) |
| index.html cp-input x2 | `classList.contains('cp-input')` | `keydown`, odakli input | hayir | SAGLAM |
| index.html dotsEl | `e.target.getAttribute('data-i')` | `<button class="dot">` BOS | hayir | **KIRILGAN** (ikon eklenirse kirilir) |
| index.html:2913 | `e.target.tagName` | window error ayrimi | - | ILGISIZ |
| 7 modal/lightbox + api-bridge | `e.target===this/lb/stage/modal/m` | arka plan | - | **KASITLI - closest YASAK** |

`favAll` neden AYNI SINIF: kapsayicisi `<aside id="favs" class="cart on">` - yani favori
cekmecesi `.cart` sinifini YENIDEN KULLANIYOR ve `.cart.on *` kurali orada da gecerli.
Olculdu: ink hedefiyle gonderilen click handler'i DUSURDU, buton hedefiyle CALISTI.

**DUZELTME (iki katman, ASIL olan handler):**

1. **ASIL:** iki handler `e.target.closest('#checkoutBtn')` / `closest('#favAll')` kullaniyor.
   Yarin butona ikon/span konsa da kirilmaz. (Ayni handler'in kupon dallari ZATEN closest
   kullaniyordu - duzeltme dosyanin kendi idiyomuna hizalandi.)
2. **IKINCIL:** `.cart.on .ripple-ink,.search.on .ripple-ink,.filter-side.open .ripple-ink
   {pointer-events:none}` - yazarin OZGUN niyetini (0,3,0) ozgullugu ile o uc kapsamda geri
   verir. **Tek basina COZUM SAYILMADI:** 5. kontrolde olculdu ki bu satir yerinde olsa
   bile handler kati kalirsa alt-eleman hedefi eylemi yine dusuruyor.
   `.cart.on *` kuralina DOKUNULMADI - o kural cekmece kapaliyken etkilesimi kapatmak icin var.

**"Ozellestir" dugmesi ve `hidden`:** bkz. M11 - ayni dalgada bulundu.

### M11 + M3 - CEREZ BARI KENDI ALANINDA KALMIYORDU - KAPANDI

**OLCULEN ZARAR (cerez bari ACIK, CIKISLI kullanici, `elementFromPoint` ile):**

```
360x640  bar 199-640 h=441 (ekranin %69'u)  "Giris yap" 235-284 ORTULU <- div.ck-text
                                             alt navigasyon 0/4 ulasilabilir
384x638  bar 217-638 h=421                   "Giris yap" ORTULU <- span      alt nav 0/4
412x730  bar 326-730 h=404                   "Giris yap" ULASILIR            alt nav 0/4
```

Yani cikisli kullanici "Sepeti Onayla"dan `#/odeme`ye DUSUYOR (handler duzeldikten sonra),
sayfa dogru mesaji basiyor - ama sayfadaki **TEK eylem dugmesi cerez barinin altinda** ve
tiklanamiyor. Kullanicinin cikis yolu YOK.

**KOK SEBEP (olculdu):** `#ckPanel` HTML `hidden` ozniteligini TASIYOR ama hesaplanan
`display` degeri `flex` - cunku `.ck-panel{...display:flex}` yazar kurali, UA'nin
`[hidden]{display:none}` kuralini EZIYOR. Panel "kapali" isaretliyken bile ciziliyor ve
441 px'in **268 px'i** o panelden geliyor.
**YAN SONUC:** "Ozellestir" dugmesi (`cust.onclick=function(){pan.hidden=!pan.hidden}`)
GORSEL OLARAK OLU - basiliyor, hicbir sey degismiyor.
**IDIOM KANITI:** ayni dosyada `.cmdk[hidden]`, `.a11y-panel[hidden]`, `.lb-nav[hidden]`
kurallari ZATEN bu korumaya sahip; yalniz `.ck-panel` unutulmus.

**DUZELTME - TEK MERKEZ, IKI KALEM (M3 ve M11 birlikte kapanir):**

1. `.ck-panel[hidden]{display:none}` -> bar kendi KOMPAKT boyutuna doner (441 -> 139/158 px)
   ve "Ozellestir" dugmesi GERCEKTEN calisir. **M11 bunu kapatir.**
2. `@media(max-width:768px){.cookie-bar{bottom:calc(var(--mnav-h,63px) + var(--kb,0px))}}` ->
   bar alt navigasyonun USTUNE oturur. `--mnav-h` navigasyonun OLCULEN yuksekliginden JS ile
   yazilir (`--kb` kalibinin aynisi). **M3 bunu kapatir.**
   `.cookie-bar.gone` transform'u da ofseti hesaba katar - yoksa bar kapanirken navigasyonun
   uzerinde GORUNUR kalirdi.

**IKISI DE GEREKLI - 5. kontrolde ayristirildi:** `[hidden]` guard'i kaldirilinca "Giris yap"
ULASILAMAZ oldu ama alt navigasyon 4/4 ULASILIR KALDI. Yani bir kalem digerinin yerine gecmiyor.

**SONRA (uc viewport'ta da, cerez bari ACIK):**

```
360x640  bar 419-577 h=159   GirisYap GECTI · altNav 4/4 GECTI · SepetiOnayla GECTI
384x638  bar 436-575 h=139   GirisYap GECTI · altNav 4/4 GECTI · SepetiOnayla GECTI
412x730  bar 528-667 h=139   GirisYap GECTI · altNav 4/4 GECTI · SepetiOnayla GECTI
uc viewport'ta da: inkPointerEvents=none · alt-eleman hedefiyle tiklama -> hash #/odeme
```

### YAN KALEM - "Transition was aborted because of invalid state" = GURULTU

Telefon turunda gorulen `*** PROMISE HATA` satiri olculdu. **Zarar YOK, YENI BULGU DA DEGIL:**
`index.html:2914`'te uygulamanin KENDI `unhandledrejection` dinleyicisi tam bu mesaji
ACIKCA suzuyor (`if(/abort|invalid state|transition was aborted/i.test(_m))return;`) ve hata
raporlamasina GONDERMIYOR. Iki hizli `hashchange` ust uste geldiginde ilk View Transition
iptal oluyor; DOM guncellemesi TAMAMLANIYOR, yalniz animasyon dusuyor.
Satiri gorunur kilan sey GECICI TANI KATMANIDIR - o, uygulamanin suzgecine sahip degil.
**DUZELTME YAPILMADI** (bir sey duzeltmek gerekmiyor); tek kalan kalem tarayici konsoluna
basilan kozmetik satir. Kapatmak istenirse `withVT`/`openProductVT` icinde `vt.ready`
promise'ine bos bir `.catch` baglamak yeter - **karar kullanicinin.**

### CIKISLI KULLANICIDA SEPET CEKMECESININ KAPANMASI (kullanicinin sordugu gerekce)

Bugunku davranis DOGRU tarafta ve DEGISTIRILMEDI. Gerekce: `#/odeme` bir SAYFA, cekmece ise
onun UZERINDE duran bir katman; acik birakilsaydi kullanici odeme panelini goremeden ayni
sepete bakmaya devam ederdi ve "bir sey olmadi" hissi ARTARDI. Baglam kaybi da gercek degil -
sepet icerigi KORUNUYOR (E2'de pinli) ve odeme sayfasi ozeti tekrar gosteriyor.
**GERCEK KUSUR CEKMECE DEGILDI**, hedefteki sayfanin tek eyleminin ortulu olmasiydi (M11) -
o kapandi. Yine de "cikisli kullaniciyi odeme sayfasina dusurmek yerine dogrudan giris
katmanini acmak" ayri ve savunulabilir bir URUN karari; **degisiklik karari kullanicinin.**

### PINLER (`FrontendDokunmaHedefiTests`, 7)

- `SepetOnayHandleri_HEDEFI_closest_ILE_Cozer_ALT_ELEMAN_DUSURMEZ` (vakum kirici: handler'in
  hala bagli oldugu once dogrulanir)
- `FavorileriSepeteEkle_Handleri_HEDEFI_closest_ILE_Cozer`
- `HICBIR_YENI_EYLEM_HANDLERI_target_id_ILE_KATI_KARSILASTIRMA_YAPMAZ` - SINIF DUZEYI tarama;
  izinli set TAM OLARAK `{giftChk, cmpDiffChk}` (ikisi de `change`-olayli checkbox, yapisal
  olarak alt eleman TASIYAMAZ). Cift-anlam kirici: izinli kullanimlarin GERCEKTEN durdugu da
  assert edilir, yoksa liste bosalinca tarama vakuma duserdi. `.target.matches(` = 0 pinli.
- `ARKA_PLAN_KAPATMA_Handlerlari_KIMLIK_KARSILASTIRMASINI_KORUR` - CIFT-ANLAM KIRICI:
  "hepsini closest yap" YANLIS duzeltmedir; 7 modal/lightbox + api-bridge kalibi kimlik
  karsilastirmasina DAYANIR (closest'a cevrilseydi modallar iclerine tiklandiginda kapanirdi).
- `RippleInk_CEKMECE_ARAMA_FILTRE_ICINDE_de_ISABET_HEDEFI_OLMAZ` (vakum kirici: EZEN kuralin
  hala yururlukte oldugu once dogrulanir)
- `CerezPaneli_hidden_OZNITELIGINE_SAYGI_Duyar` (vakum kirici: `display:flex`in hala orada
  oldugu - yani guard'in GEREKLI oldugu - dogrulanir)
- `CerezBari_MOBILDE_ALT_NAVIGASYONUN_USTUNE_Oturur` (cift-anlam kirici: degisken "gizliyse 0"
  yazmali, aksi halde masaustunde bar gerekcesiz kayardi)

**KIRILAN PIN YOK.**

### PIN MEKANIZMASININ SINIRI (DURUST KAYIT - KARAR BEKLIYOR)

Depoda **JS/DOM test kosucusu YOK** (olculdu: `Divisima.IntegrationTests.csproj`'de
AngleSharp / Jint / ClearScript / Playwright / Selenium **yok**). Bu yuzden TARAYICI
SEMANTIGI - hit-test, CSS ozgullugu, `elementFromPoint` - CI'da pinlenemiyor. Yukaridaki 7
pin KAYNAK SOZLESMESINI tutar ("handler hedefi closest ile cozer", "bar kendi alanindadir"),
davranisi degil. Bosluk gizlenmedi, IKI kanalla telafi edildi:

1. **`frontend/test/mobil-erisilebilirlik.js`** - depoya konan TEKRARLANABILIR olcum betigi.
   `await divisimaMobilKontrol()` uc kontrolu + alt-eleman hedefi kontrolunu kosar ve
   GECTI/KALDI doner. Sepet bossa ya da cerez bari kapaliysa **yanlis yesil vermez**, uyarir.
   CSP `unsafe-eval`e izin vermedigi icin `<script src>` ile yuklenir (olculdu).
2. Bu bolumdeki olculen sayilar.

Kalici cozum (bir JS test kosucusu eklemek) YENI BIR BAGIMLILIKTIR ve `dependency-scan`
kapsamina girer - **ayri bir karar, kullanicinin.**

### DIS KONTROLU + 5. KONTROL

5 assert ters cevrildi (BES AYRI test) -> **5 AYRI ISIMLI KIRMIZI** (geri alindi).

5. kontrol, IKI uretim mutasyonu:
- **M1** (handler eski kati karsilastirmaya donduruldu): TARAYICIDA olculdu ->
  alt-eleman (ripple ink) hedefiyle click **hash `#/` DEGISMEDI**, buton hedefiyle
  `#/odeme` - telefonda olculen tablonun BIREBIR aynisi. Ustelik ikincil CSS savunmasi
  YERINDEYKEN: yani handler'in ASIL duzeltme oldugu kanitlandi. .NET tarafinda TAM 2 pin
  kirmizi (dogrudan pin + sinif duzeyi tarama), diger 5 YESIL - mutasyon lokalize.
- **M2** (`.ck-panel[hidden]` guard'i kaldirildi): bar 441 px'e geri sisti,
  "Giris yap" **ULASILAMAZ** (`ustundeki: button#ckCustom.ck-cust`) - olculen once-durum.
  Alt navigasyon 4/4 ULASILIR KALDI (ofset duzeltmesi yerinde) - iki kalemin ayri isler
  oldugunun kaniti. .NET tarafinda TAM 1 pin kirmizi, 6 YESIL.
Ikisi de geri alindi.

### YEREL DOGRULAMA

252/252 `Category=Sql` · tam suitte **389 basarili / 392** (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`) · Release 0 hata · whitespace + style TEMIZ (exit 0).

### SURECTE YASANAN (kayit - iki ders)

- **`</script>` dizgesi bir JS YORUMUNUN icine yazildi ve HTML ayristiricisi script blogunu
  ORADA KAPATTI.** Blogun kalani ham metne dondu; belirti sinsiydi - `--mnav-h` yazilmadi ve
  `.mob-nav` yuksekligi 63 yerine **245** olculdu. Fark edildi, yorum metni degistirildi,
  onarim dogrulandi (tum global fonksiyonlar mevcut, `cart` yine Map, govde metni normal).
  **DERS: index.html icindeki JS yorumlarina `</script>` YAZILMAZ.**
- **`--no-build` ile kosulan test DEGISTIRILEN kodu dogrulamaz** (CLAUDE.md'de zaten yazili
  olan tuzak bir kez daha yasandi): dis kontrolu geri alindiktan sonra yeniden derlemeden
  kosulan mutasyon turu, bir ONCEKI kosumun 5 kirmizisini tekrarladi. Derleyip tekrarlandi.
- Olcum betigimde `var cart = ...` yazarak sayfanin GLOBAL `cart` Map'ini ezdim; belirti
  `cart.get is not a function` oldu ve iki olcum turu bosa gitti. Ayrica `navigate` yalniz
  hash'i degistirdiginde sayfa YENIDEN YUKLENMEZ - clobber reload'lari asti.
  **DERS: sayfa baglaminda calisan olcum betiginde uygulama global adlari KULLANILMAZ.**

## DALGA-4-FIX PUSH RAPORU (77c0308) - HER IKI WORKFLOW TAMAMEN YESIL

**Push `d40be2f..77c0308`** (tek commit -> tek push). Adim bazinda + annotation
duzeyinde dogrulandi.

### CI - Build & Test (run 32644536553) - TAMAMEN YESIL

`format-check`: **iki ZORUNLU adim** (`Bicimlendirme dogrulama - whitespace` +
`- style`) SUCCESS.
`build-and-test`: `.NET 8 kurulumu` · `Bagimliliklari geri yukle` ·
`Derle (Release, uyarilar gorunur)` · `SQL Server hazir mi (service container)` ·
**`SQL gerektiren testler (ATLANMAMALI)`** · **`Testler + coverage`** ·
**`Coverage raporunu yukle`** hepsi SUCCESS; `TESHIS` skipped.

### Security CI (run 32644536471) - TAMAMEN YESIL

`tests`: `Is mantigi guvenlik simulasyonu` · `SQL Server hazir mi` ·
**`Entegrasyon testleri`** SUCCESS, `TESHIS` skipped.
`codeql`: init / Build / analyze SUCCESS.
**`secret-scan` -> `Gitleaks (secret taramasi)` SUCCESS** (adim sonucundan okundu -
bolum 7 kurali; "Leaks detected" satiri YOK).
`dependency-scan`: `Restore` · `Acik bagimlilik taramasi - RAPOR` ·
**`Acik bagimlilik KAPISI (uretim projeleri)`** · `Kullanimdan kalkmis paket kontrolu`
hepsi SUCCESS.

### ANNOTATION DURUMU: ALTI JOB'IN HICBIRINDE `failure` SEVIYESI YOK

Tek tek tarandi. Bulunan her annotation `warning` seviyesinde ve UCU DE ONCEDEN VARDI:
- `Node.js 20 is deprecated` (actions/checkout@v4, setup-dotnet@v4, upload-artifact@v4,
  gitleaks-action@v2) - GitHub kosucusunun kendi uyarisi, bizim kodumuz degil.
- `CodeQL Action v3 will be deprecated in December 2026` - ayni sinif.
- `Cannot convert null literal to non-nullable reference type` -
  `Divisima.Core/DataAccess/IEntityRepository.cs` ve `EfEntityRepositoryBase.cs`
  (Dalga 1'de "eleneneler" arasinda orneklenmisti: guard'li ama derleyicinin
  kanitlayamadigi desen).

**Bu commit YENI bir uyari uretmedi.** Ozellikle: yeni eklenen
`FrontendDokunmaHedefiTests.cs` ve `frontend/test/mobil-erisilebilirlik.js`
`secret-scan`, `format-check` ve `dependency-scan` kapilarinin UCUNDEN DE temiz gecti.

### KAYDA DEGER

- **Yeni pin dosyasi DEPO KOKUNU bulup `frontend/index.html`'i okuyabildi** - yani
  `AppContext.BaseDirectory`'den yukari yurume CI kosucusunun dizin duzeninde de
  calisiyor. Bu, yerelde yesil olup CI'da sessizce atlanabilecek bir tasarimdi;
  `KokDizin` bulunamazsa `InvalidOperationException` firlatiyor (sessiz skip YOK),
  dolayisiyla `Testler + coverage` adiminin SUCCESS olmasi pinlerin GERCEKTEN
  kostugunun kanitidir.
- Yereldeki uc Docker'li `OrderEndpointTests` kirmizisi CI'da YOK (beklendigi gibi -
  kosucuda Docker var).
- Yerelde bir kez gorulen adi belli flake (`RefreshCookieContractTests.
  Cerez_Secure_HER_ORTAMDA_ISARETLI_OrtamGuardi_YOK`) bu kosumda TEKRAR ETMEDI.

## DALGA 4 SAHA TURU - GERCEK CIHAZ KANITI (M10 / M11 / M3 KAPANIS DOGRULAMASI)

Duzeltme sonrasi tur kullanicinin kendi telefonunda kosuldu (Android/Opera, 384x694).
**Uc kalem de CIHAZDA dogrulandi** - emulasyon degil, gercek dokunus:

```
click -> button#checkoutBtn.btn.rippling   idEsit=TRUE      (once: span.ripple-ink, idEsit=false)
hash  -> #/odeme  (gecis OK)               (once: #/ -> #/ DEGISMEDI)
cekmece: on=false  sol=378                 (kapandi)
[e] cerez bari 0-0                         (ORTME YOK)
alt navigasyon 4/4 gorunur · "Giris yap" ULASILABILIR
```

Ardindan **odeme sayfasi acildi ve IYZICO KART FORMU MOBILDE YUKLENDI** (kart no /
ay-yil / CVC / 3DS + "2.499,50 TL ODE"). **Mobil satin alma akisi UCTAN UCA CALISIYOR.**

**PWA (M6/M7) OLCULEMEDI - DURUST KAYIT:** "Ana ekrana ekle" CALISTI ve ikon ana ekrana
dustu, ancak kisayol standalone modda ACMADIGI icin `safe-area-inset-top` (M6) ve
`theme_color` sicramasi (M7) GORULEMEDI. Ikisi de **"test edilmedi, bloke etmez"**
olarak kapatildi - olculmemis bir sey "yesil" diye yazilmiyor.

**M8 / offline (KULLANICI KARARI):** offline deneyimi oncelik degil, test ATLANDI.
Service worker `VERSION` bump'i **DAGITIM KURALI** olarak kaliyor (asagidaki checklist
maddesinde); offline davranisi icin AYRI is ACILMADI.

## DALGA-4-FIX-2 (M1) - STOREFRONT API ORIGIN'I TEK KAYNAKTAN

Launch'i bloke eden SON teknik kalem. Kapsam kullanici karariyla cizildi.

### OLCULEN ZARAR

`http://localhost:5000` **BES ayri yerde** SABIT gomuluydu:

```
index.html:5      CSP meta      (img-src + connect-src + form-action)
index.html:3076   window.DIVISIMA_API_BASE = "http://localhost:5000"
api-bridge.js:27  window.DIVISIMA_API_BASE || "http://localhost:5000"   (sessiz yedek)
admin.html:5      CSP meta      (img-src + connect-src)
admin.html:95     localStorage(...) || "http://localhost:5000"
```

Depo neyse o yayina gidiyordu: LAN adresinden acilinca istekler kullanicinin KENDI
makinesine gidiyor, tarayici engelliyor (`ERR_BLOCKED_BY_CLIENT`) ve **katalog BOS**
geliyordu. Ustelik API tabani ile CSP origin'leri **ELLE** senkron tutuluyordu.

### TASARIM: OLCUMLE SECILDI

Iki aday vardi: (a) calisma ani yapilandirma, (b) dagitim adiminda yerine koyma.

**(a) TEK BASINA YETMEZ - TARAYICIDA OLCULDU.** CSP `<meta>` belge AYRISTIRILIRKEN
uygulanir; calisma aninda DAHA GENIS bir CSP meta'si eklemek politikayi GEVSETMEZ.
Denendi:

```
1) mevcut politika altinda  fetch(LAN/health)  -> ENGEL
2) daha GENIS bir CSP meta'si JS ile eklendi
3) ayni fetch tekrar         -> YINE ENGEL
   securitypolicyviolation: connect-src -> http://192.168.x.x:5000/health
```

Yani API tabani runtime'da ayarlanabilirdi ama **UC CSP DIREKTIFI ayarlanamazdi** - sart
ise "hepsi TEK KAYNAKTAN turesin" idi. **(a) elendi.**

**(b) TEK BASINA da yetersizdi:** bugunku elle senkron zaten "dagitim ani"ydi; kusur
mekanizma degil, **DOGRULANMAMIS** olmasiydi.

**SECILEN = (b) + CALISMA ANI TUTARLILIK GUARD'I.** Origin dosyaya dagitim aninda
yazilir; calisma aninda yalnizca DOGRULANIR.

### YAPILAN

**1) TEK KAYNAK:** `<meta name="divisima-api-origin" content="...">` (index.html ve
admin.html). API tabani BURADAN turer - `window.DIVISIMA_API_BASE=origin`.
`api-bridge.js`'teki sessiz `|| "http://localhost:5000"` yedegi KALDIRILDI: bos taban
GORUNUR sekilde bozuktur, sessiz yanlis taban DEGILDIR (sart ii).
Admin'in `localStorage("divisima_api_base")` override'i KORUNDU (operatorun paneli baska
bir ortama yoneltmesi mesru), ama ardindaki sabit yedek kalkti.

**2) CALISMA ANI GUARD'I:** sayfa acilirken beyan edilen origin CSP'nin `img-src`,
`connect-src`, `form-action` direktiflerinde ARANIR; eksikse konsola ERROR + **ekranda
kirmizi uyari** basilir ve ne yapilmasi gerektigi (`ops/set-api-origin.sh`) SOYLENIR.
API storefront ile AYNI origin'de servis edilirse `'self'` kapsami kabul edilir - yoksa
mesru bir dagitimda YANLIS ALARM verirdi.

**3) DAGITIM MEKANIZMASI:** `ops/set-api-origin.sh <origin>` - TEK girdiden hem meta hem
UC CSP direktifi yazilir, sonra DOGRULANIR; eski origin bir yerde kalirsa HATA verir.
`--verify` modu yalnizca dogrular ve tutarsizlikta **exit 1** doner.

**4) CHECKLIST:** `ops/deployment-checklist.md` -> "Frontend origin'i - HER YAYINDA":
betik kosuldu mu · `Iyzico:CallbackUrl` ayni origin mi (form-action senkronu) · SW
`VERSION` bump'i · yayin sonrasi katalog dolu mu ve `[DIVISIMA YAPILANDIRMA]` satiri yok mu.

**SENKRON KURALI KORUNDU:** `form-action` <-> `Iyzico:CallbackUrl` esitligi hem betigin
basindaki yorumda, hem meta'nin yanindaki yorumda, hem checklist'te yazili - callback
POST'u TARAYICIDAN gelir, uyusmazsa odeme sonucu SESSIZCE kaybolur (E2b'de yasandi).

### OLCUMLER (once -> sonra)

```
YEREL VARSAYILAN (sart i - ek adim GEREKMEZ)
  api tabani = http://localhost:5000 · guard SESSIZ · katalog 2 urun ("E4a Test Urun")

FARKLI ORIGIN'E DAGITIM (gercek mekanizma, betikle)
  ops/set-api-origin.sh http://127.0.0.1:5000  -> 7/7 OK
  sayfa http://localhost:5173'ten servis edildi
  api tabani = http://127.0.0.1:5000 · guard SESSIZ · katalog 2 urun
  AG: GET http://127.0.0.1:5000/api/category/getlist -> 200
      POST http://127.0.0.1:5000/api/product/filter  -> 200
      OPTIONS .../product/filter                      -> 204
```

**OLCUM ORTAMI SINIRI (durust kayit):** ayni dogrulama LAN adresiyle de denendi; sayfa ve
varliklari 200 geldi, `window.DIVISIMA_API_BASE` LAN degerini tasidi, guard SESSIZ kaldi -
**ama API istekleri tarayici korumali alani tarafindan `ERR_BLOCKED_BY_CLIENT` ile
engellendi.** Bu bir URUN kusuru DEGIL, olcum ortaminin ozel-ag kisitidir; bu yuzden
"farkli origin" kaniti LAN yerine `127.0.0.1` ile uretildi (localhost'tan FARKLI bir
origin, ama korumali alanin erisebildigi bir adres). Kullanicinin telefonu LAN uzerinden
zaten calisiyordu.

### PINLER (`ApiOriginTekKaynakTests`, 6)

- `API_TABANI_TEK_KAYNAKTAN_Turer_IKINCI_LITERAL_YOK` (vakum kirici: once TEK KAYNAGIN
  var ve dolu oldugu dogrulanir)
- `CSP_UC_DIREKTIF_BEYAN_EDILEN_ORIGINI_Tasir` - **elle senkronun CI'daki karsiligi**;
  CSP GERCEKTEN ayristirilir (split+trim, uretimdeki guard ile ayni yontem) ve uc
  direktifin de beyan edilen origin'i tasidigi hesaplanir
- `ADMIN_PANELI_de_AYNI_TEK_KAYNAK_SOZLESMESINI_Tasir` (iki yuzeyin AYNI origin'i beyan
  ettigi de assert edilir - ayrisirlarsa dagitim yine elle senkrona donerdi)
- `TEK_GIRDIYLE_DEGISTIRME_TUM_YERLERI_KAPSAR_ESKI_ORIGIN_KALMAZ` - **davranis pini**:
  betigin yaptigi is bellekte simule edilir; cift-anlam kirici olarak eski origin'in
  HICBIR dosyada kalmadigi, vakum kirici olarak degistirmenin GERCEKTEN bir sey yaptigi
  assert edilir
- `CALISMA_ANI_GUARD_I_UC_DIREKTIFI_de_Kontrol_Eder_ve_GURULTULUDUR` (uyari EKRANDA
  gorunmeli - yalniz konsol son kullanicida SESSIZDIR; ayni-origin `'self'` istisnasi da pinli)
- `DAGITIM_BETIGI_ve_CHECKLIST_MADDESI_VAR`

**KIRILAN PIN YOK.**

### DIS KONTROLU + 5. KONTROL

DIS: 5 assert ters (BES AYRI test) -> **5 AYRI ISIMLI KIRMIZI**. Geri alindi.

5. KONTROL, IKI mutasyon:
- **A (tarayici):** `connect-src`ten origin SILINDI (elle senkronun unutuldugu durumun ta
  kendisi) -> `--verify` **exit 1** ve YALNIZ `connect-src`i EKSIK gosterdi (digerleri OK -
  guard'in isabetli oldugunun kaniti); tarayicida guard bannerı yalnizca `connect-src`i
  adiyla bildirdi ve **katalog 0 URUN** oldu - M1'in olculen belirtisi.
- **B (.NET):** `api-bridge.js`'e sessiz `|| "http://localhost:5000"` yedegi GERI KONDU ve
  tek kaynak meta'si KALDIRILDI -> **4 pin kirmizi**, 2 yesil (lokalize). Tarayicida:
  `window.DIVISIMA_API_BASE` **undefined**, guard "meta etiketi yok" diye BAGIRDI - yani
  sessiz yedek geri gelse bile guard bagimsiz bir emniyet agi olarak calisiyor.
Ikisi de geri alindi.

### YEREL DOGRULAMA

252/252 `Category=Sql` · tam suitte **395 basarili / 398** (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`) · Release 0 hata · whitespace + style TEMIZ (exit 0).

### TEMIZLIK (bu dalgada yapildi)

- LAN shim KALDIRILDI: scratchpad'teki sunucu artik CSP/API tabani YENIDEN YAZMIYOR ve
  tani katmani ENJEKTE ETMIYOR - duz bir statik dosya sunucusu (uretimdeki bir statik
  host gibi). **Shim'i birakmak, gercek dagitim mekanizmasini TEST ETMEMEK olurdu.**
- Tani katmani (`tani.js`) kaldirildi; saha turu tamamlandi.
- Gecici sunucular DURDURULDU (port 5000 ve 5173 bos).
- `Iyzico:CallbackUrl` user-secrets'ta `http://localhost:5000/api/payment/callback`
  degerine dondu (diger secret'lar OKUNMADI/BASILMADI).
- Depo tarandi: `tani.js` / `tani yuklendi` / `agsunucu` / `m10tani` -> **0 dosya**.
  `trycloudflare` yalniz CLAUDE.md'nin TARIHSEL kaydinda ve gitignore'lu log dosyalarinda.
  Olcum kanitindaki LAN IP'si depo PUBLIC oldugu icin `192.168.x.x` olarak genellestirildi
  (kanit degeri "farkli bir origin engellendi" cumlesindedir, adresin kendisinde degil).
- **KALAN (bilincli):** `frontend/test/mobil-erisilebilirlik.js` DEPODA KALIYOR - o bir
  tani artigi degil, pin mekanizmasinin bilincli telafisi (Dalga 4 bolumu).

### SURECTE YASANAN (kayit - iki ders)

- **`set -o pipefail` + eslesmesi olmayan `grep` betigi YARIDA KESTI.** `ops/set-api-origin.sh`
  ilk kosumda dosyalari degistirdikten SONRA, `api-bridge.js`'te sifir gecis oldugu icin
  `grep -o` 1 dondu ve boru hatti tumuyle basarisiz sayildi; dogrulama ve "eski origin
  kalmadi" kontrolu HIC kosmadi. CLAUDE.md bolum 7'deki "CI script'leri CALISTIRILARAK
  dogrulanir" kurali bu betik icin de gecerliydi ve tam da bunu yakaladi. Duzeltildi
  (sifir gecis acikca yutuluyor) ve gerekcesi koda yazildi.
- **TERS BOLU KACISI SESSIZCE KAYBOLDU.** Guard'a yazilan `'\\s'` dosyaya `'\s'` olarak
  indi; JS'te `'\s'` duz `s` demektir, yani regex HIC eslesmedi ve guard UC DIREKTIFI DE
  "eksik" sanip **YANLIS ALARM** verdi. Fark edildi (tarayicida CSSOM ile olculdu: regex
  aslinda dogru eslesiyordu), ve regex TUMDEN kaldirildi - CSP artik duz `split(';')+trim`
  ile ayristiriliyor (kacis semantigi YOK, sessizce bozulamaz). Ayni yontem .NET pininde
  de kullanildi. **DERS: uretim koduna gomulen regex'lerde ters bolu kacisi, dosyaya
  yazim zincirinde kaybolabilir - kacissiz bir cozum varsa o tercih edilir.**
  NOT: guard'in bozulma yonu FAIL-LOUD idi - yanlis alarm verdi, sessiz kalmadi.

## DALGA-4-FIX-2 PUSH RAPORU (dbaa763) - HER IKI WORKFLOW TAMAMEN YESIL

**Push `77c0308..dbaa763`** (tek commit -> tek push). Adim bazinda + annotation
duzeyinde dogrulandi.

### CI - Build & Test (run 32648639604) - TAMAMEN YESIL
`format-check`: **iki ZORUNLU adim** (`Bicimlendirme dogrulama - whitespace` + `- style`)
SUCCESS.
`build-and-test`: `.NET 8 kurulumu` · `Bagimliliklari geri yukle` ·
`Derle (Release, uyarilar gorunur)` · `SQL Server hazir mi (service container)` ·
**`SQL gerektiren testler (ATLANMAMALI)`** · **`Testler + coverage`** ·
**`Coverage raporunu yukle`** hepsi SUCCESS; `TESHIS` skipped.

### Security CI (run 32648639646) - TAMAMEN YESIL
`tests`: `Is mantigi guvenlik simulasyonu` · `SQL Server hazir mi` ·
**`Entegrasyon testleri`** SUCCESS, `TESHIS` skipped.
`codeql`: init / Build / analyze SUCCESS.
**`secret-scan` -> `Gitleaks (secret taramasi)` SUCCESS** (bolum 7 kurali geregi ADIM
SONUCUNDAN okundu; "Leaks detected" satiri YOK).
`dependency-scan`: `Restore` · `Acik bagimlilik taramasi - RAPOR` ·
**`Acik bagimlilik KAPISI (uretim projeleri)`** · `Kullanimdan kalkmis paket kontrolu`
hepsi SUCCESS.

### ANNOTATION: ALTI JOB'IN HICBIRINDE `failure` SEVIYESI YOK
Bulunan her annotation `warning` ve UCU DE ONCEDEN VARDI: Node.js 20 deprecation,
CodeQL Action v3 deprecation, ve `Divisima.Core/DataAccess/*` nullable uyarilari.
**Bu commit YENI uyari uretmedi**; yeni `ops/set-api-origin.sh` ve
`ApiOriginTekKaynakTests.cs` dosyalari `secret-scan` / `format-check` /
`dependency-scan` kapilarinin UCUNDEN DE temiz gecti.

---

