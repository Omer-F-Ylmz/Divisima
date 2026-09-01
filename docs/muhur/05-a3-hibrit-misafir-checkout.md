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

