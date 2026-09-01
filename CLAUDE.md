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

## DALGA ICI DENETIM - HER DALGADA, PUSH'TAN ONCE (KALICI)

Bir dalganin kod isi bittiginde, **PUSH'TAN ONCE** o dalganin YAPTIGI HER SEY tek tek geri
donulur ve kanitlanir. Rapora AYRI BASLIK olarak yazilir: **"DALGA ICI DENETIM"**.
Kullanici ayrica ISTEMEZ - bu dalgadan itibaren her dalgada uygulanir.

Alti baslik, sirayla:

1. **KALEM KALEM.** Dalgada dokunulan her kalem icin: ne olculdu, ne degistirildi, hangi
   KANITLA dogrulandi, hangi PIN koruyor. Kaniti OLMAYAN her satir ISARETLENIR -
   "degistirdim ama surmedim" varsa ACIKCA soylenir.
2. **YARIM KALAN VAR MI.** Dalganin kapsaminda olup atlanan, ertelenen ya da "sonra bakarim"
   denen ne kaldiysa LISTELENIR.
3. **YAN ETKI TARAMASI.** Degistirilen her sozlesmenin / alanin / davranisin BASKA nerede
   kullanildigi taranir; tuketiciler cikarilir ve hepsinin hala tutarli oldugu GOSTERILIR.
   Ozellikle: DTO alani degistiyse panel + storefront + mail; davranis degistiyse ona
   guvenen pinler.
4. **KENDI HATALARIM.** Bu dalgada kac kez yanlis olculdu, yanlis varsayildi ya da bir
   duzeltme baska bir seyi kirdi - HEPSI yazilir.
5. **PIN DURUSTLUGU.** Eklenen pinler gercekten DAVRANISI mi olcuyor, yoksa yalniz KAYNAK
   METNINI mi? Kaynak-sozlesmesi pinleri isaretlenir ve davranis kanitinin NEREDE oldugu
   soylenir.
6. **BOZDUKLARIM.** Bilincli kirilan her pinin YERINE konan pin AYNI SEYI mi koruyor -
   tek tek karsilastirilir.

**Denetim bulgu cikarirsa: duzeltme karari KULLANICININ, PUSH BEKLER.**

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
- **BIR DOSYA KENDI ICERIGINDEN TURETILEREK USTUNE YAZILMAZ (KALICI - GUVENLIK-FIX-3'te
  bedeli odendi).** Kabuk `>` yonlendirmesini komut CALISMADAN ONCE acar ve hedefi **budar**.
  Yani `awk ... CLAUDE.md > CLAUDE.md` ya da girdisi hedeften tureyen (`cp hedef yedek &&
  awk ... yedek > hedef`) her zincir, ARADAKI HERHANGI BIR ADIM DUSERSE hedefi **SIFIR BAYT**
  birakir. GUVENLIK-FIX-3'te birebir yasandi: bir onceki komutta `awk` girdi dosyasini
  bulamayip dustu, ama `> CLAUDE.md` coktan calismisti - **6670 satirlik dosya sifirlandi**.
  Kurtaran sey `git checkout -- CLAUDE.md` oldu (calisma agaci o dosya icin TEMIZDI).
  **KURAL:** cikti **GECICI bir dosyaya** yazilir, **satir sayisi/boyutu DOGRULANIR**, ancak
  ondan sonra hedefin ustune tasinir:
  ```
  awk ... CLAUDE.md > $T/yeni && N=$(wc -l < $T/yeni) && [ "$N" -gt <esik> ] \
     && cp $T/yeni CLAUDE.md || echo "IPTAL - dosyaya DOKUNULMADI"
  ```
  **YEDEGIN VAR OLDUGU DA DOGRULANIR** - yedegi alan komut basarisiz olduysa "yedek var"
  varsayimi ikinci bir kayip uretir. Ayrica: takip edilmeyen (untracked) bir dosyada bu hata
  **GERI ALINAMAZ** - git kurtarmaz.
