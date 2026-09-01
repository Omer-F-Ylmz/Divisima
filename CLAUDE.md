---

# B0 — MK-11 ARSIV VE MUHUR USULU (ARSIV-1, zemin d8f12dd)

a) Kapanmis dalga muhurleri docs/muhur/NN-*.md'de BAYT-AYNI ham dilimdir; docs/muhur/INDEX.md
   eski satir araliklarini dosyaya esler. Bu dosyalar acilista yuklenmez, `@` ile baglanmaz,
   .claude/ altina tasinmaz. CLAUDE.md butcesi ≤80 KB; asilirsa siradaki ARSIV turu merkezden
   acilir.
b) Okuma kurali: arsive yalniz somut gerekceyle bakilir (hangi muhur + hangi baslik + neden);
   once INDEX.md, sonra grep ve dar aralik Read. Bir arsiv dosyasinin tamami okunmaz; "eski
   satir N" atiflari INDEX ile cozulur.
c) Yeni muhur usulu: dalga muhru docs/muhur/'a YENI dosya (NN siradaki, slug dalga adi);
   CLAUDE.md'ye YALNIZ operatif delta girer — yeni MK / suzgec girdisi / ders-sayac / tuzak /
   kurgu sabiti / D-YAN / acik SUPHELI / kuyruk — ilgili B-bolumu basligi capasiyla (MK-7: capa
   HAM'dan). Dosya-sonu guvenli-ekleme capa deseni EMEKLI. MK-8 surer.
d) Atif bicimi: satir numarasi yazilmaz; "muhur adi + baslik" (or. MF-4 · KURGU). Muhur metni
   sonradan degistirilmez; duzeltme yeni muhrun ya da ilgili B-satirinin isidir.
e) Operatif delta isaretci disiplini: tablo/envanter/itiraz listeleri muhurde kalir, CLAUDE.md'ye tek satir
   isaretci (muhur adi + baslik); MAX, kuyruk ve sayac satirlari YERINE yazilir, eklenmez; her ders <=2 satir;
   INDEX.md toplam satiri her muhurde ureten ifadeyle yeniden olculur.

**BOLUM DUZENI:** B0 bu blok · B1 calisma kurallari · B2 SDP v1.3 · B3 dalga ici denetim kurali
+ SUREC · B4 MK-1..MK-11 · B5 suzgec kutuphanesi · B6 dersler · B7 kurgu sabitleri + D-YAN ·
B8 baglayici kararlar + acik SUPHELI · B9 kuyruk + devir. B1/B2/B3 kaynagindan BAYT-AYNIDIR.

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

## B2 SDP v1.3 → skill `sdp` (.claude/skills/sdp/SKILL.md)
Her dalga/denetim tarifi "SDP ve SUREC skill'lerini yukle" satiriyla baslar; yuklenmeden olcum yapilmaz (MK-12).
**1.12 GUVENLIK modulu (SDP v1.3): tam metin `42·GUVENLIK-AV-1 · SDP 1.12` — guvenlik
dalgalarinda arsivden okunur (MK-11 b somut gerekce).**
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

## SUREC (degismez) → skill `surec` (.claude/skills/surec/SKILL.md)
# B4 — MIKRO-KURALLAR MK-1..MK-10 (son metinler; MK-11 → B0)

Her blok kaynagindan BAYT-AYNI kopyadir; kaynak etiketi arsiv dosyasini ve muhur
basligini verir (MK-11/d: atif biçimi "muhur adi + baslik").

## MK-1

kaynak: 33·MFIX-3_MUHRU

**MK-1 - SOKUM ICEREN HER DALGADA CERCEVE TARAMASI ZORUNLU.**
Bir dalga fonksiyon/blok SOKUYORSA: (a) cerceve GIRIS NOKTALARINDA (`applyI18n`,
`setLang`, `setCur`, `refreshPrices` ve muadilleri) **tanimsiz-fonksiyon taramasi**
yapilir; (b) **dil / para birimi / tema gecisleri** REPRO setine EKLENIR.
Gerekce: MFIX-2'nin sokumu iki komsu fonksiyonu goturdu, cagri yerleri kaldi ve
**dil degistirme BOZULDU** - hicbir pin yakalamadi, hicbir REPRO dokunmadi.
Pin karsiligi: **P11**.

## MK-2

kaynak: 33·MFIX-3_MUHRU

**MK-2 - GIT KOMUTU CALISTIRAN HER CAGRI CWD'YI ONCE DOGRULAR.**
Gerekce: MFIX-2 push turunda `cd` ayni cagrida kaldigi icin `git push` **scratchpad'de**
kostu, `fatal: not a git repository` verdi ve **PUSH OLMADI**; yalnizca ciktinin
okunmasi sayesinde fark edildi. Kural: git cagrisi `pwd` + `git rev-parse
--is-inside-work-tree` teyidiyle baslar.

## MK-3

kaynak: 34·MFIX-B_MUHRU

## MK-3 GUCLENDIRILDI (KALICI)

**Her muhur/ozet degeri URETEN IFADESIYLE kaydedilir.** Pending birimi bu turdan itibaren
**DORTLU**:

```
BIRINCIL : status=0 AND id<=210  ->  COUNT=35 · MIN=9 · MAX=210 · SUM=3837
IKINCIL  : CHECKSUM_AGG(id)=239
```

**XOR-CEBIRSELLIK NOTU (canli ornek):** `CHECKSUM_AGG` XOR temellidir ve TEK BASINA KOR
KALABILIR - bu veritabaninda `id>210` olan **ALTI** yeni Pending satirin toplami **TAM 0**
cikti, yani kume degisirken muhur "degismedi" diyebilirdi. Tarihsel `561429369` degeri
**ON IKI ifadeyle YENIDEN URETILEMEDI**; dolayisiyla o deger artik birincil olcut degildir.


