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