- **COK SATIRLI KOD BLOKLARI BETIKLE DEGISTIRILMEZ (KALICI - FLAKE-FIX'te bedeli odendi).**
  `perl -0pi -e 's|...|...|'` ile cok satirli bir C# blogunu degistirmek, desen bir karakter
  bile kaymissa dosyayi SESSIZCE BOZAR. FLAKE-FIX'in M1 mutasyonunda birebir yasandi:
  `Program.cs`'in `using` blogu govde ile birlesti ve build **82 hata** verdi; test o turda
  BAYAT IKILILERLE kosup 1 kirmizi verdigi icin sonuc GECERSIZ oldu ve ancak
  **"(b) TEMIZ BUILD"** adimi sayesinde yakalandi. **KURAL:** cok satirli kod degisikligi
  hassas duzenleme araciyla yapilir; betik kullanildiysa (a) `[MUTASYON]` izi, (b) BUILD HATA
  SAYISI ve (c) `git diff --stat` ile "yalniz amaclanan degisiklik" DOGRULANIR. Ayni tuzagin
  markdown karsiligi capa benzersizligidir (GUVENLIK-FIX-4) ve dosya budama karsiligi
  yonlendirmedir (GUVENLIK-FIX-3) - ucu de AYNI aile: DUZENLEME SONRASI DOGRULAMA.
- **5. KONTROLUN KENDISI DOGRULANIR (KALICI - kullanici karari, Dalga D).**
  5. kontrolun sonucu ("mutasyon lokalize kaldi") ancak mutasyon GERCEKTEN uygulandiysa
  anlamlidir. Dalga D'de uc mutasyon **HIC UYGULANMADI** (`powershell -File` yurutme
  politikasina takildi) ve testler "14 basarili" dedi - yani rapor "mutasyon lokalize"
  diye YANLIS yazilacakti. Fark edilmesi kalinti kontrolune, yani TESADUFE kalmisti.
  Bundan sonra HER uretim mutasyonunda, sirayla:
  - **(a) YAZILDI MI:** mutasyonun dosyaya gercekten indigi `grep` / `git diff` ile
    DOGRULANIR. "Betik hata vermedi" kanit DEGILDIR.
  - **(b) TEMIZ BUILD:** mutasyondan sonra derleme yapilir ve `grep " Hata"` / `grep "error"`
    ile bakilir (`tail -1` ALDATIR). `--no-build` ile kosulan test degistirilen kodu
    dogrulamaz; `Copy-Item` zaman damgasini korudugu icin geri alinan dosya `touch`lanir.
  - **(c) BEKLENEN PIN KIRMIZI OLMADIYSA:** bu **"mutasyon lokalize"** DEGIL,
    **"MUTASYON UYGULANMADI"** suphesidir. ONCE bu ihtimal elenir; ancak (a) ve (b)
    kanitlandiktan sonra "lokalize" sonucu yazilabilir.
  Ayni kural DIS KONTROLU icin de gecerlidir (ters cevrilen assert dosyaya indi mi).
- **TEST, URUNUN GERCEK KAYNAKLARINA DOKUNMAZ (KALICI - kullanici karari, Dalga D).**
  Bir test altyapisi eklenirken sorulacak soru: **bu, gelistiricinin ya da uretimin GERCEK
  kaynagina mi yaziyor?** Kaynak = depo agaci, gelistirici veritabani, kullanici secret'lari,
  gercek dosya sistemi, dis saglayici. Cevap "evet" ise test AYRI, atilabilir bir koke
  yonlendirilir. Ayni sinif **UC KEZ** cikti ve ucu de sessizdi:
  - **wwwroot sizintisi (D1):** her kosum depo agacina 64 baytlik sahte PNG birakiyordu
    (96 dosya birikmisti). Cozum: `UseWebRoot(TestWebRoot.Yol)` - UCUNCU bir kok.
  - **user-secrets sizintisi (Dalga C):** `WebApplicationFactory` Development'ta user-secrets
    yukledigi icin `AdminSeed:Enabled=true` her test host'una siziyordu - sonuc MAKINEYE gore
    degisiyordu. Cozum: `TestHostConfig`te varsayilan `false`.
  - **Hangfire dev DB'ye yaziyordu (Dalga D):** her test host'u kosulsuz bir arka plan
    sunucusu acip GELISTIRICININ veritabanina recurring job tanimi yaziyor ve dakikalik
    outbox isini testlerin drenajiyla YARISTIRIYORDU (CI kirmizisi `cd51a52`).
    Cozum: `BackgroundJobs:Enabled=false`.
  Ortak belirti: **yerelde yesil, CI'da kirmizi** (ya da tersi) - yani sonuc ORTAMA bagli
  hale gelir ve pin yalan soyler. Yeni yazilan her test altyapisi bu soruyla gecer.
- **Izleyici adabi**: nabiz >= 300 sn, tur basina TEK konsolide cagri, kota yandiysa bekle.
  Dependabot run'i beklenmez - asil iki workflow (CI + Security) yeter.
- **PAT veya tarayici eklentisi ASLA istenmez.**
- **Yerel SQL**: `DIVISIMA_TEST_SQL` her zaman set edilir (skip modu kullanilmaz);
  dizgede `Database=` bulunmalidir. LocalDB cokmus durumda ve **`sqllocaldb delete`
  YASAK** (ayni ornekte baska bir projenin `GarajimDb` veritabani var). Tam ornek
  (`Server=localhost`) kullaniliyor.
- **Uretim kodu**: yalniz kullanicinin acikca izin verdigi kalemlerde. Kapsam disi
  bulgular duzeltilmez, **SUPHELI DAVRANISLAR** basligiyla raporlanir.

---

