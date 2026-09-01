# MTUR-OLCUM KAPANISI (salt olcum, zemin a58a204)

SDP v1.0'in ilk tam uygulamasi. **Kod degismedi, commit atilmadi, build alinmadi.**

## KALEM OZETLERI

| Kalem | Kok sebep (ozet) | Siddet |
|---|---|---|
| **F-M3a** | index.html'in MOCK checkout'u ile api-bridge'in gercek checkout'u AYNI kaba yaziyor (`#checkoutView`, index.html:1600); tercih CIZIM SIRASINA bagli. Mock'u DORT yol diriltiyor: kupon uygula (2447), kupon kaldir (2490), para birimi (2766), **DIL** (2806). api-bridge geri ALAMAZ (`odemeOzetiniTazele` yalniz `#coSubmit`/`#mgGonder` arar). Mock CANLI KART FORMU tasiyor ve `coFinish()` (2732) **sunucuya HICBIR istek atmadan** "Order received!" deyip sepeti bosaltiyor. Cekmecede GOMULU SAHTE KUPON TABLOSU (2438) ve ekran bunlari REKLAM EDIYOR ("Try: HOSGELDIN · STIL20 · KARGOBEDAVA") | **[PARA+DURUSTLUK] AKTIF** |
| **F-M3f** | Sunucu idempotency CALISIYOR ama istemci HER TIKTA yeni `request_id` uretiyor (api-bridge.js:1518 / :1286) -> koruma YAPISAL OLARAK ULASILAMAZ. "Form donmedi" dali `return` ediyor, `finally` dugmeyi geri aciyor, mesaj "tekrar deneyebilirsin" diyor - siparis ZATEN OLUSTU. Omer'in turu: **dort saniyede uc siparis**, tek denemeden ALTI Pending | **[PARA] AKTIF** |
| **F-M3b** | Oturum DUSMUYOR (jeton/`loggedIn`/cookie sabit, `logout` cagrisi SIFIR). `setLang` (2793) satir **2806**'da mock'u cagiriyor; mock'un misafir uyarisi `coStep1()` icinde KOSULSUZ -> GIRISLI kullaniciya "Continuing as guest". **Yon TERS: gorunurdeki oturum dusmesi SAYFA DEGISIMININ SONUCU** | **[UX] AKTIF** |
| **F-M1** | H1 (DB) ELENDI - stok dogru duser (87 stok satirinda invariant 0 ihlal; 55 onayli kalemin 55'inde hareket=miktar). **H2 KIRIK**: `product/get` FIZIKSEL stok donuyor (`ProductManager.cs:370`), `product/filter` ise `available` (`:517-530`) - AYNI SINIFTA IKI TANIM; `ProductStockDto`da alan olmadigi icin istemci TELAFI EDEMEZ. **H3 KIRIK**: `api-bridge.js:655` fiziksel toplami `p.stock` uzerine yaziyor; siparis sonrasi katalog tazeleme YOK. **DENGELEYICI: `order/place` asiri satisi 400 ile DURDURUYOR** | **[VERI-BOZAN] AKTIF** |
| **F-M4** | `index.html:2644` sepeti geri yuklerken `if(byId(it.id))` kapisi koyuyor; acilista `PRODUCTS` hala MOCK dizi -> gercek urun ATLANIYOR, ardindan `saveCart()` (2432) bosalmis sepeti GERI YAZIYOR. **AYIRT EDICI DENEY**: id 2 (mock'ta var) SAG KALDI, id 955 (yalniz gercek) SILINDI | **[VERI-BOZAN/UX] AKTIF** |
| **F-M5** | Backend favori yuzeyi TAM ve CALISIYOR (`WishlistController` uc rota; jetonla 200) ama vitrin HIC cagirmiyor. Favoriler `localStorage['dvs_favs']`de: cikista TEMIZLENMIYOR, **misafir hesabin favorisini SILEBILIYOR** | **[OTURUM/UX] AKTIF** |
| **F-M2** | Sozlukte esasli boskuk YOK (T=561/AR=559, AR'da 2 eksik; EN eksik 0). Sebep: api-bridge index.html'in i18n-farkinda cizicilerini SARMALAYIP EZIYOR ve CEVIRISIZ TURKCE koyuyor (2655'teki 5 anahtar -> api-bridge.js:2199-2205'te 9 gomulu dizge); uretilen HTML `data-i18n` tasimadigi icin `applyI18n()` ULASAMIYOR | **[UX] AKTIF** |
| **F-M6** | Kok sebep TEK yerde: `index.html:2301` (`.pd-rate`/`#pdRateJump`) KOSULSUZ ciziliyor. VITRIN-FIX-2 kart/cross-sell/karsilastirma/JSON-LD/yorum bolumunu korumaya aldi, YALNIZ 2301 disarida kaldi. Yildizlar BOS iskelet ama "0.0" yaziyor; alt bolum "yorum yok" derken ust satir puan gosteriyor | **[DURUSTLUK] AKTIF** |
| **F-M7** | Carpi · ESC · **tarayici GERI** (modal `history.pushState` yapiyor, `index.html:2360`) CALISIYOR. **KAPATMAYAN TEK YOL: OVERLAY tiklamasi** - handler YOK. Hash degismiyor -> paylasilabilir adres YOK | **[UX] AKTIF (dar)** |
| **F-M8** | Iki siparis ucu de YALNIZ sayisal id donuyor (`OrderManager.cs:449`). `renderPaymentResult` (api-bridge.js:1613) uc branch tasiyor: girisli yol siparisi yeniden cekip `order_number`i kurtariyor (:1647), misafir yolu cekemiyor (`order/get` Customer'a kilitli, anonim 401) ve `'#'+orderId` basiyor (:1671), **girisli branch'i KOSULSUZ eziyor**. Iade listesi (:1962) `r.order_id` basiyor oysa `order_number` DTO'da MEVCUT | **[UX] AKTIF** |
| **F-M9** | 12 ikna yuzeyinden **5'i PRNG uydurmasi** (`rngOf`), **3'u sabit-kosulsuz**, **2'si GERCEK** ("senin bedenin" rozeti; "Kolay Iade 14 gun" = `ReturnManager.ReturnWindowDays`), 2'si kismi. Kumas kompozisyonu **YASAL BEYANDIR** ve `detailsOf -> rngOf(p.id*3313+17)`den geliyor. Ic celiski: fit paneli "dar kaliyor" derken Urun Bilgisi "Oversize" (ayri tohumlar 4517/3313). Aksesuar korumasi OLU (`p.cat==='aksesuar'` vs canli slug `goz1-aksesuar`) -> deri kemere "bir beden buyuk al", yun bereye model boyu | **[DURUSTLUK] AKTIF** |
| **KAYIT** | **Katalogda GERCEK urun SIFIR** - 35 urunun tamami test artifakti, 33'u vitrinde canli. `products`a isaret eden 17 FK'nin tamami NO_ACTION, 10 urun siparis/fatura/iade kaydina bagli -> hard silinemez. Bes kuponun UCU sifir degerli. 10 CMS sayfasinda satici kimligi YOK | envanter |

**EK BULGULAR (denetimde cikti):** gecersiz kupon siparis yolunda **SESSIZCE yok sayiliyor**
(HTTP 201, indirim yok, uyari yok) · misafir hesap sahiplenme zincirinin ILK halkasi
istemci<->sunucu sozlesme uyusmazligiyla kirik (`resendVerification` GOVDE gonderiyor, uc
`[FromQuery]` bekliyor, canli 400) · `MailSettings:Host` bossa `SmtpMailService` sessizce
donuyor ve outbox mesaji "Processed" isaretleniyor (**25 Agustos'ta 38 uyari <-> 38 mesaj,
saniye duzeyinde birebir**) — bugun ise `--BackgroundJobs:Enabled=false` yuzunden isleyici
HIC kosmuyor (25 Agustos 11:50'den beri tek posta uretilmedi).

## DENETIM METRIKLERI
- **27 ajan** (12 kaynak + 13 denetci + 2 final) · hata 0 · bos sonuc 0
- **6 itiraz -> 6'si da ILK turda KABUL** · **HAKEM 0** · **CEKISMELI 0** · plan sapmasi 0
- **GERCEK UYDURMA: 0.** Rapor denetcisinin 13 "uydurma adayi"nin cogu **DEFTER BOSLUGU**
  cikti (terimler muhurlu ham dosyalarda izlenebilir ama deftere yazilmamis).
- **KURAL-UYUM: UYUMLU** (alti maddenin altisi; cift-kor izolasyonu TEMIZ - uc L3
  transkriptinde "mtur" gecisi 0, pozitif kontrollu)
- Defter: 190 satir / 121 kanit satiri / 13 ham dosya, **SHA 13/13 TUTTU**
- **L3 cift-kor IKI kalemde yakinsadi ve IKISINDE DE denetci ana akistan DAHA KESKIN
  ornek buldu** (urun 1'in tamami rezerve S bedeni; model boyunun tek sayfada uc degeri)

## DENETIMIN DUZELTTIKLERI (on bir) - ozet
`F-M3c` user-secrets'ta anahtarlar VAR, asil engel komut satiri · `F-M3g` YENI BULGU
(sozlesme uyusmazligi) · `F-M3g` "Processed" bugunku ortam icin yanlis · `F-M7` geri tusu
CALISIYOR (olcumum ileri-gecmis budamasi yuzunden yaniltiiciydi) · `F-M2` sozluk sayimi
eksigi GIZLEYEN artefaktti · `F-M2` ham enum kullaniciya ULASMIYOR, cerceve yanlisti ·
outbox deltasi 10 -> **8** · F-M9'un L3 satiri defterde YOKTU · **[F-M1][2] "sunucu stok
kapisi saglam" defterde VARDI ama rapora GIRMEMISTI** (risk oldugundan agir gorunuyordu) ·
muhurlu kanit defterlenmemis olcumle DEGISTIRILMISTI · ajan sayimi 25 -> 27.

## KURGU KAYIT ENVANTERI (MTUR)
`orders 213, 214` musteri 74 online **Pending** (idempotency olcumu) · `orders 215, 216, 217`
musteri 74 COD **Confirmed** · `user_sessions` 6 yeni satir (218 -> 224) ·
`outbox_messages` **8 yeni mesaj (id 141-148)**, hepsi Pending · **yeni musteri YOK**
(max 77) · **yeni adres YOK** (max 44). Mock checkout turu HICBIR DB kaydi uretmedi.
Mevcut Pending'lere DOKUNULMADI (muhur `561429369 / 35`, id<=210 kumesi).

---