## MK-4

kaynak: 34·MFIX-B_MUHRU

## MK-4 (YENI KALICI MIKRO-KURAL)

**Denetim dagitimindan ONCE is LOKAL COMMIT'e alinir; L3 ve kural-uyum denetcileri AYRI bir
`git worktree`'de o commit uzerinde kosar.** Boylece cift-kor TEKNIK izolasyon (SDP 1.9)
lokal islerde de saglanir.

Gerekce OLCULDU: MFIX-B'de is commit EDILMEMIS oldugu icin bir worktree calisma agacindaki
degisiklikleri GOREMEZDI; izolasyon zorunlu olarak yalniz prompt duzeyinde kaldi ve
denetciler bunu CELISKI-1 olarak isaretledi. Commit'i denetimden ONCE atmak bu kisiti
tumden kaldirir ve commit'in **amend edilmemesi** disinda hicbir bedeli yoktur.


## MK-4a

kaynak: 35·MFIX-3b_MUHRU

### MK-4a (YENI KALICI MIKRO-KURAL)

**Her worktree denetcisi, RAPORUNUN BASINA kendi `pwd` + `git rev-parse HEAD` olcumunu
koyar** (beklenen worktree yolu + beklenen SHA). Transkript grep'i ancak transkript VARSA
**EK** kanittir; **birincil kanit denetcinin kendi beyan ettigi olcumdur.**

Gerekce OLCULDU: MK-4'un ilk uygulamasinda transkript kanali BOS cikti ve izolasyon iddiasi
desteksiz kaldi. Kural-uyum denetcisi bunu M8'de kendi calisma dizinini beyan ederek kismen
telafi etti (ve git nesne veritabaninin dolayli erisimini kendi DURUST SINIRI olarak yazdi) -
yani cozum ZATEN sahada dogmustu; kural onu zorunlu kiliyor.

## MK-4b

kaynak: 37·MANTIK-FIX-1_MUHRU

**MK-4b - HER DENETCI KENDI WORKTREE'SINI ALIR.** Paylasilan durum tasiyan kaynaklar
(**TEST VERITABANI ADLARI DAHIL**) denetci basina ayrilir ya da denetciler SERILESTIRILIR.
Gerekce OLCULDU: uc denetciyi tek worktree'ye gonderdim; ikisi uretim mutasyonu yapiyordu
(`HEAD~1`'e checkout dahil) ve ucuncusunun olcumlerini KIRLETTI. Kural-uyum denetcisi kendi
worktree'sinde BASKA bir ajanin mutasyon izini gordu; L3'un ilk iki tam suit kosumunda
P19/P22/P23 kirmizi cikti ve hata metninde `mapProduct` ESKI haliyle gorundu. Celiski avcisi
bunu FARK EDIP tum kritik olcumlerini `git show <sha>:<yol>` ile blob'dan yeniden uretti.

worktree'siz iki ardışık tam doğrulama birebir (Sql 0/339/0/339 · tam suit 3/575/0/578 ·
kırılanlar ikisinde de aynı: `Divisima.IntegrationTests.OrderEndpointTests.PlaceOrder_ConcurrentRequests_NoOverselling`,
`Divisima.IntegrationTests.OrderEndpointTests.PlaceOrder_InsufficientStock_Returns`,
`Divisima.IntegrationTests.OrderEndpointTests.PlaceOrder_ValidCart_Returns`); isimsiz
338/339 flake tekrarlamadı — paylaşılan test-DB açıklamasıyla TUTARLI gözlem, kanıt değil.


## MK-5

kaynak: 37·MANTIK-FIX-1_MUHRU

**MK-5 - HER ON-OLCUM AJANI RAPORUNU KENDI HAM DOSYASINA YAZAR.** Harness'in cikti dosyasina
GUVENILMEZ ve boyutunun 0 olmadigi ajan tarafindan DOGRULANIR. Gerekce OLCULDU: bu dalganin
kesintisinde ajan cikti dosyalarinin **13/13'u 0 bayt** cikti (negatif kontrol: ayni dizinde
`b*.output` 842 KB'a kadar dolu) ve alti defter satiri DAYANAKSIZ kaldi; MFIX-3b'nin MK-4
turunda **AYNI olgu** yasanmisti. Rapor "yalnizca konusma baglaminda" var olursa defterin
HAM/SHA butunlugu YAPISAL OLARAK saglanamaz.

## MK-6

kaynak: 37·MANTIK-FIX-1_MUHRU

**MK-6 - KAYNAK-SOZLESME PINLERI MUTASYONLA SINANIR.** Bir pin yalnizca kaynak metnini
tariyorsa, "kirmizi-once" kaniti YETMEZ: aranan dizgenin **BASKA bir baglamda da** gecip
gecmedigi, korunan alani ONCEKI haline donduren bir uretim mutasyonuyla gosterilir.
Gerekce OLCULDU: P19'un `Contain("effective_price")` asserti BEDAVA DOGRUYDU (dizge
`mapProduct` govdesinde DORT satirda geciyordu); alan K1 oncesine dondurulunce **TUM SUIT
575/578 ile temiz durumla BIREBIR AYNI** kaldi - yani duzeltmenin istemci yarisi PINSIZDI.
Assert ALAN BAZLI yapilinca ayni mutasyon TAM 1 ISIMLI KIRMIZI verdi.

## MK-7

kaynak: 37·MANTIK-FIX-1_MUHRU