# SDP — SAHADA DOGRULANMIS DENETIM PROTOKOLU v1.2 (KALICI; v1.1 27 Agustos 2026, v1.2 28 Agustos 2026)

**Bu bolum BAGLAYICIDIR: bundan sonraki her CC isi bu protokole uyar.**
v1.0 MTUR-OLCUM turunda sahada surulda; her v1.1 maddesi O TURDA OLCULEN bir
surtunmeye dayanir ve gerekcesi maddenin yaninda yazilidir. Iki parcadir:
proje-bagimsiz CEKIRDEK ve depoya ozgu DIVISIMA EKI.

## 1. SDP-CEKIRDEK v1.2 (PROJE-BAGIMSIZ)

### 1.1 ORANTILILIK
Denetim derinligi RISKLE orantilidir; amac toren degil, **YANLIS RAPORUN
IMKANSIZLASMASI**. Maliyet olculur ve sonraki surum kalibre edilir.

| Seviye | Ne zaman | Denetim bicimi |
|---|---|---|
| **L1** kaynak tespiti | "kod boyle yaziyor" turu iddialar | Denetci, satir iddialarinin **>=%50 RASTGELE** orneklemini KENDI actigi dosyalardan dogrular; rastgelelik yontemi kayda gecer |
| **L2** davranis/canli | "sistem boyle davraniyor" turu iddialar | TAM BAGIMSIZ denetci; paketteki HER kaniti KENDI komutuyla yeniden uretir. **Kopyala-onayla GECERSIZ**: kendi komut ciktisi olmayan onay sayilmaz |
| **L3** kritik | para · stok · oturum · durustluk | **CIFT-KOR**: denetci ana akis sonuclarini GORMEDEN, yalniz gorev tanimi + kendi planiyla olcer; sonuclar sonra kiyaslanir, her fark tek tek kapatilir |

Is turu -> seviye: para/stok/oturum/durustluk = **L3** · davranissal = **L2** ·
salt kaynak okumasi = **L1**.

### 1.2 KANIT DEFTERI (tek gercek kaynak, APPEND-ONLY)
Satir silinmez/degistirilmez. Duzeltme:
`[KALEM][sira-DUZELTME] SUPERSEDES sira-n + gerekce`.

SEMA: `[KALEM][sira][SINIF][GUVEN] IDDIA | KOMUT | CIKTI-OZETI | HAM: yol | SHA | SAAT`
SINIFLAR: `K`=kaynak · `C`=canli · `D`=DB · `A`=ag · `J`=journal/denetim
GUVEN: KESIN / YUKSEK / ORTA / DUSUK — **DUSUK tek basina fix dayanagi OLAMAZ.**

Zorunlu kayit turleri:
- **ON-KAYIT**: her kalemde OLCUMDEN ONCE `[KALEM][PLAN]` — sorular + komut taslagi +
  **KARAR KRITERI** ("X gorursem kirik, Y gorursem saglam"). Sapma serbest ama
  `[PLAN-SAPMA]` gerekceli. **Bu zorunluluk AJAN SEMALARINA GOMULUR** (bkz 1.3).
- **ANLIK GORUNTU**: AYRI bir kayit turudur ve **ON-KAYIT kurali KAPSAMINDA DEGILDIR**.
  *(v1.1 — gerekce: MTUR ara kapisi anlik goruntuleri "plansiz olcum" sayip YANLIS ihlal
  uretti.)*
- **YOKLUK**: "temiz/yok" da bir IDDIADIR — `[YOKLUK]` + tarama kapsami + komut +
  **negatif kontrol kaniti** sart.
- Denetci/hakem gorev promptlari da deftere girer (verbatim ya da yol).
- **FINAL RAPOR YALNIZ DEFTERDEN TURETILIR.**

### 1.3 ROLLER ve SEMA ZORUNLULUGU
- **ANA AKIS** olcer, bulgu paketi uretir.
- **DENETCI (L1/L2/L3)** dogrular. Karar: `ONAY` / `ITIRAZ`(gerekce + KENDI kaniti) /
  `OLCEMEDIM`. Ayrica **PLAN-UYUM** kontrolu: sonuc PLAN'daki karar kriteriyle mi
  verilmis; kriter degistiyse `[PLAN-SAPMA]` gerekcesi var mi.
- **HAKEM**: yalniz cozulmeyen itirazda, 1 tur, iki tarafin kanitini gorur, kararini
  KENDI olcumuyle verir.
- **RAPOR DENETCISI** (final): taslagi DEFTERE karsi satir satir — (a) defterde olmayan
  iddia = **UYDURMA ADAYI, en agir**, (b) rapora girmeyen kritik bulgu, (c) sayi
  uyusmazligi, (d) gruplar arasi capraz tutarlilik, (e) denetim matrisi <-> defter
  eslesmesi, (f) **SUPERSEDES zinciri** — rapor gecersiz kilinmis bir satira dayaniyor mu.
  *(v1.1 (f) — gerekce: MTUR'da muhurlu bir kanit, defterlenmemis bir olcumle
  DEGISTIRILMISTI; rapor denetcisi yakaladi.)*
