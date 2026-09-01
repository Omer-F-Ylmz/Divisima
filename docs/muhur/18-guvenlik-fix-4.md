# GUVENLIK-FIX-4 - DALGA 2'NIN ACIK KALEMLERI (25 Agustos 2026)

Zemin: `f800afe`. Dort kalem: SUPHELI #22 (idempotency), Dalga-2 #2 (cop misafir siparisi),
Dalga-2 #1 (kabul edilen risk - KOD DEGISMEDI), Dalga-2 #7 (cerez kapsami - checklist).

## KALEM 1 - SUPHELI #22 KAPANDI: IDEMPOTENCY GOVDE + KIMLIK BAGI

### OLCULEN ONCE-DURUM (canli, gercek uclar, iki GERCEK hesap)

```
(a) CAPRAZ KULLANICI  /api/order/place
    A + anahtar K -> 201  siparis 180
    B + AYNI K    -> 201  "Idempotency-Replayed: true", GOVDEDE 180
    B'nin siparis sayisi -> 0            (B'nin istegi SESSIZCE dustu)

(b) GOVDE BAGI  /api/guest-checkout/place
    anahtar K + govde(E2) -> 201  siparis 179
    anahtar K + govde(E3) -> 201  replayed, govdede 179
    E3 icin musteri 0, siparis 0         (istek SESSIZCE dustu)

(c) BICIM AYRISMASI
    orijinal {"data":179,"success":true,...}      (camelCase - MVC)
    replay   {"Data":179,"Success":true,...}      (PascalCase - varsayilan secenekler)
```

### KOK SEBEPLER

