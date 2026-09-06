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
**S2 - TEST OZETI.** Ureten ifade: `grep -oE "Toplam:[ ]*[0-9]+" <log> | tail -1`
```
POZ  scratchpad/cib9c/t1full.log -> "Toplam:   578"
     ham satir: "Başarısız! - Başarısız:     3, Başarılı:   575, Atlanan:     0,
                 Toplam:   578, Süre: 51 s - Divisima.IntegrationTests.dll (net8.0)"
NEG  ayni dosyada "ZZZToplam:" -> 0
```
Capa `Toplam:` HAM CIKTIDAN KOPYALANDI (MK-7).

**S3 - RUN DURUMU.** Ureten ifade:
`curl -s ".../actions/runs?head_sha=<SHA>&per_page=20"` -> `"total_count"` + `"status": "completed"` + `"conclusion": "success"` sayimi
```
POZ  4d8d4c2 (onceki muhur)  -> total 2 · completed 2 · success 2
POZ  b9c9ff0 (bu push)       -> total 2 · completed 2 · success 2
NEG  f0f27dc (ARA COMMIT)    -> total 0   (tek basina push EDILMEDI)
NEG  0000...0 (uydurma SHA)  -> total 0
```
**S4 - RUN KIMLIGI.** Ureten ifade:
`grep -oE '"html_url": "[^"]*/actions/runs/[0-9]+"' <dosya> | grep -oE '[0-9]+"$' | tr -d '"' | sort -u`
```
POZ  ci0655/runs0655.json   -> 33165306227 · 33165306239        (iki DOGRU kimlik)
POZ  cib9c/rson.json        -> 33213028751 · 33213028838        (MANTIK-FIX-1 push'u)
NEG  cib9c/bos.json         -> []
NEG  cib9c/sizma.json       -> []                                (DEPO ID'si SIZMIYOR)
```
**Kayitlarda anilan girdi dosyalari SILINMEDI.**

**S5 - CR (SATIR SONU) DEDEKTORU.** Ureten ifade: `tr -cd '\r' < <dosya> | wc -c`

```
POZ  /tmp/poz_crlf.txt  (printf 'a\r\nb\r\n')  -> 2   od kanit:  a \r \n b \r \n
NEG  /tmp/neg_lf.txt    (printf 'a\nb\n')      -> 0   od kanit:  a \n b \n
POZ  CLAUDE.md (d8f12dd)                       -> 0   (saf LF)
POZ  git show HEAD:CLAUDE.md                   -> 0   (blob da LF)
```

**S6 - ALT-DIZGE SAYACI.** Ureten ifade: `grep -oi "<capa>" <dosya> | wc -l`

```
POZ  /tmp/poz_pii.txt ("PII satiri" + "pii kucuk")  -> 2
NEG  ayni dosyada "zzzpii"                          -> 0
POZ  tum-defterler.txt "PII"                        -> 17  (capraz: grep -c -> 17)
NEG  tum-defterler.txt "zzzssrf"                    -> 0
```

**ALTI EMEKLI DEDEKTOR (neden emekli + olculen kanit) -> `54·ARSIV-4` bolum 2.1.**
Ozetleri B6'daki OLU DEDEKTOR AILESI satirinda yasar; burada yalniz CALISAN ifadeler durur.
---

# B6 — DERSLER: AILE SAYACLARI · SALINIM · TUZAKLAR · RIG KOR NOKTALARI

**Usul:** her ders TEK SATIR + muhur atfi; ayni aile TEK satirda toplanir. Tam metinler
ilgili muhurlerde; kesilen bloklar bayt-aynen `54·ARSIV-4` bolum 2.3.

## AILE SAYACLARI (olculdu, tahmin degil — SAYAC KORUNUR)

- **KACIS-KAYBI AILESI — ALTINCI ornekte KALIR** (`git log -S` ile olculdu: "DORDUNCU"
  1 commit · "ALTINCI" 1 commit `a5add91` · "BESINCI"/"YEDINCI" 0). Kok: **akis duzenleyici
  metnin BAYTLARINI korumaz** (MK-8). -> 37 · 38 · 40
- **ASCII/TURKCE YUKLEM AILESI — 3 vaka** (`0655178` i18n envanteri · `4d8d4c2` `<> 'Silinmis'`
  yuklemi dort dogru satiri hatali sayip bulguyu **5 KAT abartti** · ASCII ozet filtresi).
  Capa EZBERDEN YAZILMAZ, HAM CIKTIDAN kopyalanir (MK-7). -> 37