**MK-7 - EŞLEŞTIRME ÇAPALARI:**
"Eşleştirme çapaları ezberden yazılmaz: çapa metni, HAM çıktıdan kopyalanan bilinen-pozitif
parçadan alınır; ASCII'leştirme/transliterasyon yasak; her çapanın bilinen-pozitif sınaması
girdi dosyasının yoluyla birlikte kaydedilir. Bilinen-pozitif seti hedef alfabeyi temsil
eder — rakam dahil."
**Gerekce:** ASCII/Turkce yuklem ailesinin **3. vakasi** - **ilk kayit `0655178`** (MFIX-3b
muhru, i18n envanteri cift-yontem kurali; capa ders metninden KOPYALANIP `git log -S` ile
olculdu, tahmin EDILMEDI), genellemesi `4d8d4c2` (MANTIK-AV-1, `<> 'Silinmis'` yuklemi dort
dogru satiri hatali sayip bulguyu **5 kat abartti**), ucuncusu bu tur (ASCII ozet filtresi).
Ders CLAUDE.md'de KAYITLIYKEN ucuncu kez dusuldu.
**NUMARA SAPMASI (raporlandi):** merkez MK-5 bekliyordu; 4b'nin iki kalici kurali
(MK-5 "ajan kendi HAM dosyasina yazar" · MK-6 "kaynak-sozlesme pinleri mutasyonla sinanir")
numara aldigi icin siradaki MK-7 atandi. MK-4b harflidir - MK-4'u genisletir, tam sayi
TUKETMEZ.

## MK-8

kaynak: 38·MANTIK-FIX-2R_MUHRU

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

## MK-9

kaynak: 39·MANTIK-FIX-3_MUHRU

### MK-9 (YENI MIKRO-KURAL)

**"Bicim kapilari (whitespace + style) her checkpoint commit'inden ONCE kosulur; kapidan
gecmemis commit checkpoint sayilmaz."**

**Gerekce OLCULDU:** `add4009` bicim kapisindan gecmeden commit'lendi; whitespace kapisi
**exit 2** (10 hata, hepsi tek dosyada 16-bosluk girinti) ancak BIR SONRAKI kalemde fark
edildi. Kapi dalga sonunda kosulursa, arada atilan her checkpoint "gecmis gibi" gorunur ve
`git bisect` okunabilirligi bozulur.

## MK-10

kaynak: 40·MANTIK-FIX-4_MUHRU

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

---

# B5 — SUZGEC KUTUPHANESI

kaynak: 37·MANTIK-FIX-1_MUHRU (S1-S4, bayt-ayni) · S5 ARSIV-1'de olculdu

## SUZGEC KUTUPHANESI (yeni bolum)