- **KURAL-UYUM DENETCISI** (final): baslangic anlik goruntusune karsi KENDI komutlariyla;
  **git diff KAPSAM TARAMASI** (yalniz beklenen dosyalar degismis mi) ve **cift-kor
  izolasyon kaniti** dahil.
- **SEMA KURALI (v1.1):** `plan` alani **TUM ajan semalarinda ZORUNLUDUR** (yalniz L3'te
  degil), karar kriteri dahil. *(Gerekce: MTUR'da L1 kaynak semasinda zorunlu olmadigi
  icin YEDI kalem plansiz kaldi ve ara kapi bunu ihlal olarak buldu.)*

### 1.4 AKIS ve SONLANMA
olcum -> BULGU PAKETI deftere -> denetci -> `ONAY`/`ITIRAZ`/`OLCEMEDIM` -> itirazda
yeniden olcum. **Ana <-> denetci EN FAZLA 2 TUR** -> **HAKEM** (1 tur) -> cozulmezse
**CEKISMELI** -> merkez.

BUTCELER: denetci yazmali tekrari kalem basina 1 · hakemde 1 · ana akis kalem basina en
fazla 2 derinlesme turu, sonrasi "KISMEN OLCULDU + neden".
**PLANSIZ AJAN DAGITILAMAZ**: dagitim plani (kim, ne, hangi seviye) ONCE deftere.

### 1.5 ARA KAPILAR (her grup sonunda)
1. **DEFTER BUTUNLUK BOTU**: her satirin HAM dosyasi mevcut + SHA tutuyor · her kalemde
   PLAN satiri olcumden ONCE · PLAN'siz olcum satiri yok (anlik goruntuler HARIC) ·
   suzgec sinamalari kayitli.
2. **GRUP ICI CAPRAZ TUTARLILIK**: grubun kalemleri birbiriyle celisiyor mu (2-3 satir).
3. **CHECKPOINT + 2 satir mini-retro.**

### 1.6 BULGU BICIMI
- **SIDDET**: `[PARA]` / `[VERI-BOZAN]` / `[OTURUM]` / `[DURUSTLUK]` / `[MANTIK]` /
  `[UX]` / `[KOZMETIK]` + **AKTIF|LATENT** + tek satir maruziyet.
  **`[MANTIK]` (v1.2, GEZGIN modulunden):** kod DOGRU calisirken bile SACMA / CELISKILI /
  YANILTICI olan sey. Onceki liste bunu ifade edemiyordu.
- **KOR NOKTA**: her kalem kapanisinda "bu olcumun goremeyecekleri" 1-2 satir; denetci
  ekleyebilir.
- **REPRO**: davranissal her bulguya NUMARALI yeniden-uretim blogu (temiz kosullar +
  adimlar + beklenen KIRIK sonuc). **Fix dalgasinin once/sonra olcumu BIREBIR bununla
  kosar.**
- **BULGU PAKETINE GOMULU NOT (v1.1):** "Satir numarasi kaymasi ITIRAZ DEGILDIR —
  denetci iddianin OZUNU dogrular, kaymayi NOT eder." *(Gerekce: MTUR'da bu, prompt'a
  elle eklenmek zorunda kaldi.)*

### 1.7 OLCUM DISIPLINI
1. **SINIFLANDIRICI ONCE BILINEN GIRDIYLE SINANIR** — her eslestirme dizgesi/grep/cikis
   kosulu, KARAR icin kullanilmadan once bilinen-POZITIF **ve** bilinen-NEGATIF girdiyle
   sinanir; sinama deftere. Hata yutan bir yedek (`|| echo`, `2>/dev/null`, try/catch)
   KARAR besleyemez.
2. **AD/ROTA/KOLON KAYNAKTAN OKUNUR, TAHMIN EDILMEZ** — okundugu yer yazilir.
3. **CALISMA ORTAMI OLCULUR — ZORUNLU ILK ADIM (v1.1).** Kosan surecin **KOMUT SATIRI**,
   ortam degiskenleri ve gizli yapilandirma katmanlari (user-secrets vb.) olculur ve
   deftere gecer. *(Gerekce: MTUR'da BES komut satiri argumani — odeme modu, arka plan
   isleri, posta host'u, rate limit, admin seed — URUN DAVRANISI SANILIYORDU; olculunce
   IKI iddia birden duzeldi.)*
4. **AYIRT EDICI DENEY (v1.1, kalip):** kok sebebi, tahminin ONCEDEN AYRISTIGI iki
   girdiyle **TEK olcumde** sina. *(Gerekce: MTUR'da misafir sepetinin kok sebebi,
   "mock katalogda olan" ve "yalniz gercek katalogda olan" iki kalemle tek yenilemede
   ispatlandi.)*