- **CAPA / ESLESME BICIMI — 5 vaka:** capa POZ olcumu "kac" yaninda "NEREDE" sorar (43) ·
  indeks/kisit sayimi DOSYA-GENELI grep ile, blok penceresi YOKLUK KANITI DEGILDIR (44) ·
  satir sonunda biten atama gorunmez (46) · assert KUSUR SINIFINI pinler, ESKI LITERAL
  BICIMINI degil (48) · **NEG capa dizesi BELGEYE YAZILMAZ**, raporda/muhurde anilir (43).
- **"AYNI KURALIN IKINCI KOPYASI" — 6 vaka** (depoda 7 kez bedeli odendi); en yenisi
  `OrderStatusMachine`in IKI ELLE KOPYASI. -> 53/T4-F5
- **OLU DEDEKTOR AILESI (bu kabukta, KALICI):** `grep -oiF` hicbir sey dondurmez ·
  `grep -P` calismaz · **`grep -E` icinde `\t` TAB ESLESMEZ** · CR icin YALNIZ
  `tr -cd '\r' | wc -c` · `grep -o $'…'` OLU (dogrusu
  `LC_ALL=C grep -o "$(printf '\xe2\x80\xa6')"`) · `grep -rn -- <desen> --include=*.cs <dizin>`
  calismaz (`--` secenek ayrimini bitirir) · **`grep -c` sifir eslesmede EXIT 1 doner ve
  `&&` zincirini KIRAR** (`;` ile ayir). Emekli girdilerin gerekceleri `54·ARSIV-4` 2.1.
- **TEK KANAL = SUPHE**, tarife KALEM OLMAZ (47) · **YORUM != OLCUM — UC TUR UST USTE**
  (51 iki yorum birden yanlis · 52'de 17 bayat atif · 53'te iki KAPALI BILINEN acik sanildi).
  **Bir yorum satir numarasi veriyorsa o numara BAYAT OLABILIR** - sembol aranir.

## SALINIM · TUZAKLAR · RIG KOR NOKTALARI

- **ANNOTATION SALINIMI HIPOTEZ DEGIL OLCULMUS OLGUDUR:** sapma YALNIZ bilinen alti satir
  kumesiyse (`EfEntityRepositoryBase.cs` 45/50/60/61/88/96) **ve o dosya diff'te yoksa** TEK
  SATIR "bilinen salinim" notu yeter; kumenin DISINA tasan her sapma `dosya:satir` incelemesi
  + diff kesisimi ister, `failure` seviyesi -> **DUR**. -> 39
- **YONLENDIRME SIRASI:** `> dosya 2>&1`; ters sira (`2>&1 > dosya`) `[FAIL]` satirlarini
  log'a DUSURMEZ. Build ozetinde `tail -1` ALDATIR. -> 40
- **BIR DOSYA KENDI ICERIGINDEN TURETILEREK USTUNE YAZILMAZ:** cikti GECICI dosyaya yazilir,
  satir/boyut DOGRULANIR, ancak sonra tasinir; yedegin VAR OLDUGU da dogrulanir. Takip
  edilmeyen dosyada bu hata GERI ALINAMAZ. -> 47
- **RIG KOR NOKTALARI:** CSS gecisi ilerlemiyor -> **gecise bagli hicbir geometri olcumu
  DOGRUDAN ALINMAZ** · JS/DOM kosucusu YOK, tarayici semantigi CI'da pinlenemiyor ·
  harness fetch katmani SW kaydini engeller · `register()` OK, SW KAYDI DEMEK DEGILDIR
  (kanit `getRegistrations` + `active` + controller). -> 40 · 48 · 49
- **goz1:** saglik ucu `/health` (200), **`/api/health` YOKTUR** (404); surec adi
  `Divisima.API` DEGIL **`dotnet`**; **`curl -I` KULLANILMAZ**; PowerShell
  `Invoke-WebRequest` 404'te ISTISNA atar ve "rig kalkmadi" yanilgisi uretir. -> 51
- **ORTAM:** `sqlcmd` QUOTED_IDENTIFIER kapali baslar -> **`-I` ZORUNLU** · `api/gift-card`
  TIRELI · `schtasks` PowerShell uzerinden cagrilir · build ONCESI API sureci DURDURULUR
  (MSB3027/MSB3021 DLL kilidi), sonra bes arguman TEYIT EDILIR. -> 37
- **`Directory.Build.props` XML'i BOZUKKEN `dotnet restore` exit 0 verir ve MSB4024 BASMAZ**;
  tek durust sinyal `msbuild -getProperty` probudur (UC KEZ dusuldu). -> 50 · 52
- **MCR digest'i Accept turune gore DEGISIR** (manifest listesi olmayan imajda Schema 1 doner
  ve o digest CEKILEMEZ); digest her zaman GET ile dogrulanir. -> 50
- **RUNTIME SOZLUK = DB METNI:** kaynakta eksik bir anahtar, calisma aninda enjekte edilen
  anahtarla MASKELENIR; sozluk butunlugu RUNTIME'dan degil **KAYNAKTAN** pinlenir. -> 36 · 46
