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
f) **MK-12 PROSEDUR SKILL'LERI:** SDP ve SUREC `.claude/skills/` altinda proje skill'idir
   (arsiv DEGILDIR; MK-11 a)'nin `.claude/` yasagi yalniz `docs/muhur/` icindir). Her
   dalga/denetim tarifi "SDP ve SUREC skill'lerini yukle" satiriyla baslar; CC `Skill`
   cagrisiyla yukler, donmezse `Read .claude/skills/<ad>/SKILL.md` ile yukler ve hangisiyle
   yukledigini kapi bolumunde beyan eder (ad + govde bayt). Yuklenmeden olcum/uygulama
   yapilmaz. Skill govdesi CLAUDE.md gibi delta alir (MK-11 c/e); surum baslikta.

**BOLUM DUZENI:** B0 bu blok · B1 calisma kurallari · B2 SDP v1.3 -> skill `sdp` ·
B3 DALGA ICI DENETIM (bayt-ayni) + SUREC -> skill `surec` · B4 MK-1..MK-10 (MK-11/12 B0'da) ·
B5 suzgec kutuphanesi · B6 dersler · B7 kurgu sabitleri + D-YAN ·
B8 baglayici kararlar + acik SUPHELI · B9 kuyruk + devir.
B1 ve DALGA ICI DENETIM kaynagindan BAYT-AYNIDIR.

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
- **KOD YORUMUNDA SATIR NUMARASI YAZILMAZ — atif SEMBOL/METOT adiyla yapilir (52·GF-5).**
  MK-11/d'nin kod yuzu. Gerekce OLCULDU: GF-5'in ekledigi yorumlarda **17 bayat atif** cikti;
  hepsi ZEMINE karsi DOGRUYDU ve atifi yazan dalganin KENDISI ayni dosyalarda satirlari
  kaydirdi (`Program.cs` atiflari TAM +10). Dordu MERKEZ KARARLARININ dayanagini tasiyordu.
  Ozu hicbirinde yanlis degildi - bozulan sey CAPAYDI; satir numarasi, yazildigi anda dogru
  olsa bile KENDINI KORUYAMAYAN bir capadir.
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

**AD HARITASI (47·GUVENLIK-FIX-3):** `GF-3` = **GUVENLIK-FIX-3, Eylul 2026** (sizinti /
yapilandirma / limit / kalinti). Zemindeki `GuvenlikFix3SozlesmeTests.cs` ise **Agustos 2026**
dalgasinin DAGITIM YUZEYI pinleridir (nginx/CSP/clickjacking); Eylul dalgasinin pin dosyasi
`GuvenlikFix3SizintiSozlesmeTests.cs` adiyla ayrilmistir.

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
`git worktree`'de o commit uzerinde kosar.** Boylece cift-kor TEKNIK izolasyon (skill `sdp` · 1.9)
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

kaynak: 37·MANTIK-FIX-1_MUHRU

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

kaynak: 37·MANTIK-FIX-1_MUHRU

**MK-6 - KAYNAK-SOZLESME PINLERI MUTASYONLA SINANIR.** Bir pin yalnizca kaynak metnini
tariyorsa, "kirmizi-once" kaniti YETMEZ: aranan dizgenin **BASKA bir baglamda da** gecip
gecmedigi, korunan alani ONCEKI haline donduren bir uretim mutasyonuyla gosterilir.
Gerekce OLCULDU: P19'un `Contain("effective_price")` asserti BEDAVA DOGRUYDU (dizge
`mapProduct` govdesinde DORT satirda geciyordu); alan K1 oncesine dondurulunce **TUM SUIT
575/578 ile temiz durumla BIREBIR AYNI** kaldi - yani duzeltmenin istemci yarisi PINSIZDI.
Assert ALAN BAZLI yapilinca ayni mutasyon TAM 1 ISIMLI KIRMIZI verdi.