- **(a)** Filtre kapsami `User.Identity.Name` okuyordu ve o **DAIMA null** - JwtHelper
  `ClaimTypes.Name` YAZMIYOR (D4'te de birebir gorulmustu). Yani "kullanici ile kapsandi"
  diyen yorum dogru, KOD yanlisti: her kimlikli cagiran `"anon"` kapsamina dusuyordu.
  D4 bunu MIDDLEWARE icin duzeltmis, FILTREYI atlamisti.
- **(b)** Kayitta istek govdesinin ozeti YOKTU - anahtar yalnizca `key|path|user` idi.
- **(c)** Govde `JsonSerializer.SerializeToElement` ile **varsayilan** seceneklerle
  saklaniyordu; MVC ise camelCase yaziyor.

### YAPILAN

**`IdempotencyKimligi.Coz` (TEK KAYNAK).** Kimlik cozunurlugu artik iki mekanizmada da
`ClaimTypes.NameIdentifier ?? "anon"`. Ortaklastirma **zorlama degil dogaldi**: iki tip de
`Divisima.API` derlemesinde ve middleware ZATEN `using Divisima.API.Filters;` diyordu.

**Govde bagi.** Kayda istek govdesinin SHA-256'si yazilir. Ayni anahtar + FARKLI ozet ->
**422**, replay EDILMEZ, sessizce DUSMEZ. Ayni anahtar + ayni ozet -> replay (asil vaat korunur).

**422 GOVDESI MEVCUT HATA SOZLESMESIYLE - OLCULDU.** Kullanicinin metni "RFC7807 hata
sozlesmesi" diyordu; olculen sozlesme FARKLI ve daha dar: bu API'de **ELE ALINAN** hatalar
`ErrorResult` zarfi doner - `Program.cs`'in `InvalidModelStateResponseFactory`si varsayilan
`ProblemDetails` yerine ACIKCA bunu seciyor ("API sozlesmesi tutarli").
`application/problem+json` YALNIZCA yakalanmayan istisnalarda (`ExceptionMiddleware`).
Dolayisiyla 422 `{"success":false,"message":"..."}` doner - yani "mevcut hata sozlesmesi"
sarti karsilanmis, "RFC7807" etiketi ise depoda o anlama gelmiyor.

**Bayt-birebir replay.** Filtre `ActionFilterAttribute`ten **resource filter**'a cevrildi.
Gerekce yapisal: (b) ham istek govdesini MODEL BINDING'DEN ONCE okumayi, (c) ham yanit
baytlarini SONUC YURUTULDUKTEN SONRA yakalamayi gerektirir; action filter ikisini de goremez
(`next()` dondugunde sonuc HENUZ yurutulmemistir). Yanit bir tampona alinip AYNEN hem
istemciye yazilir hem cache'e konur - bicim hakkinda hicbir varsayim YOK, bayt-birebirlik
YAPISAL.

**Cache oneki `idem2:`** - saklanan kaydin SEKLI degisti; eski onekle devam etmek dagitim
aninda cache'te duran eski kayitlarin sessizce "bozuk" sayilmasina yol acardi.

**YAN DUZELTME (ayni dosya, raporlandi):** lock cakismasi 409'u eskiden ANONIM bir nesneydi
(`new { Success, Message }` -> PascalCase) ve ayni ucun diger hatalari camelCase `ErrorResult`
donerken ayrisiyordu. Zarf birlestirildi. Kirilma riski YOK: o dala ancak `Idempotency-Key`
gonderen bir istemci ulasir ve storefront o basligi HIC gondermiyor (olculdu).

### SONRA (canli)

```
S1 anahtar K + govde(X)   -> 201  {"data":183,...}
S2 anahtar K + govde(Y!)  -> 422  {"success":false,"message":"Bu Idempotency-Key farkli ..."}
   Y icin musteri -> 0                       (yan etki SIFIR)
S3 anahtar K + AYNI govde -> 201 replayed=true, govde S1 ile BAYT-BIREBIR (-ceq True)
S4 BASLIKSIZ              -> 201             (basliksiz akis DEGISMEDI)
C1 A + anahtar K          -> 201 siparis 188
C2 B + AYNI K             -> 201 siparis 189, replay YOK   (capraz sizinti KAPANDI)
```

## KALEM 2 - COP MISAFIR SIPARISI GUARD'I (SPEC OLCUMLE DUZELTILDI)

### SPEC'IN ILK HALI OLCUMLE CURUDU - IKI YONDEN

Istenen: "Ayni e-postaya acik misafir siparisi (**Pending** + odenmemis) sayisi esigi
doldurduysa". Olculdu:

1. **Misafir COD siparisi `Pending` DOGMUYOR** - `Confirmed(1)` dogar
   (`is_online_payment_done=0`). Tum veritabaninda "Pending + odenmemis misafir COD siparisi"
   = **0 SATIR**. Yuklem HIC ATESLEMEZDI.
2. **SAKLANAN e-posta basina acik siparis sayisi YAPISAL OLARAK <= 1** - ikinci siparis zaten
   mevcut 409'a takilir (olculdu: 5 e-postanin hepsinde n=1). Gruplama anahtari esigi HIC
   dolduramazdi.

**GERCEK VEKTOR OLCULDU:**
```
kurban@example.com    -> 201  (siparis 181)
kurban+a@example.com  -> 201  (siparis 182)     <- AYNI FIZIKSEL KUTU, 409'u ASIYOR
KURBAN@example.com    -> 409                    <- Dalga 1 kanoniklestirmesi TUTUYOR
```
Yani buyuk/kucuk harf ekseni ZATEN kapaliydi, **`+etiket` ekseni ACIKTI**.

**KULLANICI KARARI: guard olculen vektore gore kuruldu** (secenek 1). Rapor edildi, karar
alindi, sonra uygulandi - kapsam kendi basima genisletilmedi.

### YAPILAN

- Sayac ekseni **kanonik posta kutusu** (`PostaKutusu.Kanonik`: kucuk harf + `+etiket`
  siyirma). **Kanoniklestirme YALNIZ SAYACTA** - hesap kimligi, `customers.email` ve 409
  semantigi DEGISMEZ. `KimlikDizgesi`nin "E-POSTAYA UYGULANMAZ" siniri AYNEN gecerli; bu
  yuzden donusum oraya KONMADI, ayri bir tip olarak yazildi.
- **"ACIK" TANIMI DURUM MAKINESINDEN TURETILIR, elle yazilmaz:**
  `d != Cancelled && OrderStatusMachine.IsValidTransition(d, Cancelled)` -> **Pending,
  Confirmed, Preparing**. Gerekce: iptal edilebilen = operatorun HALA ugrastigi.
  `Shipped` DISARIDA - yalniz `Delivered`a gidebilir, yani mal fiziksel olarak cikmistir ve
  yeni siparisi engellemek o maruziyeti geri almaz. `Delivered`/`Cancelled` terminal.
  Makine degisirse kume KENDILIGINDEN degisir (pin de bunu dogruluyor).
- Yuklem: `is_online_payment_done = 0 AND status IN (acik kume)`.
- **SIRA KRITIK:** guard 409 kontrolunden SONRA, ama musteri satiri / adres / dogrulama maili
  / siparis / rezervasyonun HICBIRINDEN once. Reddedilen istek HICBIR yan etki birakmaz.
- Yanit **429**, govde **NOTR ve tek tip** - adresin kayit durumunu IMA ETMEZ ve 409 ile
  karistirilamaz (aksi halde guard'in kendisi yeni bir enumeration kanali olurdu).
- Esik yapilandirmadan: `GuestCheckout:MaxOpenOrdersPerMailbox`, varsayilan **3**.
  `appsettings.Development.example.json`'a uc aciklama satiriyla yazildi.
- SQL tarafi KABA SUZGEC (sabit onek/sonek), kesin karar C#'ta ORDINAL karsilastirmayla -
  collation'a bagli yanlis pozitif riski sayacin DISINDA kalir.

### SONRA (canli, esik varsayilan 3)

```
1. duz adres      -> 201 (185)
2. +a varyanti    -> 201 (186)
3. +b varyanti    -> 201 (187)
4. +c varyanti    -> 429  {"success":false,"message":"Bu istek su anda isleme alinamiyor..."}
YAN ETKI:  +c musterisi 0 · kutuda musteri/siparis/adres/rezervasyon/outbox = 3/3/3/3/3
409 KORUNDU: ayni SAKLANAN adres -> 409 · BUYUK harf varyanti -> 409
```

### DEFTERE - UC NOT (kullanicinin sarti)

1. **`+etiket` varyanti KIMLIK duzeyinde AYRI musteri satiri acabiliyor** - bilincli, RFC
   uyumu: `a+x@d` ile `a@d` FARKLI adreslerdir ve saglayici bazli bir varsayimi kimlige
   uygulamak kullanicinin kendi kimligini yeniden yazmak olurdu. Guard bunlari YALNIZCA
   SAYACTA birlestirir.
2. **NOKTA SIYRILMAZ** - bilinen sinir. Yalniz BAZI saglayicilar (Gmail) yerel kisimdaki
   noktayi yok sayar; siyirmak `a.b@x` ile `ab@x`i AYNI kisi sayardi ve iki FARKLI musteriyi
   birbirinin esigine yazardi (yanlis pozitif).
3. **GUARD'IN TERS YUZU:** kutusuna cop yigilan kurbanin MISAFIR yolu, o siparisler kapanana
   kadar acilmaz. Bilincli: kayit/giris yolu ACIK kalir (kurban hesap acip siparis verebilir),
   ve alternatif - cop yigmaya devam izni vermek - daha kotudur. **B13 (terk edilmis Pending
   siparislere TTL) bu guard'in TAMAMLAYICISIDIR:** TTL gelirse acik siparisler kendiliginden
   kapanir ve kurbanin misafir yolu da kendiliginden acilir. B13 BU COMMIT'E GIRMEDI -
   launch-sonrasi karari DURUYOR.

## KALEM 3 - DALGA-2 #1 MISAFIR CHECKOUT ENUMERATION: KABUL EDILEN RISK (25 Agustos 2026)

**KOD DEGISMEDI.** `POST /api/guest-checkout/place` kayitli bir e-postaya **409**
("Bu e-posta kayitli. Lutfen giris yapin."), kayitsiz olana **201** donmeye DEVAM EDIYOR.

**G2 DESENI BURADA ELENDI - gerekce:**
- G2'nin cozumu "her zaman ayni yanit + gercegi e-postayla soyle" idi. Bir SIPARIS ucunda
  "alindi" deyip siparis OLUSTURMAMAK musteriye YANLIS BILGI vermek ve satis kaybetmektir.
- Siparisi mevcut hesaba BAGLAMAK ise kurbanin hesabina cop siparis yazdiran bir TACIZ
  vektoru acar.
- **409 hesap ele gecirmeyi ENGELLIYOR** (var olan hesabin ustune yazilamiyor) - kaldirmak
  daha buyuk bir riski acar.
- Kanal **10/dk/IP** ile sinirli (Dalga-2 kaniti: 11. istek 429).

**YENIDEN DEGERLENDIRME TETIKLEYICILERI:** (a) misafir checkout'a **KART** eklenirse,
(b) rate limit **topolojisi/kovasi** degisirse (bkz. GUVENLIK-FIX-3 / #3 - `KnownProxies`
bos birakilan bir topolojide kova HERKES icin tek olur ve bu sinir yok olur).

**PIN:** 409 yolu musteri / adres / siparis / rezervasyon / outbox satiri **YAZMAZ**. Yani
kabul edilen risk bir ENUMERATION kanalidir ve bir KAYNAK TUKETIMI vektorune DONUSEMEZ -
sinir sabitlendi.

## KALEM 4 - DALGA-2 #7 CEREZ KAPSAMI (checklist, kod yok)

GUVENLIK-FIX-3'te DNS hijyeni maddesi eklenmisti; bu dalgada **YENI ALT ALAN ADI ACILMADAN
ONCE** basligi eklendi: `Cookies:Domain = .divisima.com` bugun var olanlari degil TUM alt
alan adlarini kapsar, yani her yeni alt alan adinda YENIDEN DEGERLENDIRILMESI gereken bir
karardir. Az guvenilir / ucuncu taraf icerik bu alan adinin alt alanina KONMAZ (boyle bir
alt alandaki tek bir XSS `.divisima.com` kapsamindaki cerezlere erisir; `csrf_token` JS'ten
okunabilir, `refresh_token` httpOnly ama ayni kapsamdaki bir sayfadan `/api/auth/*`'a giden
isteklere OTOMATIK eklenir). Statik varliklar icin ayri KAYIT ALANI onerildi.

## PINLER (12 yeni/genisletilmis - YENI VERITABANI ACILMADI)

**`PostaKutusuTests` (5, VERITABANI ACMAZ):** etiket siyrilir ve kucultulur (Theory x5) ·
**nokta SIYRILMAZ** (cift-anlam kirici: "her seyi kirp" YANLIS donusumdur) · farkli kutular
BIRLESTIRILMEZ (vakum kirici) · cozumlenemeyen girdi bozulmaz (`+` ile baslayan adreste yerel
kisim BOSALTILMAZ) · **kulturden bagimsiz** (tr-TR altinda da ayni sonuc - bolum 6c).

**`IdempotencyContractTests` (+1):** `FILTREDE_CAPRAZ_KULLANICI_AYRISIR_A_NIN_ANAHTARI_B_YI_ETKILEMEZ`
- B kendi sonucunu alir, replay basligi GELMEZ, B'nin puani GERCEKTEN harcanir; cift-anlam
kirici: AYNI kullanici + AYNI anahtar HALA replay alir ve islem TEKRARLANMAZ.

**`MisafirCheckoutTests` (+6):** ayni anahtar farkli govde **422** + ikinci siparis olusmaz
(cift-anlam kirici: 422 kozmetik degil AMA sessiz de degil) · ayni govde replay **bayt-birebir**
(ORDINAL karsilastirma) + ikinci siparis olusmaz · guard esik alti 201 / esikte **429** kanonik
kutu ekseninde (esik AYIRT EDICI degerle - varsayilan 3 DEGIL, 2; ayar okunmasaydi ucuncu istek
201 gecerdi) + govde NOTR · guard reddinde **hicbir yan etki** (bes sayac ONCE==SONRA) ·
guard **409 semantigini DEGISTIRMEZ** (saklanan adres ve BUYUK harf varyanti HALA 409) ·
**kabul edilen riskin siniri**: 409 yolu hicbir satir yazmaz · acik durum kumesi **durum
makinesinden turetilir** (VERITABANI ACMAZ).

**KIRILAN PIN YOK.** Yeni veritabani acan sinif YOK (10d794d dersi): davranis pinleri mevcut
iki SQL sinifina eklendi, saf donusum pini DB'siz bir sinifta.

## YENI BULGU - ADI OLAN FLAKE'IN KOK SEBEBI ILK KEZ OLCULDU [KAPANDI - FLAKE-FIX]

`RefreshCookieContractTests.Cerez_Secure_HER_ORTAMDA_ISARETLI_OrtamGuardi_YOK` bu dalganin
tam suit kosumlarindan BIRINDE kirildi ve **hata mesaji ILK KEZ yakalandi** (guvenlik
dalgasinda "mesaj YAKALANAMADI" diye kaydedilmisti):

```
Autofac.Core.DependencyResolutionException : An exception was thrown while activating
  λ:Hangfire.IGlobalConfiguration.
---- System.InvalidOperationException : Timeout expired. The timeout period elapsed prior to
     obtaining a connection from the pool. This may have occurred because all pooled
     connections were in use and max pool size was reached.
  at ... RefreshCookieContractTests.cs:line 318 / 326      (IKINCI host'un kurulumu)
```

**MEKANIZMA (olculdu, artik "aday" DEGIL):** `BackgroundJobs:Enabled=false` YALNIZCA
`AddHangfireServer()` ve recurring is kayitlarini kapatiyor - **Hangfire'in DEPOLAMA
YAPILANDIRMASI (`AddHangfire` -> `UseSqlServerStorage`) HALA kosuyor ve konteyner kurulurken
SQL'e BAGLANIYOR**. Bu test PRODUCTION ortamli IKINCI bir host actigi icin ekstra bir
baglanti kumesi daha aciliyor; tam suit paralel kosarken SQL baglanti havuzu tukeniyor.

CLAUDE.md'de bu kayit icin iki ADAY aciklama vardi (Hangfire yarisi ve `model` kilidi/1807).
**Dogru aile Hangfire'di ama mekanizma FARKLI:** yaris/zamanlama degil, **BAGLANTI HAVUZU
TUKENMESI**; ve `model` kilidi (1807) DEGIL - bu kosumda 1807 hic ateslemedi (0).

**SIKLIK (bu oturumda olculdu):** tam suit UC KEZ kosuldu -> **1 kirmizi / 2 temiz**.
Sinif TEK BASINA kosuldugunda 4/4 yesil (16 sn). Yani yuke bagli, deterministik degil.

**BU DALGANIN ACTIGI BIR KAPI DEGIL - ama olasiligi ARTIRMIS OLABILIR (durust kayit):**
mekanizma guvenlik dalgasindan beri kayitli ve bu dalgada YENI HOST ya da YENI VERITABANI
EKLENMEDI; ancak suit 517 -> 538 teste cikti, yani paralel yuk bir miktar arttı.

**O DALGADA DUZELTILMEDI (kapsam sabit kurali); FLAKE-FIX dalgasinda KAPANDI - asagidaki
secenek (i) uygulandi.** Aday cozumler (o gun sunulanlar):
(i) `BackgroundJobs:Enabled=false` iken Hangfire'in DEPOLAMA yapilandirmasini da atlamak -
    en dogrudan cozum; test host'u SQL'e Hangfire icin hic baglanmaz,
(ii) test baglanti dizgesine `Max Pool Size` yukseltmesi - belirtiyi orter, kok sebebi degil,
(iii) bu sinifin IKINCI host'unu kaldirmak - ama o host pinin OLCTUGU seydir (uretim ortami).
**(i) onerilir.** CI'da tekrar ederse SUPHELI olarak acilir.

## SURECTE YASANAN (kayit - iki ders)

- **SPEC'IN YUKLEMI OLCUMLE CURUDU ve DURULDU.** Kalem 2'nin istenen hali ("Pending +
  saklanan e-posta basina") olculdugunde HIC ATESLEMEYECEGI cikti. Kod yazmak yerine
  DURULDU, olcum raporlandi ve karar kullanicidan alindi. 5. kontrolun M3 mutasyonu bunu
  ampirik olarak da gosterdi: eski gruplama anahtariyla guard 429 yerine 201 donuyor.
  **Ev kuralinin ("yeni bulgu -> yalniz olc+raporla") dogrudan ise yaradigi bir vaka.**
- **Iki uc rotasi kaynaktan DOGRULANMADAN tahmin edildi ve olcum iki tur kaybettirdi:**
  `verify-email` POST sanildi (gercekte `HttpGet` + `[FromQuery]`), ve `/api/order/place`
  govdesine `customer_id` konmadi - CLAUDE.md bolum 5'te ZATEN yazili olan tuzak
  ("`customer_id > 0` sart - FluentValidation controller token'dan set etmeden ONCE kosar").
  **DERS: olcum betigi yazmadan once ROTA ve ZORUNLU ALANLAR kaynaktan okunur.**
- **CLAUDE.md EKLEMESI YANLIS BOLUME DUSTU - CAPA BENZERSIZ DEGILDI.** Flake bulgusu
  `## DIS KONTROLU + 5. KONTROL` capasiyla eklendi; o baslik dosyada ONLARCA KEZ geciyor ve
  `awk ... && !d` ILK esleseni sectigi icin bolum **Dalga A'nin icine** dustu. Fark edildi
  (ekleme sonrasi "hangi `#` basliginin altina dustu" diye DOGRULANDI), gecici dosyadan geri
  alinip capa HESAPLANARAK (`GUVENLIK-FIX-4 basligindan SONRAKI ilk eslesme`) tekrarlandi.
  **DERS: bu dosyada tekrar eden bir baslik CAPA OLARAK KULLANILMAZ; capa ya benzersiz bir
  dizge olur ya da bolum baslangicina gore HESAPLANIR - ve ekleme sonrasi HANGI BOLUME
  dustugu dogrulanir.** (GUVENLIK-FIX-3'un "budama" dersiyle ayni aile: duzenleme SONRASI
  dogrulama, ONCESI degil.)

## DIS KONTROLU + 5. KONTROL

**DIS:** 6 assert ters (ALTI AYRI test, UC ayri sinif) -> **6 AYRI ISIMLI KIRMIZI**.
Geri alindi (zaman damgasi tazelendi), 33/33 yesil.

**5. KONTROL - UC URETIM MUTASYONU** (her birinde (a) dosyaya indi mi / (b) temiz build /
(c) kirmizi yoksa once "uygulanmadi" suphesi):

| Mutasyon | Kirilan pin | Uretilen once-durum |
|---|---|---|
| M1 kimlik cozunurlugu `Identity.Name`e donduruldu | `FILTREDE_CAPRAZ_KULLANICI_...` (1) | `Idempotency-Replayed ... but found True` - B, A'nin yanitini aldi |
| M2 govde bagi devre disi | `IDEMPOTENCY_AYNI_ANAHTAR_FARKLI_GOVDE_422_...` (1) | `to be 422 ... but found 201` - sessiz replay |
| M3 sayac ekseni SAKLANAN e-postaya donduruldu | `MISAFIR_GUARD_ESIK_ALTI_...` + `..._YAN_ETKI_...` (2) | `to be 429 ... but found 201` - **spec'in ilk gruplama anahtariyla guard HIC ATESLEMIYOR** |

M3 ayrica **spec duzeltmesinin gerekliligini ampirik olarak kanitliyor**. Ucu de geri alindi;
`[MUTASYON]` izi depoda **0 dosya**.

---