- **ROTA TAHMIN EDILMEZ:** `stock-notification` sanildi, dogrusu `api/StockNotification`
  (`price-drop`un gercekten tireli olmasi yaniltti). -> 39
- **`admin.html` TUZAGI:** duzenleme formu stok satirlarini ANONIM detay ucundan dolduruyor
  ve geri POST ediyor; tek basina sevk edilen bir alan degisikligi `available`i EKSIYE
  dusururdu. **Bu vakadan dogan KALICI KURAL: KAPSAM ELESTIRMENI ROLU, ON OLCUM FAN-OUT'UNUN
  ZORUNLU UYESIDIR** - gorevi bulgu aramak degil, **TARIFIN KENDISININ ACACAGI KAPIYI**
  aramaktir (bes bagimsiz okuyucu + ana akis kacirdi, tek elestirmen yakaladi). -> 34
- **ISIMSIZ FLAKE (durust kayit):** bir `Category=Sql` kosumunda **338/339** gorundu, ADI
  YAKALANMADI; ayni anda alinan tam suit 575/578 - **TUTARSIZ**. Paylasilan test-DB
  aciklamasiyla TUTARLI bir gozlem ama **ISPAT DEGIL**. -> 37
- **SUZGEC DERSI:** basliksiz kaynak etiketi sayimi AYRAC sayar (ilk kosum 34 dedi); dogru
  ifade iki-ust-satiri kontrol eder:
  `awk '/^kaynak: /{ if (p2 !~ /^#/) n++ } { p2=p1; p1=$0 } END{print n+0}'`. -> 41
- **FORM <-> DTO ALAN ESLEMESI BAGLAMADAN ONCE OLCULUR:** formda FAZLA olan alan bir YALAN
  uretir (kullanici degistirir, sunucu gormez); EKSIK olan alan PUT-ez semantiginde
  SESSIZ KAYIP verir. -> 39
- **DENETIM MALIYETI RAPORLANIR** (ajan sayisi, tur sayisi, plan sapmasi, ara kapi bulgusu);
  uc denetcinin ikisi gercek kusur buldugu turlar kalibrasyon kaydidir. -> 39 · 53

# B7 — KURGU SABITLERI ve D-YAN

## Olcum duzenegi (goz1) — bes arguman

kaynak: 40·MANTIK-FIX-4_MUHRU · ORTAM UYARISI

**ORTAM UYARISI (Divisima Eki 2.3):** olcumler `goz1` duzeneginde kosuldu; API surecinin
komut satirinda BES ARGUMAN var ve **BUNLAR URUN VARSAYILANI DEGILDIR** -
`--Iyzico:UseRealSdk=false` · `--AdminSeed:Enabled=false` · `--BackgroundJobs:Enabled=false`
· `--RateLimit:AuthPermitLimit=100` · `--MailSettings:Host=`.


## KURGU KAYIT ENVANTERI — OZ (dalga bazli tam metin: her dalganin kendi muhrunde)

kaynak: 40·MANTIK-FIX-4 · KURGU KAYIT ENVANTERI + `42·GUVENLIK-AV-1 · KURGU`
(bayt-ayni kopya `54·ARSIV-4` 2.2)

**Dalga bazli "kurgu kaydi uretti/uretmedi" satirlari, MAX serileri ve eski suit tabanlari
`54·ARSIV-4` bolum 2.2'de BAYT-AYNI.** Burada yalniz GUNCEL taban durur.

**KURGU MAX (`53·AV-3` kapanisi):** musteri **202** · urun **955** · siparis **295** ·
adres **131** · fatura **128** · `user_sessions` **420** · `security_events` **83**.
Uretim imzasi zarfi (GF-1/K6 v2): `DATALENGTH(password_hash)` **69** / `password_salt` **16**.

**MK-3 UCLUSU — URETEN IFADELERIYLE (uc olcumde de BIREBIR):**
```
SELECT COUNT(*),MIN(id),MAX(id),SUM(CAST(id AS bigint))
  FROM orders WHERE status = 0 AND id <= 210;                             -> 35 / 9 / 210 / 3837
SELECT COUNT(*), MAX(id) FROM orders WHERE customer_id = 10;              -> 38 / 211
SELECT COUNT(*), SUM(total_price), STRING_AGG(CAST(status AS varchar),',')
  FROM orders WHERE customer_id = 74 AND id BETWEEN 234 AND 237;          -> 4 / 4698,60 / 0,0,1,1
```

**TEK SEMA DEGISIKLIGI (kalici kayit):** `user_sessions.auth_time` kolonu (`44·GUVENLIK-FIX-1`);
GF-1b..AV-3 arasindaki hicbir dalga sema DEGISTIRMEDI.

