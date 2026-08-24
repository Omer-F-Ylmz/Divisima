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
- **DEPO DA PUBLIC: KANIT DEPOYA GIRMEDEN MASKELENIR (bir kez bedeli odendi, Sprint 8).**
  Olcum kaniti olarak yapistirilan ham govdeler (webhook payload'lari, saglayici yanitlari,
  istek/yanit dokumleri) **jeton ve kimlik tasir**. Sprint 8 push'unda gercek bir Iyzico
  odeme jetonu (`"token":"<tam GUID>"`) CLAUDE.md'ye BIREBIR yapistirildi ve `secret-scan`
  (Gitleaks) job'ini KIRDI. KURAL: depoya (kod, yorum, CLAUDE.md, commit mesaji) yazilan her
  ornek govdede jeton/kimlik **ilk 8 karaktere kirpilir** (`76ee5138-...`). Kanit degeri
  kaybolmaz - "webhook jetonu `payments.token` ile ESLESIYOR" cumlesi tam degeri gerektirmez.
  Ayni kural `paymentConversationId`, `iyziReferenceCode`, oturum/refresh jetonlari ve
  imza degerleri icin de gecerlidir.
- **MASKELEME URETIM NOKTASINDA YAPILIR, RAPOR ANINDA DEGIL (KALICI - UC KEZ KIRILDI).**
  Yukaridaki kirpma kurali insan disiplinine birakildi ve **UC KEZ** kirildi; ucunde de bedeli
  KIRMIZI BIR RUN oldu: Sprint 8 (Iyzico odeme jetonu CLAUDE.md'ye yapistirildi),
  GUVENLIK-FIX-2 (test sifre literalleri), LAUNCH-FIX Dalga A (ikisi birden). Ortak nokta her
  seferinde **uretim kodu degil, KANIT YAZMA ANIYDI** - ustelik Dalga A'da ayni blokta bir
  sonraki satirda jeton KIRPILMISTI, yani kural biliniyordu ve tutarsiz uygulandi.
  **Bu yuzden kirpma, kanitin URETILDIGI yere tasindi:**
  - `Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(...)` - ham govdeyi ciktiya/loga koyan
    her yer once buradan gecer. Bagli yerler: `TestAuthHelper` (register/verify/**login**
    kosuyor, basarisiz login yaniti JWT tasir), assert mesajina govde koyan tum test siteleri,
    ve `NetgsmSmsService`'in saglayici yaniti logu.
  - Depo disindaki olcum araclari da **yazma aninda** maskeler (SMTP yakalayicisi `.eml`'i
    kirpilmis yazar), boylece rapora ne yapistirilirsa yapistirilsin jeton ciplak halde
    **ELDE OLMAZ**.
  **OLCUT ENTROPI DEGIL, KARAKTER SINIFI - VERIDEN CIKARILDI:** `uzunluk >= 16 + en az bir
  rakam + en az bir kucuk harf`. Gerekce olculdu: `Guid("N")` entropisi **3.480** ile
  gitleaks'in 3.5 esiginin ALTINDA kalir ama maskelenmelidir; buna karsilik
  `paymentTransactionId` **3.746** ile esigin USTUNDEDIR ama GORUNMELIDIR. Tam tablo ve
  gerekce `KanitMaskesi`'nin basinda; davranis `KanitMaskesiTests` ile pinli.
  **TESHIS DEGERI KORUNUR:** baglantida origin ve yol gorunur kalir
  (`http://localhost:5173/#/dogrula/RcR276Ak…`), yalniz jetonun kendisi gider.
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

## 6c. KIMLIK vs GORUNTU - KULTURLU CASING KURALI (KALICI)

**Kimlik/makine dizgesinde KULTURLU casing ve karsilastirma YASAK. Kultur YALNIZ
insan-gorunur bicimlendirmede kullanilir.**

Uygulama `tr-TR`'ye pinli (bolum 6, madde 13) ve Turkcede **`i` ile `I` ayni harf DEGIL** -
cift'ler `I <-> ı` (U+0131) ve `İ` (U+0130) `<-> i`. Veritabani collation'i da `Turkish_CI_AS`.
Olculdu: `'irem' = 'IREM'` -> **FARKLI**. Bu yuzden kimlik dizgesinde `.ToLower()` /
`.ToUpper()` kullanmak, ayni degerin iki yazimindan **iki farkli anahtar** uretir.

| Tur | Ornek | Kural |
|---|---|---|
| **KIMLIK** | e-posta, kupon/hediye kodu, URL yolu, MIME tipi, HTTP baslik semasi, saglayici durum kodu, jeton | `ToLowerInvariant` / `ToUpperInvariant` / `StringComparison.Ordinal(IgnoreCase)` |
| **GORUNTU** | fatura tutari, tarih, urun adi/marka aramasi, ad-soyad | Kultur AYNEN (madde 13 pinleri gecerli) |

- **ELLE YAZILAN kodlarda invariant casing YETMEZ.** Turkce klavyede buyuk harf `i` -> `İ`
  ve invariant casing bunu ASCII `I`ya cevirmez. `Divisima.Core.Utilities.Text.KimlikDizgesi.KanonikKod`
  once Turkce'ye ozgu harfleri katlar, sonra invariant buyultur. **E-POSTAYA UYGULANMAZ** -
  gerekce o dosyanin basinda.
- **SQL tarafinda `LOWER()`/`UPPER()` sarmalayicisi KULLANILMAZ.** Veritabani collation'ini
  (Turkish) kullanir, invariant normalize edilmis degerle yeniden ayrisir; ayrica indeksi
  kullanilamaz hale getirir. Saklanan deger zaten kanonik oldugu icin **duz esitlik** dogrudur.
- **CANLI BEDELI ODENDI:** ayni e-postanin iki yazimi **IKI HESAP** acti (olculdu) ve kullanici
  ancak kayitta yazdigi harf duzeniyle giris yapabiliyordu. Ayrica buyuk harfli URL, auth
  rate-limit kovasindan **kaciyordu**.
- **TESHIS SORGUSU DA COLLATION'A TABIDIR.** `Turkish_CI_AS` altinda `LIKE N'%ı%'` ifadesi
  `i` iceren HER satiri de yakalar. Hasar arayan sorgular **`COLLATE Latin1_General_BIN2`**
  ile yazilir (bu dalgada birebir yasandi: ilk teshis 11 satiri hasarli sandi, gercekte 1'di).
- **ORTAM SARTI PINLI:** testlerin kostugu veritabani `Turkish_CI_AS` olmalidir
  (`CollationMetaPinTests`). Latin1 bir kurulumda bu sinif hatalar **GORUNMEZ** ve pinler
  yalanci yesil verir; iki workflow'a `MSSQL_COLLATION` bu yuzden eklendi.

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
  CI'da TEKRAR ETMEDI.
- **SIPARIS #33'UN ENVANTER SAPMASI GIDERILDI** (kullanici karari: secenek B). Duzeltilmis
  uretim yolu bir kez kosturuldu: stok 10 -> 8, rezervasyon Expired -> Confirmed, denetim izli
  TEK hareket satiri. Ikinci cagri NO-OP (canli teyit). Elle SQL YOK. Ayrinti MINI DALGA 2
  bolumunun sonunda.
- **MINI DALGA 2 TAMAMLANDI** - SUPHELI #18 duzeltildi (ayrinti MINI DALGA 2 bolumunde).
  **Yerel: 204/204 `Category=Sql`, tam suitte 328 basarili / 331** (kirilan 3'un UCU DE
  Docker'li `OrderEndpointTests`; UC ARDISIK kosumda ayni sonuc). Release 0 hata, format TEMIZ.
  **DURUST KAYIT - ISIMSIZ FLAKE:** bicim duzeltmesinden hemen sonraki TEK bir kosumda 4
  kirmizi gorundu; adlari YAKALANMADI. Ardindan UC kosum ust uste 3 kirmizi (yalnizca Docker)
  verdi. Dorduncusunun ne oldugu BILINMIYOR - uydurma bir aciklama yazilmiyor. CI'da tekrar
  ederse adiyla yakalanacak.
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

# KAPANIS KAYDI - KALITE SUPURMESI KAPANDI (23 Agustos 2026)

**LAUNCH'I BLOKE EDEN TEKNIK KALEM KALMADI.**

Bes olcum dalgasi ve karsiliklarindaki duzeltme dalgalari, artik yesil bir CI ile
kapandi. Kapanisi kanitlayan son SHA: **`dbaa763`** (her iki workflow tamamen yesil,
alti job'da failure seviyeli annotation SIFIR).

## KAPANAN DALGALAR

| Dalga | Konu | Duzeltme commit'i / durum |
|---|---|---|
| Dalga 1 | Envanter + tarama (B1..B9) | DALGA-1-FIX |
| Dalga 2 | Mantik / invariant denetimi (B10..B14) | DALGA-2-FIX + veri temizligi (7 iptal faturasi) |
| Dalga 3 | Performans (P1..P5) | DALGA-3-FIX |
| (C) Guvenlik | IDOR / tutar / mass assignment / enjeksiyon / yaris (G1..G9) | GUVENLIK-FIX + GUVENLIK-FIX-2 |
| Dalga 4 | Mobil + capraz cihaz (M1..M11) | M10/M11-FIX (`77c0308`) + DALGA-4-FIX-2 / M1 (`dbaa763`) |

**Dalga 4'un UC LAUNCH-BLOKE kaleminin UCU DE kapandi:**
- **M10** - "Sepeti Onayla" mobilde hic calismiyordu (delege handler'in kati hedef
  karsilastirmasi ripple ink yuzunden dusuyordu). GERCEK CIHAZDA dogrulandi.
- **M11** (+ M3) - cerez bari odeme sayfasinin TEK eylem dugmesini ve alt navigasyonu
  ortuyordu (`.ck-panel{display:flex}` HTML `hidden`'i eziyordu). GERCEK CIHAZDA dogrulandi.
- **M1** - storefront API adresi ve CSP origin'leri kaynakta sabit gomuluydu ve elle
  senkron tutuluyordu. Tek kaynak + dagitim betigi + calisma ani tutarlilik guard'i.

**MOBIL SATIN ALMA UCTAN UCA SURULDU** (kullanicinin telefonu, Android/Opera 384x694):
sepet -> "Sepeti Onayla" -> `#/odeme` -> **Iyzico kart formu mobilde yuklendi**
(kart no / ay-yil / CVC / 3DS + tutar). Bu, kapanisin saha kanitidir.

## ACIK KALANLAR (HICBIRI LAUNCH'I BLOKE ETMIYOR)

**TEKNIK DEFTER**
- **SUPHELI #14** - `X-Api-Version` ayristirilamazsa TUM API blanket 400 veriyor.
  Kapsam Sprint 8'de webhook yolu icin DARALTILDI ve pinlendi; genel cozum
  **LAUNCH SONRASI** (bkz. SUPHELI DAVRANISLAR).
- **SUPHELI #20** - varsayilan-kapali yetki kurali controller'larla sinirli; bugun
  BOSLUK YOK (olculdu) ve bosluk testte kapatildi.

**GUVENLIK**
- **G4** - satici girisi refresh token'i GOVDEDE donuyor (`SellerAuthManager.cs:101`).
  Bugun ERISILEMEZ (`sellers` 0 satir, kayit kapali/403). **Satici modulu acilmadan
  ONCE ZORUNLU ON KOSUL** - ikinci on kosul (kilit kontrolu sirasi) ile birlikte
  KARARLAR bolumunde.

**DALGA 4 - BLOKE ETMEYENLER**
- **M2** 376 px altinda header aksiyon kumesi tasiyor (gercek cihazda DOGRULANMADI -
  emulasyon kaniti gecerli).
- **M4** dokunma hedefleri 44x44 altinda (sepette `-`/`+` gercek cihazda sorunsuzdu).
- **M5** `autocomplete` eksik, `<form>` elementi yok (onem derecesi DUSURULDU - telefon
  parola kaydetmeyi onerdi, klavyede "Git" tusu var).
- **M6 / M7** PWA standalone kalemleri - kisayol standalone ACMADIGI icin **OLCULEMEDI**;
  "test edilmedi, bloke etmez" olarak kapatildi.
- **M8** service worker `VERSION` E3'ten beri bumplanmadi. Offline testi kullanici
  karariyla ATLANDI (oncelik degil); **VERSION bump'i DAGITIM KURALI olarak
  `ops/deployment-checklist.md`'de**.
- **M9** alt navigasyon etiketleri 9.5 px.

**ERTELENENLER**
- **B5** - 150 API ucunun 100'u HTTP duzeyinde test gormuyor (ayri kapsam dalgasi).
- **B13** - terk edilmis Pending siparislere TTL yok (17 siparis, hepsi >24 saat;
  rezervasyonlar serbest, stok/kupon guvende - politika URUN karari).
- **B8**, **P4**, **P2-inline-bolme** ve KARARLAR'daki launch-sonrasi defterin tamami
  (gift-card expiry, 2FA enrollment ucu, step-up `auth_time`, loyalty oransal geri alma
  + referral clawback, Dashboard tam-tablo agregalari, sabit-zamanli kayit, RFC 2606
  ust alan adlari, Turkce klavyede yazilan e-posta, istemci onbellegi, cikisli
  kullaniciya dogrudan giris katmani, JS/DOM test kosucusu).

## KAPANISTA KAYDA DEGER UC SEY

1. **GERCEK CIHAZ TURU EMULASYONUN GOREMEDIGINI GOSTERDI.** M10 emulasyonda CURUK
   gorundu: sentetik `.click()` dogrudan butona gider, o an ripple ink YOKTUR. Gercek
   dokunusta ink DOM'a girer ve click hedefi O olur. Kok sebep ancak cihazda gorundu.
2. **CI'DA JS/DOM PINI YOK.** Tarayici semantigi (hit-test, CSS ozgullugu,
   `elementFromPoint`) bu suitte dogrulanamiyor; 13 kaynak/hesap pini
   (`FrontendDokunmaHedefiTests` 7 + `ApiOriginTekKaynakTests` 6) sozlesmeyi tutuyor ve
   `frontend/test/mobil-erisilebilirlik.js` olcumu tekrarlanabilir kiliyor. Kalici cozum
   launch-sonrasi defterde (yeni bagimlilik + `dependency-scan` kapsami).
3. **DAGITIM ARTIK BIR ADIM ISTIYOR.** `ops/set-api-origin.sh` kosulmadan yapilan bir
   yayin, storefront'u localhost'a bakar halde birakir. Bu SESSIZ DEGIL: calisma ani
   guard'i ekrana kirmizi uyari basar ve `--verify` exit 1 doner. Checklist maddesi
   `ops/deployment-checklist.md`'de.

---

# LAUNCH-FIX - DALGA A: ILK MUSTERI ZINCIRI (TAMAMLANDI)

Kapsama denetiminin (bir onceki dalga) cikardigi kirik halkalardan **ilk musteri zincirine**
ait olanlar kapatildi. A1 + A2 + A4 yapildi; **A3 YALNIZ OLCULDU** (kullanici karari bekliyor,
kod YAZILMADI).

## OLCUM DUZENEGI - YEREL SMTP YAKALAYICI (depo DISINDA)

Gercek SMTP hesabi HENUZ YOK (**"gercek mail turu - BEKLIYOR"**, ayri is: domain/hosting
karariyla birlikte). Dalga kanitsiz kalmasin diye scratchpad'e **STARTTLS konusan bir SMTP
yakalayicisi** yazildi ve `MailSettings` ona yonlendirildi.

**Neden hazir bir arac degil, neden duz metin degil - OLCULDU:** `SmtpMailService` 465 disi
portlarda `SecureSocketOptions.StartTls` kullaniyor ve bu **bilincli** bir karar ("Sifresiz
baglanti KABUL EDILMEZ"). Duz metin konusan bir yakalayici bu yuzden ise yaramaz; uretim kodunu
gevsetmek ise sorulu bile degil. Sertifika olarak makinede **ZATEN GUVENILEN** ASP.NET Core
gelistirme sertifikasi (`CN=localhost`, thumbprint `A1BC63BC...`) disa aktarilip sunucu
tarafinda sunuldu - **hicbir guven deposu DEGISTIRILMEDI**.

Depo tarandi: `smtpsink` / `sink.pfx` / `2525` -> kod, yapilandirma ve dokumanda **SIFIR** iz.

## A1 - MAIL ALTYAPISI

### (a) SAHTE ALICI KALKTI

OLCULEN ONCE-DURUM: `OrderPlacedEmailHandler` -> `To = $"customer-{id}@divisima.local"`.
Siparis onay maili musteriye **HIC GITMIYORDU**; ustelik `.local` yonlendirilemez bir ust alan
adidir, yani gercek SMTP'de gonderim **REDDEDILIR** ve `SmtpMailService` (bilincli olarak)
istisna firlatir.

ADRESIN KAYNAGI OLCULDU, UYDURULMADI: siparisin musterisi **her iki yolda da** `customers`
tablosunda gercek e-postasiyla duruyor - uye siparisinde `customer_id` token'dan gelir,
**misafir siparisinde** `GuestCheckoutManager` once Customer satirini `dto.guest_email` ile
OLUSTURUR ve PlaceOrder'a o id ile devreder. Bu yuzden tek dogru kaynak `customer_id` uzerinden
okumaktir; event'e ayri bir e-posta alani **EKLENMEDI** (snapshot degil, GUNCEL adres istenir).

### (b) SMTP HATASI ARTIK AKISI DUSURMUYOR - IKI YERDE

**Siparis yolu.** `PublishAsync` COMMIT'TEN SONRA ve **try blogunun DISINDA** cagriliyordu;
publisher handler'lari duz `foreach { await }` ile kosuyor (try/catch YOK). Sonuc: siparis
commit olmus haldeyken uc **HTTP 500** doner. Cozum **MEVCUT ALTYAPI**: `OutboxProcessor`'da
`case "OrderPlaced"` ZATEN VARDI ve ayni publisher'i cagiriyordu - o dala uretimde mesaj YAZAN
kimse yoktu (olculdu). Mesaj artik **transaction'in ICINDE** yaziliyor.

**KAYIT YOLU - BU DALGANIN KENDI OLCUMUNDE CIKAN YENI BULGU.** Pin yazilirken sahte mail
servisi her gonderimde istisna atacak sekilde ayarlandi ve `POST /api/auth/register` **HTTP 500**
dondu. Zarar siparistekinden **AGIR**: musteri satiri ZATEN yazilmis oluyor (`AddAsync` mail'den
ONCE), yani kullanici "kayit olamadim" sanip tekrar deniyor ve bu kez "var olan hesap" dalina
dusuyor - hesabi VAR ama dogrulama maili HIC GITMEMIS durumda kaliyor. Kayit / var-olan-hesap
bildirimi / yeniden-dogrulama / sifre sifirlama mailleri de `"EmailNotification"` outbox tipine
tasindi (EngagementManager'in kullandigi kanal).

**2FA KODU BILINCLI OLARAK HARIC**: o bir giris anahtaridir, 5 dakika omru vardir; gecikmeli ya
da kayip gitmesi kullanicinin giris yapamamasi demektir - orada **gurultulu basarisizlik dogru
davranistir**. (Bugun zaten ulasilamaz bir dal: `two_factor_enabled` hicbir kod yolunda `true`
yapilmiyor - olculdu.)

**BEDEL - DURUST KAYIT:** teslimat artik **at-least-once** ve `OrderPlaced` mesaji UC handler'i
birden tasiyor. Son handler (SignalR bildirimi) patlarsa mesaj yeniden denenir ve onay maili
IKINCI KEZ gidebilir. Kabul edildi: bir siparis onay mailinin tekrarlanmasi, hic gitmemesinden
iyidir. Ikinci bedel: gecikme (~1 dk, `Cron.Minutely`).

**SESSIZ DEGIL:** 5 deneme -> `status=Failed` + `LogError` + **siparis zaman cizelgesine KRITIK
notu**. `KaliciHataylaBirakAsync` bugune kadar yalniz `PaymentConfirmed` icin not dusuyordu;
`OrderPlaced` dali eklendi (durum olarak `Pending` kullaniliyor - not "Siparis olusturuldu"
ANINA ait, yeni bir gecis DEGIL).

### (c) TIKLANABILIR BAGLANTILAR - TEK KAYNAK

Yeni `IMailLinkBuilder` / `MailLinkBuilder`. **IKI AYRI ORIGIN VAR VE BU BIR CELISKI DEGIL:**

| Tur | Kaynak | Kullanan |
|---|---|---|
| VITRIN (kullanicinin acacagi sayfa) | `Storefront:BaseUrl` | dogrulama, sifre sifirlama, siparis takibi |
| API (dogrudan bir uca giden) | `Api:PublicBaseUrl` -> `Storage:PublicBaseUrl` | abonelikten cikma (Sprint 8 madde 10 kalibi) |

`Storefront:BaseUrl` yeni bir ayar DEGIL - `PaymentController.Callback` yonlendirmesi zaten onu
kullaniyor, yani vitrin origin'inin TEK KAYNAGI oydu. **Ucuncu bir sabit origin EKLENMEDI**;
kaynak dosyalarda tek bir `http://...` literali yok.

**BOS ORIGIN'DE GURULTULU:** iki metot da `null` doner VE `LogError` basar; cagiran kullaniciya
yedek yonergeyi kendi yazar. Yarim bir URL asla uretilmez. `StockNotificationManager` ve
`PriceDropManager`'daki ikiz `AbonelikCikisMetni` metotlari da bu tek kaynaga baglandi
(`IConfiguration` bagimliliklari KALKTI); dusus AYNEN korundu, degisen tek sey bos origin'in
artik loglanmasi.

**FAIL-FAST SECILMEDI - GEREKCE OLCUM:** `Storefront:BaseUrl` bos birakmak `PaymentController`
icin **belgelenmis bir kacis yoludur** ("BOS ise callback eski davranisla ham JSON doner").
Uretim fail-fast'ine eklemek o kacis yolunu kirardi; calisma ani gurultulu log + yedek metin,
abonelikten-cikma kaliginda zaten kabul edilmis desen.

### (d) SABLON: DUZ METIN - OLCUME DAYALI

Depodaki **TUM** mailler duz metin (`IsHtml = false`): EngagementManager, StockNotificationManager,
PriceDropManager, AuthManager. HTML sablon katmani **ACILMADI**; duz metinde kendi satirinda
duran ciplak URL her istemcide tiklanabilir. Jeton her iki durumda da govdede KALIYOR - Giris
ekranindaki mevcut dogrulama kutusu (E1'den beri calisan yol) bozulmasin diye; baglanti EK bir
yoldur, YERINE GECEN degil.

### YAKALANAN GERCEK MAIL GOVDELERI (STARTTLS uzerinden)

```
To: dalgaa.<...>@example.com     Subject: Divisima - E-posta adresinizi doğrulayın
  Merhaba,
  Divisima hesabını doğrulamak için aşağıdaki bağlantıya tıkla:
  http://localhost:5173/#/dogrula/94-SsO4Z...
  Bağlantı çalışmazsa Giriş ekranındaki doğrulama kutusuna şu kodu gir: 94-SsO4Zz-...

To: dalgaa.<...>@example.com     Subject: Divisima - Şifre sıfırlama
  Şifreni sıfırlamak için aşağıdaki bağlantıya tıkla (30 dakika geçerli):
  http://localhost:5173/#/sifre-sifirla/a7sK1hP...
  Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin; şifren değişmez.

To: dalgaa.<...>@example.com     Subject: Divisima - Siparişin alındı (#DVS20260823-54740CC62D)
  Merhaba Dalga A,
  #DVS20260823-54740CC62D numaralı siparişin başarıyla oluşturuldu.
  Tutar: 549,80 TL.
  Siparişinin durumunu buradan takip edebilirsin:
  http://localhost:5173/#/hesabim/siparislerim
```

Alici **gercek musteri adresi**, tutar **tr-TR bicimli** (madde 13 pini calisiyor), baglantilar
**yapilandirilan origin'i** tasiyor.

### HATA YOLU - CANLI (SMTP olu porta cevrildi, 2599)

```
POST /api/order/place                -> HTTP 201  {"data":89,"success":true}   (once: 500)
outbox id=18  OrderPlaced  status=0  retry_count=1  error="Hedef makine ... reddettiğinden
                                                            bağlantı kurulamadı."
... dort minutely tur sonra ...
outbox id=18  OrderPlaced  status=2 (Failed)  retry_count=5
siparis #89 zaman cizelgesi:
   "Sipariş oluşturuldu"
   "Kapıda ödeme - sipariş onaylandı"
   "KRITIK: sipariş bildirimleri 5 denemede tamamlanamadı (onay e-postası/admin bildirimi)..."
```

## A2 - SIFREMI UNUTTUM (OLU LINK BAGLANDI)

OLCULEN ONCE-DURUM: `index.html`'de `<a href="#" data-i18n="forgot">Sifremi unuttum</a>` -
**href="#" olu link**. `api-client.js`'te `forgotPassword`/`resetPassword` TANIMLIYDI ama
`api-bridge.js`'te `forgot` **0 kez** geciyordu. Sifresini unutan musterinin siteden geri donus
yolu **HIC YOKTU**.

Iki yeni rota **router SARMALANARAK** eklendi (index.html'in router'ina DOKUNULMADI):
`#/dogrula/<token>` ve `#/sifre-sifirla/<token>`. Bilinmeyen rota `show404()`'e dustugu icin bu
iki yol ONCE yakalaniyor; sarmalayici digerlerini `origRouter.apply` ile devrediyor. Ekranlar
yeni bir view acmadan `showVerifyPrompt` kalibiyla `#paneLogin`'e enjekte ediliyor. Ilk yukleme
yarisi (E3/M12'de olculen) icin sarmalama kurulduktan sonra rota bir kez daha degerlendiriliyor.

**BU DALGADA OLCULEN VE DUZELTILEN KENDI KUSURUM (M12 SINIFI):** ekran DOGRU cizildigi halde
sekme basligi **"Sayfa Bulunamadi · Divisima"** kaliyordu - `setDocTitle()`'in bu yollar icin
dali yok ve sarmalayici orijinal router'a devretmediginde o hic cagrilmiyor. Paylasilan/yer
imine eklenen bir sifirlama baglantisinin "Sayfa Bulunamadi" gorunmesi kullaniciya linkin BOZUK
oldugunu soyler. Baslik iki rota icin acikca set edildi.

### ELLE DOGRULAMA (tarayici, uctan uca)

```
"Sifremi unuttum" linkine tiklandi -> kutu acildi ("Sifremi unuttum", e-posta alani)
gonderildi -> "Bu adres kayıtlıysa şifre sıfırlama bağlantısını gönderdik. Bağlantı 30 dakika
              geçerli."   (G2 kalibi: uc varligi sizdirmiyor, istemci de KESIN konusmuyor)
outbox bosaldi -> YAKALANAN MAIL'deki link acildi
  baslik  : "Yeni Şifre Belirle · Divisima"       (once: "Sayfa Bulunamadı")
  ekran   : authView + "Yeni şifre belirle"; token URL'den geldigi icin kod alani CIZILMEDI
yeni sifre girildi -> "Şifren güncellendi."
  ESKI sifreyle giris  -> 401
  AYNI jeton 2. kez    -> 400 "Geçersiz sıfırlama bağlantısı."   (TEK KULLANIMLIK korundu)
  YENI sifreyle giris  -> 403 "Giriş için e-posta adresinizi doğrulamanız gerekiyor."
maildeki DOGRULAMA linki acildi -> "E-postan doğrulandı."  -> YENI sifreyle giris -> 200
```

## A4 - TEK PARA BIRIMI (TRY) - KULLANICI KARARI

OLCULEN ONCE-DURUM: `var CUR={TRY:{rate:1},EUR:{rate:53.2},USD:{rate:46.6}}` - kurlar **kaynaga
gomulu** sabitlerdi. `tl(n)` non-TRY'de `sym+(n/rate)` donduruyordu. Buna karsilik `api-bridge.js`
`tl()`'i **HIC KULLANMIYORDU** (olculdu: 0 cagri) ve odeme paneli / siparis listesi / faturalar
ham TRY basiyordu. Backend her kosulda TRY tahsil ediyor. Yani USD secili kullanici vitrinde
`$X`, odeme panelinde TRY tutar goruyordu.

YAPILAN: kur tablosu TRY'ye indirildi ve `tl()` icindeki cevrim dali KALDIRILDI (tabloyu
bosaltmak yetmez - dal kalsaydi tablo geri geldigi gun ayrisma da geri gelirdi). Secici
**GIZLENDI, KALDIRILMADI** (`#curbox` + `#curSelect`, markup duruyor). `api-bridge.js`'in iki
bicimleyicisi (`money`, `paraTL`) `window.tl`'e **delege** ediyor - "fiyat bicimi tek kaynaktan"
sarti. Eski oturumdan kalan `dvs_cur` anahtari temizleniyor.

**KENDINI SAVUNAN TASARIM:** `CUR[code]` guard'i her tuketicide ZATEN vardi; tablodan EUR/USD
girdilerini kaldirmak `setCur('USD')`'i ve `localStorage`'daki eski secimi **otomatik olarak**
etkisiz kildi.

### ELLE DOGRULAMA (tarayici)

```
CUR = {"TRY":{"rate":1,"sym":"₺"}}      curCode = TRY      dvs_cur = null
#curbox display=none  #curSelect display=none   (markup IKISI DE DURUYOR)
setCur('USD') cagrildi -> curCode HALA "TRY", tl(499.90) HALA "499,90 TL"
localStorage'a dvs_cur=USD YAZILDI + sayfa yenilendi
   -> curCode=TRY, dvs_cur=null (temizlendi), sayfada $ veya € : YOK
vitrin fiyati "499,90 TL"  =  tl(499.90)  ;  odeme panelinde yalniz TL satirlari, $/€ YOK
```

## A3 - MISAFIR CHECKOUT: YALNIZ OLCULDU, KOD YAZILMADI

Kullanicinin karari beklendigi icin **hicbir sey degistirilmedi**. Olculenler:

- `POST /api/guest-checkout/place` VAR: `[AllowAnonymous]` + `[EnableRateLimiting("auth")]` +
  `[Idempotency]`. Musteri satirini rastgele guclu sifreyle olusturur, adresi yazar, `PlaceOrder`'a
  devreder. E-posta kayitliysa **409** doner (hesap ele gecirme engeli).
- **`GuestCheckoutDto`'da `payment_method` ALANI YOK** -> `PlaceOrder` varsayilani alir ->
  `payment_type = 0` (Online).
- `/api/payment/initialize` **`[RequireUserType(Customer)]`** ve musteriyi TOKEN'dan okuyor.
  Misafirin token'i yok.
- **SONUC: bugun bir misafir siparisi olusturulabilir ama ASLA ODENEMEZ** - sonsuza kadar Pending
  kalir (B13'teki terk edilmis siparis yiginina duser).
- Storefront'ta `guest-checkout` cagrisi: index.html 0, api-bridge 0, api-client 0.
- `.co-guest` blogu **DOM'DA YOK** (tarayicida olculdu): E2'nin gercek odeme paneli, index.html'in
  o blogu cizen mock checkout'unun yerine gecti. Yani UI vaadi zaten olu.
- **YASAYAN TEK VAAT SSS'DE:** "Evet. Ödeme sayfasında misafir olarak devam edebilirsin; sipariş
  bilgilerin belirttiğin e-posta adresine gönderilir." (tr/en/ar).
- `IssueSessionAndTokenAsync` **`email_verified` KONTROLU YAPMIYOR** - o kapi `Login`'de. Yani
  misafire oturum vermek yetki modelini degistirmeden mekanik olarak MUMKUN.

Uc secenek ve maliyetleri rapora yazildi; **KARAR KULLANICININ**.

## PINLER

`LaunchFixMailZinciriTests` (7, SQL): dogrulama maili tiklanabilir link tasir ve origin TEK
KAYNAKTAN gelir (+ yedek kod yolu KORUNUR - cift-anlam kirici) · sifre sifirlama maili TAM
rotayi ve sure sinirini tasir · siparis onay maili GERCEK musteri adresine gider ve
`divisima.local` HIC gecmez · **SMTP patlarsa siparis ucu 201 doner ve olay outbox'ta gorunur**
(+ mesajin O siparise ait oldugu) · kalici hata 5 denemede `Failed` olur ve zaman cizelgesine
KRITIK notu duser (GERCEK publisher + GERCEK handler kosuluyor, stub degil) · origin yoksa link
URETILMEZ, varsa URETILIR (vakum kirici) ve vitrin origin'i API origin'inin YERINE GECMEZ
(cift-anlam kirici) · **SUPHELI pini** (asagi).

`LaunchFixDalgaAFrontendTests` (6, kaynak sozlesmesi): "Sifremi unuttum" handler'i VAR ve hedefi
`closest` ile cozer (M10 dersi) · iki rota router sarmasinda tanimli + bilinmeyen rota orijinale
DEVREDILIR (cift-anlam kirici) + ilk yukleme yarisi kapatilmis · sifirlama ucu istemcide tanimli
ve token ile cagriliyor · kur tablosu yalniz TRY ve `(n/c.rate)` dali KALMADI · `money`/`paraTL`
`tl()`'e delege eder (+ ikisi de HALA VAR - vakum kirici) · secici gizlenir ama markup DURUR ve
`dvs_cur` temizlenir.

**KIRILAN PIN YOK.**

**PIN SINIRI (Dalga 4'teki ayni durust kayit):** depoda JS/DOM kosucusu YOK; frontend pinleri
KAYNAK SOZLESMESINI tutar, tarayici semantigini degil. Davranis kaniti yukaridaki elle
dogrulama bloklarinda.

## DIS KONTROLU + 5. KONTROL

**DIS:** 5 assert ters cevrildi (BES AYRI test, IKI ayri sinif) -> **5 AYRI ISIMLI KIRMIZI**.
Geri alindi, 13/13 yesil.

**5. KONTROL - IKI URETIM MUTASYONU:**
- **M1** (outbox yazimi kaldirildi, publish ESKI YERINE - commit sonrasi, try disinda):
  `SMTP_PATLARSA_...` -> **HTTP 500**, govde `{"status":500,...,"instance":"/api/order/place"}`.
  Olculen once-durumun **BIREBIR** aynisi. Diger 5 pin yesil kaldi (mutasyon lokalize).
- **M2** (alici adresi sahte haline donduruldu): `SiparisOnayMaili_...` ->
  **`"customer-2@divisima.local"`** buldu. Olculen once-durumun BIREBIR aynisi. TAM 1 pin kirildi.
Ikisi de geri alindi.

## YEREL DOGRULAMA

259/259 `Category=Sql` · tam suitte **408 basarili / 411** (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`) · Release 0 hata · whitespace + style **exit 0**.

**SURECTE YASANAN (kayit):** `dotnet format style` iki test dosyasinda `IMPORTS` hatasi verdi -
`sed` ile dosyanin BASINA eklenen `using` satirlari siralamayi bozmustu. CLAUDE.md'de zaten
yazili olan tuzak (migration notu) elle eklenen using'ler icin de gecerli; `dotnet format style
--include <dosya>` ile duzeltildi.

## DEFTERE (BEKLEYEN)

- **GERCEK MAIL TURU - BEKLIYOR.** Gercek bir SMTP hesabiyla (domain/hosting karariyla birlikte)
  dogrulama / sifre sifirlama / siparis onayi mailleri **gercek bir gelen kutusuna** surulmeli:
  teslim edilebilirlik (SPF/DKIM/DMARC), spam klasoru, gonderen adi/adresi ve linklerin gercek
  origin'i. Bu dalga yerel yakalayiciyla **govde + alici + link** duzeyinde kanitladi; **teslimat**
  duzeyi kanitlanmadi.


## DALGA A PUSH RAPORU (e6e9b71) - CI YESIL, SECURITY KIRMIZI (TEK JOB)

**Push `dbaa763..e6e9b71`** (tek commit -> tek push). Adim bazinda + annotation duzeyinde okundu.

### CI - Build & Test (run 32655634070) - TAMAMEN YESIL
`format-check`: iki ZORUNLU adim SUCCESS.
`build-and-test`: 14 adimin tamami SUCCESS (`SQL gerektiren testler` + `Testler + coverage`
+ `Coverage raporunu yukle` DAHIL); `TESHIS` skipped. **failure seviyeli annotation YOK.**

### Security CI (run 32655634056) - KIRMIZI, TEK JOB
`tests` SUCCESS (`Entegrasyon testleri` DAHIL, TESHIS skipped) · `dependency-scan` SUCCESS ·
`codeql` SUCCESS · **`secret-scan` -> `Gitleaks (secret taramasi)` FAILURE**, annotation
`warning` seviyesinde ve yalnizca "Leaks detected, see job summary for details" - **dosya,
satir ve kural TASIMIYOR** (bolum 7 kurali bir kez daha dogrulandi).

### KOK SEBEP - DEPO TARAMASIYLA, IKI AYRI BICIMDE VE IKISI DE BENIM RAPOR/TEST ALISKANLIGIM

**(1) TEST SIFRE LITERALLERI - GUVENLIK-FIX-2'NIN BIREBIR TEKRARI.**
Yeni pin dosyasinda `password = "<deger>"` bicimli uc satir vardi. `generic-api-key` anahtar
kelime + entropi >= 3.5 arar. OLCULDU (Shannon; degerler bolum 1 geregi KIRPILDI):

```
"LinkTest..."   (13 krkt) -> 3.547   ESIGIN USTUNDE   (satir 177, 217)
"GucluSifre..." (14 krkt) -> 3.522   ESIGIN USTUNDE   (satir 423)
"abc"                     -> 1.585   esigin ALTINDA   (bulgu DEGIL - SUPHELI pininin kendisi)
YanlisSifre               -> 3.278   depoda VAR, tam-gecmis taramasi YESIL
TestAuthHelper.TestPassword -> 3.027  depoda VAR, tam-gecmis taramasi YESIL
```

**(2) CLAUDE.md'YE YAPISTIRILAN IKI JETON.** Rapor yazarken yakalanan mail govdeleri BIREBIR
kopyalandi ve iki jeton (43 krkt) TAM HALIYLE depoya girdi. `generic-api-key` icin anahtar
kelime tasimadiklarindan bulgu OLMAYABILIRLER - ama **bolum 1'in ACIK kuralinin ihlaliydi**
("depoya yazilan her ornek govdede jeton ilk 8 karaktere kirpilir") ve Sprint 8'de bedeli bir
kez odenmisti. Ustelik AYNI blokta bir sonraki satirda jetonu KIRPMISTIM - yani kurali
biliyordum ve tutarsiz uyguladim.

### DUZELTME (yerelde hazir)

1. **Ileriye donuk:** uc sifre literali tek bir DUSUK ENTROPILI sabite (`GecerliSifre`, 2.855)
   cevrildi; sabit kayit politikasini karsiliyor (>=8, buyuk, kucuk, rakam). Tanim satirinda
   anahtar kelime YOK, kullanim satirinda TIRNAKLI deger YOK. Iki jeton CLAUDE.md'de kirpildi.
2. **Gecmis icin:** `.gitleaksignore`'a DAR KAPSAMLI **bes fingerprint** + gerekcesi. Uc tanesi
   sifre satirlari, ikisi CLAUDE.md jeton satirlari ve **ONLEM AMACLIDIR** (Sprint 8'deki 1277
   satiri gibi - eslesmiyorlarsa etkisiz kalirlar). Force-push YASAK oldugu icin `e6e9b71`'in
   gecmiste kalan hali ancak boyle susturulur.

**SUSTURULAN SEY KIMLIK BILGISI DEGIL:** sifreler bir testin KENDI olusturdugu, yalniz o testin
gecici veritabaninda var olan hesaplara ait; jetonlar YEREL bir gelistirme veritabaninin tek
kullanimlik dogrulama/sifirlama jetonlari - **ikisi de olcum sirasinda KULLANILDI** (dolayisiyla
null'landi) ve sifirlama jetonunun 30 dakikalik omru coktan doldu.

### DOGRULAMA BOSLUGU KAPANDI - KONTROLLU A/B/C (dispatch run 32658699209)

Sprint 8'den beri UC KEZ "tutuyor gorunuyor" diyebildigimiz sey KANITLANDI. Kullanici
`workflow_dispatch`'i `cee76fb` HEAD iken tetikledi; o kosum `--log-opts` ALMADIGI icin
**TUM GIT GECMISINI** taradi - jetonlarin ve yuksek entropili sifre literallerinin durdugu
`e6e9b71` commit'i DAHIL.

```
KOSUM         EVENT              KAPSAM            .gitleaksignore   secret-scan
32655634056   push               son commit        fingerprint YOK   FAILURE
32657695876   push               son commit        kapsam DISI       SUCCESS  (KANIT DEGIL)
32658699209   workflow_dispatch  TUM GECMIS        fingerprint VAR   SUCCESS  <- KANIT
```

`32658699209` adim bazinda: `secret-scan` / `tests` / `codeql` / `dependency-scan` hepsi
SUCCESS; dort job'da da **failure seviyeli annotation 0** ve **"Leaks detected" 0**.
Bes fingerprint (uc sifre satiri + iki CLAUDE.md satiri) GERCEKTEN TUTTU.

**Asagidaki eski not TARIHSEL kayittir; bosluk yukarida kapandi.**

**DOGRULAMA BOSLUGU (onceki iki kalemle AYNI, durust kayit):** fingerprint'lerin tuttugu bir
sonraki PUSH run'inda GORULEMEZ (push yalniz son commit'i tarar, orada bulgu zaten olmayacak).
Kanit ancak TUM GECMISI tarayan bir kosumdan gelir - Pazartesi cron'u ya da ELLE
`workflow_dispatch`.

### DERS (UCUNCU KEZ - ARTIK KALIP)

Ayni hata sinifi ucuncu kez tekrarladi (Sprint 8 Iyzico jetonu, GUVENLIK-FIX-2 test sifreleri,
Dalga A ikisi birden). Ortak nokta: **uretim kodu degil, KANIT YAZMA ANI**. Bundan sonra bir
dalga raporuna govde/sifre yapistirilirken iki soru ONCE sorulur: (a) jeton/kimlik ilk 8
karaktere kirpildi mi, (b) `password =` gibi bir anahtar kelimenin yanindaki deger dusuk
entropili bir SABIT mi.

### YEREL DOGRULAMA (duzeltme sonrasi)

259/259 `Category=Sql` · 13/13 Dalga A pini · whitespace + style **exit 0**.
Dis kontrolu ve 5. kontrol YENIDEN KOSULMADI - degisiklik yalnizca sabit degeri (literal ->
`GecerliSifre`) ve bir dokuman kirpmasi; pinlerin OLCTUGU sey ve assert'ler DEGISMEDI.


---

# A2-FIX - SIFRE POLITIKASI TEK MERKEZDEN (SUPHELI #21 KAPANDI)

Kullanici karari: #21 duzeltilir, ama A3'le AYNI commit'te degil - AYRI kucuk commit.

## OLCUM: DORT KOPYA VARDI (kullanicinin verdigi UC UCTAN FAZLASI)

Kapsam "uc uc" olarak verilmisti; tarama DORDUNCU bir kopya cikardi:

```
POST /api/auth/register            8 + buyuk + kucuk + rakam   CustomerRegisterRequestValidator
POST /api/seller/auth/register     AYNI KURALIN BIREBIR KOPYASI (dorduncu kopya)
POST /api/account/change-password  YALNIZCA >= 6, karmasiklik YOK
POST /api/auth/reset-password      HICBIR KONTROL YOK - dogrudan hash'leniyordu
```

Bir politika ancak **EN ZAYIF girisi kadar** gucludur ve en gevsek olan (reset-password),
**EN KOLAY ulasilan** yoldu. A2 bu akisi arayuze bagladigi icin kapi her musteriye acilmisti.

## YAPILAN

**Yeni merkez:** `Divisima.Core/Security/SifrePolitikasi.cs`.
`Dogrula(sifre)` -> `null` (gecerli) ya da **IHLAL EDILEN ILK kuralin OZEL mesaji**.
Genel bir "sifre gecersiz" mesaji SECILMEDI: kullanici hangi kurali cignedigini bilmezse
deneme yanilmaya duser. Bu mesajlar kayit ucunda zaten gosteriliyordu; degisen tek sey artik
DORT ucta da ayni olmalari.

**Dort giris de merkeze baglandi** - satici kopyasi DAHIL. O kural zaten BIREBIR ayniydi,
yani davranis DEGISMIYOR; ama dorduncu kopyayi birakmak "TEK MERKEZ" iddiasini bosa dusururdu.
Satici modulu bugun kapali (`Seller:RegistrationEnabled=false`).

**change-password icin bu bir SIKILASTIRMADIR** (6 -> 8 + karmasiklik) ve bilinclidir: ayni
hesabin sifresini belirleyen iki yolun farkli guc istemesi savunulabilir degil.

### IKI OLCUME DAYALI TASARIM KARARI

1. **`char.IsUpper` / `char.IsLower`, `[A-Z]`/`[a-z]` regex'i DEGIL.** Eski regex Turkce
   `Ş`/`ş` harflerini GORMUYORDU ve Turkce harfli sifre kullanan musteriyi gereksizce
   zorluyordu. Kural GEVSEMEDI, **KAPSAMI GENISLEDI** - uzunluk ve rakam sartlari aynen
   duruyor. (CLAUDE.md bolum 6c ile celiski YOK: orada yasak olan kimlik dizgesinde KULTURLU
   DONUSTURME; burada yapilan SINIFLANDIRMA ve kultur bagimsiz.)
2. **Politika kontrolu JETON DOGRULAMASINDAN ONCE kosuyor.** Jeton TEK KULLANIMLIK; zayif bir
   sifre denemesi onu HARCAMAMALI, yoksa kullanici yeniden "sifremi unuttum" yapmak zorunda
   kalirdi. Ayrica pinlendi.

### TEMIZLIK (ayni commit)

- `Messages.PasswordTooShort` (`"Şifre en az 6 karakter olmalıdır."`) **SILINDI** - hem OLU
  kaldi hem metni artik YALAN olurdu. Derleme olu oldugunu kanitladi (Sprint 8 madde 11 kalibi).
- `ResetPassword`'un basindaki **ULASILAMAZ ikinci bos-token kontrolu** silindi.
- A2'de yazilan istemci yorumu ("sunucuda hicbir kural yok") artik YANLIS olacagi icin
  duzeltildi. Istemci kurali sunucudan **bir tik KATI** (ASCII regex): yanlis pozitif uretmez,
  yalniz Turkce harfli bir sifreyi istemcide reddedip sunucuda kabul ettirebilir.
  **Ters yonde bosluk YOK** - kritik olan da bu.

## BILINCLI KIRILAN PIN

`LaunchFixMailZinciriTests.SUPHELI_SifreSifirlamada_SUNUCU_TARAFI_SIFRE_POLITIKASI_YOK_PINLENIR`
kaldirildi. Bozuk davranisi (reset-password'un `"abc"` sifresini 200 ile kabul etmesi) KABUL
EDILMIS gibi sabitliyordu; kural duzelince duzeltmeyi KIRARDI. Yerine gerekcesi yazildi.

## YENI PINLER (`SifrePolitikasiTests`, 11)

- `MERKEZ_IHLAL_EDILEN_ILK_KURALIN_OZEL_MESAJINI_Doner` (Theory x5 - bos / kisa / buyuksuz /
  kucuksuz / rakamsiz)
- `MERKEZ_GECERLI_SIFREYI_KABUL_Eder` - **vakum kirici** ("her seyi reddet" de Theory'yi gecerdi)
- `MERKEZ_TURKCE_BUYUK_KUCUK_HARFI_DE_SAYAR`
- `ZAYIF_SIFRE_UC_UCTA_DA_REDDEDILIR`
- `GECERLI_SIFRE_UC_UCTA_DA_KABUL_EDILIR` - **cift-anlam kirici** + sifirlama sonrasi YENI
  sifreyle giris 200 (sifirlama KOZMETIK degil)
- `ZAYIF_SIFRE_SIFIRLAMA_JETONUNU_HARCAMAZ`
- `HICBIR_UC_KENDI_SIFRE_KURALINI_TANIMLAMAZ` - SINIF DUZEYI kaynak taramasi; **BESINCI** bir
  kopya eklenirse kirilir (vakum kirici: taramanin gercekten dosya okudugu da assert ediliyor)

## DIS KONTROLU + 5. KONTROL

**DIS:** 6 assert ters -> **BES AYRI ISIMLI test kirmizi** (Theory'lerle 9 vaka). Geri alindi.

**5. KONTROL - kullanicinin sarti birebir karsilandi:**
- **M1** (reset-password'den politika cagrisi kaldirildi): zayif sifre **200 ile KABUL** edildi -
  #21'in olculen zararinin ta kendisi. `ZAYIF_SIFRE_UC_UCTA_DA_REDDEDILIR` ve
  `ZAYIF_SIFRE_SIFIRLAMA_JETONUNU_HARCAMAZ` kirildi; diger 9 pin YESIL kaldi (lokalize).
- **M2** (change-password'de merkez kaldirilip eski `>= 6` kurali geri kondu): **TAM 6
  KARAKTERLIK** `Aa1234` sifresi **200 ile GECTI** - eski davranisin birebir aynisi.
Ikisi de geri alindi.

## YEREL DOGRULAMA

269/269 `Category=Sql` · tam suitte **430 basarili / 433** (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`) · Release 0 hata · whitespace + style **exit 0**.

**SURECTE YASANAN (kayit - AYNI TUZAK IKINCI KEZ):** `dotnet format style` yine `IMPORTS`
hatasi verdi - `sed -i '1i using ...'` ile dosya BASINA eklenen using satirlari siralamayi
bozuyor. Dalga A'da da yasanmisti. **DERS: bu depoda `using` satiri `sed` ile dosya basina
EKLENMEZ; eklendiyse hemen ardindan `dotnet format style --include <dosya>` kosulur.**

---

# A3 HIBRIT - MISAFIR CHECKOUT, YALNIZ KAPIDA ODEME (TAMAMLANDI)

Kullanici karari: **secenek (iii)**. Gerekce: dogrulanmamis hesaba oturum verme kapisi HIC
acilmaz (bu projenin defalarca bedelini odedigi sinir), SSS'deki vaat DOGRU hale gelir,
(i)'ye giden yol kapanmaz.

## OLCULEN ONCE-DURUM (kapsama denetiminden)

- `POST /api/guest-checkout/place` VARDI ama storefront'ta cagrisi **SIFIRDI** (index.html 0,
  api-bridge 0, api-client 0).
- `GuestCheckoutDto`'da `payment_method` **YOKTU** -> `PlaceOrder` varsayilani aliyor ->
  `payment_type = 0` (Online). Ama `/api/payment/initialize` `[RequireUserType(Customer)]` ve
  musteriyi TOKEN'dan okuyor; misafirin token'i YOK.
  **Sonuc: misafir siparisi OLUSTURULABILIYOR ama ASLA ODENEMIYORDU** - sonsuza kadar Pending
  (B13'teki terk edilmis siparis yigini).
- `.co-guest` blogu **DOM'DA YOKTU** (tarayicida olculdu): E2'nin gercek odeme paneli,
  index.html'in o blogu cizen mock checkout'unun (`coStep1`) USTUNE yaziyor.
- **YASAYAN TEK VAAT SSS'DEYDI** ve yanlisti.

## YAPILAN

**Backend**
- `GuestCheckoutDto` += `payment_method`. Misafir icin **YALNIZ COD (1)**; baska deger gelirse
  uc **REDDEDER** (`Messages.GuestOnlyCashOnDelivery`). **SESSIZCE COD'A DUSURME YOK** - musteri
  kart sectiyse bunu ACIKCA ogrenmeli, aksi halde "kartla odedim" sanip kapida nakitle
  karsilasirdi.
- `payment_method` `PlaceOrder`'a TASINIYOR (eksikligi asil kusurdu).
- **Misafir hesabini sahiplenebilsin diye dogrulama maili tetikleniyor - YENI UC ACILMADAN.**
  Var olan ANONIM zincir zaten cozuyordu; eksik olan MISAFIRE SOYLENMESIYDI:
  `resend-verification` -> `#/dogrula` -> `forgot-password` -> sifre belirle -> `my-orders`.
  Cagri **BEST-EFFORT**: mail tetiklenemezse siparis DUSMEZ, hata loglanir.
  "Siparis no + e-posta ile sorgulama" ucu **REDDEDILDI** - yeni bir ANONIM sorgu yuzeyi acar.
- Siparis onay maili, **yalnizca dogrulanmamis musteriye** (yani misafire) "sifre belirle"
  satiri + baglanti ekliyor. Uyeye eklemiyor (cift-anlam kirici pinli).
- `ResendVerification` konusundaki **"(yeniden)" KALDIRILDI**: dal iki durumda kosuyor
  (kullanici ilk maili hic almadi / misafir checkout'u ILK KEZ tetikliyor) ve ikisinde de
  "yeniden" yanlis bir sey soyluyordu.

**Frontend**
- Cikisli kullaniciya artik DUZ DUVAR degil **MISAFIR FORMU** ciziliyor (ad/e-posta/telefon/
  adres + ozet). Onceki hal "giris yapmalisin" + tek butondu ve SSS ile CELISIYORDU.
- **Kart secenegi misafire KAPALI ve NEDENI GORUNUR** ("Kartla odeme icin uye girisi yapman
  gerekiyor"). Sessizce gizlemek, kullaniciya neden secemedigini soylememek olurdu.
- **OLU `co_guest_*` i18n anahtarlari YENI FORMA BAGLANDI** (silinmedi): uc dilde cevirileri
  ZATEN vardi, blok oluydu. Ceviriler kazanildi, olu anahtar kalmadi.
- **Misafir sonuc ekrani**: "Siparislerime git" GOSTERILMIYOR - misafirin oturumu yok, o sayfa
  ona bos/401 verirdi (M11 dersi: hedefteki eylem GERCEKTEN kullanilabilir olmali). Yerine
  siparis numarasi + sahiplenme yonergesi + calisan "Sifre belirle" butonu.
  Misafir oldugu **URL'den okunuyor** (`guest=1`), tahmin edilmiyor.
- SSS metni (tr/en) gercege uyduruldu. **DURUST KAYIT: SSS'in ARAPCA karsiligi YOK** - liste
  `[tr-soru, en-soru, tr-cevap, en-cevap]` bicimindedir ve Arapca FAQ hic tanimli degil. Yani
  "tr/en/ar" kapsaminda gercekte guncellenecek iki dil vardi.

## BU DALGADA OLCULEN VE DUZELTILEN KENDI KUSURUM

Misafir ozeti **"Ara toplam 0 TL"** gosterdi. Kok sebep tarayicida olculdu: sepet kalemi
`{id,size,qty,color}` tutuyor, **FIYAT TASIMIYOR**; ben `it.price` okumustum. Bu dosyada ZATEN
`cartSubtotal()` var ve uye yolu da onu kullaniyor - kendi hesabimi yazmak ayni sayinin iki
yerde ayrismasi demekti. Duzeltildi, tarayicida dogrulandi: 6 x 499,90 = **2.999,40 TL**,
kargo ucretsiz (>= 2000).

## AUTH KOVASI - CANLI OLCULDU (kullanicinin sarti)

Zincir kovaya **SIGIYOR**, takilma YOK:

```
guest-checkout/place -> verify-email -> forgot-password -> reset-password = 4 istek
hepsi "auth" kovasinda; limit 10/dk/IP
canli sonuc: 201 / 200 / 200 / 200  ->  429 GORULMEDI, giris 200
```

**YAN BULGU (yorum duzeltildi):** `GuestCheckoutController` yorumu **"5/dk/IP"** diyordu -
YANLISTI. `Program.cs`'te `authPermitLimit` varsayilani **10** ve example.json da 10 diyor;
hicbir yerde 5'e cekilmiyor. Yorumdan sayi KALDIRILDI (iki yerde duran bir sayi kacinilmaz
olarak ayrisiyor - bu satir ayrismisti). **Kova GEVSETILMEDI.**

## CANLI DOGRULAMA (uctan uca)

```
payment_method=0 (online) -> HTTP 400 "Misafir siparisinde yalnizca kapida odeme..."
payment_method=2 (havale) -> HTTP 400 (ayni)
payment_method=1 (COD)    -> HTTP 201, siparis #90
  orders    #90  status=1 (Confirmed)  payment_type=1  total=549.80
  customer  #37  email_verified=0  verify_token=VAR
  yanitta token YOK, Set-Cookie YOK
YAKALANAN MAILLER (STARTTLS): dogrulama maili (tiklanabilir link) + siparis onayi
  ("Siparisini takip edebilmek icin hesabina bir sifre belirle: ...")
SAHIPLENME ZINCIRI: verify-email 200 -> forgot-password 200 -> reset-password 200 -> giris 200
TARAYICI: misafir formu cizildi, kart secenegi disabled, siparis #91 olustu, sepet bosaldi,
  sonuc ekrani "Sifre belirle -> #/giris" gosterdi
```

## PINLER

`MisafirCheckoutTests` (7, SQL): misafir COD siparisi olusur ve **Confirmed** olur
(+ `payment_type` tasinmis + misafir dogrulanmamis) · COD disindaki yontem **REDDEDILIR**
(Theory: online + havale; mesaj sebebi soyler; **musteri satiri bile olusmaz** - sessizce COD'a
dusurulmedigi kaniti) · misafire **token DONMEZ**, Set-Cookie yok, **oturum satiri olusmaz** ·
misafir checkout'u dogrulama mailini tetikler ve jeton uretilir · onay maili misafire "sifre
belirle" yolunu soyler · **uyeye o satir EKLENMEZ** (cift-anlam kirici) + uye icin takip
baglantisi YINE var (vakum kirici).

`MisafirA3FrontendTests` (5, kaynak sozlesmesi): misafir formu + uc istemcide tanimli ·
kart secenegi kapali ve **nedeni gorunur** · SSS vaadi davranisla uyumlu (vakum kirici: soru
hala duruyor) · olu `co_guest_*` anahtarlari yeni forma baglandi · sonuc ekrani ulasilamayan
yol gostermez.

**KIRILAN PIN YOK.**

## DIS KONTROLU + 5. KONTROL

**DIS:** 6 assert ters -> **ALTI AYRI ISIMLI test kirmizi** (Theory ile 7 vaka). Geri alindi.

**5. KONTROL - iki uretim mutasyonu:**
- **M1** (COD guard'i kaldirildi): misafir online/havale ile **HTTP 201** aldi - yani odenemez
  siparis yeniden acilabilir oldu. Olculen once-durumun kapisi.
- **M2** (`payment_method` PlaceOrder'a tasinmadi): COD secilmis olmasina ragmen siparis
  **`status=0x00` (Pending)** kaldi - misafir siparisinin sonsuza kadar asili kalmasinin
  BIREBIR aynisi.
Ikisi de geri alindi.

## YEREL DOGRULAMA

276/276 `Category=Sql` · tam suitte **442 basarili / 445** (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`) · Release 0 hata · whitespace + style **exit 0**.

---

# DALGA B - OPERASYON YUZEYI (TAMAMLANDI)

Amac: site acildiginda gelen siparisi YONETEBILMEK. Admin panelinin sekiz ekranindan besi
(dashboard, orders, returns, shipments, coupons) BUGUNE KADAR HIC ACILMAMISTI. Acilinca
**uc ayri kusur sinifi** cikti; hepsi canli olculdu ve dordu kullanici karariyla duzeltildi.

## OLCUM DUZENEGI

API `:5000` (ayrik surec), panel `:5173` (statik sunucu, depo disinda), gercek admin hesabi
(register/verify/login zinciri + `user_type=1`). Mail kaniti icin Dalga A'nin STARTTLS
yakalayicisi yeniden kullanildi (scratchpad; `MailSettings` gecici olarak ona yonlendirildi,
is bitince GERI ALINDI - dosya zaten gitignore'lu). Olcumler gercek dev veritabaninda
(52 siparis, 35 musteri) yapildi.

## B1 - KUPON EKRANI: BULGU UC PARCAYDI

Kapsama denetimi tek bir alan adi uyusmazligi gormustu; ekran acilinca **uc** cikti.

| # | Bulgu | OLCULEN ZARAR (canli, panelden) |
|---|---|---|
| a | Ekleme `discount_value` gonderiyor, `CouponAddRequestDto` alani `value` | Operator %30 girdi -> DB `value=.00`, uc **HTTP 201**, panel **"Kupon eklendi"**, musteri 1000 TL sepette **"Kupon gecerli." + discount_amount 0.00**. HER KATMAN BASARILI DIYORDU. |
| b | Liste `c.discount_value` okuyor | Her satirda **"-"** |
| c | Liste `discount_type`i SAYIYLA karsilastiriyor; uc ENUM ADINI (metin) doner | `"Percentage"==0` ve `==1` ikisi de false -> ucuncu dala dusuyor, **HER kupon "Kargo" gorunuyordu**. Eksik bilgi degil **YANLIS** bilgi: yuzde kuponu ucretsiz-kargo kuponu gibi okunuyordu. |

**SESSIZLIGIN SEBEBI:** bilinmeyen bir JSON alani model binding tarafindan sessizce ATILIR.
DB'de canli kanit vardi: `E2TEST` ve `PANELDEN30` satirlari `value=.00`.

**DUZELTME.** `value` gonderiliyor; `is_active` KALDIRILDI (DTO'da yok, sunucu ekleme aninda
zaten true yaziyor). Liste icin TEK MERKEZ: `KUPON_TIPI` / `kuponTipEtiket` / `kuponDegerMetni` -
hem SAYI hem METIN gosterimini tanir (ekleme ucu sayi bekler, liste ucu metin doner).
Listeye **"Kullanım"** sutunu eklendi (`used_count / usage_limit`, limitsizse `∞`).
**Sifir deger GIZLENMIYOR:** `%0` yaziliyor - bozuk kupon operatore GORUNMELI.
Giris kapisi: yuzde/sabit kuponda deger `<= 0` ve yuzde `> 100` reddediliyor (sunucu 0'i
reddetmiyor, yalniz negatif ve %100 ustunu).

**SONRA (canli, panel formundan):**
```
girdi   : %30, min 200
liste   : DALGAB30 | Yüzde | %30 | ₺200,00 | 0 / ∞
DB      : value = 30.00
musteri : 1000 TL sepet -> discount_amount = 300      (once: 0)
kapilar : deger 0 -> reddedildi · %150 -> reddedildi
```

## B2 - BES EKRAN TURU

| Ekran | Durum | Kanit |
|---|---|---|
| Panel (dashboard) | **CALISIYOR** | 6 kart + 2 grafik + 2 tablo, hepsi gercek veri (ciro ₺20.144,61 / 52 siparis / 35 musteri / 5 stok uyarisi). Alan adlarinin tamami ortusuyor. |
| Siparisler | **LISTE OLUYDU -> DUZELTILDI** | asagi |
| Iadeler | **CALISIYOR** | liste + onay + ret, ucu de canli suruldu |
| Kargo | **CALISIYOR (form)** | kargo olusturuldu, sorgu dondu. **Liste YOK - kor form** (asagi, supheli) |
| Kuponlar | **BOZUKTU -> DUZELTILDI** | B1 |

### SIPARIS LISTESI - PANEL KENDI KENDISIYLE CELISIYORDU

Ayni oturumda **Panel sekmesi "SIPARIS 52"** derken **Siparisler sekmesi "Siparis yok"**
diyordu. Tarayicida olculdu:

```
zarfAlanlari      : ["items","totalCount","page","size","totalPages"]
d.Items (panel)   : UNDEFINED        d.items      : 50
d.TotalCount      : UNDEFINED        d.totalCount : 52
```

**KOK SEBEP:** `GetAllForAdmin`, repository katmaninin kendi tipini
(`Core.Utilities.Dtos.PagedResult<T>`) DOGRUDAN HTTP yanitina koyuyordu. O tipin KENDI yorumu
"repository katmanindan doner, servis DTO'ya cevirir" diyor - yani sizinti bilincli bir tasarim
degil, ATLANMIS bir donusum. PascalCase ozellikler camelCase'e serilesiyor, oysa deponun DIGER
sayfali uclari (`product/filter`, admin urun listesi) snake_case zarf donuyor.
**AYNI API'DE IKI KONVANSIYON.**

**KULLANICI KARARI: sunucu tarafi.** Yeni `AdminOrderPagingListResponseDto`
(`ProductPagingListResponseDto` kalibinin birebir esi) - yeni bir kalip uydurulmadi, var olan
konvansiyona hizalandi. Panel `items` / `total_count` okuyor.

**SONRA:** `Siparisler (54)`, 50 satir, gercek siparis numaralari, Turkce durum etiketleri.

### SESSIZ CATCH KALDIRILDI

`allOrders(...).catch(()=>({Items:[]}))` **401/403/500 dahil HER hatayi yutup BOS TABLO**
ciziyordu - "hic siparis yok" ile "uc patladi" AYIRT EDILEMIYORDU. Kaldirildi; hata artik
`render()`'in ortak hata daline dusuyor ve gorunuyor.

### 403 DALI EKLENDI

`render()` yalniz 401 taniyordu. Bayat/baska hesaba ait bir token localStorage'da kalinca uclar
403 doner ve panel **"Veri alinamadi" yazip KILITLI kaliyordu** - bu oturumda birebir yasandi.
Iki durum da ayni sey demek: elindeki token bu panel icin GECERSIZ. Artik token temizlenip
yeniden girise yonlendiriliyor.

### URUN EKRANI: EKLEME VE GUNCELLEME OLUYDU (sinif taramasinin cikardigi)

Panel `stocks` ve `color_hex` gondermiyordu - ikisi de ZORUNLU. Operatore ham cerceve mesaji
dusuyordu: **"The stocks field is required."** ve formda o alan YOKTU. Ikinci deneme
`stocks:[]` ile "The color_hex field is required." dedi. Panelden urun eklemek/duzenlemek
MUMKUN DEGILDI.

**KULLANICI KARARI: eksik alanlar forma eklensin.** Forma eklenenler: Kategori (ID kutusu ->
**acilir liste**), Indirimli fiyat, Ustu cizili fiyat, Renk (hex), Urun tipi, **Beden/stok
satirlari** (ekle/kaldir). Giris kapilari Turkce ve duzeltilebilir.

**GUNCELLEMEDE SESSIZ VERI KAYBI - AYRI VE DAHA AGIR TUZAK.** `ProductManager.Update`
TAM-VARLIK map yapar (`_mapper.Map(dto, product)`): DTO'da BULUNAN ama gonderilmeyen her alan
varsayilanina duser ve MEVCUT DEGERI EZER. Eski form 5 alan gonderiyordu; calissaydi bile
`old_price` / `sale_price` / `sub_category_id` SILINIR, `product_type` sessizce Clothing'e
donerdi. Bu yuzden DUZENLEME formu artik urunun GERCEK halini `GET /api/product/get/{id}`'den
yukluyor ve hepsini geri gonderiyor.

`sale_price` o uctan DONMUYORDU; `ProductDetailResponseDto`'ya eklendi. **SIZINTI
DEGERLENDIRILDI:** uc `[AllowAnonymous]`, ama `sale_start`/`sale_end` depoda HICBIR kod
yoluyla yazilmiyor (tarandi - yalniz `PricingHelper` okuyor), dolayisiyla `IsOnSale`
`salePrice>0` iken HER ZAMAN true doner: `sale_price` zaten musterinin ODEDIGI fiyattir ve
listeleme uclarindan gorunur. **Ileride ZAMANLI indirim eklenirse burasi yeniden
degerlendirilmelidir** (gerekce DTO'nun icinde yazili).

## URETIM HATASI - FORM CALISIR HALE GELINCE ORTAYA CIKTI (VERI-BOZAN)

Ilk gercek guncelleme denemesi **HTTP 500** verdi:

```
Cannot insert duplicate key row in object 'dbo.product_stocks' with unique index
'IX_product_stocks_product_id_size'. The duplicate key value is (123, S).
```

`IX_product_stocks_product_id_size` **UNIQUE ve FILTRESIZ** (`is_active` ICERMEZ). Eski kod
tum beden satirlarini `is_active=false` yapip gelenleri **YENI SATIR** olarak ekliyordu; bir
satiri pasiflemek `(product_id, size)` ciftini SERBEST BIRAKMAZ.

**IKI AYRI SEKILDE BOZUKTU:**
1. **HER guncelleme 500 verir.** Ustelik `Update` TRANSACTION'SIZ: pasifleme ZATEN
   KAYDEDILMIS, insert patliyor -> urun **TUM AKTIF BEDEN SATIRLARINI KAYBEDIYOR** ve satin
   ALINAMAZ hale geliyor. Operator yalnizca "Istek basarisiz (500)" goruyor. (Urun 123'te
   birebir yasandi: iki satir da `is_active=0` kaldi, urun adi ise DEGISMISTI - kismi yazim.)
2. **Insert basarili olsaydi daha SESSIZ bir zarar olurdu:** yeni satir `reserved_quantity=0`
   ile baslar. O anda sepetlerde tutulan rezervasyonlarin muhasebesi SIFIRLANIR,
   `available = stock_quantity - reserved_quantity` kimligi bozulur ve ayni mal iki kez
   satilabilir.

**BU YOL BUGUNE KADAR ULASILAMAZDI:** panel `stocks` gondermiyordu (dogrulamaya takiliyordu)
ve CSV ice-aktarma yalnizca EKLIYOR.

**DUZELTME - UPSERT:** satir KIMLIGI korunur (dolayisiyla `reserved_quantity` de), listede
olmayan beden PASIFLENIR (silinmez - siparis/rezervasyon gecmisi durur), yalnizca GERCEKTEN
yeni olan beden INSERT edilir. Once pasiflenmis bir beden geri gonderilirse YENIDEN ACILIR.
Ayni beden iki kez gelirse ONDEN reddedilir (aksi halde ayni yarim-durum olusurdu);
karsilastirma **OrdinalIgnoreCase** - veritabani indeksi `Turkish_CI_AS` altinda
buyuk/kucuk harf DUYARSIZ eslesir (bolum 6c), C# tarafinda Ordinal kullanmak "S" ve "s"yi
farkli sanip ayni cakismayi yeniden uretirdi.

**HASAR URETIM YOLUYLA ONARILDI** (elle SQL YOK): duzeltilmis `Update` bir kez kosturuldu ->
satir 326/327 `is_active` 0 -> 1, miktarlar 4/7, yeni satir yazilmadi.
**"YALNIZCA ADI DEGISTIR" SINAVI:** form acildi, sadece ad degistirildi, kaydedildi ->
`sale_price 249.90` · `old_price 399.90` · `color_hex #7733cc` · `product_type 0` · iki beden
satiri ve **SATIR KIMLIKLERI** aynen korundu.

## B3 - IADE ISLEME UCTAN UCA

**ONAY** (iade #1, siparis 32, kapida odeme -> magaza kredisi), **panelden tiklandi**:

| Yan etki | Once | Sonra |
|---|---|---|
| `return_requests.status` | 0 Pending | **3 Completed** + `processed_at` |
| `orders.refunded_amount` | 0,00 | **499,90** |
| stok (urun 2 / M) | 1 | **2** |
| `customers.store_credit` | 0,00 | **499,90** |
| `store_credit_transactions` | 0 satir | **1 satir** (order_id 32) |
| `stock_movements(ref=32)` | 1 satir | **2 satir** (type 1 = In, +1) |

**RET** (iade #2, siparis 93), panelden: durum **2 Rejected**, `admin_note` yazildi,
`processed_at` damgalandi - ve **hicbir sayac kipirdamadi**: `refunded_amount` 0,00, stok 1,
kredi 0,00, defter 0 satir, hareket 1 satir. Musteri ret notunu `return/my`'de goruyor.

`refund_id` NULL kaldi ve bu DOGRU: o alan Iyzico iade kimligidir, magaza-kredisi yolunda
karsiligi yok. (Kusur SANILMASIN diye kayda geciyor.)

### BOSLUK: MUSTERIYE HICBIR BILDIRIM GITMIYORDU

`ReturnManager`'da mail/outbox/bildirim **SIFIR referans** (tarandi). Admin iadeyi onaylayip
499,90 TL kredi yazsa da, ya da reddetse de musteriye HICBIR SEY gitmiyordu.

**KULLANICI KARARI: e-posta eklensin.** Kanal Dalga A'nin `EmailNotification` outbox'i.
**ONAY YOLUNDA MESAJ TRANSACTION ICINDE yaziliyor** (Sprint 8 madde 3 kalibi): rollback olursa
mail de yazilmamis olur - "iaden onaylandi" maili alip iadesi geri alinmis musteri OLUSAMAZ.
Ret yolunda sira ONCE KALICI SONRA BILDIR (tersi, kaydedilemeyen bir ret icin mail gonderirdi).
Tutar nereye gitti UYDURULMUYOR: `RefundOutcome` zaten `OnlineRefunded` / `CreditRefunded`
ayrimini tasiyor. Urun pasiflenmisse UYDURMA ad yazilmaz, kimlikle gosterilir.

**YAKALANAN GERCEK MAILLER (STARTTLS):**
```
Subject: Divisima - Iade talebin onaylandi
  Iade talebin onaylandı.
  E4a Test Urun · L · 1 adet
  499,90 TL mağaza kredine yatırıldı; sonraki siparişinde kullanabilirsin.
  Ayrıntı için:
  http://localhost:5173/#/hesabim/iadelerim

Subject: Divisima - Iade talebin hakkinda
  İade talebin değerlendirildi ve onaylanmadı.
  E4a Test Urun · L · 1 adet
  Değerlendirme notu: Iade suresi disinda kalan kalem - Dalga B ret turu.
```

## B4 - KARGO (ELLE TAKIP NUMARASI - IS KARARI: ENTEGRASYON YOK)

Backend ZATEN yeterliydi: `CreateShipment` idempotent, `OrderStatusMachine` ile dogrulanmis,
zaman cizelgesine yaziyor, bildirim tetikliyor. Eksik olan MUSTERI TARAFIYDI.

**OLCULEN UC BOSLUK:**
- `OrderDetailResponseDto`'da **kargo alani YOK** - musteri takip numarasini goremiyordu
- Storefront `shipment.track`'i **HIC cagirmiyordu** (index.html 0, api-bridge 0)
- `NotifyStatusChangeAsync` in-app + push + SMS gonderiyor, **e-posta YOK** ve mesajda
  **takip no YOK** ("Siparisiniz kargoya verildi. Siparis no: X"). Uc kanal da yapilandirilmis
  saglayici ister; gerceklikte musteriye HICBIR SEY ulasmiyordu.

### `Shipping:Enabled=false` SAHTE DURUM YAZIYORDU (H53'un GORMEDIGI YARISI)

Kapali dal `Success=true` + `NormalizedStatus=1` (InTransit) + `RawStatusText="Takip devre
disi (dev)"` donuyordu; cagiran `Success=true` gorunce kaydi GUNCELLIYOR - yani bu deger
**VERITABANINA YAZILIYORDU**. Canli olculdu:

```
admin kargoyu olusturdu   -> shipments.status = 0 (Preparing)
musteri BIR KEZ track cagirdi
DB'deki satir             -> status = 1 (InTransit)
                             last_status_text = "Takip devre disi (dev)"
```

Paketi kimse tasimadi; durum uyduruldu ve bir GELISTIRICI DIZGESI hem musteriye hem admin
paneline servis edilir hale geldi. H53 ayni kusuru `Enabled=true` dali icin duzeltmisti;
**FALSE dali atlanmisti - ustelik LAUNCH YAPILANDIRMASI o.**

**KONTROLLU A/B (ayni akis, ayni ayar, tek fark duzeltme):**
```
siparis 93 (duzeltme ONCESI) : status 1 (InTransit)   last_status_text "Takip devre disi (dev)"
siparis 94 (duzeltme SONRASI): status 0 (Hazirlaniyor) last_status_text NULL   last_checked_at NULL
```

### YAPILAN
- Saglayicinin kapali dali `Success=false` doner -> cagiran kaydi GUNCELLEMEZ, saklanan gercek
  durum korunur.
- `IOrderNotificationService.NotifyStatusChangeAsync` += `kargoFirmasi` / `takipNo`
  (opsiyonel). **Takip satiri YALNIZ gercekten biliniyorsa yazilir** - `ChangeOrderStatus`
  kargo kaydini gormez, oradan null gecer; uydurma numara ya da bos "Takip no:" satiri URETILMEZ.
- E-posta kanali eklendi (outbox). **Mevcut try/catch'in DISINDA** - o catch dis saglayici
  entegrasyonlarinin hatasini yutmak icin var; outbox yazimini da yutmak "mail hic yazilmadi
  ve kimse bilmiyor" demek olurdu.
- **GORUNTU ADLARI TURKCELESTI:** `carrier_name` "Mng" -> "MNG Kargo", `status_name`
  "Preparing" -> "Hazırlanıyor". Bunlar KIMLIK degil GORUNTU dizgeleridir (bolum 6c);
  programatik kullanim icin DTO zaten `carrier` ve `status` byte'larini tasiyor.
- Storefront siparis detayina **Kargo blogu**: firma + takip no + durum. Kargo kaydi yoksa uc
  404 doner (NORMAL durum) ve blok HIC cizilmez - detay ekrani bozulmaz.
- **Musteri zaman cizelgesindeki not TURKCELESTI**: `"Durum güncellendi: Preparing"` ->
  `"Durum güncellendi: Hazırlanıyor"`. Bu not musteriye gorunuyor (order/timeline) ve ham enum
  adi basiyordu.

**YAKALANAN MAIL:**
```
Subject: Divisima - Siparisin kargoya verildi
  DVS20260824-B9607F498A numaralı siparişin kargoya verildi.
  Kargo firması: MNG Kargo
  Takip numarası: MNG555444333
  Siparişini buradan takip edebilirsin:
  http://localhost:5173/#/hesabim/siparislerim
```

**MUSTERI EKRANI (canli, Hesabim > Siparislerim > detay):**
```
Kargo
MNG Kargo · Takip no: MNG555444333 · Hazırlanıyor
```

## B5 - HAVALE/EFT: BACKEND TAM CALISIYOR, IKI UCTA DA YUZEY YOK

`POST /api/order/confirm-manual-payment/{id}` canli suruldu (siparis 94):

```
ONCE : status 0 Pending · is_online_payment_done 0 · rezervasyon Active · fatura 0 · puan 0
SONRA: status 1 Confirmed · is_online_payment_done 1 · rezervasyon Confirmed
       stok 1 -> 0 · fatura DIV-2026-000094 (Sent) · puan 54
       timeline "Havale/EFT odemesi onaylandi"
```

Dalga 2'nin B10 duzeltmesi burada da calisiyor: dort yan etkinin dordu de uygulandi.

**AMA:** panelde onay butonu YOK (`confirm-manual-payment` -> admin.html 0, api-client 0) ve
storefront havale secenegi SUNMUYOR (`payment_method` yalniz 0 veya 1).

**KULLANICI KARARI: UYKUDA KALSIN.** Backend calisir halde durur, yuzey BILINCLI olarak
acilmaz. Hicbir kod degismedi. Acilmasi istenirse UC parca birden gerekir: storefront'ta
havale secenegi + musteriye IBAN/hesap bilgisi gosterimi + panelde onay butonu.

## PINLER

**`AdminPanelSozlesmeTests` (8, kaynak sozlesmesi):**
- kupon ekleme govdesi DTO ile ortusur, `discount_value` ARTIK YOK (+ vakum kirici: tarama
  gercekten alan bulmus olmali)
- kupon listesi `value` okur ve tipi METIN olarak cozer (+ cift-anlam kirici: merkez
  `Percentage`/`Fixed`/`FreeShipping` adlarini GERCEKTEN taniyor olmali; sayisal karsilastirma
  geri gelemez)
- **SINIF DUZEYI TARAMA** (Theory x3): hicbir admin yazma ekrani DTO'da olmayan alan gondermez
- **KAPSAM PINI**: govde gonderen HER admin yazma cagrisi taramanin icinde olmali - yeni ekran
  eklenip listeye yazilmazsa KIRILIR (tarama sessizce eskiyemez)
- urun formu zorunlu alanlari gonderir (`color_hex`, `stocks`) + tam-varlik map'in ezecegi
  ucunu de (`sale_price`, `old_price`, `product_type`)
- siparis zarfi TEK KONVANSIYON: sunucu DTO'su `ProductPagingListResponseDto` ile BIREBIR ayni
  alan setine sahip + panel `res.items`/`res.total_count` okur + sessiz catch geri gelmez

**`DalgaBOperasyonTests` (7, SQL + HTTP):**
- admin siparis listesi snake_case zarf doner (+ vakum kirici: liste GERCEKTEN dolu;
  + cift-anlam kirici: `totalCount`/`totalPages` ARTIK YOK - ikisi birden donseydi panel
  calisirdi ama iki konvansiyon yasamaya devam ederdi)
- urun guncelleme ayni beden tekrar gonderilince 500 VERMEZ, satir KIMLIGI ve
  **`reserved_quantity` KORUNUR** (+ vakum kirici: miktar gercekten degisir)
- gonderilmeyen beden PASIFLENIR, satir SILINMEZ (+ cift-anlam kirici: gonderilen beden AKTIF
  kalir - "hepsini pasifle" uygulamasi gecemez)
- iade onayi musteriye mail yazar, tutari VE nereye gittigini soyler (+ vakum kirici: kredi
  gercekten yatmis olmali)
- iade reddi mail yazar + admin notu tasir (+ cift-anlam kirici: HICBIR para/stok hareketi
  olmaz - yalnizca maile bakan bir pin, yanlislikla iade de yapan uygulamayi yesil gosterirdi)
- kargo olusturulunca mail takip numarasini ve firmayi (GORUNTU adiyla) tasir
- **entegrasyon kapaliyken sahte durum YAZILMAZ**: yanit da veritabani da adminin biraktigi
  hali tasir, `last_checked_at` damgalanmaz (+ vakum kirici: uc gercekten kargo kaydini donmus
  olmali)

**KIRILAN PIN YOK.**

**PIN SINIRI (Dalga 4 ve Dalga A'daki AYNI durust kayit):** depoda JS/DOM kosucusu YOK;
frontend pinleri KAYNAK SOZLESMESINI tutar, tarayici semantigini degil. Davranis kaniti
yukaridaki canli olcumlerde ve uc duzeyindeki `DalgaBOperasyonTests`te.

## DIS KONTROLU + 5. KONTROL

**DIS:** 6 assert ters cevrildi (ALTI AYRI test, IKI ayri sinif) -> **6 AYRI ISIMLI KIRMIZI**.
Geri alindi, 15/15 yesil.

**5. KONTROL - DORT URETIM MUTASYONU** (farkli testleri vurduklari icin ayristirilabilir):

| Mutasyon | Kirilan pin | BULUNAN | Olculen once-durum |
|---|---|---|---|
| M1 kupon alani `discount_value`'ya donduruldu | `KuponEkleme...` + sinif taramasi (saveCoupon) | alan DTO'da YOK | %30 kupon 0 indirimle kaydediliyordu |
| M2 zarf `PagedResult`'a donduruldu | `AdminSiparisListesi_SNAKE_CASE...` | `total_count` **YOK** | panel "Siparis yok" gosteriyordu |
| M3 upsert "pasifle+ekle"ye donduruldu | `UrunGuncelleme_..._500_VERMEZ...` + `..._PASIFLENIR...` | **HTTP 500** `/api/product/update` | canli 500 + urun tum bedenlerini kaybetti |
| M4 saglayici kapali dali sahteye donduruldu | `KargoTakibi_..._SAHTE_DURUM_YAZMAZ` | `status` **0x01** (beklenen 0x00) | siparis 93'te olculen tablo |

Dordu de geri alindi; 15/15 yesil.

## YEREL DOGRULAMA

283/283 `Category=Sql` · tam suitte **457 basarili / 460** (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`) · Release 0 hata · whitespace + style **exit 0**.

## SUPHELI / DEFTERE (DUZELTILMEDI - KARAR KULLANICININ)

- **`ProductManager.Update` TRANSACTION'SIZ.** Upsert, eski "pasifle+ekle" kalibina gore cok
  daha guvenli (ve on-kontrol tekrar eden bedeni reddediyor), ama dongu ortasinda bir DB
  hatasi olursa bazi bedenler guncellenmis bazilari kalmis olur. Yeniden gondermekle
  duzelir; yine de atomik degil. `ProductManager`'da `IUnitOfWork` YOK - eklemek ayri bir is.
- **120 YETIM `product_stocks` SATIRI.** Dalga 3'un performans seed'i urunleri silmis, stok
  satirlarini BIRAKMIS (`products` 2 satir, yetim stok 120). FK yok. Dashboard'un dusuk-stok
  sorgusu urunlere join yaptigi icin sayilari SISIRMIYOR (olculdu: 5), ama temizlenmemis veri.
- **KARGO EKRANI KOR FORM.** "Kargo Olustur" siparis ID'sini ELLE istiyor; kargolanmayi
  bekleyen siparislerin listesi YOK. Operator hangi siparisi kargolayacagini baska ekrandan
  bulup ID'yi kopyalamak zorunda.
- **SIPARIS 93'UN DEV-DB ARTIGI.** Duzeltme oncesi yazilmis sahte kargo durumu (InTransit +
  "Takip devre disi (dev)") ve Ingilizce timeline notlari o satirlarda DURUYOR. Yeni siparisler
  (94) temiz. Gelistirme veritabani; temizlik AYRI bir karar.

## SURECTE YASANAN (kayit - uc ders)

- **API KOSARKEN MUTASYON KONTROLU YAPILAMAZ.** CLAUDE.md'de yazili tuzak birebir yasandi:
  `Divisima.API.exe` ayakta oldugu icin `Divisima.Bussiness.dll` kopyalanamadi ve build
  **MSB3027 ile KIRILDI**. Iyi tarafi: sessizce eski ikililerle kosmadi, GURULTULU dustu.
  Sureci durdurup tekrarlandi.
- **PIN KENDI ACIKLAMA YORUMUNA TAKILDI.** "Sessiz catch geri gelmesin" pini duz metin olarak
  `".catch(()=>"` ariyordu ve kaldirilmis kalibi ALINTILAYAN kendi yorumumu buldu - yanlis
  kirmizi. Arama CAGRI YERINE bakacak sekilde daraltildi
  (`allOrders\s*\([^)]*\)\s*\.catch`). **DERS: kaynak tarayan bir pin, kendi belgeledigi
  kalibi da tarar.**
- **`perl` degistirmesi SESSIZCE ESLESMEYEBILIR.** 5. kontrol mutasyonlarindan biri
  (`mail.Body...` vs gercekteki `mail!.Body...`) desen tutmadigi icin UYGULANMADI ve o tur
  yalnizca 4 kirmizi verdi; fark edilip duzeltildi. **DERS: her mutasyondan sonra degisikligin
  GERCEKTEN uygulandigi grep ile dogrulanir.**


---

# DALGA C - YAYIN ALTYAPISI (TAMAMLANDI)

Dalga A "ilk musteri zinciri"ni, Dalga B "gelen siparisi yonetebilme"yi kapatti. Dalga C'nin
sorusu farkli: **depo bugun yayina cikabilir mi?** Alti kalemin ortak ozelligi, uygulamanin
CALISMASI degil YAYINLANABILMESIYDI.

## C1 - STOREFRONT'U KIMSE SUNMUYORDU

```
Dockerfile           : yalniz Divisima.API publish ediliyor; frontend/ HIC kopyalanmiyor
docker-compose.yml   : sqlserver + redis + api   (frontend servisi YOK, nginx YOK)
ops/infra/nginx.conf : TEK server block -> server_name api.divisima.com
                       divisima.com icin HICBIR TANIM YOK
Divisima.API/wwwroot : yalniz uploads/products   (frontend dosyasi yok)
```

Yani "dosyalari bir yere kopyala" adimi depoda **yazisiz** kaliyordu.

**KULLANICI KARARI: nginx + ayri statik konteyner (iki origin KORUNUR).** Gerekce olcumden
geldi: depo ZATEN iki origin varsayiyor ve bunu **uc yerde** belgeliyor -
`AllowedOrigins: ["https://divisima.com", ...]`, `Storefront:BaseUrl = https://divisima.com`,
ve Sprint 8 madde 6'da eklenen `Cookies:Domain = .divisima.com` (gerekcesi birebir:
*"storefront (divisima.com) ile API (api.divisima.com) FARKLI HOSTLAR"*). API'nin
wwwroot'undan sunmak bir bosluk doldurma degil, **bu ucunu birden geri alma** olurdu.

YAPILAN:
- `ops/infra/nginx.conf`'a `divisima.com` server blogu: statik kok, SPA fallback
  (`try_files ... /index.html` - hash router), `/sitemap.xml` proxy'si, `admin.html` icin
  `X-Robots-Tag: noindex`, kod tasiyan dosyalarda `no-cache`.
- `docker-compose.yml`'a `frontend` servisi (nginx:alpine, `./frontend` **salt-okur** mount)
  + `ops/infra/frontend-dev.conf`. Yerelde bugune kadar elle kurulan duzen artik
  `docker compose up` ile geliyor.
- `set-api-origin.sh` DAGITIM adimi olarak AYNEN kaldi; checklist'e sira sarti yazildi
  (once betik, sonra kopyalama - tersi olursa storefront localhost'a bakar).

## C2 - YUKLENEN GORSELLER KONTEYNER DEGISINCE KAYBOLUYORDU

Compose'da `mssql_data` ve `redis_data` vardi, **yuklemeler icin volume yoktu**.
`LocalImageStorage` dosyalari `WebRootPath/uploads/products` altina yazar (Sprint 8 madde 4),
yani konteynerin YAZILABILIR KATMANINA -> yeni surumde `product_images` satirlari var olmayan
dosyalari gosterir (vitrinde kalici 404).

**KRITIK SORU VARSAYILMADI, OLCULDU** (non-root konteynerde volume yazilabilir mi):

```
dotnet publish ciktisinda wwwroot VAR -> 77 dosya (dev'de yuklenmis gorseller)
Dockerfile: COPY --from=build ... ; chown -R divisima:divisima /app ; USER divisima
=> /app/wwwroot/uploads imajda VAR ve divisima:divisima sahipli
=> adlandirilmis volume ilk mount'ta imaj icerigini VE SAHIPLIGI devralir -> yazilabilir
```

**AMA `.dockerignore` DUZELTMESI BU ZINCIRI KIRIYORDU** (uygularken fark edildi): dev
gorsellerini build context'inden dislamak -> publish **bos dizini kopyalamaz** -> imajda
`/app/wwwroot/uploads` YOK -> Docker volume'u **root:root** olusturur -> `USER divisima`
YAZAMAZ. Bu yuzden Dockerfile'a `RUN mkdir -p /app/wwwroot/uploads/products` eklendi ve
**chown'dan ONCE** konumlandirildi. Sira pinlendi (indeks karsilastirmasiyla).

## C3 - ILK ADMIN: SIFRE POLITIKASININ BESINCI VE GOZDEN KACMIS GIRISI

`AdminSeeder.SeedAsync` acilista ZATEN cagriliyordu (idempotent, `Enabled=false` varsayilan).
Iki bulgu:

- **`SifrePolitikasi` UYGULANMIYORDU.** A2-FIX (SUPHELI #21) kurali TEK MERKEZE tasimis ve
  DORT girise baglamisti (kayit, satici kaydi, sifre degistirme, sifre sifirlama). Bu
  **besincisiydi**: sistemin EN YETKILI hesabi, kayit ucunun reddedecegi `abc` gibi bir
  sifreyle acilabiliyordu.
- Tohumlama hatasi yalniz loglaniyor (uygulama devam ediyor) - yanlis yapilandirilmis bir
  `AdminSeed` **sessiz** kaliyordu.

**FAIL-FAST SECILMEDI - GEREKCE:** `AdminSeed` tek seferlik bir ONYUKLEME bayragidir; yanlis
yazilmis bir sifre yuzunden uygulamanin acilmamasi **siteyi tumden indirir**. Dogru davranis
"admini OLUSTURMA ve GURULTULU soyle". Mesaj IHLAL EDILEN KURALI adiyla yaziyor.

**TEMIZ VERITABANINDA UCTAN UCA SURULDU** (`DivisimaSeedTest`, migration'la kuruldu, is bitince
DROP edildi):

```
ZAYIF SIFRE ("abc")
  API acildi: 200            <- bayrak siteyi INDIRMEDI
  log: [ERR] AdminSeed sifresi POLITIKAYA UYMUYOR, ilk admin OLUSTURULMADI:
       Şifre en az 8 karakter olmalı. (AdminSeed:Password duzeltilip ... )
  DB : ADMIN_SAYI 0   MUSTERI_SAYI 0

GECERLI SIFRE
  log: [INF] İlk admin oluşturuldu (ilk.admin@divisima.com).
  DB : id=1  user_type=1  email_verified=1  is_active=1  phone dolu
  GERCEK GIRIS: HTTP 200, token uretildi, customer_id=1

IKINCI ACILIS (FARKLI e-posta ile)
  log: [INF] Admin zaten mevcut - tohumlama atlandı.
  DB : ADMIN_SAYI 1  TUM_MUSTERI 1  -> e-posta ILK acilistaki
```

## C4 - ARKA PLAN IS HATALARI KIMSEYE GORUNMUYORDU

```
Uretimde kosan recurring is : 7 adet (outbox islemcisi, veri saklama, rezervasyon temizligi,
                              terk sepet, dogum gunu, win-back, yorum daveti)
Hangfire panosu : filtre "authenticated + user_type=1" ister
                  AMA uygulamada TEK kimlik semasi JwtBearer (AddCookie YOK)
                  tarayici /hangfire'a giderken Authorization basligi GONDERMEZ
                  => IsAuthenticated HER ZAMAN false => HERKESE KAPALI
                  (ustelik nginx'te ikinci kilit: allow 10.0.0.0/8)
Outbox/is durumu donen admin ucu : SIFIR (tarandi)
```

`ops/deployment-checklist.md`'deki *"Hangfire dashboard yetkilendirme (yalniz admin - su an
acik!)"* maddesi **BAYATTI** - filtre var ve kapali; sorun tersineydi. Madde duzeltildi.

**KULLANICI KARARI: salt-okur admin ucu** (Hangfire panosunu acmak yerine). Gerekce olcumden:
`DataRetentionJob` YALNIZCA `status=1` (Processed) mesajlari siliyor, **`Failed` olanlar
KALICI**. Yani basarisiz arka plan isinin dayanikli kaydi ZATEN veritabaninda - gostermek icin
yeni depolama ya da yeni bir kimlik yuzeyi (cerez semasi) acmaya gerek yok.

- `GET /api/dashboard/failed-jobs` (`DashboardController`, sinif duzeyinde `RequireUserType(Admin)`)
- **`payload` BILINCLI OLARAK DISARIDA**: mesaj govdesi e-posta adresi/jeton/siparis ayrintisi
  tasir ve operatorun sorusuna ("hangi is, kac denemede, hangi hatayla") gerekli DEGILDIR.
  Hata metni ayrica `KanitMaskesi`'nden gecirilir (bolum 1).
- Panel: **Panel sekmesine** konuldu (ayri sekmede gozden kacardi). Canli teyit - Dalga A'nin
  SMTP turundan kalan GERCEK basarisiz mesaji buldu:
  `18 | OrderPlaced | 5 | Hedef makine ... reddettiginden baglanti kurulamadi. | 23.08.2026`

**LOG SAKLAMA:** `WriteTo.File(..., rollingInterval: Day)` disinda sinir YOKTU.
Serilog.Sinks.File 5.0.0 varsayilanlari `fileSizeLimitBytes=1GB`, **`rollOnFileSizeLimit=false`**,
`retainedFileCountLimit=31`. Tehlikeli olan ikincisiydi: bir gunun dosyasi 1 GB'a ulasinca sink
**yazmayi SESSIZCE birakir** - yani en cok log ureten (en cok sorun yasanan) gunde loglar tam da
ihtiyac duyuldugu anda kesilir. Degerler ACIKCA yazildi: gunluk + **100 MB'da parcala** +
30 dosya. (`DataRetentionJob` bu bosluğu kapatmiyor - o yalniz VERITABANINI temizler.)

## C5 - PAYLASIM ONIZLEMESI ve SITEMAP ZINCIRI

```
frontend/robots.txt      : VAR, "Sitemap: https://divisima.com/sitemap.xml" diyor
GET /api/seo/sitemap     : VAR ve gercek sitemap uretiyor (urun + kategori)
divisima.com/sitemap.xml : bunu SUNAN hicbir sey YOK   <- zincirin ORTA halkasi kopuk (C1)
og:type/site_name/title/description/locale : VAR
og:image, og:url                            : YOK
twitter:card                                : "summary_large_image" (GENIS gorsel VAAT ediyor,
                                               hicbir gorsel VERMIYOR -> bos kutu)
Organization schema logo : https://divisima.com/logo.png  <- depoda BOYLE BIR DOSYA YOK
```

YAPILAN: `og:image` (mutlak URL, `icons/icon-512.png` - **depoda var oldugu pinde dogrulaniyor**),
`og:url`, `og:image:width/height/alt`. **`twitter:card` "summary"ye CEKILDI**: elimizdeki varlik
512x512 KARE, `summary_large_image` 1200x630 bekler - yanlis vaat etmek yerine dogru kart turu
secildi (gercek marka gorseli hazirlaninca geri genisletilebilir, checklist'te madde).
Organization logosu var olan dosyaya cevrildi. Sitemap zinciri C1'in nginx blogu ile kapandi.

**DURUST SINIR:** `setProductSchema` calisma aninda og etiketlerini guncelliyor ama **paylasim
botlari JS CALISTIRMAZ** - URUNE OZEL onizleme bu etiketlerle SAGLANAMAZ; onun icin sunucu
tarafi render/prerender gerekir (ayri is). Buradaki etiketler SITE DUZEYI onizlemeyi calisir
hale getirir.

## C6 - IKI DAR KALEM

**(a) `ProductManager.Update` stok dongusu ATOMIK.** Dalga B upsert'e gecmisti ama dongu HALA
transaction'sizdi: ortada bir DB hatasi bazi bedenleri yazilmis bazilarini yazilmamis birakirdi.
`IUnitOfWork` zaten `InstancePerLifetimeScope` kayitliydi - kurucuya tek satir.
`ExecuteInTransactionAsync` SECILDI (manuel `BeginTransaction` DEGIL): `Program.cs`'in kendi notu
*"EnableRetryOnFailure acilirsa manuel BeginTransaction retry stratejisi tarafindan REDDEDILIR"*
diyor. **KAPSAM EN DAR:** yalniz stok dongusu (+ pasifleme). Urun satirinin kendi yazimi tek
`SaveChanges`, zaten atomik; `NotifyPriceDrop` DIS IS yapar ve transaction icinde tutulmaz.

**(b) Kargolanmayi bekleyenler listesi.** Kargo ekrani KOR FORMDU - operatorden siparis ID'si
elle isteniyordu. **Hangi durumun "bekliyor" oldugu UYDURULMADI, durum makinesinden turetildi:**
`OrderStatusMachine`'e gore `Shipped`'e YALNIZ `Preparing` gecebilir ve `CreateShipment` bunu
zaten dogruluyor. **Backend degisikligi SIFIR** - `AdminOrderFilterDto.status` filtresi vardi.
Pin bu degeri elle yazmiyor, **makineden HESAPLIYOR** (makine degisirse pin kirilir).

Canli: bos durum -> "Kargolanmayi bekleyen siparis yok" · siparis Preparing'e alindi -> liste
doldu · "Kargo gir" formu doldurdu (#92) · kargo olusturuldu -> form temizlendi, liste yeniden
bosaldi (siparis Shipped'e gecti).

## C5'IN B5'LE ILISKISI YOK - HAVALE UYKUDA

Dalga B'de verilen karar (havale yuzeyi acilmaz) DEGISMEDI; bu dalgada dokunulmadi.

## SURECTE ORTAYA CIKAN BULGU - GELISTIRICI SECRET'LARI TEST HOST'UNA SIZIYORDU

`AdminSeed_KAPALIYKEN_HICBIR_ADMIN_ACILMAZ` pini ilk kosumda **YEREL MAKINEDE KIRILDI**
(1 admin buldu). Kok sebep: `WebApplicationFactory<Program>` uygulamanin TAM yapilandirmasini
yukler - Development ortaminda **USER-SECRETS DAHIL**. Bu makinede
`dotnet user-secrets list` ciktisinda `AdminSeed:Enabled=true` (+ e-posta/sifre) VARDI,
dolayisiyla **HER test host'u** acilirken o testin veritabanina beklenmeyen bir admin satiri
yaziyordu.

Zarar iki yonlu: (a) "admin sayisi" olcen bir pin **yerelde kirmizi, CI'da (secret yok) yesil**
olur - sonuc MAKINEYE gore degisir; (b) tersi de mumkun: bir yetki pini hazir bulunan admin
yuzunden YANLIS SEBEPTEN yesil kalabilir.

DUZELTME `TestHostConfig`'te (TUM test host'larini kapsar): `AdminSeed:Enabled` varsayilan
**false**. Tohumlamayi OLCEN testler bayragi KENDILERI aciyor (`UseSetting` daha SONRA
cagrildigi icin oradaki deger kazanir). Ayrica ilgili pin degeri artik ACIKCA veriyor -
**yapilandirmayi olcen bir pin, degeri kendisi vermelidir.**

## PINLER (14 yeni)

**`DalgaCYayinAltyapisiTests` (7, SQL + HTTP):**
- ilk admin temiz DB'de olusur ve **GERCEKTEN GIRIS YAPABILIR** (vakum kirici: satirin var
  olmasi yetmez - yanlis hash'lenmis/dogrulanmamis bir admin tum alan assertlerini gecer ama
  operator panele GIREMEZ)
- zayif sifreyle olusturulmaz **ve uygulama YINE ACILIR** (vakum kirici: fail-fast bilincli
  olarak secilmedi)
- idempotent: ikinci acilis **FARKLI e-postayla** da ikinci admin ACMAZ
- bayrak kapaliyken hicbir admin acilmaz (cift-anlam kirici; deger ACIKCA veriliyor -
  gerekcesi yukarida)
- basarisiz arka plan isleri admin ucundan gorunur, **payload SIZMAZ** (cift-anlam kirici:
  yalniz Failed donmeli, Processed donmemeli; kisisel veri yanitta olmamali)
- uc anonim ve musteri tarafindan okunamaz (401 / 403)
- urun guncelleme stok dongusu atomik: reddedilen istek **hicbir iz birakmaz** (+ vakum kirici:
  gecerli istek GERCEKTEN yazar)

**`DalgaCDagitimSozlesmesiTests` (7, artefakt sozlesmesi):**
- nginx hem API hem storefront blogunu tasir + sitemap proxy + SPA fallback (vakum kirici:
  dosya gercekten nginx yapilandirmasi olmali)
- compose storefront servisini tasir ve **yapilandirmasi depoda**
- yukleme dizini kalici volume'de **ve SAHIPLIK ZINCIRI kurulu** (mkdir'in chown'DAN ONCE
  geldigi INDEKS KARSILASTIRMASIYLA dogrulanir)
- paylasim etiketleri tam **ve kart turu gorselle tutarli** (+ og:image'in gosterdigi dosyanin
  depoda GERCEKTEN var oldugu; olmayan logo yolu geri gelemez)
- robots'taki sitemap adresi nginx'in **sundugu yolla ORTUSUR** (yol robots.txt'ten
  AYRISTIRILIP nginx'te aranir - iki taraf elle esitlenmiyor)
- kargo ekrani bekleyen listesi tasir **ve durum makinesiyle tutarli** (beklenen durum
  `OrderStatusMachine`'den HESAPLANIR)
- admin tohumlama varsayilani kapali ve sifre alani bos

**KIRILAN PIN YOK.**

**PIN SINIRI (durust kayit):** Docker imaji ve nginx yapilandirmasi bu suitte AYAGA
KALDIRILAMAZ; `DalgaCDagitimSozlesmesiTests` **artefaktin kendisini** okur. "Konteyner gercekten
ayaga kalkiyor ve volume yaziliyor" bir CI-with-Docker isidir (ayri karar). Sahiplik zincirinin
DOGRU KURULDUGU olcumle (publish ciktisi + chown sirasi) gerekcelendirildi, calistirilarak
degil.

## DIS KONTROLU + 5. KONTROL

**DIS:** 6 assert ters (ALTI AYRI test, IKI sinif) -> **6 AYRI ISIMLI KIRMIZI**. Geri alindi.

**5. KONTROL - DORT URETIM MUTASYONU** (farkli testleri vurduklari icin ayristirilabilir):

| Mutasyon | Kirilan pin | BULUNAN | Olculen once-durum |
|---|---|---|---|
| M1 `AdminSeeder`'dan politika cagrisi kaldirildi | `IlkAdmin_ZAYIF_SIFREYLE_OLUSTURULMAZ...` | `"abc"` ile admin **OLUSTU** (found True) | C3'un besinci-kopya boslugu |
| M2 DTO'ya `payload` geri kondu | `BasarisizArkaPlanIsleri_..._PAYLOAD_SIZMAZ` | `error` alani **`{"To":"gizli.musteri@example.com",...}`** - kisisel veri SIZDI | C4'un sizinti kapisi |
| M3 nginx storefront blogu kaldirildi | `NGINX_HEM_API_HEM_STOREFRONT...` + `ROBOTS_SITEMAP_ADRESI...` | storefront blogu ve `/sitemap.xml` yolu YOK | C1'in olculen once-durumu |
| M4 Dockerfile `mkdir` satiri kaldirildi | `YUKLEME_DIZINI_KALICI_VOLUME...` | mkdir satiri YOK -> sahiplik zinciri kirik | C2'nin sessiz kirilma yolu |

Dordu de geri alindi; 14/14 yesil.

## YEREL DOGRULAMA

290/290 `Category=Sql` · tam suitte **471 basarili / 474** (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`) · Release 0 hata · whitespace + style **exit 0**.

## ACIK KALANLAR (bloke etmez)

- **Docker/nginx CALISTIRILARAK dogrulanmadi** - artefakt sozlesmesi pinli, ayaga kaldirma
  CI-with-Docker isi (yeni bagimlilik + kosum suresi; ayri karar).
- **URUNE OZEL paylasim onizlemesi** sunucu tarafi render gerektirir; bugun site duzeyi
  onizleme calisiyor.
- **1200x630 marka gorseli** hazirlaninca `og:image` degistirilip `twitter:card` geri
  genisletilebilir (checklist'te madde).
- **Hangfire panosu bilincli olarak KAPALI** - operatorun yuzeyi `failed-jobs` ucudur.
- **120 yetim `product_stocks` satiri** DALGA D'ye (kullanici karari): temizligin URETIM
  YOLUYLA mi migration'la mi yapilacagi orada olculecek.

## SIRA

0. **KALITE SUPURMESI KAPANDI - LAUNCH'I BLOKE EDEN TEKNIK KALEM KALMADI.**
   Kanit SHA: **`dbaa763`** (her iki workflow tamamen yesil, alti job'da failure seviyeli
   annotation SIFIR). Bes olcum dalgasi ve duzeltmeleri, kapanan/acik kalan kalemlerin tam
   listesi ve kapanisin saha kaniti icin **yukaridaki "KAPANIS KAYDI" bolumune** bak -
   acik kalemlerin GUNCEL ve TEK dogru listesi ORASIDIR; bu madde yalnizca isaret eder.
   Ozetle acik kalanlar (HICBIRI BLOKE ETMEZ): SUPHELI #14 · G4 (satici modulu ON KOSULU) ·
   M2/M4/M5/M6/M7/M8/M9 · B5 · B13 · launch-sonrasi defterin tamami.
0b. **LAUNCH-FIX FAZI SURUYOR.** Bes dalga planlandi: A ilk musteri zinciri, B operasyon
   yuzeyi, C yayin altyapisi, D gercek veri provasi, E olu yuzey karari.
   **DALGA A TAMAMLANDI** (`8818f19` - A1 mail altyapisi + A2 sifremi unuttum + A2-FIX sifre
   politikasi + A3 misafir checkout + A4 tek para birimi).
   **DALGA B TAMAMLANDI** (`8e46337` - her iki workflow tamamen yesil, alti job'da failure
   seviyeli annotation SIFIR) - admin panelinin HIC ACILMAMIS bes ekrani surulup B1..B5
   kapatildi; ayrintisi "DALGA B - OPERASYON YUZEYI" bolumunde.
   **DALGA C TAMAMLANDI** (bu bolum yazilirken push BEKLIYOR) - C1 storefront'u sunan tanim,
   C2 gorsel kaliciligi, C3 ilk admin, C4 arka plan is hatalari + log saklama, C5 paylasim/
   sitemap, C6 Update transaction'i + kargo bekleyen listesi; ayrintisi "DALGA C - YAYIN
   ALTYAPISI" bolumunde.
   **SIRADAKI: DALGA D (gercek veri provasi)** - kapsami KULLANICIDAN gelecek, HENUZ IS
   ACILMADI. Kullanici karariyla D'ye TASINAN kalem: **120 yetim `product_stocks` satiri**
   (Dalga 3 perf seed artigi) - temizligin URETIM YOLUYLA mi yoksa migration'la mi
   yapilacagi ORADA olculecek.
1. **TEKNIK DEFTERDE ACIK KALEM KALMADI - TEK ISTISNA SUPHELI #14** (surum okuyucusu
   kirilganligi, genel) ve o da **LAUNCH SONRASI**. #15, #17 ve **#18** KAPANDI; #16 BILINCLI
   olarak bos birakildi; siparis #33 hem odeme hem envanter tarafinda TEMIZLENDI.
   **ISIMSIZ FLAKE (kullanici karari):** yerelde bir kez gorulen ve adi yakalanamayan 4.
   kirmizi icin simdilik KAYIT YETERLI. CI'da adiyla yakalanirsa SUPHELI olarak ACILIR.
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
  **ZORUNLU ON KOSUL (GUVENLIK DALGASI / G4): modul acilmadan ONCE satici refresh token'i
  httpOnly cereze tasinmali.** Olculdu: `SellerAuthManager.cs:101` refresh token'i YANIT
  GOVDESINDE donuyor - Sprint 8 madde 6 bunu YALNIZ musteri yolunda duzeltmisti. Bugun
  ERISILEMEZ (`Seller:RegistrationEnabled=false` -> gecerli govdeyle kayit 403, `sellers`
  tablosu 0 satir), bu yuzden GUVENLIK-FIX dalgasinda DOKUNULMADI ve pin de YAZILMADI
  (var olmayan bir yuzeyi pinlemek yanlis guvence olurdu). Modul acilirken musteri
  tarafindaki cerez sozlesmesi (`OturumCerezleriniYaz` + CSRF double-submit) satici
  tarafina da tasinir ve `RefreshCookieContractTests` kalibinda pinlenir.
  **IKINCI ON KOSUL (GUVENLIK-FIX-2 eki): `SellerAuthManager.Login` kilit kontrolunu SIFRE
  DOGRULAMASINDAN ONCE yapiyor** - musteri tarafinda SUPHELI #19 olarak kapatilan oracle'in
  aynisi. Bugun uretemez (`sellers` 0 satir -> her giris `seller == null` dalina duser), ama
  modul acilir acilmaz uretir. Musteri tarafindaki sira (dogrula -> kilitliyse ve sifre DOGRU
  ise 403, degilse 401 + sayac artirma YOK) satici tarafina da tasinir ve pinlenir.
- **invoice_number**: entegrator (Nilvera) numarasi esas, bizimki ic referans - degisiklik yok.
- **Launch sonrasi defteri** (simdi is yok): gift-card expiry, 2FA enrollment ucu,
  step-up `auth_time` refresh'te sifirlanmasi, loyalty oransal geri alma + referral
  clawback, Dashboard tam-tablo agregalari. **Dusen kalem:** Http.Abstractions 2.2.0
  (hicbir csproj'de referans yok).
  **YENI KALEM (Dalga 2 / B13 - kullanici karari): TERK EDILMIS PENDING SIPARISLERE TTL.**
  Olculdu: 17 Pending siparis, HEPSI 24 saatten eski (en eski 20 Agustos). Rezervasyonlar
  serbest (5 dk'lik `reservation-cleanup` calisiyor - suresi gecmis Active rezervasyon 0), stok
  ve kupon limitleri guvende; ama bu siparisler musterinin "Siparislerim" ekraninda SONSUZA
  KADAR "Onay bekliyor" duruyor ve onlari iptale ceken bir arka plan isi YOK. Aday: 24-48 saat
  sonra otomatik iptal + bildirim. **POLITIKA URUN KARARIDIR, kullanici sonra verecek.**
  **YENI KALEM (Dalga 3 / P4 - kullanici karari): ISTEMCI TARAFI ONBELLEK.**
  Olculdu: hesap sekmeleri arasi her gecis yeniden cekiyor; AYNI siparis detayini kapatip acmak
  2 istek daha (order/get + order/timeline). Tazelik acisindan savunulabilir bir tercih, ama
  olculmus ve ucretsiz bir kazanc kapisi.
  **YENI KALEM (Dalga 3 / P2 kalani - kullanici karari): index.html'in SATIR ICI 704 KB
  script + 142 KB style BLOKU BOLUNMESI.** DALGA-3-FIX yalniz (a) harici script'lere `defer`
  ve (b) fontun render-bloklamamasini yapti; render-bloklayan kaynak 5 -> 0 oldu. Ama belge
  hala 883 KB ve %95'i satir ici kod. Bolme AYRI bir is: dis dosyalara cikarma + onbelleklenebilir
  hale getirme + CSP'nin `unsafe-inline` bagimliliginin gozden gecirilmesi birlikte ele alinmali.
  **YENI KALEM (dalga-1-fix eki - kullanici karari): TURKCE KLAVYEDE YAZILAN E-POSTA.**
  `KimlikDizgesi.KanonikKod` (Turkce harf katlamasi) BILEREK e-postaya UYGULANMIYOR - e-posta
  kullanicinin KENDI kimligidir, oradaki karakteri sessizce degistirmek kimlik verisini yeniden
  yazmak olur. Sonuc: adresini Turkce klavyede `İ`/`ı` ile yazan kullanici, kayitta yazdigi
  harfle giris yapmak zorunda. Invariant casing bu ikisini katlamaz. Karar kullanicinin.
  **YENI KALEM (GUVENLIK-FIX / G2 eki - kullanici karari): SABIT-ZAMANLI KAYIT.**
  G2 kayit ucunun YANIT sizintisini kapatti (var olan ve yeni adres birebir ayni 201 + ayni
  govde) ama ZAMANLAMA kanalini kapatmaz: yeni kayit yolu hash + INSERT + riza satirlari
  yazar, var olan yol yalniz bir e-posta gonderir. OLCULDU: 400 yolu 9 ms, 201 yolu 14 ms
  (duzeltme sonrasi 49 ms / 56 ms). Fark kucuk ve aglar uzerinden gurultuye gomulur, ama
  yerel/hizli bir ag uzerinde istatistiksel olarak ayrilabilir. Sabit-zamanli kayit AYRI bir
  istir (her iki yolda da ayni is birimini harcamak ya da yaniti sabit bir sureye yaymak).
  **Kullanici karari: launch-sonrasi deftere.**
  **YENI KALEM (Sprint 8 madde 8 eki - kullanici karari): RFC 2606 ust alan adlarini KAYITTA
  reddetme.** Kayit validatoru FluentValidation'in permisif `.EmailAddress()` kuralini kullaniyor
  ve `.test` / `.example` / `.invalid` / `.localhost` adreslerini KABUL EDIYOR; gercek Iyzico
  reddediyor (E2b'de olculdu), yani o adresle uye olan musteri HIC kart odemesi yapamiyor.
  Sprint 8'de AYIRT EDILEBILIR MESAJ eklendi (init hatasinda sebep soyleniyor, saglayicinin ham
  metni sizdirilmiyor) ve bu YETERLI goruldu. Validatoru sikilastirmak ayri bir URUN karari:
  gecerli ama alisilmadik adresleri kapida cevirmek gercek musteri kaybettirebilir.
  **Sprint 8'e GIRMEZ.**
  **YENI KALEM (Dalga 4 / M10-M11 eki - kullanici karari): CIKISLI KULLANICIYA DOGRUDAN
  GIRIS KATMANI.** Bugun "Sepeti Onayla" cikisli kullaniciyi `#/odeme`ye dusuruyor ve orada
  "Siparisi tamamlamak icin giris yapmalisin" + "Giris yap" gorunuyor. Bu davranis
  DEGISTIRILMEDI ve gerekcesi Dalga 4 bolumunde: sepet icerigi KORUNUYOR (E2'de pinli),
  odeme sayfasi ozeti tekrar gosteriyor, cekmecenin acik kalmasi "bir sey olmadi" hissini
  ARTIRIRDI. Gercek kusur cekmece degil, hedef sayfanin tek eyleminin ortulu olmasiydi (M11)
  ve o KAPANDI. Yine de "cikisli kullaniciyi ara bir sayfaya dusurmek yerine dogrudan giris
  katmanini acmak" savunulabilir ve muhtemelen daha az adimli bir URUN karari. LAUNCH ONCESI
  DEGIL.
  **YENI KALEM (Dalga 4 eki - kullanici karari): JS/DOM TEST KOSUCUSU (Playwright vb.).**
  Olculdu: depoda JS/DOM kosucusu YOK, dolayisiyla TARAYICI SEMANTIGI (hit-test, CSS
  ozgullugu, `elementFromPoint`) CI'da pinlenemiyor - M10'un kok sebebi tam da bu katmandaydi.
  Bugunku telafi YETERLI goruldu: 7 kaynak sozlesmesi pini (`FrontendDokunmaHedefiTests`) +
  depoya konan tekrarlanabilir olcum betigi (`frontend/test/mobil-erisilebilirlik.js`).
  **LAUNCH ONCESI EKLENMEZ:** yeni bir bagimlilik `dependency-scan` kapsamina girer ve tarayici
  ikilisi indiren bir kosucu CI suresini/yuzeyini buyutur - launch oncesi alinacak risk degil.
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

## MINI DALGA (LAUNCH ONCESI SON ISLER) - TAMAMLANDI

Sprint 8 kapandiktan sonra kullanicinin actigi kapsam-sinirli dalga: bes kalem.

### (a) `workflow_dispatch` TETIGI

`security.yml`'a eklendi. **Gerekcesi OLCUMDUR, kolaylik degil:** `gitleaks-action` kaynagi
okundu - `push` yalniz SON COMMIT'i tarar (`--log-opts=-1`), `schedule`/`workflow_dispatch`
ise HICBIR `--log-opts` almaz ve TUM GECMISI tarar. Yani ".gitleaksignore gercekten tutuyor
mu" sorusunu bir PUSH kosumu ASLA yanitlayamaz. Tetik olmadan tek kanit haftalik cron'du.
**ELLE TETIKLEME KULLANICIDA:** `POST .../workflows/{id}/dispatches` KIMLIK ISTER; anonim
API ile tetiklenemez ve PAT ISTENMEZ (ev kurali). Tetik main'e dustukten sonra GitHub
arayuzunde "Run workflow" gorunur.
**SONUC:** kullanici tetikledi - **run 32540908505 SUCCESS**, `Gitleaks (secret taramasi)`
SUCCESS. O kosum TUM GECMISI taradi (jetonlarin durdugu `19d101f` DAHIL); `.gitleaksignore`
fingerprint'leri TUTTU. Dogrulama boslugu KAPANDI.

### (b) SUPHELI #15 KAPANDI - WEBHOOK'TA TOKEN YASI SINIRI GEVSEDI

**TASARIM OLCEREK KURULDU.** Onceki imza `HandleCallback(dto, bool imzaZorunlu = true)` idi.
Olculdu: Sprint 8 madde 9'dan sonra **her iki uretim cagri yeri de `false` veriyordu**, yani
bayrak artik KANALI ayirt etmiyordu. Ikinci bir bool eklemek (`tokenYasiSiniriUygula`)
gecersiz bilesimlere kapi acardi (`imzaZorunlu: true` + `tokenYasi: false` gibi hicbir kanalin
karsiligi olmayan bir kombinasyon).

**SECILEN: TEK ENUM** - `PaymentNotificationChannel { Strict = 0, BrowserCallback, ProviderWebhook }`.
Politika TEK YERDE turer (`HandleCallback` basi), cagri yerleri yalnizca KANALI soyler.
Varsayilan `Strict` - FAIL-CLOSED. Gerekce enum'un basinda, `SuccessDataResult` belirsizligi
(madde 11) referansiyla: "bir bayragin sessizce yanlis anlama gelmesi" bedeli bu depoda bir kez
odendi, ayni tuzak bilerek tekrarlanmiyor.

| Kanal | Imza | Token yasi siniri (30 dk) |
|---|---|---|
| `Strict` (varsayilan) | ZORUNLU | UYGULANIR |
| `BrowserCallback` | gelirse dogrulanir | **UYGULANIR** (tarayici replay'i gercek senaryo) |
| `ProviderWebhook` | gelirse dogrulanir | **UYGULANMAZ** |

**CF-CALLBACK YOLUNA DOKUNULMADI** (kullanici sarti) - pinli.
Gevseyen TEK sey yas siniri: yalniz-Pending + retrieve otoritesi + tutar + para birimi + fraud
AYNEN duruyor.

**STOK TARAFI OLCULDU** (relaxation oncesi zorunlu kontrol): `ConfirmReservation` "rezervasyon
expire olmustu ama odeme basarili" durumunu ELE ALIYOR - stok varsa dogrudan dusuyor, yoksa
hareket kaydina GURULTULU uyari yaziyor. Yani sessiz overselling riski YOK **diye dusunuldu** -
ama (c)'deki canli kurtarma bu telafinin OLU oldugunu gosterdi; bkz. **SUPHELI #18**.

PINLER (`WebhookContractTests`): `GECIKMIS_GercekBildirim_WEBHOOKTA_FAILEDLANMAZ_Confirmeda_Tasir` ·
`AyniGecikme_TARAYICI_CALLBACKINDE_TokenYasi_Guardina_TAKILIR` (cift-anlam kirici - gevseme
KANAL BAZLI) · `VARSAYILAN_KANAL_STRICT_GecikmisTokeni_REDDEDER_FailClosed` (gecerli imza
gonderilir ki red sebebi YAS olsun).

### (c) SIPARIS #33 KURTARILDI - KURTARMA YOLUNUN CANLI KANITI

(b) girdikten sonra gercek webhook govdesi elle tetiklendi (token yasi **173 dakika**).

```
YANIT : 200 in 1063 ms   ("Ödeme başarılı, siparişiniz onaylandı.")
        1063 ms = retrieve GERCEKTEN kostu (gercek Iyzico sorgusu)
orders   #33  status=1 (Confirmed)  is_online_payment_done=1
payments      payment_status=1  transaction_id=37415135  item_transaction_id=39331730
              paid_price=1049.70
outbox        PaymentConfirmed x1 -> status=1 (Processed)  retry_count=0
invoices      1 satir  DIV-2026-000033  status=1 (Sent)
loyalty       1 satir  104 puan
timeline      "Ödeme onaylandı"  UYARI/KRITIK notu: 0
```

`transaction_id` 1. turdaki gercek bildirimin `iyziPaymentId` degeriyle BIREBIR AYNI - yani
kurtarilan sey gercekten O odeme.

**AMA STOK DUSMEDI** - bkz. SUPHELI #18. Kurtarma odeme/siparis/fatura/puan tarafinda tamdir,
envanter tarafinda DEGILDIR.

### (d) SUPHELI #17 KAPANDI - CALLBACK DA "payment" KOVASINDA

`Callback` action'ina `[EnableRateLimiting("payment")]`. Yeni bir sayi degil: Redis yolu
(`/payment/` -> 10/dk) ile yerlesik yolu ayni davranisa getiriyor.
PIN (`PaymentCallbackRedirectTests`): `Callback_PAYMENT_KOVASINDA_OnBirinci_Istek_429`
(AYRI host, uretim varsayilani; ilk on istek **302** aliyor - uygulamaya ULASIYORLAR).
Sinifin diger pinleri icin ana fabrikada limit yukseltildi (iki-host deseni).

### (e) SUPHELI #16 BILINCLI BOS BIRAKILDI (kullanici karari)

`Webhook:AllowedIps` DOLDURULMUYOR. Gerekce deftere ve `appsettings.Development.example.json`
aciklamasina yazildi: bu uc, kaybolan callback'in TEK kurtarma yoludur; liste BAYATLARSA
gercek bildirimler 403 yer ve kurtarma yolu SESSIZCE OLUR - **yanlis doldurulmus bir allowlist
bos birakmaktan DAHA TEHLIKELIDIR**. Doldurulacaksa: yalniz resmi Iyzico IP listesinden,
bayatlama riski bilinerek ve `ForwardedHeaders:KnownProxies` ile BIRLIKTE.
Ayrica example.json'daki eski "yalniz imza kalir" ifadesi DUZELTILDI - madde 9'da olculdu ki
gercek bildirim imza TASIMIYOR.
**SUPHELI #14 launch-sonrasi deftere alindi.**

### DIS KONTROLU + 5. KONTROL

5 assert ters -> **5 AYRI ISIMLI KIRMIZI** (geri alindi).
5. kontrol, iki uretim mutasyonu TEK dalgada (farkli testleri vurduklari icin ayristirilabilir):
- `tokenYasiSiniriUygula = true` (kanal gevsemesi geri alindi) -> `GECIKMIS_GercekBildirim_...`
  **400** dondu; siparis #33'un kurtarma ONCESI zarari BIREBIR. `AyniGecikme_TARAYICI_...` ve
  `VARSAYILAN_KANAL_STRICT_...` dogru sekilde YESIL kaldi (mutasyon KATI davranisi bozmuyor).
- `[EnableRateLimiting("payment")]` kaldirildi -> `Callback_..._429` on birinci istekte **302**
  buldu; olculen boslugun aynisi.
Ikisi de geri alindi.

## MINI DALGA 2 - SUPHELI #18 DUZELTMESI (TAMAMLANDI)

Kullanici karari: #18 launch ONCESI duzeltilir, kapsam sinirli.

### SINIR OLCEREK CIZILDI - HANGI DURUMLAR ONAYA DAHIL?

Rezervasyon durum gecisleri okundu (`TryReserveAsync` / `ConfirmReservation` /
`ReleaseReservation` / `ReleaseExpiredReservations`) ve her durum icin FIZIKSEL stok hali
cikarildi:

| Durum | `reserved_quantity` | `stock_quantity` | Onayda dogru islem |
|---|---|---|---|
| `Active` (0) | TUTULUYOR | dusmemis | atomik gecis + `ConfirmStockAsync` |
| `Confirmed` (1) | serbest | **ZATEN DUSMUS** | DOKUNULMAZ (cift dusum olurdu) |
| `Expired` (3) | serbest (cleanup birakti) | dusmemis | **DAHIL** - dogrudan dusum |
| `Released` (2) | serbest | dusmemis | **DAHIL EDILMEDI** |

**`Released` NEDEN DISARIDA - gerekce FIZIKSEL DEGIL ANLAMSAL (durust duzeltme):**
Fiziksel olarak `Released` ile `Expired` AYNIDIR - `ReleaseReservedAsync` yalniz
`reserved_quantity`'yi azaltir, fiziksel stogu GERI EKLEMEZ (kodda da "fiziksel degismez"
yaziyor). Yani buradaki risk **"cift dusum" DEGIL**. Gercek gerekce su: `Released`i YALNIZCA
`ReleaseReservation` yaziyor ve o da yalniz iki yerden cagriliyor - `IyzicoPaymentManager`in
odeme BASARISIZ dali ve `OrderManager`in siparis IPTAL yolu. Yani `Released` = **"bu siparis
iptal edildi" karari**. Boyle bir rezervasyonun onaya gelmesi bir stok kurtarma senaryosu
degil, bir **DURUM MAKINESI IHLALIDIR**. Stogu orada dusmek (a) kimsenin sevk etmeyecegi bir
siparis icin hayalet kayip yazar, (b) asil hatayi - iptal edilmis siparisin yeniden
onaylanmasini - SESSIZCE ortbas eder.

### YAN BULGU: TELAFI DALI ATOMIK DEGILDI

Eski telafi dali `TryDirectDeductAsync` yapip rezervasyonu **Expired BIRAKIYORDU**. Sorgu
Expired'i hic getirmedigi icin bu gorunmuyordu; ama Expired ARTIK normal bir yol oldugu icin
ikinci bir `ConfirmReservation` cagrisi ayni satiri TEKRAR dusurebilirdi. Bu yuzden her iki
yol da `Active->Confirmed` / `Expired->Confirmed` gecisini KAZANMAK zorunda birakildi.
Yani duzeltme, kendi actigi kapiyi da kapatiyor.

### SESSIZ HICBIR YOL KALMADI - IKI KANAL

`ExpireSonrasiTelafiAsync`: stok varsa dogrudan dusulur; **yoksa**
1. `stock_movements` notu (envanter defteri) - **MEVCUT DAVRANIS AYNEN KORUNDU**,
2. **siparis zaman cizelgesi** (H53 "KRITIK/UYARI" kalibi) - YENI kanal.
Ikincisi eklendi cunku hareket kaydini kimse duzenli okumuyor; #33'te zaten HICBIR satir
yazilmamisti ve sapma aylarca gorunmeyebilirdi. Zaman cizelgesi yazimi BEST-EFFORT
(try/catch + `LogError`): not yazilamazsa onay akisi KIRILMAZ, birinci kanal zaten yazildi.
`StockManager` iki yeni bagimlilik aldi (`IOrderStatusHistoryService`, `ILogger`); dongusel
bagimlilik YOK - `OrderStatusHistoryManager` yalniz DAL'lara bagli (kontrol edildi).

### BILINCLI KIRILAN PIN

`SUPHELI_RezervasyonEXPIRE_Olduysa_Onay_STOK_DUSURMUYOR_ve_UYARI_YAZMIYOR_PINLENIR` ->
`RezervasyonEXPIRE_Olsa_da_Onay_STOK_DUSURUR_ve_HAREKET_YAZAR`.
Eski pin OLCULEN supheli davranisi (stok DUSMEZ + hareket YOK) sabitliyordu; #18 duzelince
envanter sapmasini SAVUNUR hale gelirdi.

YENI PINLER (`WebhookContractTests`):
- `RezervasyonEXPIRE_Olsa_da_Onay_STOK_DUSURUR_ve_HAREKET_YAZAR` - stok duser, `reserved`
  EKSIYE gitmez, TEK hareket satiri yazilir, notu "expire" iceri (cift-anlam kirici: normal
  onay notuyla karismaz) ve rezervasyon **Confirmed**'a gecer (ikinci dusumu engelleyen sey).
- `RezervasyonEXPIRE_ve_STOK_TUKENMISSE_UYARI_ZAMAN_CIZELGESINE_Duser` - stok EKSIYE
  cekilmez, hareket notunda "UYARI" var VE zaman cizelgesinde uyari notu var. Ikinci assert
  olmadan "sessiz hicbir yol kalmaz" iddiasi kanitlanmis olmazdi.
Ikisi de on kosulu GERCEK temizlik yoluyla kuruyor (`ReleaseExpiredReservations`) - sahte
kurgu degil.

### DIS KONTROLU + 5. KONTROL

3 assert ters (uc AYRI test: iki yeni pin + `StockReservationTests.Confirm_IkiKezCagrilinca_CiftDusumYok`)
-> **3 AYRI ISIMLI KIRMIZI** (geri alindi).
5. kontrol: `ConfirmReservation` sorgusu `Active`-only haline dondurulda ->
`RezervasyonEXPIRE_Olsa_da_...` **stok 10 buldu (dusmedi)** ve uyari pini de kirildi -
**siparis #33'te olculen tablonun BIREBIR aynisi**. Diger 21 test YESIL kaldi (mutasyon
kesin olarak lokalize). Geri alindi.

### SIPARIS #33'UN KENDI ENVANTER SAPMASI - OLCULDU, DOKUNULMADI

```
siparis 33  urun 2 / M  quantity 2   rezervasyon status=3 (Expired)  hareket kaydi YOK
siparis 34  urun 2 / M  quantity 3   rezervasyon status=1 (Confirmed) hareket kaydi VAR
product_stocks  urun 2 / M  ->  stock_quantity 10   reserved_quantity 0
```
Yani #34'un 3 adedi dusuldu, **#33'un 2 adedi DUSULMEDI**. Dogru deger 8 olmaliydi.
**Bu GELISTIRME veritabani (`DivisimaDb`) ve sandbox siparisi** - fiziksel mal yok.
Secenekler sunuldu: (A) hicbir sey yapma, (B) duzeltilmis uretim yolunu bir kez kostur,
(C) elle SQL. **KULLANICI KARARI: B.** Gerekcesi: "bulguyu doguran canli artigin, bulgunun
duzeltmesiyle temizlenmesi en durust kapanis"; (C) denetim izi birakmaz, (A) yarim kapanis olur.

### #33 ENVANTER SAPMASI GIDERILDI - CANLI OLCUM (secenek B)

`StockManager.ConfirmReservation(33)` **URETIM KODU** tek seferlik bir kosucuyla cagrildi.
Kosucu DEPO DISINDA (scratchpad) tutuldu ve is bitince SILINDI - commit'e girmesi mumkun
degildi. **ELLE SQL YAZILMADI**: hem stok dusumu hem denetim izi uretim yolunun kendisi
tarafindan uretildi.

```
ONCE
  urun 2 / M   siparis adedi = 2
  stock_quantity = 10   reserved_quantity = 0
  rezervasyon status = 3 (Expired)
  stock_movements(reference_id=33) = 0 satir

BIRINCI CAGRI -> 200 / success=True / "Rezervasyon onaylandı (stok düşüldü)."
  stock_quantity = 8    reserved_quantity = 0      <- 2 adet DUSTU, reserved EKSIYE GITMEDI
  rezervasyon status = 1 (Confirmed)               <- Expired -> Confirmed ATOMIK GECIS
  stock_movements = 1 satir
     tip=2 (Out) adet=2
     not="Ödeme onaylı - rezervasyon expire olmuştu, stok yeniden güvenceye alındı"

IKINCI CAGRI -> 200 / success=True / (ayni mesaj)
  stock_quantity = 8    reserved_quantity = 0      <- DEGISMEDI
  rezervasyon status = 1 (Confirmed)               <- DEGISMEDI
  stock_movements = 1 satir                        <- IKINCI SATIR YAZILMADI
```

**KENDINI SINIRLAMA CANLI TEYIT EDILDI:** ikinci cagri hicbir yan etki uretmedi - rezervasyon
artik `Confirmed` oldugu icin sorgunun (`Active` VEYA `Expired`) disinda kaliyor. Bu, madde
(1)'in "Confirmed DOKUNULMAZ" sinirinin ve yan bulgunun (telafi dalinin atomik gecise
baglanmasi) canlida calistiginin kanitidir.
Not: ikinci cagri da 200/success donuyor - "yapacak is yok" ile "basarili" ayni yaniti veriyor.
Bu idempotent bir onay ucu icin DOGRU davranis (cagiran tekrar denedi diye hata almamali) ve
etkisizligin kaniti YANIT DEGIL, yukaridaki sayaclardir.

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

## SUPHELI DAVRANISLAR

**DURUM: ACIK KALEMLER #14 (LAUNCH SONRASI) ve #20 (bugun BOSLUK YOK, testte kapatildi).**
**#21 KAPANDI - A2-FIX (kullanici karari: sifre politikasi TEK MERKEZDEN, dort giriste de).**
**#19 KAPANDI - GUVENLIK-FIX-2 (kullanici karari: secenek iii).**
Kapananlar: #1..#13 ilgili sprintlerde · **#15, #17, #18 mini dalgalarda** ·
**#16 BILINCLI olarak bos birakildi (verilmis karar, erteleme degil)**.
Asagidaki maddeler kayit olarak duruyor; her birinin basinda guncel durumu yazili.

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
   kuyrugu acmak.
   **[KAPANDI - MINI DALGA] (i) SECILDI.** Kanal ayrimi bir enum'a tasindi
   (`PaymentNotificationChannel`); yas siniri YALNIZ `ProviderWebhook`'ta gevsedi, tarayici
   yolunda AYNEN duruyor. Ayrinti ve pinler: MINI DALGA bolumu (b).

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
   **[KARAR VERILDI - MINI DALGA] BILINCLI OLARAK BOS BIRAKILDI.** Gerekce: bu uc, kaybolan
   callback'in TEK kurtarma yoludur; liste BAYATLARSA gercek bildirimler 403 yer ve kurtarma
   yolu SESSIZCE OLUR - yanlis doldurulmus bir allowlist bos birakmaktan DAHA TEHLIKELIDIR.
   Doldurma kosullari `appsettings.Development.example.json`'daki `//Webhook3` / `//Webhook4`
   aciklamalarina yazildi. Bu bir ERTELEME DEGIL, VERILMIS bir karardir.

17. **`/api/payment/callback` RATE LIMIT POLICY'SI DISINDA.** (Sprint 8 madde 9'da olculdu)
   `[EnableRateLimiting("payment")]` yalniz `Initialize` uzerindeydi; madde 9'da `Webhook`'a da
   eklendi. `Callback` HALA yalniz GlobalLimiter'in 100/dk'sinda (yerlesik yolda). Redis yolu
   path eslesmesiyle (`/payment/`) onu da 10/dk'ya bagliyor - yani IKI YAPILANDIRMA HALA
   AYRISIYOR, sadece webhook icin hizalandi. Callback bir TARAYICI ucu oldugu icin farkli
   degerlendirilebilir (musteri basina dogal olarak seyrek), ama ayrisma bilincli bir karar
   degil, sadece kapsam disinda kalmis bir bosluk.
   **[KAPANDI - MINI DALGA (d)]** `Callback` action'ina `[EnableRateLimiting("payment")]`
   eklendi; iki yapilandirma artik ayni davraniyor. Pin:
   `Callback_PAYMENT_KOVASINDA_OnBirinci_Istek_429`.

18. **[KAPANDI - MINI DALGA 2] `ConfirmReservation` EXPIRE OLMUS REZERVASYONU HIC GORMUYOR:
   ONAY STOGU DUSURMUYOR VE UYARI DA YAZMIYOR.**
   **KAPANIS:** sorgu `Active` VEYA `Expired`'i kapsayacak sekilde genisletildi; `Released`
   ANLAMSAL gerekceyle DISARIDA birakildi; telafi dali ATOMIK gecise baglandi (kendi actigi
   ikinci-dusum kapisini kapatir); "stok yok" uyarisi hareket kaydinin YANINDA siparis zaman
   cizelgesine de dusuluyor. Ayrinti, sinirin gerekcesi ve pinler: **MINI DALGA 2** bolumu.
   Asagidaki metin bulgunun kaydidir.
   Siparis #33'un kurtarmasinda OLCULDU. Kurtarma odeme tarafinda kusursuz calisti (Success,
   Confirmed, fatura DIV-2026-000033, 104 puan) ama:
   ```
   stock_reservations  id=34  order_id=33  status=3 (Expired)
   product_stocks      urun 2 / M  stock_quantity=10  reserved_quantity=0   (DEGISMEDI)
   stock_movements     reference_id=33 -> 0 SATIR                            (UYARI BILE YOK)
   order_items         urun 2 / M  quantity=2                                (2 adet satildi)
   ```
   **KOK SEBEP (kaynak okundu):** `StockManager.ConfirmReservation` ilk satiri
   `GetListAsync(r => r.order_id == orderId && r.status == Active)` - sorgu YALNIZ **Active**
   rezervasyonlari getiriyor. Icerideki "expire olmustu, stogu yeniden guvenceye al; yoksa
   `UYARI: odeme alindi fakat stok yok ... manuel iade/tedarik gerekli` yaz" telafi dali
   yalniz `TryTransitionAsync` **0** dondugunde, yani expire islemi sorgu ILE gecis ARASINDA
   olustugunda calisiyor. Rezervasyon sorgu anINDA **ZATEN Expired** ise dongu HIC donmuyor ve
   o telafi dali **OLU** kaliyor. (Madde (b) hazirliginda bu telafiyi okuyup "sessiz overselling
   riski yok" demistim - **YANLISTI**, telafi yalniz YARIS durumunu kapsiyor. Duzeltiliyor.)
   **URETIMDEKI ANLAMI:** para alinir, siparis Confirmed olur, fatura kesilir, puan yazilir -
   fiziksel stok DUSMEZ ve kimse bunu goremez (hareket kaydi bile yok). Envanter SESSIZCE sisirilir.
   **KAPSAM:** yalniz webhook degil - `ConfirmReservation`'i cagiran HER onay yolu (COD/havale
   admin onayi dahil) ayni bosluga sahip. Yani bosluk (b) ile OLUSMADI, ama (b) "uzun sure
   Pending kalmis siparisi onayla" yolunu NORMALLESTIRDIGI icin **ulasma olasiligini artirdi**.
   **PINLENDI, DUZELTILMEDI** (ev kurali): `WebhookContractTests` ->
   `SUPHELI_RezervasyonEXPIRE_Olduysa_Onay_STOK_DUSURMUYOR_ve_UYARI_YAZMIYOR_PINLENIR`.
   Pin, GERCEK temizlik yolunu (`IStockService.ReleaseExpiredReservations`) kosturarak on kosulu
   kuruyor - sahte kurgu degil. Aday duzeltme: `ConfirmReservation`'in sorgusunu Active +
   Expired'i kapsayacak sekilde genisletmek (mevcut telafi dali zaten yazili, yalnizca
   ULASILAMIYOR). **Duzeltme karari kullanicinin.**


19. **[KAPANDI - GUVENLIK-FIX-2] HESAP KILITLENMESI BIR ENUMERATION KANALIYDI (G2'nin KALAN yuzeyi).**
   **KAPANIS:** kullanici karari secenek (iii) - kilit bilgisi YALNIZ SIFRE DOGRUYSA bildirilir.
   Yanlis sifre + kilitli hesap artik kayitsiz adresle BIREBIR ayni 401'i doner; dogru sifre +
   kilitli hesap 403 kilit mesajini alir. Sira degisikliginin actigi KILIT UZATMA kapisi da
   kapatildi (kilitliyken yanlis sifre sayaci artirmaz, kilidi uzatmaz). Ayrinti ve pinler:
   **GUVENLIK-FIX-2** bolumu. Asagidaki metin bulgunun kaydidir.
   (GUVENLIK-FIX dalgasinda olculdu) `AuthManager.Login` kilit kontrolunu SIFRE
   DOGRULAMASINDAN ONCE yapiyor: 5 basarisiz denemeden sonra KAYITLI bir adres
   **403 "Cok fazla basarisiz deneme..."**, kayitsiz bir adres **401 "E-posta veya sifre
   hatali."** doner. Yani saldirgan 5 istek harcayarak adresin kayitli olup olmadigini
   ogrenebilir. G2/G2b kayit ve dogrulama uclarindaki kanallari KAPATTI; bu kanal ACIK KALDI.
   **BILEREK DOKUNULMADI - CUNKU KAPATMAK BEDELLI:** kilidi gizlemek, gercek kullaniciya
   "hesabin 15 dakika kilitli" diyememek demektir; kullanici sifresini dogru yazdigi halde
   401 alir ve neden giremedigini anlayamaz. Auth kovasi (10/dk/IP) hizi kisitliyor.
   Aday cozumler: (i) aynen birak (bugunku), (ii) kilit bilgisini de E-POSTAYA tasi ve
   yanitta 401 don (G2 kalibi), (iii) kilidi sifre DOGRUYSA bildir (o zaman kanal kapanir
   ama kilitli hesaba dogru sifreyle gelen kullanici yine bilgilenir). **Karar kullanicinin.**


20. **VARSAYILAN-KAPALI KURAL CONTROLLER'LARLA SINIRLI - MINIMAL-API UCU EKLENIRSE
   VARSAYILAN ACIK OLUR.** (GUVENLIK-FIX / G5'te olculdu, kullanici karariyla deftere alindi)
   `app.MapControllers().RequireAuthorization()` YALNIZ controller uclarini kapsar.
   Istenen `options.FallbackPolicy` idi ve HER endpoint'i kapsardi, ama OLCULDU ki mevcut bir
   pini kiriyor: `X-Api-Version` ayristirilamayinca Asp.Versioning gercek endpoint yerine
   METADATA'SIZ bir HATA endpoint'i koyuyor; FallbackPolicy onu da kapsayinca 400'u yazan kod
   HIC calismiyor ve istek 401'e donusuyor. Bu, SUPHELI #14'u DAHA KOTU yapardi (entegratore
   401 demek onu kimlik hatasi aramaya yonlendirir), bu yuzden kapsam controller'lara
   daraltildi - gerekce `Program.cs`'te.
   **BUGUN BOSLUK YOK** (olculdu: 150 action'in tamami acikca isaretli, uygulamada minimal-API
   ucu ve [Authorize]'siz hub YOK). **RISK GELECEKTE:** ileride eklenecek bir `app.MapGet` /
   `app.MapPost` ucu ya da yeni bir hub, isaretlenmezse VARSAYILAN OLARAK ACIK olur.
   Bu bosluk RUNTIME'da degil TEST'te kapatildi:
   `SecurityHardeningTests.VarsayilanKapali_ACIK_Uclari_KIRMAZ_ve_HER_UC_ACIKCA_ISARETLIDIR`
   her uretim ucunun acikca isaretli oldugunu tarar (oznitelikler YANSIMAYLA okunur;
   `EndpointMetadata` okunsaydi konvansiyonun ekledigi `AuthorizeAttribute` yuzunden tarama
   VAKUM olurdu). Sessiz bir 401 yerine KIRMIZI BIR TEST secildi.
   Aday kalici cozum: Asp.Versioning'in hata endpoint'ine anonim metadata iliskilendirilebilir
   hale gelirse (ya da SUPHELI #14 genel olarak cozulurse) FallbackPolicy'ye gecilebilir.

21. **[KAPANDI - A2-FIX] SIFRE POLITIKASI UC AYRI GIRIS NOKTASINDA UC AYRI - SIFIRLAMA UCUNDA HIC YOK.**
   **KAPANIS:** kural TEK MERKEZE (`Divisima.Core.Security.SifrePolitikasi`) tasindi ve DORT
   giriste de uygulaniyor. Ayrinti: A2-FIX bolumu. Asagidaki metin BULGUNUN kaydidir.
   (LAUNCH-FIX Dalga A / A2'de olculdu; A2 bu akisi ARAYUZE BAGLADIGI icin kapi artik her
   musteriye acik.) Olculen tablo:
   ```
   POST /api/auth/register        CustomerRegisterRequestValidator
                                  -> >=8 karakter + buyuk + kucuk + rakam
   POST /api/account/change-password  AccountManager.cs:73
                                  -> yalnizca >= 6 karakter, KARMASIKLIK YOK
   POST /api/auth/reset-password      AuthManager.ResetPassword
                                  -> HICBIR KONTROL YOK; dto.new_password dogrudan hash'leniyor
                                     (bu DTO icin FluentValidation validator'i da YOK - tarandi)
   ```
   **URETIMDEKI ANLAMI:** "Sifremi unuttum" ile gelen bir kullanici, KAYITTA reddedilecek bir
   sifreyi (ornegin `abc`) belirleyebilir. Yani politika, atlatilmasi en kolay yoldan
   uygulanmiyor. Dalga A'da istemci tarafina kayit kuralinin AYNISI kondu (`sifreSifirlaEkrani`)
   ama bu bir GUVENCE DEGIL - dogrudan uca istek atan biri icin yok hukmunde.
   **DUZELTILMEDI** (ev kurali: supheli uretim davranisi duzeltilmez, pinlenir).
   Bugunku davranis ADIYLA sabitlendi:
   `LaunchFixMailZinciriTests.SUPHELI_SifreSifirlamada_SUNUCU_TARAFI_SIFRE_POLITIKASI_YOK_PINLENIR`
   - pin CIFT-ANLAM KIRICI: ayni zayif sifrenin KAYITTA 400 aldigi da assert ediliyor, yani
   kural VAR, yalnizca bu ucta UYGULANMIYOR.
   Aday cozum: sifre politikasini TEK yerde toplayip (ornegin `SifrePolitikasi.Dogrula`) uc
   giris noktasinin ucunde de cagirmak. `ChangePassword`'un 6 karakterlik esigi de bu kararin
   kapsamina girer - onu 8'e cikarmak MEVCUT kullanicilarin sifre degistirmesini zorlastirir,
   yani bir URUN karari. **Karar kullanicinin.**
   **YAN GOZLEM (kozmetik, ayni metotta):** `ResetPassword`'un basinda AYNI
   `string.IsNullOrWhiteSpace(dto.token)` kontrolu IKI KEZ var (farkli mesajlarla); ikincisi
   ULASILAMAZ. Zarar yok, temizlik kalemi.

### KALICI ONLEM: KANIT MASKESI (Dalga A duzeltmesine bindi)

`secret-scan` kirmizisi UCUNCU KEZ ayni sinifta tekrarlayinca kural **kaynaginda kapatildi** -
ayrinti bolum 1'deki "MASKELEME URETIM NOKTASINDA YAPILIR" maddesinde. Uygulanan yuzey:

| Yer | Ne yapiyor |
|---|---|
| `Divisima.Core/Utilities/Text/KanitMaskesi.cs` | tek olcut, tek uygulama |
| `TestAuthHelper.EnsureAsync` | **paylasilan** yardimci; register/verify/**login** kosuyor |
| 26 test sitesi | assert mesajina ham govde koyan her yer mekanik olarak sarmalandi |
| `NetgsmSmsService` | uretimdeki TEK ham saglayici-govdesi logu |
| SMTP yakalayicisi (scratchpad) | `.eml`'i **yazarken** kirpar |

**OLCULEN YAN ETKI (durust kayit):** olcut, uretilmis test e-postalarinin yerel kismini da
maskeliyor (`maske.17…@example.com`) - cunku onlar da 16+ karakter, rakam ve kucuk harf
iceriyor. Gercek musteri adresleri (`ad.soyad@...`) rakam icermedigi icin DOKUNULMAZ. Bu bir
kayip degil kazanc sayildi: adres kisisel veridir, teshis kanalinda maskeli olmasi dogrudur.

**SINIR (durust kayit):** `/` bilincli olarak jeton karakteri SAYILMIYOR - iceri alindiginda
`.../#/dogrula/<jeton>` tek parca sayilip YOL da yutuluyordu (pin bunu yakaladi). Bedeli:
standart base64 (base64url degil) bir sir `/` karakterlerinde parcalara bolunur; her parca
ayri degerlendirilir ve 16+ karakterli olanlar YINE maskelenir. Olctugumuz jetonlarin
(dogrulama/sifirlama, JWT, Guid) hicbiri `/` icermiyor.

## SUREC (degismez)

- **Tek push -> tek run -> tek rapor.** Commit/push karari HER ZAMAN kullanicidan gelir.
- **FORCE-PUSH YASAK (kalici).** Gecmisi yeniden yazmak paylasilan `main`'i bozar, tum
  klonlari ayristirir ve daha once verilen HER run raporunun SHA'sini gecersiz kilar -
  raporlarin kanit degeri SHA'ya bagli oldugu icin bu, gecmis butun kaniti curutur.
  Depoya yanlislikla giren bir sey varsa cozum: ileriye donuk maskeleme + gerekiyorsa
  **DAR KAPSAMLI** `.gitleaksignore` fingerprint'i (bkz. `.gitleaksignore` basligi).
  Gercek bir kimlik bilgisi sizarsa yol farklidir: once **iptal/rotasyon**, sonra karar -
  o durumda gecmis yeniden yazmak gundeme gelebilir ve karar kullanicinindir.
- **Push on-onayinin dort kosulu**: (a) `Category=Sql` yerel komut yesil,
  (b) tam suit yesil, (c) Release build 0 hata, (d) o sprintin pinlerinde dis kontrolu
  (>=3 assert ters cevir -> isimli kirmizi gozle -> geri al).
- **Test sayilari CI'dan OKUNAMAZ.** Job log'u anonim erisime 403, Summary imza istiyor,
  annotation yalniz `Failed` satirlari tasiyor, check-run `output` bos (dordu de denendi).
  Kanit = **adimin SUCCESS olmasi** + yerelde `ci.yml`'dan cikarilan komutun verdigi sayi.
- **`secret-scan` TERSINE: ANNOTATION'DAN DEGIL ADIM SONUCUNDAN OKUNUR (kalici kural).**
  Gitleaks bulgusunu **`warning`** seviyeli bir annotation olarak basiyor
  ("Leaks detected, see job summary for details"); job'da `failure` seviyeli annotation
  **SIFIR** kaliyor. Yani annotation'a bakan bir okuyucu bu job'i YESIL sanir. Tek durust
  sinyal **adim sonucu** (`Gitleaks (secret taramasi)` = FAILURE). Ayrintili bulgu listesi
  Summary'de ve SARIF artefaktinda; ikisi de imza istiyor (artefakt indirme anonim **401**,
  `code-scanning/alerts` anonim **401** - ikisi de olculdu). Kok sebep bu yuzden **depo
  taramasiyla** bulunur, kanit kanalindan degil.
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
- **DIS/MUTASYON KONTROLUNDEN ONCE `Divisima.API.exe` DURDURULUR (iki kez bedeli odendi).**
  API kosarken `dotnet build`, bagimli projelerin (Bussiness/API) ciktilarini yazamaz ve
  **SESSIZCE ESKI IKILILERLE** devam eder: `dotnet test --no-build` bir ONCEKI kosumun
  sonucunu birebir tekrarlar. Mini dalgada tam bu yasandi - mutasyon kosumu, diş kontrolu
  kosumunun ciktisinin AYNISINI verdi ve mutasyon uygulanmamis gibi gorundu.
  **TESHIS:** build ciktisinda `tail -1` ALDATIR (yalniz "Geçen Süre" satirini gosterir);
  her zaman `grep " Hata"` ya da `grep "error"` ile bakilir.
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
