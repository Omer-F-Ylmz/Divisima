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
   CLAUDE.md'ye YALNIZ operatif delta girer, CLAUDE.md'deki basligina: yeni MK -> B4 · tuzak ->
   5. Bilinen tuzaklar · baglayici karar -> B8 · BILINEN risk -> B9 · suzgec girdisi / ders-sayac /
   kurgu sabiti-D-YAN / kuyruk / acik SUPHELI -> B10 · ACIK DELTALAR (MK-7: capa HAM'dan).
   Kapanan kalem muhurden sonra `divisima-defter` arsivine tasinir; arsive YENI delta YAZILMAZ.
   Dosya-sonu guvenli-ekleme capa deseni EMEKLI. MK-8 surer.
d) Atif bicimi: satir numarasi yazilmaz; "muhur adi + baslik" (or. MF-4 · KURGU). Muhur metni
   sonradan degistirilmez; duzeltme yeni muhrun ya da ilgili B-satirinin isidir.
e) Operatif delta isaretci disiplini: tablo/envanter/itiraz listeleri muhurde kalir, CLAUDE.md'ye tek satir
   isaretci (muhur adi + baslik); MAX, kuyruk ve sayac satirlari YERINE yazilir, eklenmez; her ders <=2 satir;
   INDEX.md toplam satiri her muhurde ureten ifadeyle yeniden olculur.
f) **MK-12 PROSEDUR SKILL'LERI:** SDP ve SUREC `.claude/skills/` altinda proje skill'idir
   (arsiv DEGILDIR; MK-11 a)'nin `.claude/` yasagi yalniz `docs/muhur/` icindir). Her
   dalga/denetim tarifi "SDP ve SUREC skill'lerini yukle" satiriyla baslar; CC `Skill`
   cagrisiyla yukler, donmezse `Read .claude/skills/<ad>/SKILL.md` ile yukler ve hangisiyle
   yukledigini kapi bolumunde beyan eder (ad + govde bayt). Yuklenmeden olcum/uygulama
   yapilmaz. Skill govdesi CLAUDE.md gibi delta alir (MK-11 c/e); surum baslikta.
g) B10 her mühürde boşaltılır; kapanan kalemler arşive taşınır, açık kalanlar B10'da kalır. CLAUDE.md 18k'yı aşarsa mühür kapanmaz.

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
- **KOD YORUMUNDA SATIR NUMARASI YAZILMAZ — atif SEMBOL/METOT adiyla yapilir (52·GF-5).**
- Run izleme **SHA bazlidir** (`head_sha=` ya da `?branch=main` + SHA eslesmesi).
  "En son run" ile calisilmaz — Dependabot kosulari araya girer ve yanlis run raporlanir.

**RIG NOTU (46·GUVENLIK-FIX-2a):** `goz1` statik sunucusu `curl -I` (HEAD) istegini
kaldiramiyor - baglantiyi resetliyor ve ayni zincirdeki sonraki istek de baglanamiyor.
**`curl -I` KULLANILMAZ**, `curl -s -o /dev/null -w '%{http_code}'` ile GET yapilir.

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

## MK-1

**MK-1 - SOKUM ICEREN HER DALGADA CERCEVE TARAMASI ZORUNLU.**
Bir dalga fonksiyon/blok SOKUYORSA: (a) cerceve GIRIS NOKTALARINDA (`applyI18n`,
`setLang`, `setCur`, `refreshPrices` ve muadilleri) **tanimsiz-fonksiyon taramasi**
yapilir; (b) **dil / para birimi / tema gecisleri** REPRO setine EKLENIR.
Pin karsiligi: **P11**.

## MK-2

**MK-2 - GIT KOMUTU CALISTIRAN HER CAGRI CWD'YI ONCE DOGRULAR.**
Gerekce: MFIX-2 push turunda `cd` ayni cagrida kaldigi icin `git push` **scratchpad'de**
kostu, `fatal: not a git repository` verdi ve **PUSH OLMADI**; yalnizca ciktinin
okunmasi sayesinde fark edildi. Kural: git cagrisi `pwd` + `git rev-parse
--is-inside-work-tree` teyidiyle baslar.

## MK-3

## MK-3 GUCLENDIRILDI (KALICI)

**Her muhur/ozet degeri URETEN IFADESIYLE kaydedilir.** Pending birimi bu turdan itibaren
**DORTLU**:

```
BIRINCIL : status=0 AND id<=210  ->  COUNT=35 · MIN=9 · MAX=210 · SUM=3837
IKINCIL  : CHECKSUM_AGG(id)=239
```

## MK-4