**KURAL (ARSIV-3): mutasyon geri alma YALNIZ olcum yedeginden yapilir; `git checkout` /
`git stash` YASAK; dongu `git status --porcelain` BOS DEGILSE CALISMAZ.** Bedeli IKI KEZ
odendi: GF-3'te dongu commit'lenmemis uretim kodunu SILDI, GF-2b/F1'de ayni tuzaga YENIDEN
dusuldu (geri alma commit'lenmemis F1 isini goturdu; `_dbHamAd` sayimi 0 gorulunce yakalandi).

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
OLMADIGI cikti. skill `sdp` · 1.7/1'in bu turdaki kazanci.

**S6 - ALT-DIZGE SAYACI.** Ureten ifade: `grep -oi "<capa>" <dosya> | wc -l`

```
POZ  /tmp/poz_pii.txt ("PII satiri" + "pii kucuk")  -> 2
NEG  ayni dosyada "zzzpii"                          -> 0
POZ  tum-defterler.txt "PII"                        -> 17  (capraz: grep -c -> 17)
NEG  tum-defterler.txt "zzzssrf"                    -> 0
```

**EMEKLI:** `grep -oiF` - bu kabukta `-o` + `-i` + `-F` BIRLIKTE HICBIR SEY dondurmuyor;
ayni POZ girdide **0** doner. 27 ankrajlik bir kor-nokta taramasi bu dedektorle
"27/27 sifir" verdi ve SSRF disindaki **26 sonuc YANLISTI**. Kapsam elestirmeni "27/27
sifir makul degil" deyip POZ kontrol kosunca yakalandi (`51·AV-2`).
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
   `price-drop`un GERCEKTEN tireli olmasi yaniltti. **skill `sdp` · 1.7/2 - bu dalgada 1. dusus.**
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

## Dalga dersleri — AILE GRUPLARI (tam metin muhurde, kesilenler 49'da)

**A · CAPA / ESLESME BICIMI (5 vaka)**
- Capa POZ olcumu "kac" yaninda "NEREDE" sorar - sayim dogru/konum yanlis. -> 43
- Indeks/kisit sayimi DOSYA-GENELI grep ile; blok penceresi YOKLUK KANITI DEGILDIR. -> 44
- Sink sayimi eslesme-bicimi kusuru tasiyabilir: satir sonunda biten atama gorunmez. -> 46
- Assert KUSUR SINIFINI pinler, ESKI LITERAL BICIMINI degil (5. vaka). -> 48
- NEG capa dizesi belgeye YAZILMAZ; NEG kontrolu raporda/muhurde anilir. -> 43

**B · TEK KANAL / KAYNAK BUTUNLUGU (4 vaka)**
- **YORUM != OLCUM - IKI TUR UST USTE.** `AuthManager.cs:468` "ikisi de zaten
  security_events'te tutuluyor" diyor (olculdu: TUTULMUYOR, ip/ua 0/40) ve
  `AuditLogController` yorumu "40 controller icinde TEK ornek (olculdu)" diyor
  (olculdu: `SeoController` ayni kusuru tasiyor ve iddiadan ALTI GUN once oradaydi). -> 51
- Tek kanalli on olcum bulgusu = SUPHE; tarife KALEM OLMAZ. -> 47
- BILINEN listesi B8 fragmanlarindan KURULMAZ; 00a/00b tam metni okunur. -> 42
- RUNTIME SOZLUK = DB METNI; kaynak okuyana "sabit" gorunur, DEGILDIR. -> 46

**C · GERI ALMA / DOSYA GUVENLIGI (3 vaka)**
- Yeni test/pin dosyasi yazilmadan ONCE yol YOKLUGU olculur; `git status`ta `M` = DUR. -> 47
- MK-6 mutasyon dongusu `git status --porcelain` BOS DEGILSE CALISMAZ. -> 47
- Kirmizi-once geri almada `git stash` KULLANILMAZ; olcum yedegi + elle geri alma + md5. -> 45

**D · SIR HIJYENI (1 vaka)**
- Ham yanit dokumleri diske MASKELI yazilir; ajan ortak kurali "diske yazilmaz"i da icerir. -> 42

**E · URETIM DAVRANISI (1 vaka)**
- `ExecuteUpdateAsync` `AuditInterceptor`i ATLAR; CAS yolunda denetim kaydi ELLE yazilir. -> 45

**F · RIG / OLCUM DUZENEGI (7 vaka)**
- **goz1 API'sinin saglik ucu `/health` (200); `/api/health` YOKTUR (404).** Surec adi
  `Divisima.API` DEGIL **`dotnet`**tir. PowerShell `Invoke-WebRequest` 404'te ISTISNA atar -
  iki dedektor birden "rig kalkmadi" der, oysa rig KOSUYORDUR. -> 51
- `Directory.Build.props` XML'i BOZUKKEN `dotnet restore` exit 0 verir ve MSB4024 BASMAZ;
  ozellik projeye ULASTI MI sorusu yalniz `msbuild -getProperty` probuyla yanitlanir. -> 50
- MCR digest'i Accept turune gore DEGISIR: manifest LISTESI olmayan imajda liste turleri
  istenirse Schema 1 doner ve o digest CEKILEMEZ; digest her zaman GET ile dogrulanir. -> 50
- CR dedektoru olarak YALNIZ `tr -cd '\r' | wc -c` calisir; `awk '/\r$/'` ve
  `grep -c "$(printf '\r')"` bu kabukta 0 doner (`grep -P` de calismaz). -> 50
- Harness fetch katmani SW kaydini engeller; SW kabulu GERCEK CHROME ister. -> 48
- Chrome/CDP rig: `--user-data-dir` %LOCALAPPDATA% altinda (temp'te Cache Storage KIRIK);
  offline kaniti SUNUCUYU DURDURARAK alinir - `emulateNetworkConditions` SW'yi KAPSAMAZ. -> 49
- `register()` OK, SW KAYDI DEMEK DEGILDIR; kanit `getRegistrations` + `active` + controller. -> 49
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
**GF-1 KOD FAZI hicbir kurgu kaydi uretmedi** (MAX'lar ve MK-3 uclusu push aninda BIREBIR).
Kapanis fazinda goz1'de TEK kayit uretildi: musteri **169** `gf1.1@example.com` (uretim
yolundan: register -> verify -> login) + `consent_records` 1 + `user_sessions` 340-342.
**MAX musteri 168 -> 169**; siparis/adres/fatura/Pending DEGISMEDI.
Tek sema degisikligi `user_sessions.auth_time` kolonudur (`44·GUVENLIK-FIX-1`).
**GF-1b HICBIR KURGU KAYDI URETMEDI** - testler ayri CI/sinif veritabanlarinda kostu, dev
DB'ye YALNIZ OKUMA yapildi. MAX musteri **169** BIREBIR kaldi; `user_sessions` **342**
(K3 geriye donuk ozetleme YAPMADI, bu satirlar fiilen OLU oturum - `45·GUVENLIK-FIX-1b`).
Suit tabani `45·GUVENLIK-FIX-1b` kapanisinda **Sql 378/378 · tam 641/644** (3 kirmizi =
bilinen Docker uclusu); ureten ifade `dotnet test ... --filter "Category=Sql"` ve filtresiz.
**GF-2a HICBIR KURGU KAYDI URETMEDI** (olculdu: `email LIKE 'gf2a%'` 0 · `name LIKE '%GF2A%'
COLLATE Latin1_General_BIN2` 0). Olcumler tarayicida SENTETIK girdilerle yapildi, DB'ye
YAZILMADI. MAX'lar BIREBIR: musteri **169** · urun **955** · siparis **286** ·
`user_sessions` **342** · Pending(status=0, id<=210) **35/3837**.
Suit tabani `46·GUVENLIK-FIX-2a` kapanisinda **Sql 378/378 · tam 651/654** (+10 pin).
**GF-3 HICBIR KURGU KAYDI URETMEDI** (olculdu: `email LIKE 'gf3%'` -> 0; olcumler ayri test
veritabanlarinda ve ikinci API surecinde SENTETIK girdilerle yapildi). MAX'lar BIREBIR:
musteri **169** · urun **955** · siparis **286** · adres **119** · fatura **119** ·
`user_sessions` **342** · Pending(status=0, id<=210) **35/3837**.
Suit tabani `47·GUVENLIK-FIX-3` kapanisinda **Sql 382/382 · tam 710/713** (+59 pin;
uc kirmizi = bilinen Docker uclusu). Ureten ifade: `dotnet test ... --filter "Category=Sql"`
ve filtresiz.
**GF-2b HICBIR KURGU KAYDI URETMEDI** (olculdu: `email LIKE 'gf2b%'` -> 0; olcumler ayri
test veritabanlarinda ve tarayicida SENTETIK girdilerle yapildi, panel giris denemesi VAR
OLMAYAN bir adresle kosuldu). MAX'lar goz turu kapanisiyla BIREBIR: musteri **171** ·
urun **955** · siparis **286** · adres **119** · fatura **119** · `user_sessions` **356** ·
Pending(status=0, id<=210) **35/3837**.
Suit tabani `48·GUVENLIK-FIX-2b` kapanisinda **Sql 382/382 · tam 730/733** (+20 pin;
uc kirmizi = bilinen Docker uclusu). Ureten ifade:
`dotnet test Divisima-Backend.sln -c Release --filter "Category=Sql"` ve filtresiz.
**GF-4 HICBIR KURGU KAYDI URETMEDI.** MAX'lar GF-2b kapanisiyla BIREBIR (musteri 171 ·
urun 955 · siparis 286 · adres 119 · fatura 119 · `user_sessions` 356 · Pending 35/3837).
**CAPA TUZAGI - KAYIT:** onceki muhurlerin `email LIKE 'gfN%' -> 0` kanit bicimi GF-4 icin
KIRLIDIR - `'gf4%'` bugun **11** satir donduruyor (id 55-65) ve onbiri de **25 Agustos
2026** tarihli AGUSTOS dalgasinin kurgusudur (collation tuzagi DEGIL; BIN2 ile de 11).
Durust ureten ifade tarih niteleyicisi ister: `... AND created_at >= CAST(GETDATE() AS date)`
-> **0**.
Suit tabani `50·GUVENLIK-FIX-4` kapanisinda **Sql 382/382 · tam 743/746** (+13 pin;
uc kirmizi = bilinen Docker uclusu, yerelde Docker YOK). Ureten ifade ayni.
**GF-5 KURGU (`52·GUVENLIK-FIX-5`)** - ureten ifadeleriyle:
`SELECT MAX(id) FROM customers` -> **184** (178 `gf5.b.1@` · 179 `gf5.guest.1@` **SD-7 yetimi,
ESKI kodun urunu** · 182 `gf5.1@` · 184 `gf5.guest.5@`; **180/181/183 SAF KIMLIK BOSLUGU**,
`SELECT COUNT(*) ... WHERE id BETWEEN 180 AND 183 AND id<>182` -> 0).
siparis **287** · adres **126** · fatura **120** · `COUNT(*) user_sessions` **372** ·
`security_events` **46** (altisinda ip+ua DOLU; GF-5 oncesi 0/40) · `audit_logs` 4328.
`SELECT COUNT(*),MIN(id),MAX(id),SUM(CAST(id AS bigint)) FROM orders WHERE status=0 AND
id<=210` -> **35 / 9 / 210 / 3837 BIREBIR**. Yetim adres 0 · yetim siparis 0 (depo geneli);
musteri 184 TAM uretim imzasi tasiyor. Elle INSERT YOK, sema degisikligi YOK.
Suit tabani `52·GUVENLIK-FIX-5` kapanisinda **Sql 382/382 · tam 777/780** (+34 pin;
uc kirmizi = ayni bilinen Docker uclusu). Ureten ifade ayni.
**AV-2 DORT KURGU KAYDI URETTI (SALT OLCUM turu, hepsi URETIM YOLUNDAN):** musteri
**172** `av2.sb.1@` · **174** `av2.sf.1@` · **175** `av2.sd.2@` · **177** `av2.sc.1@`.
URETIM IMZASI: dordunde de `password_hash` **69** / `password_salt` **16** (GF-1/K6 v2
zarfi). **173 ve 176 SAF IDENTITY BOSLUGUDUR** - bes FK tablosunda (`addresses`, `orders`,
`consent_records`, `user_sessions`, `carts`) 0 yetim satir.
**D-YAN:** musteri **175 MISAFIR YOLUNDAN dogdu** (register DEGIL; SD-7'nin 151-karakter
reprosu), bu yuzden `consent_records` **0** tasiyor ve o e-posta misafir checkout'tan
KALICI olarak disaniyor. Temizlik karari merkezin.
MAX'lar kapanista: musteri **177** · urun **955** · siparis **286** · adres **119** ·
fatura **119** · `user_sessions` **369** · Pending(status=0, id<=210) **35/9/210/3837**
(uc olcumde de BIREBIR; `orders`'a Pending URETILMEDI).
**GF-3 TABANI AD ALANI KAPALIYKEN ALINMIS (kayit):** `SemaTekKaynakTests` kosucu ad alanini
yalniz baglanma noktasina uyguluyordu; yaratma ve dusurme HAM adi kullaniyordu. Bu yuzden
`DIVISIMA_TEST_DB` SET edildiginde - ki MK-4b bunu ZORUNLU kilar - dort test SQL login
hatasiyla dusuyordu ve MK-4b tabani fiilen OLCULEMIYORDU. GF-2b/F1 ile yapisal olarak
kapatildi; **733/730 tabani ilk kez env SET edilmis turda da dogrulandi.**

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

- `37·MANTIK-FIX-1·MF-2 ONCESI ARA DURUM` - **(a)** `InvoiceManager.cs:76`'nin **BRUT** toplama bagi MF-2'de ACIK HALE GETIRILIP
- `38·MANTIK-FIX-2R·ACIK OLCUM (2)` **URETIM KAYNAGI SAYIMA GIRMEDI ve DOKUNULMADI** (C4): `InvoiceManager.cs:24` (`0.20m`)
- `39·MANTIK-FIX-3·MERKEZ KARARLARI N2` | **N2** | Hata eslemesi once MAKINE-OKUNUR sinyal; yoksa HAM yanit capasi + cift bicim + kirilganlik kaydi | K3 ve K3b'nin ikisi de bu capaya dayaniyor - sunucu yanit sozlesmesi DEGISTIRILMEDI, istemcide politika kopyasi ACILMADI |
- `36·MANTIK-AV-1·DALGA BOLUMLEMESI` i18n. **64 bozuk `invoice_items` satiri D-YAN'a** (veri temizligi, fix degil).
- `37·MANTIK-FIX-1·MF-2 ONCESI ARA DURUM` **InvoiceManager KODUNA DOKUNULMADI (sart aynen korundu).**
**GF-1..GF-2b kararlari OZ; tam metin muhur 44-48, kesilen 29 satir bayt-aynen 49'da.**

- `44·GF-1·K1` request_id replay'i misafir 409'undan MUAF; e-posta ordinal eslesirse 200, eslesmezse sizintisiz 400. -> 44
- `44·GF-1·K3` `auth_time` = oturum zincirinin GIRIS ani; refresh ESKI satirdan kopyalar, NULL geriye DOLDURULMAZ. -> 44
- `44·GF-1·K4` Sahiplik ihlali 404 (uc nokta); kalan 11 rol/CSRF/IP 403'u SABIT, negatif kontrol pinli. -> 44
- `44·GF-1·K5` Controller DISI yuzeyler pinli: `RequireAuthorization` tek kaynak · Hub sinif oznitelig i · Hangfire admin-only. -> 44
- `44·GF-1·K6` Sifre ozeti v2 zarfi PBKDF2-SHA512 100k (69 bayt); v1 giriste SESSIZCE tasinir, migration YOK. -> 44
- `45·GF-1b·K1` Access iptali `revoked_before` esigi, kosul `iat < esik`; hesap KILIDI yazmaz, sifre SIFIRLAMA yazar. -> 45
- `45·GF-1b·K3` Oturum/sifirlama jetonlari DB'de SHA-256 HEX (base64 DEGIL - `Turkish_CI_AS` varyant kabulu). -> 45
- `45·GF-1b·K5` Refresh cerezi ile oturum satiri AYNI ANDA biter; tek kaynak `OturumOmru.RefreshGun`=7. -> 45
- `45·GF-1b·K7` Step-up `auth_time` NULL ise FAIL-CLOSED; geriye donuk doldurma YOK. -> 45
- `45·GF-1b·F1` Yeniden kullanim alarmi CAS'ta KOSULSUZ, pasif jetonda KOSULLU; aile iptali best-effort (GF-3/K10 kapatti). -> 45
- `45·GF-1b·GF1-B9` CURUDU: "step-up auth_time refresh'te sifirlaniyor" GF-1/K3 ile ZATEN kapanmisti. -> 45
- `46·GF-2a·K3` URL sema politikasi TEK YERDE (`resolveUrl`); raster `data:image` KABUL, `svg+xml` ve protokol-goreli `//` RED. -> 46
- `46·GF-2a·K4` Renk allowlist render'da `{3,4,6,8}`, backend `{6,8}`; uzunluk kumesi BILINCLI daha genis. -> 46
- `46·GF-2a·K8` SW IKI KOVA (shell/api); `/api/` NETWORK-ONLY, cikista YALNIZ api kovasi silinir. -> 46
- `46·GF-2a·K9` Google Fonts'a SRI EKLENMEZ (KABUL EDILMIS RISK): `css2` yaniti UA'ya gore degisir. -> 46
- `46·GF-2a·K10` Refresh sekmeler arasi TEK (`navigator.locks`); desteksiz tarayicida single-flight'a duser. -> 46
- `47·GF-3·K1` Maskede IKINCI DAL e-posta (`@` jeton kumesinde); etiket ayrimi jeton dalina UYGULANMAZ. -> 47
- `47·GF-3·K5` Yer-tutucu kapisi TEK dongude YEDI anahtara + public-deger SHA-256 deny-list; Production'a KOSULLU. -> 47
- `47·GF-3·K6` HSTS TEK KAYNAK nginx; `app.UseHsts()` KALDIRILDI (iki farkli STS basligi cikiyordu). -> 47
- `47·GF-3·K9` "hassas" kovasi 20/dk IP basina; YERLESIK ve DAGITIK taraf BIRLIKTE acilir. -> 47
- `47·GF-3·K10` Rotasyon TEK DB transaction (CAS + denetim + INSERT); logout ayni CAS yardimcisinda. -> 47
- `47·GF-3·K11` Zaman ekseni UTC, DAR kapsam; yazan-okuyan CIFTLER birlikte tasinir, `lockout_end` YEREL kalir. -> 47
- `47·GF-3·K12` Replay olcutu = e-posta + sepet kalemleri (iptaller DISLANIR) + KANONIK kupon; eslesmezse 400 sizintisiz. -> 47
- `47·GF-3·F1` Musteriye donen `order_status_history.note` SABIT METIN; ham `ex.Message` YAZILMAZ. -> 47
- `48·GF-2b·K1` Refresh kilidindeki kiyas tabani BELLEK jetonu; `storage` dinleyicisi YALNIZ bellegi esitler. -> 48
- `48·GF-2b·K2` SW kaydi TEK NOKTADA (`pwa-register` -> `/service-worker.js`) + `KAPAT` bayragi UC olayda da okunur. -> 48
- `48·GF-2b·K3` 429 AYRI HATA SINIFI; arama onbellege YAZMAZ, kupon YALNIZ 400/404/422'de kalkar. `[PARA]` -> 48
- `48·GF-2b·K4` rid YALNIZ 409'da yenilenir `[VERI-BOZAN]`; `sepetImzasi` GENISLETILMEZ, niyet imzasi AYRI ve onu ICERIR. -> 48
- `48·GF-2b·K5` admin CSP `'unsafe-inline'`siz; vitrin `'unsafe-inline'` KABUL EDILMIS RISK; `frame-src` SUPHELI. -> 48
- `50·GF-4·K1` Tum GitHub action'lari 40-hane COMMIT SHA'sina pinli + surum yorumu; major yukseltme de bu usulle. -> 50
- `50·GF-4·K4` Paket kaynagi TEK (`NuGet.config` + `<clear />`) · her projede `packages.lock.json` · CI `restore --locked-mode` (CI SDK 8'de YESIL kosuldu). -> 50
- `50·GF-4·K5` Imaj referansi TEK KAYNAK: dort site ayni tag+digest, pinle zorunlu. Digest **Schema 2 POZ/NEG cozucuyle** alinir (etiketten okunan deger TEK BASINA gecersiz; digest'le geri cekilip echo-back sinanir). -> 50
- `50·GF-4·K7` AutoMapper 12.0.1 KALIR (lisans degisimi **15.0.0**); `NuGetAuditMode=all` UYARI seviyesi; deprecated adimindaki `\|\| true` BILINCLIDIR (o komut bulguda da exit 0 verir, kaldirmak olmayan bir kapiyi var sandirir). -> 50
- `51·AV-2` **LAUNCH BLOKER OLCUTU:** `KRITIK` **∨** `YUKSEK`+`KIMLIKSIZ-UZAK` **∨** `[PARA]`/`[VERI-BOZAN]`. Digerleri launch SONRASI. Siddet ON KOSULDAN bagimsiz verilemez; `ADMIN` on kosullu kalem KRITIK OLAMAZ. -> 51
- `51·AV-2` **AV KAPSAMI KUMULATIF MATRISLE OLCULUR; YER DEGISTIRME YASAK.** Her AV turu kapsam matrisini (uc/controller x tur) muhre kumulatif yazar ve sonraki tur onceki turun KOR KUMESINDEN baslar. Gerekce olculdu: AV-1'in kor 13'u ile AV-2'nin kor 17'sinin kesisimi **0**; 40 controller'in **30'u** en az bir turda kor kaldi. -> 51
- `52·GF-5` **OLAY YUZEYI:** kayitsiz **ve kilitli** hesap girisi · logout (iki dal) · sahiplik ihlali `IdorAttempt` **kapsam DUZELTILDI (`53·AV-3`): cagri yeri IKI - `IyzicoPaymentManager`(`order`) + `OrderManager`(`address`); "Order+Payment" YANLISTI** (bes uctan yalniz `payment/initialize` iz birakiyor) · 429 **ornekleme ip+uc basina 60 sn**, `customer_id` NULL **kabul edilmis sinir** (middleware `UseAuthentication`'DAN ONCE) · bozuk imza. **IMZASIZ webhook 404 STATUKO = KABUL EDILMIS RISK** (otorite retrieve zinciri; K7 DUSTU - saglayici imza GONDERMIYOR, uygulansaydi tum callback+webhook 400 olurdu). ip/ua **`SecurityEventManager` ICINDE** doldurulur; sinir 60 = iki kolonun DARI. `detail` kolon genisligine KIRPILIR. -> 52
- `52·GF-5` **MISAFIR/UYE GIRDI SINIRLARI TEK KAYNAK `GirdiSinirlari`** (sabit DEGERLER; ortak RuleBuilder ACILMAZ - Seller'a kapsam tasmasin, o kendi literalini korur). `guest_name` <=100 **olcum SANITIZE SONRASI** (`Sanitize` UZATMAZ - bes `Replace(...,"")`+`Trim`; `HtmlEncode` AYRI metot ve bu yolda cagrilmiyor). `request_id` <=80 + `[A-Za-z0-9._-]`, **GUID SARTI ASLA** (dolu 122 degerin 54'u GUID DEGIL; frontend yedek dali `co-...` uretir ve PINLI). E-posta <=200. Sinir degerleri **SEMAYA capalanir**, sabite DEGIL. -> 52
- `52·GF-5` **LOG MASKESI GLOBAL:** Serilog'un IKI sink'i de `MaskeliFormatter` (`ITextFormatter`) uzerinden yazar - **enricher yolu KAPALI** (`LogEvent.Exception` readonly, olculdu) ve yeni paket GEREKMEDI. Cerceve metinleri (SQL "Truncated value", EF `@pN=`) AYRI `LogMetniMaskesi`de; **`KanitMaskesi` olcutu GENISLETILMEZ** (`KanitMaskesiTests` sozlesmesi korunur). GF-3'un "elle `ex` gecirilmez" sozlesmesi SURER - formatter onun YERINE gecmez, ARKASINA eklenir. -> 52

## Acik SUPHELI (00b-supheli.md)

Durum satiri (00b:3-9, HAM):

**DURUM: ACIK KALEMLER #14 (LAUNCH SONRASI) ve #20 (bugun BOSLUK YOK, testte kapatildi).**
Kapananlar: #1..#13 ilgili sprintlerde · #15/#17/#18 mini dalgalarda · #19 GUVENLIK-FIX-2 ·
#21 A2-FIX · #22 GUVENLIK-FIX-4 · **#16 BILINCLI bos birakildi** (verilmis karar). Gerekceler
ilgili muhurlerde; kesilen satirlar bayt-aynen `docs/muhur/49-arsiv-3.md`de.
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

**GUVENLIK-FIX BOLUMLEMESI (merkez karari — KAYIT):**
```
GF-1 KIMLIK/OTURUM [backend, migration olasi]: DV1 (BAS) · C-1 · C-2 · B-1 · B-2 · C-4
GF-2a ISTEMCI KACIS [frontend]: D-1 · D-2 · D-3 · D-4 · D-5 · D-10 · D-11 · D-6 · D-8
GF-3 SIZINTI/YAPILANDIRMA/LIMIT [backend config]: E-2 · E-3 · B-09 failed-jobs · E-1a ·
     E-5 · E-4 · E-6 · F-1 · F-2 · A-3
GF-2b CSP [frontend, D-7]: 11 satir ici script disa + unsafe-inline/unsafe-hashes/blob sokum
GF-4 TEDARIK ZINCIRI [CI/paket]: G-2 · G-5 · G-6 · G-4 · G-3 · G-1 = 12.0.1 KALIR
BILINEN/KABUL EDILMIS RISK: C-3 (00a:101) · D-9 · E-1b · Webhook:AllowedIps bos · hibrit jeton
BASKA KUYRUGA: A-2 -> VITRIN-KALAN 8 · F-3 -> IMPORT-FIX
```

**KAPANANLAR** (tam metin muhurde, kesilen satirlar 49'da): ARSIV-1 `c6721b7`/41 ·
AV-1 `c6721b7`/42 · ARSIV-2 `4c29f32`/43 · GF-1 `189ce81`/44 · GF-1b `00b012f`/45 ·
GF-2a `1dd985b`/46 · GF-3 `33cac2e`/47 · GF-2b FAZ 1 `0fd3e62`/48 ·
GF-4 TEDARIK ZINCIRI `4976974`/50 (cift yesil: run 33891017398 · 33891017496) ·
**GUVENLIK-AV-2 (SALT OLCUM) `ce54d0c` zemininde /51** ·
**GF-5 A09 IZ/ATIF + MISAFIR BUTUNLUGU + MASKE `027a88a`/52 — LAUNCH BLOKER 2/2 KAPANDI**
(SD-7 misafir butunlugu · SC-1 A09 iz/atif). K7 DUSTU (D1).
**S-C KAPSAMA MATRISI: `H=8` -> `H=3`, uCu de BILINEN** - 403 yetki reddi (katman engeli:
`Divisima.Core` ProjectReference 0) · webhook IP allowlist reddi (dal sevk edilen
yapilandirmada YAPISAL OLARAK ULASILAMAZ, `00b:229`) · satici login (Seller'a 0 satir).
**ONCEKI TABANIN BOLUNMESI YANLISTI:** `51·AV-2` iki yerde "10/5/7" diyor; tablodan yeniden
sayilinca `E=8 · H=8 · KISMEN=6` (toplam 22 dogru, bolunme yanlis).
**PROVENANS DUZELTMESI (AV-2'de olculdu):** AV-2'nin kapsami `42·GUVENLIK-AV-1`de
"at-rest sifreleme · 2FA/TOTP · TOCTOU/ExecuteUpdateAsync · A09 · olay isleyicileri ·
13 anilmayan controller (Comparison/Collection ham entity suphesi) **· Stock yuzeyi**"
diye yaziliydi; son parca CLAUDE.md'ye TASINMAMISTI (olculdu: muhur 1, CLAUDE.md 0).
Etkisi sifirdi (Stock zaten 13'un uyesi) ama tasima kaybi GERCEKTI - **geri konuldu**.
**D-7 KISMEN**: admin TAM, vitrin `'unsafe-inline'` KABUL EDILMIS RISK; **CSP FAZ B YOK** -> ERT-DEFTER.
**GUVENLIK-AV-3 DAR (SALT OLCUM) `533f935` zemininde /53 — NO-GO 3** (T1-B1 uye `request_id`
replay'i · T1-B2 adressiz siparis · T1-B4 COD parasiz "odenmis").
Olcut **(B)**: `51·AV-2` disjunktlarina **"davranis kaniti bulunan"** on sarti eklendi; T4-F1
(UNIQUE) ve T4-F2 (rowversion) **ADAY KUTUSUNDA** - migration ister, GF-6'da
kirmizi-once denenir, hit yoksa GF-7. **KOR-30 GENISLEDI, YER DEGISTIRMEDI: 19 derinlemesine ·
4 yalniz canli yetki · 5 yalniz kaynak eleme · 2 ilan edilmis kapsam disi.**

**KUYRUK (AV-3 sonrasi yeniden dizildi):**

1. **ARSIV-4** (docs, hedef `<=60 KB`) <- SIRADA. Tarif merkezden.
2. **GF-6 LAUNCH ONCESI:** **6a** uye yolu butunlugu (T1-B1 · T1-B2 · T1-B3 · T1-B4 - TEK KOK:
   misafir yolunun kazandigi kapilar uyeye tasinmamis) · **6b** durum makinesi (T4/S-1 iptal
   edilmis siparisi dirilten callback + T4-F5 iki elle kopya - TEK KOK: durum yazimi
   `IsValidTransition`'dan gecmiyor) · **6c** X-2 hub `RequireAuthorization` (tek satir + pin) ·
   **6d** T2-1 `product/import` transaction + satir siniri + tip kontrolu ·
   **6e** T4-F1/T4-F2 **kirmizi-once denemesi**.
3. **LAUNCH GO/NO-GO TURU**.
4. **GF-7 (LAUNCH SONRASI):** AV-3'un 6b/6c/6d kalani + olu/yaniltici yuzey grubu (`53`/bolum 9) ·
   SC-12 outbox payload
   sifreleme/ozetleme (SA-1 ile birlikte - `AesEncryptionProvider` bugun TEK ANAHTARLI ve
   cozemedigi degeri OLDUGU GIBI donduruyor, yani sifreleme once SA-2'yi ister) ·
   SA-1/SA-2 at-rest kurcalama + anahtar rotasyonu · SB-1 (2FA dalinda CAS geri alma) ·
   SD-1/SD-2/SD-4 anonim uc sozlesmesi · SC-3 SIEM okuyucusu.
5. Launch SONRASI digerleri: VITRIN-KALAN (10 kalem) · FIX-1B · ADMIN-FIX · IMPORT-FIX ·
   FIX-1C · LOG-FIX · FIX-2 · FIX-3/B13

Iki BILINEN kalem (`53·GUVENLIK-AV-3`):
- **REZERVASYON BIRIKMESI - DEV RIG'TE `BackgroundJobs:Enabled=false`.** 197 rezervasyonun
  **186'si suresi dolmus ama DURUYOR**; `available` KALICI dusuk ve sonraki her olcum turunun
  stok sayimi kirlenir. Bayrak BILINEN'dir, **birikmenin olcusu DEGILDIR** (sinir genislemesi).
  **PROD CHECKLIST: `BackgroundJobs:Enabled=true` -> IRL listesi.**
- **KOR EKSENLER OWASP'TA: A02 · A03 · A05 · A04.** A03 AV-2'de `BOSLUK (istemci)` diye adiyla
  kaydedilmisti; AV-3'te de kor, gerekcesi **`frontend/*` DOKUNULMAZ** - yasak yuzeyde
  birakilmis bosluk. A04 **IKINCI KEZ** hicbir goreve girmedi.

Bes BILINEN kalem (ayni-saniye jeton penceresi · miras oturumda step-up · 342 olu oturum ·
IP davranis kaniti yok · K4 gecikmeli aile iptali) TAM METINLE `docs/muhur/45-guvenlik-fix-1b.md`
icinde; kesilen satirlar bayt-aynen `docs/muhur/49-arsiv-3.md`de.

Iki BILINEN kalem (`51·GUVENLIK-AV-2`):
- **SignalR "admins" alarmi BOS GRUBA yayin yapiyor.** `SecurityEventManager.cs:39-40` ->
  `Clients.Group("admins")`; gruba katilim `NotificationHub.JoinAdminGroup()` ile olur ve
  CAGIRANI YOK (frontend'de `signalr|hubconnection|/hubs` **0** gecis, POZ kontrol backend 9).
  **Okuyucu LAUNCH SONRASI** - alarm kanalinin kendisi GF-5'te duzelmez.
- **SC-3 belge ayrismasi GF-5'te DOCS DUZELTMESIYLE kapanir.** `ops/serilog-siem.md`
  Elasticsearch/Seq + alerting anlatiyor; gercekte sink yalniz Console+File, `Siem:` anahtari
  0 gecis, belgenin sekiz olay tipinin **BESI kodda YOK**. Kod degil BELGE yanlis;
  duzeltme GF-5'in docs yarisidir.

**B-27 KAPANDI (AV-2, 4 Eylul 2026)** - `/api/payment/callback` artik `payment` kovasinda
(`PaymentController.cs:29` sinif duzeyi `[EnableRateLimiting("payment")]`); canli sinir
kosulu **10 gecer / 11. istek 429**, iki denetci AYRI AYRI olctu. **`00b:247` arsivi
DEGISMEZ (MK-11/d)** - kayit burada.

Iki BILINEN kalem (`50·GUVENLIK-FIX-4`):
- **Yerel SDK 9.0.305 / CI SDK 8.0.x, `global.json` YOK** (DUR-2'de dusuruldu). Ayrisma
  bugun gozlenmedi - `--locked-mode` CI'da (SDK 8) YESIL kosuldu - ama PINLENMEMISTIR.
- **Dependabot `docker` ekosistemi yalniz kok `Dockerfile`/`docker-compose.yml`i tarar.**
  Workflow `services.*.image` ve C# icindeki digest literallerini HICBIR ekosistem
  tazelemez; o iki deger ELLE guncellenir (bakim notu).

Dort BILINEN kalem (`lockout_end` YEREL · kismi iptal sonrasi replay 400 · logout bayat cerezle
200 · `expiration` artik `Z` bicimli) TAM METINLE `docs/muhur/47-guvenlik-fix-3.md` icinde;
kesilen satirlar bayt-aynen `docs/muhur/49-arsiv-3.md`de.

**`frame-src` SUPHELISI - GERCEK SANDBOX ODEMESI.** Kanit celiskili: vitrin meta'sinda hic
yokken 3DS uctan uca surulmus, ama `SecurityHeadersMiddleware:29` `frame-src
https://*.iyzipay.com` tasiyor.

**GOZ TURU:** 8 kalem olculdu (`48·GF-2b · GOZ TURU`, `49·ARSIV-3 · K2`); acik yalniz
`frame-src` (gercek sandbox odemesi).

Uc BILINEN kalem (Google Fonts SRI yasak · `admin.html` kendi `imgUrl()` kopyasi · panelde
`guvenliHTML`/`guvenliYaz` cagirani yok) TAM METINLE `docs/muhur/46-guvenlik-fix-2a.md`
icinde; kesilen satirlar bayt-aynen `docs/muhur/49-arsiv-3.md`de.

## Devir ID'leri

**DURUM:** DV1 KAPANDI `44·GF-1` · DV3 KAPANDI `47·GF-3` · DV2 D-YAN · DV4-6 kayit.

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
8. A-2 (AV-1'den) — `42·GUVENLIK-AV-1`
9. `placeholder=ceviri("...")` ON DORT yerde DIZGE ICINDE kalmis — `ceviri(` CAGRILMIYOR,
   duz metin basiliyor; 14 input'ta placeholder BOZUK. [MANTIK]/[UX], XSS DEGIL.
   Ureten ifade: `grep -c 'placeholder=ceviri(' frontend/api-bridge.js` -> 14.
   kaynak `46·GUVENLIK-FIX-2a · SUPHE-6`
10. Anonim katalog yanitinda **`Pragma: no-cache` + `Cache-Control: private, max-age=60`
    CELISKISI** suruyor. GF-3/K7 yalniz KIMLIK yarisini duzeltti (kimlikli uc artik
    `no-store` aliyor); anonim yolda iki baslik hala birbiriyle celisiyor.
    kaynak `47·GUVENLIK-FIX-3 · S3`
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
- `48·GUVENLIK-FIX-2b` **YENI KALEM (kullanici karari): CHECKOUT FORMU IZOLE IFRAME'E
  (`srcdoc` + kendi CSP'si) -> VITRIN `script-src` STRICT.** Bugun vitrin CSP'sinde
  `'unsafe-inline'` KABUL EDILMIS RISK olarak duruyor; tek gerekce `embedCheckoutForm`un
  saglayici satir ici script'ini calistirmasi. Odeme formu kendi CSP'li iframe'ine alinirsa
  vitrin `'unsafe-inline'`siz kalabilir. **Tasarim launch SONRASI.** (CSP FAZ B bu kalemin
  ustune kurulur; GF-2b'de YAPILMADI.)
- `50·GUVENLIK-FIX-4` **YENI KALEM (kullanici karari): TFM `net8.0` -> `net9.0`/`net10.0`.**
  GF-4'te TFM DOKUNULMAZDI (yukseltme LAUNCH SONRASI). **TETIKLEYICI: .NET 8 EOL,
  Kasim 2026.** Yukseltme `global.json` yoklugunu (yerel SDK 9 / CI SDK 8 ayrismasi) ve
  NuGet audit varsayilanini da (`all` dali TFM >= 10.0 sartina bagli) birlikte etkiler.

## AV-2 GIRDILERI (39·MF-3'ten acik kalanlar)

- **`guest_name` UZUNLUK DOGRULAMASI YOK** - misafir yolunda sinir yok, `full_name` kolonu
  150 karakter; uzun ad EF insert'te 500 uretir. **FIX adayi.**
- **`ExecuteDeleteAsync` <-> transaction ROLLBACK** OLCULMEDI (K2 onu transaction ICINDE cagiriyor).
- **Hata kodu birlestirme** - TR serbest metin capalarinin kirilganligi (K3 + K3b ayni capa).
- **Ortak RuleBuilder** - K7 mesaj/NotEmpty ayrismasi (dort validator, regex ayni, metin farkli).
- **K4 TELAFISININ ATOMIKLESTIRILMESI** - GuestCheckoutManager telafisi IKI AYRI
  DeleteWhereAsync (adres, sonra musteri, :503-504) ve TRANSACTION YOK; ilki gecip
  ikincisi duserse KISMI DURUM olusur. Uretim kodu bu kalemi ADIYLA deftere havale
  ediyor (GuestCheckoutManager.cs:313). BILINCLI SINIRLAR 2-3 (istisna yolunda telafi
  kosmaz · outbox satiri silinmez) kod :313 yorumunda, defterde ilk kez burada.