5. **DINAMIK VERI**: canli sayilar ZAMAN DAMGALI. Denetci fark gorunce ONCE yazma
   envanterine bakar — kurgu kayit farki itiraz sebebi DEGILDIR; itiraz yalniz ayni
   kosulda YENIDEN URETILEMEYEN iddiaya.

### 1.8 KURAL SIMETRISI (v1.1)
Ana akis ve TUM denetciler **TEK ORTAK KURAL METNINI** alir (ayni yasak listesi).
*(Gerekce: MTUR'da "user-secrets okuma" yasagi yalniz kaynak ajanlarina verilmisti;
denetci onu okudu — yalniz uzunluk olctu, deger basmadi, sizinti olmadi — ama asimetri
kayda gecti.)*

### 1.9 IZOLASYON (v1.1)
L3 cift-kor izolasyonu **PROMPT duzeyinde YETMEZ**; teknik olarak da saglanir: ajanlara
ayri calisma dizini verilir, ana akisin ara dosyalarina erisim yolu ACILMAZ ve kural-uyum
denetcisi transkriptleri tarayarak **izolasyon kaniti** uretir (pozitif kontrollu).

### 1.10 RETRO ve SURUMLEME
Her tur sonunda: ne iyi calisti · ne surtundu · changelog onerileri. Surum numarasi artar;
her madde degisikligi OLCULEN bir surtunmeye dayanir. **DENETIM MALIYETI RAPORLANIR**
(ajan sayisi, tur sayisi, plan sapmasi, ara kapi bulgusu) — kalibrasyon icin.

### 1.11 GEZGIN TURU (v1.2 - MANTIK-AV-1'in ilk uygulamasindan turetildi)

#### 1.11.1 NE ZAMAN KOSULUR
Gezgin turu, bir urun yuzeyi "kabul edildi" sayildiktan SONRA ve bir sonraki fix dalgasi
BASLAMADAN once kosulur. Amaci regresyon aramak DEGIL, **kabul turunun goremedigini**
gormektir: kabul turu MADDE LISTESI izler, gezgin tur **kullanici gibi dolasir**.

**IKI AV BIRDEN, ESIT AGIRLIKTA:**
- **(a) BUG** - kod yanlis calisiyor.
- **(b) MANTIK/TUTARSIZLIK** - kod DOGRU calisiyor ama sonuc SACMA, CELISKILI ya da
  YANILTICI. Bu ikincisi klasik test disiplininin YAPISAL kor noktasidir: pin de, tip
  sistemi de, CI de "bu ekran yalan soyluyor" demez.

#### 1.11.2 PERSONA KALIBI
Gezgin turu **personalarla** kosulur. Persona = bir kullanici niyeti + o niyetin dogal
yolu. En az uc, tipik olarak bes persona; her biri AYRI ajan, AYRI defter, AYRI kurgu hesabi.

| Persona | Niyet | Ozellikle avladigi |
|---|---|---|
| **A - MISAFIR** | kimlik dogrulamadan satin almak | misafir sinirlari, cikmaz yollar, anonim uc davranisi |
| **B - YASAM DONGUSU** | kayittan cikisa tum uye yolu | durum makinesi, ekranlar arasi tutarlilik, yarim kalan islem |
| **C - PARA** | her kurusu sorgulamak | matematik, kupon, esik, yuvarlama, vaat-fiyat uyusmazligi |
| **D - DIL/BICIM** | urunu kendi dilinde kullanmak | ceviri bosluklari, tarih/sayi/para bicimi, RTL, state korunumu |
| **E - SUPHECI SAYI** | ekrandaki her sayinin kaynagini istemek | ayni buyuklugun iki yerde farkli olmasi, defter-sayac ayrismasi |

**PERSONA KURALI:** her persona kendi niyetinin DISINA cikmaz. Kapsam cakismasi
kacinilmazdir ve ISTENIR - iki personanin ayni seyi BAGIMSIZ bulmasi capraz dogrulamadir.

#### 1.11.3 HER EKRANDA ALTILI TUTARLILIK LISTESI
Gezgin, dokundugu HER ekran/uc icin alti soruyu AYRI AYRI sorar ve deftere yazar:
1. **SAYI** - ayni buyukluk iki yerde farkli mi?
2. **DIL/BICIM** - secili dile aykiri metin / tarih / sayi / para birimi?
3. **VAAT<->DAVRANIS** - buton/metin NE VAAT EDIYOR, gercekte NE OLUYOR?
4. **DURUM MAKINESI** - durum ADLARI ve GECISLERI mantikli ve TUM DILLERDE tutarli mi?
5. **BOS/UC** - 0 sonuc, bos liste, cok uzun ad, buyuk adet, sinir degeri, negatif.
6. **MATEMATIK** - toplama / indirim / yuvarlama / kurus.