**OLCULEN TABAN 0 - CLAUDE.md'de suzgec kutuphanesi BOLUMU YOKTU** (beklenen 8 degil; sapma
raporlandi). Tek yakin kayit VITRIN-FIX-2 muhrundeki anlatisal cumleydi ("bes suzgecin
tamami ... SINANDI") ve **IFADELERI KAYDEDILMEMISTI**. Kutuphanenin hic var olmamasi, her
dalgada suzgeclerin YENIDEN ICAT EDILIP YENIDEN KIRILMASININ sebebidir - bu turda tek basina
**bes suzgec kusuru** olculdu. **TOPLAM: 0 + 3 = 3 girdi, 10 kontrol.**

**S1 - RUN SAYIMI.** Ureten ifade: `grep -c "\"run_number\":" <dosya>`
```
POZ  scratchpad/ci0655/runs0655.json  -> 2
POZ  scratchpad/ci318/runs318.json    -> 2
NEG  scratchpad/cib9c/sizma.json      -> 0   (job-adi sizmasi girdisi)
NEG  scratchpad/cib9c/bos.json        -> 0   (bos dosya)
```
**EMEKLI:** `^      "id":` - ic ice `jobs` nesnelerini de sayiyordu; ayni NEG girdisinde
**1** donduruyor (bugun olculdu).

**S2 - TEST OZETI.** Ureten ifade: `grep -oE "Toplam:[ ]*[0-9]+" <log> | tail -1`
```
POZ  scratchpad/cib9c/t1full.log -> "Toplam:   578"
     ham satir: "Başarısız! - Başarısız:     3, Başarılı:   575, Atlanan:     0,
                 Toplam:   578, Süre: 51 s - Divisima.IntegrationTests.dll (net8.0)"
NEG  ayni dosyada "ZZZToplam:" -> 0
```
**EMEKLI:** `Basarili!|Basarisiz!` - cikti Turkce oldugu icin ayni dosyada **0** esliyor
(bugun olculdu). Capa `Toplam:` HAM CIKTIDAN KOPYALANDI (MK-7).

**S3 - RUN DURUMU.** Ureten ifade:
`curl -s ".../actions/runs?head_sha=<SHA>&per_page=20"` -> `"total_count"` + `"status": "completed"` + `"conclusion": "success"` sayimi
```
POZ  4d8d4c2 (onceki muhur)  -> total 2 · completed 2 · success 2
POZ  b9c9ff0 (bu push)       -> total 2 · completed 2 · success 2
NEG  f0f27dc (ARA COMMIT)    -> total 0   (tek basina push EDILMEDI)
NEG  0000...0 (uydurma SHA)  -> total 0
```
**EMEKLI:** awk tabanli satir-desenli cikarici - ic ice JSON'da depo id'sini ve aktor adini
run alani saniyordu; **karar icin HIC kullanilmadi**.

**S4 - RUN KIMLIGI.** Ureten ifade:
`grep -oE '"html_url": "[^"]*/actions/runs/[0-9]+"' <dosya> | grep -oE '[0-9]+"$' | tr -d '"' | sort -u`
```
POZ  ci0655/runs0655.json   -> 33165306227 · 33165306239        (iki DOGRU kimlik)
POZ  cib9c/rson.json        -> 33213028751 · 33213028838        (MANTIK-FIX-1 push'u)
NEG  cib9c/bos.json         -> []
NEG  cib9c/sizma.json       -> []                                (DEPO ID'si SIZMIYOR)
```
**EMEKLI:** `[0-9]{10,}` gibi HANE-SAYISINA dayali cikarici - 10 haneli DEPO ID'sini
(`1338865652`) run kimligi saniyordu; MANTIK-FIX-1 push turunda birebir yasandi.
`html_url` capasi kimligi YAPISAL olarak konumlandirir, uzunluk tahminine dayanmaz.

**Kayitlarda anilan girdi dosyalari SILINMEDI.**

**S5 - CR (SATIR SONU) DEDEKTORU.** Ureten ifade: `tr -cd '\r' < <dosya> | wc -c`

```
POZ  /tmp/poz_crlf.txt  (printf 'a\r\nb\r\n')  -> 2   od kanit:  a \r \n b \r \n
NEG  /tmp/neg_lf.txt    (printf 'a\nb\n')      -> 0   od kanit:  a \n b \n
POZ  CLAUDE.md (d8f12dd)                       -> 0   (saf LF)
POZ  git show HEAD:CLAUDE.md                   -> 0   (blob da LF)
```

**EMEKLI:** `grep -c $'\r' <dosya>` - bu ortamda `$'\r'` BOS DIZEYE cozunuyor ve HER
SATIRI esliyor; ayni NEG girdide (saf LF) de 2 dondu, yani dedektor BOZUKTU. Ilk ARSIV-1
olcumunde CLAUDE.md "12434 satirin 12434'unde CR" gorundu ve "kabul kriteri CR 0 ile
CELISIYOR" sanildi; `tr -cd` ile yeniden olculunce CR baytinin 0 oldugu, celiskinin
OLMADIGI cikti. SDP 1.7/1'in bu turdaki kazanci.
---

# B6 — DERSLER: AILE SAYACLARI · SALINIM · TUZAKLAR · RIG KOR NOKTALARI

## Aile sayaclari (olculdu, tahmin degil)

kaynak: 37·MANTIK-FIX-1_MUHRU

**KACIS-KAYBI AILESI - ALTINCI ORNEK:** P22'de `"\s+"` heredoc'ta `"\s+"`ya indi ve C#
**CS1009** verdi; `sed`/`perl` duzeltmeleri de ayni kacisi yedi. **KACISSIZ COZUME gecildi**
(regex yerine duz `Replace` zinciri). Kayitli dersin bir kez daha dogrulanmasi.

## DENETIM (MK-4) - UC DENETCI, IKISI GERCEK KUSUR BULDU

kaynak: 39·MANTIK-FIX-3_MUHRU

  **AILE SAYACI: 4. VAKA.** (1) MFIX-2/M-P8 "assert ESKI LITERAL BICIMI ariyordu, KUSUR
  SINIFINI degil" · (2) MANTIK-FIX-2R/B2-B3 "`innerHTML = ` bosluksuz bicimi kaciriyordu" ·
  (3) MF-3/ITIRAZ-1 "`.length<` regex bicimli kopyayi kacirdi" · **(4) MF-3/FF: kapsam pini
  HAM `CREATE DATABASE`i GORMUYORDU** - `EnsureCreated` dizgesine baglanmisti ve o cagri yeri
  yeniden denemeden YARARLANAMIYORDU.
- **FORM <-> DTO ALAN ESLEMESI BAGLAMADAN ONCE OLCULUR.** Formda FAZLA olan alan bir YALAN
  uretir (kullanici degistirir, sunucu gormez); EKSIK olan alan PUT-ez semantiginde SESSIZ

## Annotation salinimi (hipotezden olculmus olguya)

kaynak: 39·MANTIK-FIX-3_MUHRU

### ANNOTATION KURALI INCELMESI (hipotezden OLCULMUS OLGUYA)

Annotation sapmasi **YALNIZ** bilinen alti-satir kumesiyse - `EfEntityRepositoryBase.cs`
satir **45 / 50 / 60 / 61 / 88 / 96** - **ve o dosya diff'te yoksa**, TEK SATIR
"bilinen salinim" notu yeterlidir. Kumenin **DISINA** tasan her sapma `dosya:satir`
incelemesi + diff kesisimi (pozitif kontrollu) ister; `failure` seviyesi -> **DUR**.

**Bu kume VITRIN-FIX-2 kaydiyla (CLAUDE.md 8407-8408) BIREBIR ayni alti satirdir** ve orada
"kaybolan" olarak kaydedilmisti. Yani "yuzeye-cikarma artefakti" artik bir HIPOTEZ DEGIL,
IKI BAGIMSIZ KOSUMDA OLCULMUS BIR OLGUDUR: GitHub check-run basina annotation sayisini
sinirlar ve hangi ornegin yuzeye ciktigi kosumdan kosuma degisir.

### KALICI DERSLER


## MF-4 push turunun dort dersi

kaynak: 40·MANTIK-FIX-4_MUHRU · PUSH TURUNUN EK CC HATASI

## PUSH TURUNUN EK CC HATASI (1)

**YONLENDIRME SIRASI.** `dotnet test ... 2>&1 > dosya` yazildi; bu, stderr'i ESKI stdout'a
(terminale) baglar ve stdout'u dosyaya yonlendirir - yani `[FAIL]` satirlari log dosyasina
GIRMEZ. TUR1'de kirilan adlar yalniz EKRAN CIKTISINDAN okunabildi. Dogrusu `> dosya 2>&1`;
TUR2 ve TUR3 duzeltilmis bicimde kosuldu ve adlar log'dan `comm`/`diff` ile karsilastirildi.

## KURGU KAYIT ENVANTERI

## Rig kor noktalari

kaynak: 40·MANTIK-FIX-4_MUHRU

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


## Ortam tuzaklari

kaynak: 37·MANTIK-FIX-1_MUHRU (sqlcmd -I · tireli rota · 338/339)

**ORTAM DERSLERI (kalici):** `sqlcmd` bu ortamda **QUOTED_IDENTIFIER kapali** baslar ve
filtreli indeksi olan tabloya `UPDATE` **Msg 1934** ile duser -> **`-I` bayragi ZORUNLU** ·
`gift-card` rotasi **TIRELI** (`api/gift-card`) ve `GiftCardCreateDto` **yalniz `amount`**
tasir · `schtasks` Git Bash'ten cagrilinca yol cozumleme bozulur, **PowerShell** uzerinden
cagrilir · build ONCESI API sureci DURDURULUR (MSB3027/MSB3021 DLL kilidi), SONRASINDA
yeniden baslatilir ve bes arguman TEYIT EDILIR. Dordu de KAYNAKTAN okunarak cozuldu.


**338/339** gorundu; ADI YAKALANMADI (grep deseni mesaji disarida birakti). Ayni anda alinan
tam suit 575/578 (yani 4 degil 3 kirmizi) - **TUTARSIZ**. Worktree kaldirildiktan sonra iki
ardisik kosum 339/339. En olasi aciklama paylasilan test veritabanlari (kural-uyum M2-2'de
`already exists` cakismasi olctu) **ama BU ISPAT DEGIL**.


## Ortam tuzagi — rota asimetrisi

kaynak: 39·MANTIK-FIX-3_MUHRU (rota asimetrisi)


1. **ROTA TAHMINI** - `stock-notification` sanildi, dogrusu `api/StockNotification`.
   `price-drop`un GERCEKTEN tireli olmasi yaniltti. **SDP 1.7/2 - bu dalgada 1. dusus.**
2. **P-H2 fiksturu** var olan bir urun ariyordu, sinif her testte DB'yi yeniden kuruyor.
3. **BAYAT TOAST** - AR bacaginda ilk olcum Ingilizce metin gosterdi; toast sinifinda `on`

## Ortam tuzagi — runtime sozluk enjeksiyonu

kaynak: 36·MANTIK-AV-1_MUHRU (runtime sozluk enjeksiyonu)

**(ii) SOZLUK BUTUNLUGU KAYNAKTAN PINLENIR, RUNTIME'DAN DEGIL.**
`api-bridge` calisma aninda sozluge anahtar **enjekte ediyor** (bugun 4 tane).
`788 kaynak + 4 enjekte = 792 runtime`. **Runtime uzerinden kurulan bir "sozluk butunlugu"
pini YALANCI YESIL verir** - kaynakta eksik bir anahtar, calisma aninda enjekte edilen bir
anahtarla MASKELENIR. MFIX-3b muhrundeki `792/792` bir RUNTIME olcumudur; dogrudur ama
**KAYNAK butunlugunu KANITLAMAZ**.

---


### Denetim duzeltmesi (ARSIV-1/C3) — eksik tasinan kalici kayitlar

kaynak: 34·MFIX-B_MUHRU · ZORUNLU KAPSAM EKI (admin.html tuzagi)


```
admin.html:306  duzenleme formu stok satirlarini ANONIM detay ucundan dolduruyor
admin.html:376  ayni degerleri geri POST ediyor
ProductManager.cs:292  onu FIZIKSEL kolona yaziyor
=> K1 TEK BASINA gonderilseydi: admin 937'yi acip YALNIZ ADINI degistirip kaydettiginde
   fiziksel 10 -> 4 duser, rezerve 6 kalir, available -2 -> 0 olurdu.
   Dalga B'nin "tam-varlik map -> sessiz veri kaybi" sinifinin BIREBIR tekrari.
```

#### admin.html tuzagi — KALICI KURAL (kapsam elestirmeni)

kaynak: 34·MFIX-B_MUHRU · ZORUNLU KAPSAM EKI (kalici kural)

**KALICI KURAL (bu vakadan dogdu): KAPSAM ELESTIRMENI ROLU, ON OLCUM FAN-OUT'UNUN
ZORUNLU UYESIDIR.** Gorevi bulgu aramak degil, **verilen tarifin kendisinin acacagi kapiyi**
aramaktir. Bu turda merkezin K1 tarifi, bes bagimsiz okuyucu ve ana akis - **dordu birden**
kacirdi; tek eleştirmen rolu yakaladi.


#### Isimsiz flake — durust kayit

kaynak: 37·MANTIK-FIX-1_MUHRU · DURUST KAYIT - ISIMSIZ FLAKE (tam acilis)

**DURUST KAYIT - ISIMSIZ FLAKE:** denetciler kosarken alinan BIR `Category=Sql` kosumunda
**338/339** gorundu; ADI YAKALANMADI (grep deseni mesaji disarida birakti). Ayni anda alinan
tam suit 575/578 (yani 4 degil 3 kirmizi) - **TUTARSIZ**. Worktree kaldirildiktan sonra iki
ardisik kosum 339/339. En olasi aciklama paylasilan test veritabanlari (kural-uyum M2-2'de
`already exists` cakismasi olctu) **ama BU ISPAT DEGIL**.


#### Kacis-kaybi sayaci (HAM)

kaynak: 40·MANTIK-FIX-4_MUHRU · CC HATALARI (kacis-kaybi sayaci, HAM)

**8. HATA KACIS-KAYBI AILESINE GIRMEZ - OLCULDU.** O vakada kaynak dosyada `'⌂'`
KACIS OLARAK yazili, ben gercek karakteri (⌂) aradim: **kayip yok, eslesme bicimi farki**.
Ailenin sayaci `git log -S` ile olculdu: `"KACIS-KAYBI AILESI - DORDUNCU ORNEK"` 1 commit ·
`"... ALTINCI ORNEK"` 1 commit (`a5add91`) · `"... BESINCI ORNEK"` ve `"... YEDINCI ORNEK"`
**0 commit** (NEG kontrol `ZZZINCI` 0). **Sayac ALTINCI'da KALIR.**
**KAYIT:** MK-4b denetcisinin MUT-3b turunda gercek bir kacis-kaybi yasandi (`sed` ters bolu
## Suzgec dersi — basliksiz kaynak etiketi (ARSIV-1)

Bir etiketin yapisal basligi olup olmadigini `grep -B1 "^kaynak: " | grep -c "^--$"`
ile saymak AYRAC sayar, etiket saymaz: ilk kosumda 34 dedi. Dogru ifade iki-ust-satiri
kontrol eder: `awk '/^kaynak: /{ if (p2 !~ /^#/) n++ } { p2=p1; p1=$0 } END{print n+0}'`.
POZ (C2/2474135) 4 · NEG (`^ZZZkaynak:`) 0 · C4 sonrasi 0.
kaynak: ARSIV-1 denetim turu, muhurde 41·ARSIV-1 · CC HATALARI 4

## Iki ders — GUVENLIK-AV-1 (42·GUVENLIK-AV-1 · CC HATALARI)

**BILINEN listesi B8 fragmanlarindan KURULMAZ; 00a/00b tam metni okunur (AV-1 hatasi 2).**
Gerekce OLCULDU: AV-1'de ajanlara verilen B-01..B-18 listesi B8'in ilk-cumle
fragmanlarindan kuruldu; `00a:101` (SellerAuthManager kilit kontrolu sifreden ONCE) ve
`00a:108` (step-up `auth_time` refresh'te sifirlanmasi) DISARIDA kaldi ve IKI bulgu
(C-3, C-2) yanlis olarak "YENI" sayildi. Rapor denetcisi yakaladi.

**Sir hijyeni: ham yanit dokumleri diske MASKELI yazilir; ajan ortak kurali "basilmaz" +
"diske yazilmaz" + maske aracini icerir (AV-1 hatasi 3).**
Gerekce OLCULDU: AV-1'in ortak kurali yalniz "rapora/deftere/konsola basilmaz" diyordu;
ham yanit dokumlerinin DISKE yazilmasi kapsanmadi -> dokuz dosyada ciplak canli jeton
(6 access JWT + 3 refresh, biri ADMIN; oturumlar 7 gun gecerli, besi `is_active=1`).
Ajanin KENDI kapanis iddiasi "jetonlar ilk 8 karaktere kirpildi" diyordu ve **CURUK** cikti
— turun TEK curuyen kalemi bir bulgu degil, bir KAPANIS IDDIASIYDI.

# B7 — KURGU SABITLERI ve D-YAN

## Olcum duzenegi (goz1) — bes arguman

kaynak: 40·MANTIK-FIX-4_MUHRU · ORTAM UYARISI

**ORTAM UYARISI (Divisima Eki 2.3):** olcumler `goz1` duzeneginde kosuldu; API surecinin
komut satirinda BES ARGUMAN var ve **BUNLAR URUN VARSAYILANI DEGILDIR** -
`--Iyzico:UseRealSdk=false` · `--AdminSeed:Enabled=false` · `--BackgroundJobs:Enabled=false`
· `--RateLimit:AuthPermitLimit=100` · `--MailSettings:Host=`.


## Kurgu envanteri ve muhurler

kaynak: 40·MANTIK-FIX-4_MUHRU · KURGU KAYIT ENVANTERI (bayt-ayni KOPYA; muhurde de kalir)

## KURGU KAYIT ENVANTERI

**MF-4 UYGULAMA FAZI HICBIR YENI KAYIT URETMEDI**; Omer'in hesabi (musteri 10) ve kabul
turu kayitlari KULLANILMADI.
MAX musteri **168** · siparis **286** · adres **119** · fatura **119** · Pending(id>210)
**10** — kaynak `42·GUVENLIK-AV-1 · KURGU`

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


## D-YAN temizlik listesi

kaynak: 39·MANTIK-FIX-3_MUHRU · D-YAN TEMIZLIK LISTESINE

### D-YAN TEMIZLIK LISTESINE

- **CANLI KVKK IHLALI: adres 55 / musteri 93** (silinmis hesap, TAM PII) - KR6 geregi bu
  dalgada DOKUNULMADI; duzeltme YENI silmelerde gecerli.
- Bu dalganin kurgusu: musteri 120-135 (120/121 SILINDI; 125/133 KIMLIK BOSLUGU),
  adres 78-95, siparis 269-277 (**270-275 BOZUK ADRESLI - R-H5 ONCE kaniti**),
  fatura 102-110, 3 yetim outbox satiri.


## D-YAN devri (MF-4)

kaynak: 40·MANTIK-FIX-4_MUHRU · DV2 (D-YAN'a devredilen)

DV2  Yetim musteri 153 ve 155 + siparis 270-275 (bozuk adresli, R-H5 ONCE kaniti) -> D-YAN
DV3  429 UC AYRI KAYNAKTAN (cop-misafir guard'i · Redis rate-limit · yerlesik limiter -
---


D-YAN: AV-1 kurgusu m159-168 `gav1.*` · `user_sessions` 331-339 · `review_helpful_votes` 1 ·
admin 118 sifre sifirlama · 14 satirlik yan etki tablosu -> `42·GUVENLIK-AV-1 · KURGU ENVANTERI`

**D-YAN bloklari kumulatiftir; en guncel liste 39·MANTIK-FIX-3'tedir. Onceki bloklar
su arsiv dosyalarinda: 26 · 27 · 30 · 31 · 32 · 33 · 34 · 35 · 36 · 37 · 38 (INDEX.md ile
cozulur).**
# B8 — BAGLAYICI KARARLAR ve ACIK SUPHELI

Her satir HAM ilk cumle + kaynak atfi. Tam metin arsivde; MK-11/b geregi arsive
yalniz somut gerekceyle bakilir.

## Baglayici kararlar (00a-sira-kararlar.md)

- `00a:87` - **AutoMapper: 12.0.1'de KAL, bump YOK.** Advisory (CVE-2026-32933) okundu, maruziyet
- `00a:92` - **Seller modulu**: dokunma, veri duzeyinde kapali, migrate/seed yok.
- `00a:93`   **ZORUNLU ON KOSUL (GUVENLIK DALGASI / G4): modul acilmadan ONCE satici refresh token'i
- `00a:101`   **IKINCI ON KOSUL (GUVENLIK-FIX-2 eki): `SellerAuthManager.Login` kilit kontrolunu SIFRE
- `00a:106` - **invoice_number**: entegrator (Nilvera) numarasi esas, bizimki ic referans - degisiklik yok.
- `00a:128`   **KORLEMESINE INDEKS EKLENMEZ** (kullanici sarti). Gercek katalog hacmi olustugunda
- `00a:180`   **LAUNCH ONCESI EKLENMEZ:** yeni bir bagimlilik `dependency-scan` kapsamina girer ve tarayici
- `00a:188`   **KARAR: LAUNCH SONRASI.** Gerekce kullanicinin: **409 hesap ele gecirmeyi ENGELLIYOR** (var
- `00a:206` - **Iyzico'nun TELEMETRI alan adlari CSP'de ACILMAZ (kalici karar).** `countly.iyzico.com`
- `00a:213` - **Auth modeli**: mevcut hibrit korunuyor (access localStorage + refresh httpOnly
- `00a:215` - **`EnableRetryOnFailure`: S7'de ACILMADI.** S7 engeli kaldirdi (IyzicoPayment artik

## Baglayici kararlar (muhurlerden — EK-1)

- `37·MANTIK-FIX-1·MF-3 devri` ikinci kopyasi" - bu depoda 7 kez bedeli odendi) · (c) **409 semantigi YENIDEN ACILMAZ**
- `37·MANTIK-FIX-1·MF-2 ONCESI ARA DURUM` - **(a)** `InvoiceManager.cs:76`'nin **BRUT** toplama bagi MF-2'de ACIK HALE GETIRILIP
- `38·MANTIK-FIX-2R·ACIK OLCUM (2)` **URETIM KAYNAGI SAYIMA GIRMEDI ve DOKUNULMADI** (C4): `InvoiceManager.cs:24` (`0.20m`)
- `39·MANTIK-FIX-3·MERKEZ KARARLARI N2` | **N2** | Hata eslemesi once MAKINE-OKUNUR sinyal; yoksa HAM yanit capasi + cift bicim + kirilganlik kaydi | K3 ve K3b'nin ikisi de bu capaya dayaniyor - sunucu yanit sozlesmesi DEGISTIRILMEDI, istemcide politika kopyasi ACILMADI |
- `36·MANTIK-AV-1·DALGA BOLUMLEMESI` i18n. **64 bozuk `invoice_items` satiri D-YAN'a** (veri temizligi, fix degil).
- `37·MANTIK-FIX-1·MF-2 ONCESI ARA DURUM` **InvoiceManager KODUNA DOKUNULMADI (sart aynen korundu).**

## Acik SUPHELI (00b-supheli.md)

Durum satiri (00b:3-9, HAM):

**DURUM: ACIK KALEMLER #14 (LAUNCH SONRASI) ve #20 (bugun BOSLUK YOK, testte kapatildi).**
**#22 KAPANDI - GUVENLIK-FIX-4 (govde SHA-256 bagi + tek kaynak kimlik + bayt-birebir replay).**
**#21 KAPANDI - A2-FIX (kullanici karari: sifre politikasi TEK MERKEZDEN, dort giriste de).**
**#19 KAPANDI - GUVENLIK-FIX-2 (kullanici karari: secenek iii).**
Kapananlar: #1..#13 ilgili sprintlerde · **#15, #17, #18 mini dalgalarda** ·
**#16 BILINCLI olarak bos birakildi (verilmis karar, erteleme degil)**.
Asagidaki maddeler kayit olarak duruyor; her birinin basinda guncel durumu yazili.

- `00b:197` **ACIK** 14. **`X-Api-Version` BASLIGI AYRISTIRILAMAZSA TUM API BLANKET 400 VERIYOR.** (Sprint 8
- `00b:313` **ACIK** 20. **VARSAYILAN-KAPALI KURAL CONTROLLER'LARLA SINIRLI - MINIMAL-API UCU EKLENIRSE
- `00b:229` **BAGLAYICI** 16. **`Webhook:AllowedIps` ALLOWLIST'I VAR AMA BOS - VE PROXY ARKASINDA CALISMAZ.**
---


### Denetim duzeltmesi (ARSIV-1/C3) — fragman alintilar tam cumle sinirina cekildi

kaynak: 37·MANTIK-FIX-1_MUHRU · MF-3 SARTLARI (409 semantigi, tam blok)

**MF-3 SARTLARI:** (a) musteri+adres yazimi `PlaceOrder` BASARISINA baglanacak (transaction
ya da erteleme) · (b) cozumde **IKINCI kupon dogrulama noktasi ACILMAZ** ("ayni kuralin
ikinci kopyasi" - bu depoda 7 kez bedeli odendi) · (c) **409 semantigi YENIDEN ACILMAZ**
(GUVENLIK-2/#1 kabul edilmis karar) - satir hic yazilmazsa 409 sorunu zaten DOGMAZ ·
(d) K3'un bu dali ulasilabilir kildigi gercegi MF-3 tarifinin GEREKCESINE girer.

#### 64 bozuk invoice_items satiri

kaynak: 36·MANTIK-AV-1_MUHRU · DALGA BOLUMLEMESI (64 fatura satiri, tam cumle)

**MANTIK-FIX-2 `[FATURA]`** - kargo AYRI KALEM · KDV `invoices.tax_rate`'ten · fatura ekrani
i18n. **64 bozuk `invoice_items` satiri D-YAN'a** (veri temizligi, fix degil).
# B9 — KUYRUK · DEVIR · VITRIN-KALAN · ERTELENMIS-DEFTER

## Kuyruk (merkez metni, AV-1 muhru)

1. GUVENLIK-FIX — bolumleme merkezden (oneri: `42·GUVENLIK-AV-1 · BOLUMLEME ONERISI`)   <- SIRADA
2. GUVENLIK-AV-2 (dar olcum, ultracode YOK): at-rest sifreleme · 2FA/TOTP ·
   TOCTOU/ExecuteUpdateAsync · A09 · olay isleyicileri · 13 anilmayan controller
   (Comparison/Collection ham entity suphesi)
3. VITRIN-KALAN (7 kalem)  4. FIX-1B  5. ADMIN-FIX  6. IMPORT-FIX  7. FIX-1C  8. LOG-FIX  9. FIX-2  10. FIX-3/B13

ARSIV-1 KAPANDI c6721b7 · GUVENLIK-AV-1 KAPANDI (zemin c6721b7 · muhur
`docs/muhur/42-guvenlik-av-1.md`)

## Devir ID'leri

kaynak: 40·MANTIK-FIX-4_MUHRU · DEVIR ID'LERI (bayt-ayni KOPYA)

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

## VITRIN-KALAN

kaynak: 40·MANTIK-FIX-4_MUHRU · VITRIN-KALAN (bayt-ayni KOPYA)

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
7. A-1 arama collation/LOWER() — `42·GUVENLIK-AV-1 · A-1`
```

## ERTELENMIS-DEFTER (yeni sinif, ARSIV-1/S5)

Acilmaz; yalniz HAM kalem basliklari + 00a atfi. Tam metin arsivde.

- `00a:111` **YENI KALEM (Dalga 2 / B13 - kullanici karari): TERK EDILMIS PENDING SIPARISLERE TTL.**
- `00a:136` **YENI KALEM (Dalga 3 / P4 - kullanici karari): ISTEMCI TARAFI ONBELLEK.**
- `00a:140` **YENI KALEM (Dalga 3 / P2 kalani - kullanici karari): index.html'in SATIR ICI 704 KB
- `00a:145` **YENI KALEM (dalga-1-fix eki - kullanici karari): TURKCE KLAVYEDE YAZILAN E-POSTA.**
- `00a:150` **YENI KALEM (GUVENLIK-FIX / G2 eki - kullanici karari): SABIT-ZAMANLI KAYIT.**
- `00a:158` **YENI KALEM (Sprint 8 madde 8 eki - kullanici karari): RFC 2606 ust alan adlarini KAYITTA
- `00a:166` **YENI KALEM (Dalga 4 / M10-M11 eki - kullanici karari): CIKISLI KULLANICIYA DOGRUDAN
- `00a:192` **YENI KALEM (GUVENLIK DALGASI 2 / B5 eki - kullanici karari): `failed-jobs` PII RISKI
- `00a:200` **YENI KALEM (GUVENLIK DALGASI 2 yan gozlemi - DOKUNULMADI): `frontend/pwa/` DIZINI OLU.**

## GUVENLIK-AV-1 kapsam girdileri

- `00a:192` **YENI KALEM (GUVENLIK DALGASI 2 / B5 eki - kullanici karari): `failed-jobs` PII RISKI
- `40·MANTIK-FIX-4·DV3`      -> GUVENLIK-AV-1 girdisi
- `40·MANTIK-FIX-4·VITRIN-KALAN` ortak RuleBuilder karari GUVENLIK-AV-1 SONRASINA (K7 mesaj/NotEmpty ayrismasi)
- `39·MANTIK-FIX-3·FIX-1B DEVRI` F4 erisim jetonu iptali + F8 step-up zinciri

## GUVENLIK-AV-1 girdileri (39·MANTIK-FIX-3, bayt-ayni)

kaynak: 39·MANTIK-FIX-3_MUHRU · GUVENLIK-AV-1 GIRDILERI (bayt-ayni)

### GUVENLIK-AV-1 GIRDILERI

- **Access token iptali** - sifre degisiminden sonra eski access token YASIYOR
  (`RevokeAsync` uretimde 0 cagri, `user_sessions`ta `jti` kolonu YOK).
- **Hata kodu birlestirme** - TR serbest metin capalarinin kirilganligi (K3 + K3b ayni capa).
- **K4 telafisinin ATOMIKLESTIRILMESI** - bugun iki ayri `SaveChanges`; kismi durum mumkun.
- **`ExecuteDeleteAsync` <-> transaction ROLLBACK olcumu** - K2 `DeleteWhereAsync`i
  transaction ICINDE cagiriyor; rollback davranisi SINANMADI (denetcinin kor noktasi).
- **`guest_name` UZUNLUK DOGRULAMASI YOK** - uye yolu `MaximumLength(120)` istiyor, misafir
  yolunda sinir yok ve `full_name` kolonu 150 karakter; uzun ad EF insert'te 500 uretir.
  Manager'in KENDI dogrulama bolgesine ait oldugu icin bu dalgada dokunulmadi.
  **FIX GUVENLIK-FIX ADAYI.**