**SUIT TABANI (`53·AV-3`):** `Category=Sql` **382/382** · tam **777/780** (uc kirmizi =
bilinen Docker uclusu, yerelde Docker YOK). Ureten ifade:
`dotnet test Divisima-Backend.sln -c Release --filter "Category=Sql"` ve filtresiz.

**Omer'in hesabi (musteri 10, `e2b.sandbox@example.com`) ve verileri OLCUMDE KULLANILMAZ.**
**CAPA TUZAGI (kalici):** `email LIKE 'gfN%' -> 0` kanit bicimi TEK BASINA KIRLIDIR - eski
dalgalarin kurgusu ayni oneki tasiyabilir; durust ifade tarih niteleyicisi ister
(`... AND created_at >= CAST(GETDATE() AS date)`).

## D-YAN (tek isaretci)

**D-YAN bloklari KUMULATIFTIR.** Tam liste `39·MANTIK-FIX-3`; kesilen bloklar bayt-aynen
`54·ARSIV-4` bolum 2.2 (AV-1 kurgusu -> `42·GUVENLIK-AV-1 · KURGU ENVANTERI`). Onceki
bloklar arsiv dosyalarinda: 26 · 27 · 30-38 (INDEX ile cozulur).
**CANLI KVKK IHLALI: adres 55 / musteri 93** (silinmis hesap, TAM PII) - KR6 geregi
DOKUNULMADI; duzeltme YENI silmelerde gecerli.
**AV-3 devri (`53·AV-3` bolum 13):** DY-A musteri 192 desen disi · DY-B musteri 201/202
denetci yazimi (envanter artik alti defterden turetilemiyor) · DY-C 32 isimsiz
`security_events` · DY-D `outbox_messages` 41 + `audit_logs` 333 atfedilemedi ·
DY-E alti hesapta `failed_login_attempts=4` (kilit esigi 5, kilitlenen YOK).

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
- `37·MF-1·MF-3 SARTLARI` **(a)-(c):** 409 semantigi YENIDEN ACILMAZ · **IKINCI kupon dogrulama noktasi ACILMAZ** ("ayni kuralin ikinci kopyasi" - 7 kez bedeli odendi) · musteri+adres yazimi `PlaceOrder` BASARISINA bagli. Tam blok -> 37, bayt-aynen `54·ARSIV-4` 2.6.
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
- `50·GF-4·K1` `50·GF-4·K4` `50·GF-4·K5` `50·GF-4·K7` **TEDARIK ZINCIRI (dordu tek satirda):** action'lar 40-hane COMMIT SHA'sina pinli · paket kaynagi TEK (`NuGet.config` + `<clear />`, her projede `packages.lock.json`, CI `--locked-mode`) · imaj referansi TEK KAYNAK (dort site ayni tag+digest; digest **Schema 2 POZ/NEG cozucuyle**, etiketten okunan deger TEK BASINA gecersiz) · **AutoMapper 12.0.1 KALIR** (lisans degisimi 15.0.0), `NuGetAuditMode=all` UYARI, deprecated adimindaki `\|\| true` BILINCLI. Tam metin (K1/K4/K5/K7) -> `50·GUVENLIK-FIX-4`, kesilen satirlar `54·ARSIV-4` 2.4.
- `52·GF-5` **A09 IZ/ATIF + MISAFIR BUTUNLUGU + MASKE:** olay yuzeyi = kayitsiz/kilitli giris · logout (iki dal) · sahiplik ihlali `IdorAttempt` **kapsam DUZELTILDI (`53·AV-3`): cagri yeri IKI - `IyzicoPaymentManager`(`order`) + `OrderManager`(`address`); "Order+Payment" YANLISTI** · 429 ornekleme ip+uc/60 sn (`customer_id` NULL kabul edilmis sinir) · bozuk imza. **IMZASIZ webhook 404 STATUKO = KABUL EDILMIS RISK** (K7 DUSTU - saglayici imza GONDERMIYOR). Girdi sinirlari TEK KAYNAK `GirdiSinirlari` — **ortak RuleBuilder ACILMAZ** (Seller'a kapsam tasmasin, o kendi literalini korur), **sema'ya capalanir sabite DEGIL**; `request_id` <=80 + `[A-Za-z0-9._-]` **GUID SARTI ASLA** · `guest_name` <=100 (olcum SANITIZE SONRASI) · e-posta <=200. Log maskesi GLOBAL: iki Serilog sink'i de `MaskeliFormatter` (`ITextFormatter`), enricher yolu KAPALI, `KanitMaskesi` olcutu GENISLETILMEZ. Tam metin -> `52·GUVENLIK-FIX-5`, kesilen satirlar `54·ARSIV-4` 2.4.
- `55·GF-6` **REPLAY GUARD'I TEK SERVIS:** `request_id` replay kurali (kupon KANONIK + coklu-kume sepet + sizintisiz 400) `SiparisReplayGuardi`de TEK yerde; misafir ve uye yollari AYNI servisi FARKLI **sahiplik ekseniyle** cagirir (misafir=E-POSTA ordinal, uye=`customer_id`). Kopya ACILMAZ. -> 55
- `55·GF-6` **DURUM YAZIMI TEK KAPIDAN + TERMINAL KORUMASI:** `OrderManager`da her `order.status` yazimi `DurumYaz` -> `OrderStatusMachine`den gecer (dogrudan atama 4 -> 0). Iyzico'nun IKI dali terminal siparisi DIRILTMEZ: `payments` Success KAYDEDILIR, `PaymentAfterTerminal`/Critical yazilir, yanit **200 + ayri mesaj** (`status=review`) - `success` DEGIL. Iade **ELLE** (BILINEN). `ShipmentManager` :65/:118 GF-6 ONCESINDEN makine korumali; `DurumYaz` `OrderManager`a OZELDIR. -> 55
- `55·GF-6` **COD PARA ANLAMI `Delivered`DA (DAR):** `PaidOrderSpec.IsPaid(byte status, byte paymentType)` - COD yalniz `Delivered`. Core Entity'yi GOREMEZ, EF yuzu `Divisima.Entity.Specifications.OdenmisSiparisSpec`; **TAM MATRIS pini** ikisini baglar. GECEN SITELER YALNIZ **referans odulu + sadakat kazanimi**. **KUPON LIMITLERI ESKI KURALDA** - olculdu: gecirilince `usage_limit=1` kuponu SEKIZ es zamanli COD siparisinin HEPSI aldi (COD `Pending` DOGMAZ, sayilacak durum KALMAZ). Kupon limiti "para alindi mi" degil **"hak hala CANLI mi"** sorusudur. Sadakat: `PaymentConfirmedSideEffects` BOLUNMEZ; COD'da `Confirmed` dalinda ATLANIR, `Delivered`da ayni olay YENIDEN yazilir (dordu de idempotent). -> 55
- `55·GF-6/F5-F6` **KUPON LIMITI "HAK CANLI MI" SORUSUDUR** - `PaidOrderSpec`ten BAGIMSIZ kalir (olculdu: gecirilince COD yolunda limit UYGULANAMAZ hale gelir). **KARGO TESLIMAT DALI TRANSACTION ICINDE** (`ExecuteInTransactionAsync`): dort yazma - durum · zaman cizelgesi · bildirim · `PaymentConfirmed` olayi - ATOMIKTIR; olay kaybi TELAFISIZDIR (admin ayni durumu tekrar yazamaz). Iptalli odeme metni BASLATILDIGI iddiasini TASIMAZ; `PaymentAfterTerminal`/Critical satirlari **PROD CHECKLIST: GUNLUK ELLE KONTROL** (otomatik okuyucu YOK). -> 55
- `51·AV-2` **LAUNCH BLOKER OLCUTU:** `KRITIK` **∨** `YUKSEK`+`KIMLIKSIZ-UZAK` **∨** `[PARA]`/`[VERI-BOZAN]`. Digerleri launch SONRASI. Siddet ON KOSULDAN bagimsiz verilemez; `ADMIN` on kosullu kalem KRITIK OLAMAZ. -> 51
- `51·AV-2` **AV KAPSAMI KUMULATIF MATRISLE OLCULUR; YER DEGISTIRME YASAK.** Her AV turu kapsam matrisini (uc/controller x tur) muhre kumulatif yazar ve sonraki tur onceki turun KOR KUMESINDEN baslar. Gerekce olculdu: AV-1'in kor 13'u ile AV-2'nin kor 17'sinin kesisimi **0**; 40 controller'in **30'u** en az bir turda kor kaldi. -> 51

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
# B9 — KUYRUK · DEVIR · VITRIN-KALAN · ERTELENMIS-DEFTER

**Usul:** SIRADAKI IS + BAGLAYICI KAPANIS KAYITLARI; tam metinler ilgili muhurlerde, bu
turda kesilenler bayt-aynen `54·ARSIV-4` 2.5. **ONCEKI KESIMLER:** `49·ARSIV-3` (ARSIV-3'te
kesilenler) ve `41·ARSIV-1`; ikisi de BAYT-SABIT (MK-11/d), bu tur DOKUNMADI.

## KAPANANLAR (son uc dalga; oncekiler `docs/muhur/INDEX.md` ile cozulur)

- **LF-1 DAGITIM ARTEFAKTLARI + LAUNCH GO /56** — uc dagitim blokeri (BL-1/BL-2/BL-3) kapandi;
  **17 mutasyon**, uc ardisik tam dogrulama birebir (Sql 415/415 · tam 826/829, uc kirmizi =
  bilinen Docker uclusu). **GITLEAKS YERELDE OLCEMEDIM** (kurulu degil + Docker yok) — olcut
  bir PINE gomuldu, kesin kanit `secret-scan` ADIM SONUCU. Bir pin kendi mutasyonuyla kusurlu
  cikti (**ciplak alt-dizge capasi UST DIZGEYE kordu** — "ankrajli mukerrer" ailesi) ve
  sikilastirildi; **bayat ikili UCUNCU kez** yasandi (mutasyon geri alindiktan sonra build
  yapilmadi), aritmetikle yakalandi.
- **GF-4 TEDARIK ZINCIRI `4976974`/50** — cift yesil: run 33891017398 · 33891017496.
- **GF-5 A09 IZ/ATIF + MISAFIR BUTUNLUGU + MASKE `027a88a`/52 — LAUNCH BLOKER 2/2 KAPANDI**
  (SD-7 misafir butunlugu · SC-1 A09 iz/atif). K7 DUSTU. **S-C MATRISI `H=8` -> `H=3`**; ucu
  de BILINEN: 403 katman engeli · webhook IP allowlist (`00b:229`, sevk edilen
  yapilandirmada YAPISAL OLARAK ULASILAMAZ) · satici login (Seller'a 0 satir).
  `51·AV-2`'nin "10/5/7" bolunmesi YANLIS; yeniden sayilinca `E=8 · H=8 · KISMEN=6`.
- **GUVENLIK-AV-3 DAR (SALT OLCUM) `533f935` zemininde /53 — NO-GO 3** (T1-B1 uye
  `request_id` replay'i · T1-B2 adressiz siparis · T1-B4 COD parasiz "odenmis").
  Olcut **(B)**: `51·AV-2` disjunktlarina **"davranis kaniti bulunan"** on sarti eklendi;
  T4-F1 (UNIQUE) ve T4-F2 (rowversion) **ADAY KUTUSUNDA** - migration ister, GF-6'da
  kirmizi-once denenir, hit yoksa GF-7. **KOR-30 GENISLEDI, YER DEGISTIRMEDI:**
  19 derinlemesine · 4 yalniz canli yetki · 5 yalniz kaynak eleme · 2 ilan edilmis kapsam disi.
- **ARSIV-4 (docs) `1d67cf6` zemininde /54** — CLAUDE.md kesimi; karar envanteri ONCE/SONRA
  `comm` iki yon BOS.
- **GF-6 LAUNCH ONCESI `3095568` zemininde /55 — KAPANDI. NO-GO 3'un UCU DE KAPANDI:**
  T1-B1 (uye replay, K1) · T1-B2 (adressiz siparis, K2) · **T1-B4 (COD parasiz "odenmis",
  F1 - DAR kapsam)**. Ayrica **T4-F1 (cift para iadesi) KAPANDI** - migration YOK, kalem
  basina dagitik kilit + TAZE okuma; K8 probu ONCE 48 turda **41 ve 35** hit, SONRA **0**.
  **T4-F2 GF-7'ye GEREKCELI ISTISNAYLA devredildi** (48/48 hit, `[VERI-BOZAN]`, seri
  kontrolde kayip 0): para alanlari ZATEN atomik (H27 CAS), kayip yalniz PROFIL alanlarinda
  ve ayni hesabin kendi es zamanli guncellemesinde. **TETIKLEYICI: GF-7 ILK KALEM.**
  Suit 806/809 x3 (uc kirmizi = bilinen Docker uclusu) · Sql 411/411 · 24 mutasyon kosumu.

## KUYRUK (`56·LAUNCH GO` sonrasi)

**LAUNCH GO VERILDI (`56·LAUNCH-GO-NO-GO`) — DAGITIM OMER'DE.** Uc dagitim blokeri LF-1'de
kapandi: BL-1 `Cookies:Domain` uretimde fail-fast (bos birakilirsa `/api/auth/refresh` KALICI
403 — SESSIZ ariza, belirti 15 dk sonra ve TUM kullanicilarda ayni anda) · BL-2 uretim
sablonu + `docker-compose.prod.yml` · BL-3 olay tipi 12 -> 14 + alarm tablosu. Gerekceli
istisna T4-F2 (`55·GF-6` 5.1) DEGISMEDI. **Dagitim sarti:** `ops/deployment-checklist.md`in
20 sirali IRL adimi (9. `Cookies:Domain` ve 18. `BackgroundJobs:Enabled=true` ATLANAMAZ) +
**GUNLUK `PaymentAfterTerminal`/`Critical` SQL sorgusu** (o olayin SIEM okuyucusu YOK,
SignalR `"admins"` grubu BOS — tek okuyucu elle sorgu).

1. **GF-7 (LAUNCH SONRASI) — ILK KALEM T4-F2** (rowversion migration; gerekce ve tetikleyici
   `55·GF-6` bolum 5.1). Sonra: AV-3'un 6b/6c/6d kalani + olu/yaniltici yuzey grubu
   (`53` bolum 9) · SC-12 outbox payload sifreleme/ozetleme (SA-1 ile birlikte -
   `AesEncryptionProvider` bugun TEK ANAHTARLI ve cozemedigi degeri OLDUGU GIBI donduruyor,
   yani sifreleme once SA-2'yi ister) · SA-1/SA-2 at-rest kurcalama + anahtar rotasyonu ·
   SB-1 (2FA dalinda CAS geri alma) · SD-1/SD-2/SD-4 anonim uc sozlesmesi · SC-3 SIEM okuyucusu.
   **LF-1 SONRASI EKLENENLER (`56` bolum 4-5, K-numaralariyla):** captcha SIL ya da BAGLA
   (K1 — bugun `ValidateAsync` uretimde 0 cagri, secret zorunlulugu LF-1'de kalkti) · Key
   Vault okuyucusunu BAGLA ya da iskeleti SIL (K2 — `ISecretProvider` tuketicisi 0, rotasyon
   is akisi LF-1'de elle tetiklemeye indirildi) · kargo kaydi · K7 nginx ortak baslik include
   · K8 Hangfire dashboard · K10 OTLP/alert checklist'e (olculdu: gecis 0) · raporlama
   siteleri · frontend `odeme/sonuc` i18n.
2. Launch SONRASI digerleri: VITRIN-KALAN (10 kalem) · FIX-1B · ADMIN-FIX · IMPORT-FIX ·
   FIX-1C · LOG-FIX · FIX-2 · FIX-3/B13

## BILINEN / KABUL EDILMIS RISK (tek isaretci; DURUM sutunlu tam liste ilgili muhurde)

**SDP 1.12.8 (v1.5): BILINEN listesi DURUM sutunuyla (ACIK/KAPALI/BAGLAYICI) kurulur;
KAPALI kalem yeni bulguyu BASTIRMAZ.** Tam metinler:
- **`45·GUVENLIK-FIX-1b`** — bes kalem (ayni-saniye jeton penceresi · miras oturumda step-up ·
  342 olu oturum · IP davranis kaniti yok · K4 gecikmeli aile iptali).
- **`46·GUVENLIK-FIX-2a`** — uc kalem (Google Fonts SRI YASAK · `admin.html` kendi `imgUrl()`
  kopyasi · panelde `guvenliHTML`/`guvenliYaz` cagirani yok).
- **`47·GUVENLIK-FIX-3`** — dort kalem (`lockout_end` YEREL · kismi iptal sonrasi replay 400 ·
  logout bayat cerezle 200 · `expiration` `Z` bicimli).
- **`50·GUVENLIK-FIX-4`** — iki kalem (yerel SDK 9 / CI SDK 8, `global.json` YOK - PINLENMEMIS ·
  Dependabot `docker` ekosistemi workflow `services.*.image` ve C# digest literallerini
  TARAMAZ, elle guncellenir).
- **`51·GUVENLIK-AV-2`** — iki kalem: **SignalR "admins" alarmi BOS GRUBA yayin yapiyor**
  (`NotificationHub.JoinAdminGroup()` cagirani YOK; okuyucu LAUNCH SONRASI) · SC-3 belge
  ayrismasi (GF-5'te docs duzeltmesiyle kapandi).
- **`53·GUVENLIK-AV-3`** — iki kalem: **rezervasyon birikmesi** (dev rig'te
  `BackgroundJobs:Enabled=false`; 197 rezervasyonun 186'si suresi dolmus ama duruyor,
  `available` KALICI dusuk. **PROD CHECKLIST: `BackgroundJobs:Enabled=true` -> IRL listesi**) ·
  **kor eksenler A02 · A03 · A05 · A04** (A03'un gerekcesi `frontend/*` DOKUNULMAZ - yasak
  yuzeyde birakilmis bosluk; A04 IKINCI KEZ hicbir goreve girmedi).
- **`55·GUVENLIK-FIX-6`** — DURUM sutunlu tam liste muhurde (yedi kalem); uc tanesi **ACIK**:
  **raporlama siteleri ESKI kuralda** (Dashboard · Merchandising · Recommendation · Seller —
  COD siparisi ciro/siralama/oneride hala `Confirmed`da sayilir, GF-7) · **terminal siparise
  gelen odemenin IADESI ELLE** (otomatik iade `RefundManager`dan gecer, kapsam disiydi;
  musteri `status=review` ekrani gorur; metin BASLATILDIGI iddiasini TASIMAZ - F6) ·
  **`health` uclari BILINCLI anonim**
  (`AllowAnonymous` ISARETLI — orkestratör probe'lari kimlik tasimaz, **BAGLAYICI**).

**B-27 KAPANDI (AV-2):** `/api/payment/callback` artik `payment` kovasinda; canli sinir
10 gecer / 11. istek 429, iki denetci AYRI AYRI olctu. **`00b:247` arsivi DEGISMEZ (MK-11/d).**
**`frame-src` SUPHELISI ACIK** - gercek sandbox odemesi gerekiyor (`48·GF-2b · GOZ TURU` +
`49·ARSIV-3 · K2`: sekiz kaleminden kapanmayan tek kalem).
**D-7 KISMEN:** admin TAM, vitrin `'unsafe-inline'` KABUL EDILMIS RISK; **CSP FAZ B YOK.**

## DEVIR ID'LERI (tek satir; tam metin `40·MANTIK-FIX-4`)

**DURUM:** DV1 KAPANDI `44·GF-1` · DV3 KAPANDI `47·GF-3` · DV2 D-YAN'da · DV4-6 KAYIT.
DV1 `request_id` replay'i K4 telafisinden kaciyordu `[VERI-BOZAN]` · DV2 yetim musteri
153/155 + siparis 270-275 (bozuk adresli) · DV3 429 UC AYRI KAYNAKTAN + 500 yolunda
`message` alani YOK · DV4 suzgec sayaci 9->8 ("8->2" kaydi BAYAT) · DV5 "ayni kuralin
ikinci kopyasi" 6. vakasi · DV6 `index.html` BILINCLI-'ltr' arkeolojisi (RTL CSS'i ILK
COMMIT'ten var, ACILMAMIS).

## VITRIN-KALAN (10 kalem; tam metin `40·MANTIK-FIX-4` · `54·ARSIV-4` 2.5)

1 i18n tazeleme uclusu (sekme basligi · a11y paneli · komut paleti) · 2 K6 kozmetik 3 ·
3 K7 mesaj/NotEmpty ayrismasi (dort validator, regex AYNI metin FARKLI) · 4 BULGU-3 kalan
bes satir · 5 `POPULAR_L` AR'da Turkce etiketler · 6 `showLegal` CMS (icerik isi, i18n degil) ·
7 A-1 arama collation/`LOWER()` -> `42·GUVENLIK-AV-1 · A-1` · 8 A-2 -> `42·GUVENLIK-AV-1` ·
9 `placeholder=ceviri("...")` ON DORT yerde dizge icinde, `ceviri(` CAGRILMIYOR ->
`46·GUVENLIK-FIX-2a · SUPHE-6` · 10 anonim katalog yanitinda `Pragma: no-cache` +
`Cache-Control: private, max-age=60` CELISKISI -> `47·GUVENLIK-FIX-3 · S3`.

## ERTELENMIS-DEFTER (acilmaz; baslik + atif)

`00a:111` Pending siparislere TTL · `00a:136` istemci tarafi onbellek · `00a:140`
`index.html` satir ici 704 KB · `00a:145` Turkce klavyede yazilan e-posta · `00a:150`
sabit-zamanli kayit · `00a:158` RFC 2606 ust alan adlari · `00a:166` cikisli kullaniciya
dogrudan · `00a:192` `failed-jobs` PII riski · `00a:200` `frontend/pwa/` dizini OLU ·
`48·GUVENLIK-FIX-2b` checkout formu izole iframe'e (`srcdoc` + kendi CSP'si) -> vitrin
`script-src` STRICT, **tasarim LAUNCH SONRASI** · `50·GF-4` TFM `net8.0` -> `net9.0`/`net10.0`,
**TETIKLEYICI .NET 8 EOL Kasim 2026** (`global.json` yoklugunu ve NuGet audit varsayilanini
birlikte etkiler).

## ACIK GIRDILER (`39·MF-3` kalanlari)

Hata kodu birlestirme (TR serbest metin capasi kirilgan) · **K7 mesaj/NotEmpty ayrismasi**
(VITRIN-KALAN 3); cozum ORTAK SABIT REFERANSI — **ortak RuleBuilder ACILMAZ** (`52·GF-5`
BAGLAYICI).
**`ExecuteDeleteAsync` <-> transaction ROLLBACK OLCULMEDI.** AV-2'nin S-B ajani olctugunu
SOHBET RAPORUNDA beyan etti, muhur 51'e TASINMADI - **KANIT KAYBI** (merkez hatasi);
GF-6 kapisinda **5 dk izole EF probuyla YENIDEN OLCULUR**.
**K4 TELAFISININ ATOMIKLESTIRILMESI GF-5/K4'te KAPANDI** (tek transaction).
