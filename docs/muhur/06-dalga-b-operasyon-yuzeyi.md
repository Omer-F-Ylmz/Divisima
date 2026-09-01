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