## MK-4 (YENI KALICI MIKRO-KURAL)

**Denetim dagitimindan ONCE is LOKAL COMMIT'e alinir; L3 ve kural-uyum denetcileri AYRI bir
`git worktree`'de o commit uzerinde kosar.** Boylece cift-kor TEKNIK izolasyon (skill `sdp` · 1.9)
lokal islerde de saglanir.

## MK-4a

### MK-4a (YENI KALICI MIKRO-KURAL)

**Her worktree denetcisi, RAPORUNUN BASINA kendi `pwd` + `git rev-parse HEAD` olcumunu
koyar** (beklenen worktree yolu + beklenen SHA). Transkript grep'i ancak transkript VARSA
**EK** kanittir; **birincil kanit denetcinin kendi beyan ettigi olcumdur.**

## MK-4b

**MK-4b - HER DENETCI KENDI WORKTREE'SINI ALIR.** Paylasilan durum tasiyan kaynaklar
(**TEST VERITABANI ADLARI DAHIL**) denetci basina ayrilir ya da denetciler SERILESTIRILIR.

**EK (46·GUVENLIK-FIX-2a):** worktree sokumu ajanin TAMAMLANMA SINYALIYLE yapilir, ARA
RAPORLA degil. Gerekce OLCULDU: L3 denetcisi ilk raporunu verdikten SONRA da calisiyordu;
worktree sokulunce dizini altindan kayboldu. Sonuc etkilenmedi (olcumler sokumden onceydi)
ama servis edilen dosyalarin md5 kimligi IKINCI kez dogrulanamadi. "Rapor verdi" ile
"BITTI" AYNI SEY DEGILDIR.