Alti sorunun **hepsi** yazilir; "ilgisiz" yaniti da bir yanittir. Boylece KAPSAM MATRISI
(ekran x persona x soru) mekanik olarak dolar ve neyin OLCULMEDIGI gorunur.
**KAPSAM MATRISI KELIME SAYIMIYLA URETILMEZ** - personalarin KENDI kapsam tablolarindan
derlenir (MANTIK-AV-1'de kelime sayimi denendi ve YANILTICI cikti).

#### 1.11.4 YANLIS-POZITIF ELEME (ZORUNLU ON ADIM)
Gezgin turu BASLAMADAN once **BILINCLI KARARLAR LISTESI** derlenir ve TUM personalara
verilir. Dort kaynaktan beslenir: olcum duzeneginin kendisi (test bayraklari, mock modlar,
kapali arka plan isleri) · veri zemini (test katalogu, bekleyen temizlik artiklari) ·
bilincli urun kararlari (kabul edilen riskler, ertelenen kalemler) · zaten kuyruktaki
paketlenmis bulgular.

Kural: listedeki bir sey **BULGU DEGILDIR**. Paketlenmis bir bulgu bagimsiz yeniden
kesfedilirse **"BILINEN - capraz dogrulama"** etiketiyle TEK SATIR yazilir; sayilmaz ama
SINIRINI genisleten kisim YENI bulgudur. "Bilincli mi emin degilim" kalanlar **SORU**
listesine gider - bulgu ile soru KARISTIRILMAZ.

**LISTENIN HER MADDESI TUR BASINDA YENIDEN OLCULUR** ya da acikca **"BAYAT OLABILIR"**
etiketi tasir. Gerekce olculdu: MANTIK-AV-1'de listeye onceki muhurden kopyalanan bir
madde ("product_images BOS") gercekte 30 satir/30 dosya cikti; o madde yalnizca
YANLIS NEGATIF uretebilecegi icin zarar vermedi, ama listenin 23 maddesinden yalniz biri
yeniden olculmus oldu ve bu turun SISTEMATIK kor noktasi olarak kayda gecti.

**GEREKCE:** bu eleme yapilmazsa gezgin turunun ciktisinin buyuk kismi "mock odeme
calismiyor", "mail gelmedi", "urun adlari sacma" gibi ZATEN BILINEN duzenek gercekleriyle
dolar ve gercek bulgular gurultuye gomulur.

#### 1.11.5 ARAC PAYLASIMI VE SERILESTIRME
Personalar paralel kosar; **ancak paylasilan ve durum tasiyan araclar serilestirilir.**
Tarayici bunun kanonik ornegidir: ayni origindeki tum sekmeler `localStorage`'i PAYLASIR,
dolayisiyla bes personanin oturum/sepet durumu birbirini bozar. (MANTIK-AV-1'de olculdu:
CORS `AllowedOrigins` tek origine acik oldugu icin ayri origin acmak da mumkun degildi.)

**KURAL:** paylasilan durumlu bir arac varsa personalar ondan MEN EDILIR ve her persona
raporunun sonunda **ARAC DOGRULAMA LISTESI** verir:
```
TD-<PERSONA>-<n> | EKRAN | ADIMLAR | OLCULECEK IFADE | BEKLENEN(saglam) | BEKLENEN(kirik)
```
Ana akis bu listeleri SERILESTIREREK kosar. Persona iddiasini arac sonucuna BAGLI
birakmaz: elindeki kaynak/API/DB kaniti kadar konusur, arac olcumunu **EK KANIT** ister.

#### 1.11.6 CIKTI DISIPLINI
Her bulgu: gozlem + **numarali REPRO** + kok-sebep adayi `dosya:satir` + siddet +
AKTIF/LATENT + persona + **kor nokta**.
- **`[MANTIK]` siddet sinifi bu modulle CEKIRDEGE GIRER** (bkz. 1.6): kod dogru
  calisirken bile sacma/celiskili/yaniltiici olan sey. Onceki siddet listesi bunu ifade
  edemiyordu.
- **AKTIF/LATENT AYRIMI OLCULUR, VARSAYILMAZ.** Bir uydurma icerik DOM'da duruyor olabilir
  ama kullanicinin gordugu ANDA yerini baska bir seye birakiyor olabilir; bu ayrim ancak
  KULLANICININ IZLEDIGI YOL kosularak yapilir.

#### 1.11.7 KOK SEBEP BIRLESTIRME
Gezgin turu cok sayida YUZEYSEL belirti uretir. Rapor yazilmadan once **belirtiler kok
sebebe gore GRUPLANIR**. Bir kok sebep birden cok belirti acikliyorsa fix dalgasi
BELIRTILERI degil KOKU hedefler.
**OLCULEN ORNEK (MANTIK-AV-1):** dort ayri belirti - bos sepet onerilerinde uydurma urun ·
sitenin kendi navigasyonunda iki 404 · sekiz koleksiyon sayfasinin bos olmasi · ayni
rotanin gelis yoluna gore 24 ya da 33 urun gostermesi - TEK kokten cikti: vitrin dosyasi
hala ESKI MOCK TAKSONOMISINI ve 18 MOCK URUNU tasiyor; onceki dalga yalniz MENUYU
veritabanina baglamisti.

#### 1.11.8 FIX DALGASI ESLEMESI
Gezgin turu **SALT OLCUMDUR**; fix baslatmaz. Cikti su sekilde dalgalara doner:

| Bulgu sinifi | Hedef dalga |
|---|---|
| `[PARA]` / `[VERI-BOZAN]` / `[OTURUM]` | **ONCELIKLI** kendi dalgasi |
| `[DURUSTLUK]` (uydurma icerik, yanlis vaat) | launch-bloker sinifi; ilk dalga |
| `[MANTIK]` - tek kokten cikan grup | KOK BASINA tek dalga (belirti basina DEGIL) |
| `[UX]` / `[KOZMETIK]` | biriktirilip tek pakette |
| SORU listesi | merkeze; karar sonrasi dalgaya |

Dalga bolumlemesi **MERKEZDEN**; gezgin yalniz siniflandirir ve onceliklendirir.
**SIRALAMA KENDI OLCUTUNE UYMAK ZORUNDADIR** - MANTIK-AV-1'de "PARA > YASAL/VERI >
DURUSTLUK > UX" olcutu yazildigi halde bir `[UX]` kalemi sekizinci siraya konup bir
`[PARA]` kalemi listeye HIC alinmamisti; rapor denetcisi bunu yakaladi.

#### 1.11.9 GEZGIN TURUNUN KENDI KOR NOKTALARI (durust kayit zorunlulugu)
Gezgin raporu su ucunu ACIKCA yazar:
1. **Arac sinirlari** - olcum riginin YAPISAL olarak goremedikleri.
2. **Kosulmayan yollar** - onkosulu bugunku veriyle saglanamayan senaryolar.
3. **Onlenen yanlis bulgular** - "bulgu sandim, olcunce degilmis" kalemleri. Bunlar
   RAPORDAN SILINMEZ; gezgin turunun kalibrasyonu bu kayitlarla yapilir.

#### 1.11.10 GEZGIN TURUNUN DENETIM KAPISI
Gezgin turu **IKI denetciyle** kapanir; ikisi de personalarin sonuclarini gormeden kendi
komutlariyla olcer.

**RAPOR DENETCISI** - taslak bulgu kumesini DEFTERLERE karsi satir satir tarar (1.3'un
(a)-(f) maddeleri) ve ayrica **KANIT GUCU TABLOSU** uretir: her yuksek riskli bulgu kac
BAGIMSIZ KANALDAN (kaynak / API / DB / arac) dogrulanmis. Tek kanalli bir bulgu, cok
kanalli bir bulguyla ayni siddet sirasina KONMAZ.

**KURAL-UYUM DENETCISI** - turun kendi kurallarina uydugunu olcer. Salt-olcum turlarinda
zorunlu maddeler: kod degismedi · veri tabanina yalniz okuma · muhurler ureten ifadesiyle ·
dokunulmaz hesaplar/kayitlar kullanilmadi · kurgu envanteri raporlarla BIREBIR · sir
sizintisi · arac yasaklarina uyum.

##### 1.11.10-a URETIM IMZASI
"Veri tabanina yalnizca okuma yapildi" iddiasi DOGRUDAN gozlenemez (elle yazilan bir
`UPDATE` denetim izine dusmeyebilir). Bunun yerine **URETIM IMZASI** olculur: turda olusan
satirlarin URETIM YOLUNDAN geldigi, o yolun URETTIGI YAN ETKI ZINCIRIYLE kanitlanir.
Ornek: bir siparis satirinin yaninda kalem, rezervasyon, stok hareketi, fatura ve durum
gecmisi satirlari da olusmus olmalidir; elle bir `INSERT` bunlari uretmez. Kimlik ureteci
varsa (siparis numarasi vb.) BICIMI de kaynaktan okunup karsilastirilir.
**Kesin kanit degildir, IKI KANALLI guclu kanittir - raporda boyle yazilir.**

##### 1.11.10-b DOKUNULMAZ KAYITLARIN ICERIGI DE OLCULUR
Bir kaydin "korundugu" iddiasi SAYI ile kapanmaz. Onceki muhurler o kayitlarin ICERIGINI
(durum, tutar, zaman damgasi) not etmisse, denetci onlari da karsilastirir. Gerekce:
sayisi degismeden icerigi degismis bir kayit, yalnizca sayan bir kontrolden GORUNMEDEN
gecer. (MANTIK-AV-1'de kabul turunun dort kaydi saat damgasina kadar eslendi.)

##### 1.11.10-c PERSONA IZOLASYONUNUN AMPIRIK OLCUMU
Teknik izolasyon (ayri calisma dizini) uygulanamadigi turlarda, izolasyonun FIILEN tutup
tutmadigi **capraz atif sayimiyla** olculur: her personanin defterinde DIGER personalarin
adi kac kez geciyor. Beklenen 0; sayim **pozitif kontrollu** yapilir (personanin KENDI adi
bulunmali). Bu, mekanizmanin yerine gecmez ama korudugu degerin saglanip saglanmadigini
gosterir.
**KURAL:** kod URETEN bir dalgada teknik izolasyon (MK-4) ZORUNLUDUR; SALT-OLCUM turunda
ampirik olcum kabul edilebilir ve raporda ACIKCA "mekanizma uygulanmadi, korudugu deger
olculdu" diye yazilir.

##### 1.11.10-d DENETCININ KOR NOKTASI ANA AKISA GERI DONER
Denetci "sunu OLCEMEDIM" dediginde, ana akis o boslugu KAPATMAYA CALISIR ve sonucu deftere
yazar. Kapatamiyorsa bosluk RAPORDA ADIYLA durur. (MANTIK-AV-1 ornegi: denetci ham API
dokumlerini sir taramasina dahil etmedigini yazdi; ana akis tum scratchpad agacini - 47
dosya, uzanti farketmeksizin, suzgec sinanmis - tarayip boslugu kapatti.)

## 2. DIVISIMA EKI v1.2 (DEPOYA OZGU)

### 2.1 FIX DALGALARINA ESLEME
Mevcut pin disiplini (pin + dis kontrolu + 5. kontrol/mutasyon) **KORUNUR**; SDP onun
YANINA eklenir:

| Mevcut | SDP eki |
|---|---|
| PIN yazilir | — |
| DIS KONTROLU (assert ters -> isimli kirmizi) | — |
| 5. KONTROL (uretim mutasyonu) | — |
| — | **DAVRANIS DENETCISI (L3 cift-kor)**: dalganin ONCE/SONRA REPRO bloklarini ana akisin sonuclarini GORMEDEN yeniden uretir |
| — | **RAPOR DENETCISI**: dalga raporunu deftere karsi tarar |
| — | **KURAL-UYUM DENETCISI**: `git diff` KAPSAM taramasi — dalga kapsami disinda dosya degismis mi |

KURAL: bir FIX dalgasinda her REPRO blogu, olcum turunda yazilan **NUMARALI blokla
BIREBIR ayni adimlari** kosar; fix "once kirik / sonra saglam" olarak AYNI komutla
gosterilir.

### 2.2 IZLEYICI SOZLESMESININ SDP ICINDEKI YERI
Bolum "KALICI KURAL - IZLEYICI / OLCUM ARACI SOZLESMESI" maddesi, SDP CEKIRDEK 1.7/1'in
OZEL BIR HALIDIR. Ayni aile: **mekanizmanin CALISTIGI, sonucu bilinen bir girdiyle BIR KEZ
gozlenir.** Depoda bu ailenin bes ornegi kayitli (`Identity.Name` · `IDistributedCache` ·
uygulanmayan mutasyonlar · izleyici cikis kosulu · MTUR'daki grep hane-sayisi/diakritik
tuzaklari).

### 2.3 GOZ ORTAM KURALLARI
- Ortam `scratchpad/goz1/` altindaki `schtasks` gorevleriyle kalkar (`DivisimaGoz1Api`,
  `DivisimaGoz1Statik`). `Start-Process` bu ortamda OLUR (bkz. GOZ-FIX muhru).
- **`api-baslat.cmd` BES ARGUMAN VERIYOR ve bunlar URUN VARSAYILANI DEGILDIR:**
  `--Iyzico:UseRealSdk=false` · `--BackgroundJobs:Enabled=false` · `--MailSettings:Host=`
  · `--AdminSeed:Enabled=false` · `--RateLimit:AuthPermitLimit=100`.
  **HER OLCUM RAPORU BU LISTEYI ANMAK ZORUNDADIR** — aksi halde duzenek artifakti urun
  kusuru sanilir (MTUR'da iki kez sanildi).
- Build ONCESI gorev DURDURULUR (kosan API DLL'leri kilitler -> MSB3027), SONRASINDA
  yeniden baslatilir. **Her mutasyon/dis turu oncesi YENIDEN DERLENIR** (bayat-ikili
  kurali).
- Omer'in hesabi (musteri 10, `e2b.sandbox@example.com`) ve verileri OLCUMDE KULLANILMAZ;
  tum yazmali senaryolar kurgu hesapla ve TAMAMI envantere.
- Ekran goruntusu bu panelde ALINAMIYOR; yerlesim SAYISAL olculur
  (`getBoundingClientRect`, `elementFromPoint`).

---