**EK (47·GUVENLIK-FIX-3):** denetci test DB'si `DIVISIMA_TEST_DB` ile ayrilir; worktree'ye
`appsettings.Development.json` ana agactan kopyalanir (gitignore'lu); her denetcinin
scratchpad alt dizini ayri.

**EK (53·AV-3):** ON OLCUM ajanlari da ayri worktree + ayri test DB alir; canli rig/DB
paylasimi YALNIZ SALT-OKUR, yazan ajan TEK ve SERILESTIRILMIS; kurgu MAX mutabakati ajan
basina DEGIL **TUR BASINA**. Gerekce OLCULDU: alti ajan ana agacta ve tek canli DB'de kostu -
bir ajan musteriyi yanlis ajana atfetti, biri kirlenmis cikarimi geri aldi, bir denetci
digerinin turu ORTASINDA kayit yaratti; **ajan basina MAX mutabakati UNSOUND kaldi.**


## MK-5

**MK-5 - HER ON-OLCUM AJANI RAPORUNU KENDI HAM DOSYASINA YAZAR.** Harness'in cikti dosyasina
GUVENILMEZ ve boyutunun 0 olmadigi ajan tarafindan DOGRULANIR. Gerekce OLCULDU: bu dalganin
kesintisinde ajan cikti dosyalarinin **13/13'u 0 bayt** cikti (negatif kontrol: ayni dizinde
`b*.output` 842 KB'a kadar dolu) ve alti defter satiri DAYANAKSIZ kaldi; MFIX-3b'nin MK-4
turunda **AYNI olgu** yasanmisti. Rapor "yalnizca konusma baglaminda" var olursa defterin
HAM/SHA butunlugu YAPISAL OLARAK saglanamaz.

**EK (46·GUVENLIK-FIX-2a, denetci onerisi):** ajan HAM dosyasi ve denetci raporu OTURUM
SCRATCHPAD'INE yazilir, **ASLA worktree ICINE**. Gerekce OLCULDU: worktree'nin icine yazilan
denetci raporu, worktree sokulunce ONUNLA BIRLIKTE GITTI ve yeniden yazilmak zorunda kaldi.
Kanit loglari scratchpad'de oldugu icin KURTULDU - rapor kurtulmadi. Omru olculen seye BAGLI
bir yere kanit yazilmaz.

## MK-6

**MK-6 - KAYNAK-SOZLESME PINLERI MUTASYONLA SINANIR.** Bir pin yalnizca kaynak metnini
tariyorsa, "kirmizi-once" kaniti YETMEZ: aranan dizgenin **BASKA bir baglamda da** gecip
gecmedigi, korunan alani ONCEKI haline donduren bir uretim mutasyonuyla gosterilir.

**KURAL (ARSIV-3): mutasyon geri alma YALNIZ olcum yedeginden yapilir; `git checkout` /
`git stash` YASAK; dongu `git status --porcelain` BOS DEGILSE CALISMAZ.** Bedeli IKI KEZ
odendi: GF-3'te dongu commit'lenmemis uretim kodunu SILDI, GF-2b/F1'de ayni tuzaga YENIDEN
dusuldu (geri alma commit'lenmemis F1 isini goturdu; `_dbHamAd` sayimi 0 gorulunce yakalandi).

## MK-7

**MK-7 - EŞLEŞTIRME ÇAPALARI:**
"Eşleştirme çapaları ezberden yazılmaz: çapa metni, HAM çıktıdan kopyalanan bilinen-pozitif
parçadan alınır; ASCII'leştirme/transliterasyon yasak; her çapanın bilinen-pozitif sınaması
girdi dosyasının yoluyla birlikte kaydedilir. Bilinen-pozitif seti hedef alfabeyi temsil
eder — rakam dahil."

## MK-8

**MK-8 - AKIS DUZENLEYICIYLE METIN YAZILMAZ.**
"Kacis, tirnak, BOM ya da cok-satir tasiyan icerik akis duzenleyicileriyle (sed/perl/echo)
yazilmaz - dosya araci ya da tirnakli-EOF heredoc kullanilir; yazilan ve birlestirilen her
metin artefakti bayt duzeyinde dogrulanir (`cat -A` · `head -c 3`)."

**EK (52·GF-5) — `Directory.Build.props` DEGISIKLIGI `-getProperty` PROBUYLA DOGRULANIR;
KURAL, DERS DEGIL (UCUNCU TEKRAR).** XML yorumunda `--` dizisi dosyayi BOZAR ve `dotnet
restore` bunu **exit 0** ile gecer, MSB4024 BASMAZ. GF-4'te bir kez odendi, GF-5'te AYNI
tuzaga YENIDEN dusuldu (`dotnet --list-sdks` metni bir yoruma yazildi). Tek durust sinyal
`dotnet msbuild <proje> -getProperty:<ozellik>` probudur; prob ayrica AYIRT ETME kaniti verir
(bozukken MSB4024, duzeltilince deger).

**EK (52·GF-5) — KAYNAK-SOZLESME PINLERI YORUMSUZ METIN UZERINDE KOSAR.** Aranan dizge
uretim kodunda DEGIL onu ACIKLAYAN YORUMDA gecerse assert YANLIS atesler ya da BEDAVA dogru
olur. GF-4/K4'te `<clear />` asserti dosyanin kendi yorumuyla tatmin oldu; GF-5'te
`NotContain("action == \"Added\"")` kendi yorumunda geciyor diye yanlis kirmizi verdi.
Tarama, yorumlari AYIKLANMIS metin uzerinde yapilir. (Mutasyon sinamasi zaten MK-6'da.)

## MK-9

### MK-9 (YENI MIKRO-KURAL)

**"Bicim kapilari (whitespace + style) her checkpoint commit'inden ONCE kosulur; kapidan
gecmemis commit checkpoint sayilmaz."**

## MK-10

## MK-10 (YENI KALICI MIKRO-KURAL)

**Her commit/push kapisinda HEAD'in bir dal uzerinde oldugu dogrulanir
(`git symbolic-ref -q --short HEAD`); SHA'ya checkout yapilan her olcum donusu dala
checkout ile biter.**

---

# B8 — BAGLAYICI KARARLAR (karar cumleleri; gerekce arsivde: `divisima-defter`)

## Baglayici kararlar (00a-sira-kararlar.md)

- `00a:87` - **AutoMapper: 12.0.1'de KAL, bump YOK.**
- `00a:92` - **Seller modulu**: dokunma, veri duzeyinde kapali, migrate/seed yok.
- `00a:93` **ZORUNLU ON KOSUL (GUVENLIK DALGASI / G4): modul acilmadan ONCE satici refresh token'i httpOnly cereze tasinmali.**
- `00a:101` **IKINCI ON KOSUL (GUVENLIK-FIX-2 eki): `SellerAuthManager.Login` kilit kontrolunu SIFRE DOGRULAMASINDAN ONCE yapiyor** … Musteri tarafindaki sira (dogrula -> kilitliyse ve sifre DOGRU ise 403, degilse 401 + sayac artirma YOK) satici tarafina da tasinir ve pinlenir.
- `00a:106` - **invoice_number**: entegrator (Nilvera) numarasi esas, bizimki ic referans - degisiklik yok.
- `00a:128` EKSIK INDEKS ESIGI, GERCEK HACIMDE TEKRAR BAKILACAK. … **KORLEMESINE INDEKS EKLENMEZ** (kullanici sarti).
- `00a:180` JS/DOM TEST KOSUCUSU (Playwright vb.). … **LAUNCH ONCESI EKLENMEZ:**
- `00a:188` MISAFIR CHECKOUT ENUMERATION ve COP COD SIPARISI. … **KARAR: LAUNCH SONRASI.**
- `00a:206` - **Iyzico'nun TELEMETRI alan adlari CSP'de ACILMAZ (kalici karar).**
- `00a:213` - **Auth modeli**: mevcut hibrit korunuyor (access localStorage + refresh httpOnly cookie + kosullu CSRF).
- `00a:215` - **`EnableRetryOnFailure`: S7'de ACILMADI.**

## Baglayici kararlar (muhurlerden — EK-1)

- `37·MANTIK-FIX-1·MF-2 ONCESI ARA DURUM` - **(a)** `InvoiceManager.cs:76`'nin **BRUT** toplama bagi MF-2'de ACIK HALE GETIRILIP **PINLENECEK**.
- `38·MANTIK-FIX-2R·ACIK OLCUM (2)` **URETIM KAYNAGI SAYIMA GIRMEDI ve DOKUNULMADI** (C4): `InvoiceManager.cs:24` (`0.20m`)
- `39·MANTIK-FIX-3·MERKEZ KARARLARI N2` | **N2** | Hata eslemesi once MAKINE-OKUNUR sinyal; yoksa HAM yanit capasi + cift bicim + kirilganlik kaydi |
- `36·MANTIK-AV-1·DALGA BOLUMLEMESI` **64 bozuk `invoice_items` satiri D-YAN'a** (veri temizligi, fix degil).
- `37·MANTIK-FIX-1·MF-2 ONCESI ARA DURUM` **InvoiceManager KODUNA DOKUNULMADI (sart aynen korundu).**
- `37·MF-1·MF-3 SARTLARI` **(a)-(c):** 409 semantigi YENIDEN ACILMAZ · **IKINCI kupon dogrulama noktasi ACILMAZ** … musteri+adres yazimi `PlaceOrder` BASARISINA bagli.
- `44..48·GF-1..GF-2b` guvenlik kararlari (28) arsivde: `divisima-defter` · B8, tam metin muhur 44-48.
  kapsam: replay (misafir/uye) · auth_time/step-up · sahiplik 404 / 403 sabitleri · yetki yuzeyleri · sifre ozeti v2 · access iptali · jeton ozeti · refresh cerezi/rotasyon/sekme kilidi · yeniden kullanim alarmi · log maskesi ·
  yer-tutucu kapisi · HSTS · rate limit (hassas kova) · UTC zaman ekseni · musteriye donen hata metni · URL sema politikasi · renk allowlist · service worker · Google Fonts SRI · 429 hata sinifi · siparis rid · CSP
- `50·GF-4·K1` `50·GF-4·K4` `50·GF-4·K5` `50·GF-4·K7` **TEDARIK ZINCIRI (dordu tek satirda):** action'lar 40-hane COMMIT SHA'sina pinli · paket kaynagi TEK (`NuGet.config` + `<clear />`, her projede `packages.lock.json`, CI `--locked-mode`) · imaj referansi TEK KAYNAK … **AutoMapper 12.0.1 KALIR** … `NuGetAuditMode=all` UYARI, deprecated adimindaki `\|\| true` BILINCLI.
- `52·GF-5` **IMZASIZ webhook 404 STATUKO = KABUL EDILMIS RISK** … Girdi sinirlari TEK KAYNAK `GirdiSinirlari` — **ortak RuleBuilder ACILMAZ** … **sema'ya capalanir sabite DEGIL**; `request_id` <=80 + `[A-Za-z0-9._-]` **GUID SARTI ASLA** … Log maskesi GLOBAL: iki Serilog sink'i de `MaskeliFormatter` (`ITextFormatter`), enricher yolu KAPALI, `KanitMaskesi` olcutu GENISLETILMEZ.
- `55·GF-6` **REPLAY GUARD'I TEK SERVIS:** `request_id` replay kurali (kupon KANONIK + coklu-kume sepet + sizintisiz 400) `SiparisReplayGuardi`de TEK yerde; … Kopya ACILMAZ.
- `55·GF-6` **DURUM YAZIMI TEK KAPIDAN + TERMINAL KORUMASI:** `OrderManager`da her `order.status` yazimi `DurumYaz` -> `OrderStatusMachine`den gecer … Iyzico'nun IKI dali terminal siparisi DIRILTMEZ: … Iade **ELLE** (BILINEN).
- `55·GF-6` **COD PARA ANLAMI `Delivered`DA (DAR):** `PaidOrderSpec.IsPaid(byte status, byte paymentType)` - COD yalniz `Delivered`. … Sadakat: `PaymentConfirmedSideEffects` BOLUNMEZ;
- `55·GF-6/F5-F6` **KUPON LIMITI "HAK CANLI MI" SORUSUDUR** - `PaidOrderSpec`ten BAGIMSIZ kalir … **KARGO TESLIMAT DALI TRANSACTION ICINDE** (`ExecuteInTransactionAsync`):
- `51·AV-2` **LAUNCH BLOKER OLCUTU:** `KRITIK` **∨** `YUKSEK`+`KIMLIKSIZ-UZAK` **∨** `[PARA]`/`[VERI-BOZAN]`. Digerleri launch SONRASI. … `ADMIN` on kosullu kalem KRITIK OLAMAZ.
- `51·AV-2` **AV KAPSAMI KUMULATIF MATRISLE OLCULUR; YER DEGISTIRME YASAK.**

# B9 — BILINEN / KABUL EDILMIS RISK (yalniz ACIK/BAGLAYICI basliklar; DURUM sutunlu tam liste ilgili muhurde)

- **`45·GUVENLIK-FIX-1b`** — bes kalem (ayni-saniye jeton penceresi · miras oturumda step-up · 342 olu oturum · IP davranis kaniti yok · K4 gecikmeli aile iptali).
- **`46·GUVENLIK-FIX-2a`** — uc kalem (Google Fonts SRI YASAK · `admin.html` kendi `imgUrl()` kopyasi · panelde `guvenliHTML`/`guvenliYaz` cagirani yok).
- **`47·GUVENLIK-FIX-3`** — dort kalem (`lockout_end` YEREL · kismi iptal sonrasi replay 400 · logout bayat cerezle 200 · `expiration` `Z` bicimli).
- **`50·GUVENLIK-FIX-4`** — iki kalem (yerel SDK 9 / CI SDK 8, `global.json` YOK - PINLENMEMIS … Dependabot `docker` ekosistemi workflow `services.*.image` ve C# digest literallerini TARAMAZ
- **`51·GUVENLIK-AV-2`** — iki kalem: **SignalR "admins" alarmi BOS GRUBA yayin yapiyor**
- **`53·GUVENLIK-AV-3`** — iki kalem: **rezervasyon birikmesi** … **kor eksenler A02 · A03 · A05 · A04**
- **`55·GUVENLIK-FIX-6`** — … **raporlama siteleri ESKI kuralda** … **terminal siparise gelen odemenin IADESI ELLE** … **`health` uclari BILINCLI anonim** … **BAGLAYICI** … T4-F2 (kayip guncelleme) LAUNCH BLOKER olcutu ISTISNASI … `first_order_only` kuponu ESKI kuralda … `KodSatirlari` yalniz satir basi `//` ayikliyor … `SignalR "admins"` alarmi BOS GRUBA yayin yapiyor
- **`60·MON-1`** — … **Y1 LATENT: RCSI acilirsa alarm kaybi** (uretimde kapali).
- **`57·LAUNCH-DEPLOY-1`** — … **SQL Server EXPRESS** … **TDE YOK** … **ANAHTAR KAYBI = YEDEK KAYBI** … **`ForwardedHeaders:KnownProxies` = Docker AG GECIDI** … **SOFT-LAUNCH KAPISI ACIK**
- **`frame-src` SUPHELISI ACIK**
- **D-7 KISMEN:** admin TAM, vitrin `'unsafe-inline'` KABUL EDILMIS RISK; **CSP FAZ B YOK.**

# B10 · ACIK DELTALAR (yeni delta buraya; kapanan kalem muhurden sonra `divisima-defter` arsivine tasinir)

## Suzgec

## Ders-sayac

## Kurgu / D-YAN

## Kuyruk

## Acik SUPHELI

---

## Dalga akışı ve kabul kriteri (KALICI)

Dalga başında /clear zorunlu, dalga içinde bağlam dolunca /compact.
Kabul kriteri ölçülebilir olacak: commit · test sayısı · CI durumu · sapma listesi. "Çalışıyor", "tamam", "iyi görünüyor" kabul kriteri değildir.

Geçmiş dalga kayıtları, kapanmış kararlar, faz tarifleri ve eski B5–B9 bölümleri (süzgeç kütüphanesi, dersler, kurgu/D-YAN, bağlayıcı kararlar/SÜPHELİ, kuyruk/BİLİNEN) için `divisima-defter` skill'i çağrılır.
