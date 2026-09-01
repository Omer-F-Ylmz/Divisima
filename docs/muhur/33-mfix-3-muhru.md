# MFIX-3 MUHRU - SEPET/FAVORI/i18n + DURUSTLUK DEVIRLERI (27 Agustos 2026)

**KOD SHA: `c023f90`** (zemin `188599a`) - her iki workflow yesil.
Bu muhur AYRI ve docs-only bir commit'tir; **kendi SHA'sini ve kendi run kimliklerini
ICEREMEZ** (tavuk-yumurta) - mührün kendi cift yesili MFIX-3 raporunda verilir.
MFIX-1'de kurulan kalip.

```
MFIX-3 KODU (c023f90)
  CI - Build & Test  run 33101966175  event=push  head_sha=c023f90
  Security CI        run 33101966076  event=push  head_sha=c023f90
```

**ORTAM UYARISI (Divisima Eki 2.3):** olcumler `goz1` duzeneginde kosuldu;
`api-baslat.cmd` BES arguman veriyor - `--Iyzico:UseRealSdk=false`,
`--AdminSeed:Enabled=false`, `--BackgroundJobs:Enabled=false`,
`--RateLimit:AuthPermitLimit=100`, `--MailSettings:Host=`.
**Bunlar URUN VARSAYILANI DEGILDIR.**

**AJAN KISITI:** bu dalgada da AgentTool cagrisi yasakti; L1-L3 denetci ajanlari
DAGITILAMADI. Disiplin ELDE uygulandi (on-kayit + karar kriteri, append-only defter -
34 kanit satiri / 2 PLAN / 9 HAM, SHA 9/9 tuttu -, [YOKLUK] negatif kontrolleri,
suzgec sinamasi, TAM KAPSAMA dis kontrolu, 5. kontrol).

## MFIX-2 REGRESYONU - OLCUM SIRASINDA BULUNDU, ONCE ONARILDI

MFIX-1 devri mock-checkout sokumu, `wireCheckout` ile birlikte **KOMSU IKI FONKSIYONU DA**
goturmus (`setAnnShip` + `refreshPrices`) ama **CAGRI YERLERI KALMISTI**. CANLI OLCULDU:

```
ONCE   applyI18n()  -> "setAnnShip is not defined"        (index.html:2801'deki cagri)
       setLang()    -> ayni istisna                        (DIL DEGISTIRME BOZUKTU)
       setCur()     -> "refreshPrices is not defined"
       #annShip elemani VAR, metni BOS ("")
SONRA  ucu de "SORUNSUZ" ; #annShip "2.000 TL ve uzeri tum siparislerde kargo bedava"
```

SIDDET **[ISLEV-KIRAN] AKTIF**. F-M2 tam bu mekanizmaya baglandigi icin ONCE onarildi.
**IKI BILINCLI SAPMA:** (1) `refreshPrices`tan uydurma taksit satiri (#pdInst/instHTML)
GERI GETIRILMEDI - onu MFIX-2 DOGRU sekilde kaldirmisti; (2) `setAnnShip`e guard eklendi
(guard'siz `getElementById` zinciri bu dosyada bir kez bedeli odenmis kalip - M10/P5 dersi).

## KAPANAN KALEMLER - A/B SONUCLARIYLA

**DEVIR-1 - SOSYAL KANIT SOKUMU [LAUNCH BLOKERI SINIFI]**
Uydurma isim/sehir/dakika havuzlariyla `Math.random` secip **YESIL ONAY ISARETIYLE**
"X bu urunu satin aldi - N dk once" diyen serit TUMDEN sokuldu: markup (5 satir),
`.sp-*` CSS (14 satir), IIFE (13 satir), i18n `sp_bought`/`sp_ago`/`sp_from`.

```
ONCE  CANLI YAKALANDI t=108,6 sn: "Deniz Y. - Eskisehir" /
      "[YESIL ONAY] bu urunu satin aldi - 8 dk once"
      (ilk cycle 25 sn'de kosmustu ama BULTEN MODALI show()'u bastirdi; modal
       kapatilip sonraki cycle yakalandi)
SONRA 412 sn (6 dk 52 sn) gozlem -> 0 BILDIRIM; 13 tanimlayici icin [YOKLUK] 0
```
D-1 (sahte yorumlar) ile **AYNI SINIF, daha agir**: yorum bir GORUS, bu bir OLAY IDDIASIDIR.
**TAM TARAMA:** baska uydurma olay-iddiasi yuzeyi YOK. `index.html` `Math.random` -> 0;
`api-bridge.js`teki tek kullanim `request_id` yedegi (**MESRU**, idempotency anahtari);
`api-client.js` 0; `getRandomValues` 0. NEGATIF KONTROL: ayni tarama `function` desenini
551/302 kez buldu.

**DEVIR-2 - MOCK_ORDERS tohumu BOSALTILDI** (ADDR/CARDS tedavisi: cizici DURUYOR).
Uc uydurma siparis (no + tarih + durum + kalem) gitti; `accOrders` artik DURUST bos durum
gosteriyor (`t('orders_empty')`). Uydurma `DVS-2026...` numarasi kaynakta 0.

**DEVIR-3 - ODEME BASARI OLCUTU TEK KAYNAK**
Iki kod yolu "basarili"yi FARKLI tanimliyordu.

```
ONCE  cod: ekran "Siparisin alindi"  <->  SEKME "Odeme Tamamlanamadi"
SONRA cod "Siparisin alindi" · failed "Odeme tamamlanamadi" · success "Odemen alindi"
      EN'de "Order received" · DOGRUDAN ACILISTA da dogru
```
`odemeBasariliMi(status)` + `odemeSonucBaslikAnahtari(status)` TEK KAYNAK; ekran ve sekme
AYNI metni gosteriyor. **DOGRUDAN ACILIS YARISI da kapatildi**: paylasilan baglanti/yer imi
ile acildiginda index.html'in router'i api-bridge YUKLENMEDEN kosuyor ve baslik
"Odeme · Divisima" kaliyordu (B9'un asil gerekcesi tam bu senaryoydu); sarmalayici
kuruldugu an bir kez calistiriliyor. MFIX-1'de belgelenen `defer` yarisinin ayni sinifi.

**F-M4 - MISAFIR SEPETI CIHAZDA KALICI**
Kok sebep IKI KATMANLIYDI: (1) geri yukleme `loadAccountData` icindeydi ve init'te
KATALOGDAN ONCE kosuyordu - o an `PRODUCTS` hala MOCK dizi oldugu icin `byId` kapisi
GERCEK urunleri eliyor, ardindan `saveCart` bosalmis sepeti KALICI yaziyordu;
(2) `renderCart` urununu bulamadigi kalemi `cart.delete(k)` ile SILIYORDU.

```
AYIRT EDICI DENEY (MTUR deneyinin TERSINE DONMUS hali; mock-id 2 + gercek-id 955)
ONCE  id 2 SAG KALDI, id 955 SILINDI ve dvs_cart KALICI olarak yeniden yazildi
SONRA IKISI DE sag kaldi, IKISI DE cizildi, dvs_cart ikisini de tasiyor
      PRODUCTS 25 (24 katalog + id 2 detay ucundan TAMAMLANDI)
```
DURUST NOT: id 2'nin adedi 2 -> 1 dustu; sebep REGRESYON DEGIL, MEVCUT stok kirpmasi
(DB'de urun 2 / beden M satilabilir = 0). **Kalem KORUNDU** - F-M4'un sarti buydu.
DURUST SINIR: detay ucu `image_url` DONDURMUYOR (canli alan listesi olculdu), boyle
tamamlanan urun gorselsiz gelir ve frontend kendi yer tutucusunu cizer - bugun
katalogdaki TUM urunler zaten oyle (D1 temizliginden sonra `product_images` BOS).
Ikinci bir gorsel istegi ATILMADI: kazanci bugun SIFIR.

**F-A1 / P4 REGRESYON REPRO - BIREBIR GECTI**
```
1) hesap A ile giris, sunucu sepeti bosaltildi   yerel 0 / sunucu 0
2) girisliyken urun 954 eklendi                  yerel [954] / sunucu [954|TEK|1]
3) CIKIS                                         yerel [954] KORUNDU (sepete DOKUNULMADI)
4) misafirken urun 953 eklendi                   yerel [954, 953]
5) tekrar giris + renderCart                     yerel [954,953] / sunucu [954,953]
   => ILK SENKRON SILMEDI, BIRLESTIRDI (P4'un birinci yarisi)
6) yerelden 953 silindi + renderCart             sunucu [954]
   => AYNA HALA SILIYOR (P4'un ikinci yarisi)
```
F-M4 `index.html`in YEREL geri yukleme yoludur; P4'un olctugu sunucu birlestirmesi
api-bridge'tedir - **AYRI KOD YOLU, assert'lere DOKUNULMADI.**

**F-M5 - FAVORILER HESABA OZGU (IKI-HESAP KANITI)**
Sunucu tarafi (`WishlistController`) TAM ve CALISIYORDU ama vitrin HIC cagirmiyordu
(api-bridge'de "wishlist" gecisi 0).

```
ONCE  misafir kalbi CIHAZ-GENELI yerel anahtara yazdi; wishlist_items TOPLAM=0;
      ardindan giris yapan hesap o favorileri DEVRALDI
SONRA misafir : favs 0, yerel anahtar null (HICBIR YERE YAZMADI), hash -> #/giris,
                gorunur Turkce yonlendirme, kalp ISARETLENMEDI, rozet "0"
      hesap A : DB wishlist_items 951 + 954, rozet "2", yerel anahtar NULL
      CIKIS   : favs [], rozet "0"
      hesap B : favs [] -> 953 eklendi, rozet "1"
      A'ya donus: favs [951,954], rozet "2"      => HESABA OZGULUK KANITLANDI
      DB: musteri 79 -> 951,954 | musteri 80 -> 953
```
**SUNUCU SOZLESMESI KAYNAKTAN OKUNDU:** `POST /api/wishlist/toggle?productId=N`
(`Toggle(int productId)` - `[FromBody]` YOK) ve `GET /api/wishlist` ->
`List<ProductListResponseDto>` (katalogla AYNI sekil).
**KENDI DEGISIKLIGIMIN ACTIGI KAPI (olculup kapatildi):** async sarmalayici yuzunden
URUN DETAYINDAKI kalp BAYAT kaliyordu - `index.html`in onclick'i `toggleFav`dan HEMEN
SONRA `favs`i okuyor. Olculdu: kart ve rozet guncellendi, `#pdLike` degismedi.
`favEkranlariniTazele` `#pdLike`i da tazeler hale getirildi; ekle/cikar iki yonde senkron.
**MERKEZ KARARI:** eski cihaz-geneli anahtar sunucuya TASINMAZ; anahtar yalnizca
OKUNMAZ hale gelir (launch oncesi gercek kullanici verisi yok).

**F-M2 - i18n (ONCELIKLI ALT KUME; DURUST SINIR KULLANILDI)**
api-bridge'in kullanici-gorunur dizgeleri index.html'in **MEVCUT** sozluk mekanizmasina
baglandi - YENI MEKANIZMA ICAT EDILMEDI (`ceviri()` -> `window.t()`, sozluk T/AR'da,
`setLang` zaten `renderAccount`/`renderCheckout`/`renderCart`/`renderFavs`i yeniden ciziyor).

```
ONCE  EN modunda hesap menusu 10/10 TURKCE, siparis durumu "Onaylandi",
      "Detay ve takip", chrome "Anasayfa / Hesabim" + "Merhaba, ..." + "Uye"
SONRA EN: Summary / My Orders / My Returns / My Invoices / My Addresses /
          My Notifications / My Favourites / Saved Cards / Account Details / Sign Out
          "Home / My Account" · "Hello, MFIX3" · "Member" · "Confirmed" ·
          "Details & tracking"
      AR: menu 10/10 Arapca
      setLang tr/en/ar UCUNDE de HATA YOK
      AR EKSIK ANAHTAR 2 -> 0   (T=614, AR=614)
```
**AR'daki iki eksik anahtar** (`'sort_price-asc'` / `'sort_price-desc'`) MTUR'da
olculmustu ve **AD-TABANLI taramalarda TIRE yuzunden gozden kaciyordu**; bu dalgada
regex yontemim de "0 eksik" dedi, dogru sonuc **TARAYICI RUNTIME** olcumunden geldi
(`Object.keys(T)` vs `Object.keys(AR)`). SDP 1.7/1'in ikinci-olcum kaniti.
**KAPSAM KARARI:** ceviri() cagrilarina YEDEK METIN KONMADI - eksik/yanlis anahtar
ekranda HAM ANAHTAR gosterirdi; bunu calisma anina birakmak yerine **KIRMIZI BIR TESTE**
baglandi (P11).

**F-M3g - `api-client.resendVerification` SORGU DIZESINE cevrildi**
```
ONCE  govde ile POST -> HTTP 400 "The email field is required."
SONRA sorgu dizesi   -> 200 (istemci uzerinden de dogrulandi)
```
Kaynak: `AuthController.ResendVerification([FromQuery] string email)`. Misafir
checkout'un hesap SAHIPLENME zincirinin ILK halkasi bu uctu ve KIRIKTI.
Kalip `verifyEmail` ile AYNI (o da `_qs` kullaniyor).

**MFIX-2 REGRESYON HIZLI KONTROLU:** teslimat gercek varsayilan adres sehrinden
("Trabzon icin tahmini teslimat: 31 Agu - 2 Eyl", **Hizli Teslimat rozeti YOK** - Trabzon
hizli sehir listesinde degil, yani rozet KOSULLU calisiyor) · urun modali karartmaya
tiklaninca KAPANDI ve scroll kilidi cozuldu.

## MERKEZ ONAYLARI (KAYIT)

1. **P11 SPEC SAPMASI ONAYLANDI - 18 Fact KALICI.** Merkez "15->17" demisti; MFIX-2
   regresyonunu koruyan hicbir pin yoktu ve ayni sinif sessizce tekrar edebilirdi.
   P11 ayrica F-M2'nin "yedek metin yok" kararini kirmizi bir teste bagliyor.
2. **P2 ve MisafirA3 PREMIS DEGISIKLIKLERI ONAYLANDI.** Iki pin de LITERAL METIN
   ariyordu; F-M2 metinleri sozluge tasidi. **OLCTUKLERI SEY DEGISMEDI** ("401'de
   kullaniciya eylem iceren AYRI metin verilir" / "misafire `#/giris`e giden CALISAN
   yol gosterilir"), yalniz metnin YERI degisti. Anahtarin sozlukte GERCEKTEN bulundugunu
   **P11 AYRICA pinliyor** -> iddia ZAYIFLAMADI, **IKI PINE BOLUNDU**.
3. **BESINCI DOSYA "+test" KAPSAMINDA KABUL EDILDI.** Kapsam dort dosya olarak verilmisti;
   `Divisima.IntegrationTests/MisafirA3FrontendTests.cs` yalnizca (2)'deki premis
   guncellemesi icin degisti.

**KALICI KURAL NOTU (MFIX-2'de konuldu, burada IKINCI KEZ uygulandi):** bir pinin
PREMISI degistiginde HER ZAMAN raporda gerekceli yazilir ve muhurde **merkez onayiyla**
kayda gecer. Assert degerini degistirmeden premisi sessizce kaydirmak, pini yalanci
yesile cevirmenin en sinsi yoludur.

## IKI SDP MIKRO-KURALI (KALICI)

**MK-1 - SOKUM ICEREN HER DALGADA CERCEVE TARAMASI ZORUNLU.**
Bir dalga fonksiyon/blok SOKUYORSA: (a) cerceve GIRIS NOKTALARINDA (`applyI18n`,
`setLang`, `setCur`, `refreshPrices` ve muadilleri) **tanimsiz-fonksiyon taramasi**
yapilir; (b) **dil / para birimi / tema gecisleri** REPRO setine EKLENIR.
Gerekce: MFIX-2'nin sokumu iki komsu fonksiyonu goturdu, cagri yerleri kaldi ve
**dil degistirme BOZULDU** - hicbir pin yakalamadi, hicbir REPRO dokunmadi.
Pin karsiligi: **P11**.

**MK-2 - GIT KOMUTU CALISTIRAN HER CAGRI CWD'YI ONCE DOGRULAR.**
Gerekce: MFIX-2 push turunda `cd` ayni cagrida kaldigi icin `git push` **scratchpad'de**
kostu, `fatal: not a git repository` verdi ve **PUSH OLMADI**; yalnizca ciktinin
okunmasi sayesinde fark edildi. Kural: git cagrisi `pwd` + `git rev-parse
--is-inside-work-tree` teyidiyle baslar.

## KACIS-KAYBI AILESI ve POWERSHELL ASCII KURALI - BU TURUN ORNEKLERI

**KACIS-KAYBI (aileye yeni ornek):** `emptyState('\u{1F4E6}', ...)` yazildi, dosyaya
`{1F4E6}` olarak indi (ters bolu zincirde kayboldu). **KACISSIZ COZUM TERCIH EDILDI:**
`String.fromCodePoint(0x1F4E6)` - kaynakta hicbir kacis yok. Ailenin onceki uyeleri:
heredoc'ta ters bolu dususu, `printf`te satir sonu kacisi, guard'a gomulen regex,
`perl` revert'inde regex ters bolulari.

**POWERSHELL SALT-ASCII KURALI (tekrar, siniflandirici-sinamasiyla yakalandi):**
PowerShell komutuna DUZ Turkce karakter sinifi yazmak **bozuk desen** uretti ve
BILINEN-NEGATIF girdilere de `True` dondu (2945 satirin 1525'i "eslesti"). Desen
KOD NOKTALARINDAN kurulunca (`[char]0x015F` ...) iki pozitif True / uc negatif False
verdi ve sayim 221'e dustu. **Kural bir kez daha dogrulandi: PowerShell'e yazilan
eslestirme dizgeleri SALT ASCII olmali.**

## PINLER

**15 -> 18 Fact** (SIFIR-DDL sinif; yeni veritabani ACILMADI - 10d794d dersi).
- **P9** `KAYNAK_SOZLESMESI_UydurmaOlayIddiasi_ve_SosyalKanit_Uretilmez` - sosyal kanit
  + MOCK_ORDERS. Olcut **LITERAL BICIM DEGIL KUSUR SINIFI**: `index.html`de
  `Math.random` sayisi 0. Vakum kirici: `rngOf` HALA >1 (kapsam disi renk yuzeyi).
  Cift-anlam kirici: api-bridge'in MESRU rastgeleligi (`request_id`) DURMALI.
- **P10** `KAYNAK_SOZLESMESI_MisafirSepeti_KatalogSonrasiYuklenir_ve_Favoriler_SunucudanHesabaOzgu`
  Cift-anlam kiricilar: KULLANICI silme yollari DURMALI · yerel favori guncellemesi
  sunucu cagrisindan SONRA gelmeli (indeks karsilastirmasi) · cikista SEPETE DOKUNULMAZ.
- **P11** `KAYNAK_SOZLESMESI_CerceveGirisNoktalari_TANIMSIZ_FONKSIYON_CAGIRMAZ_ve_Olcutler_TEK_KAYNAK`
  MK-1'in pin karsiligi + DEVIR-3 tek kaynak + F-M3g sozlesmesi + **api-bridge'in
  kullandigi HER sozluk anahtari T VE AR'da bulunmali** + T/AR TAM ORTUSME (tireli
  anahtarlar DAHIL).

**UCU DE DURUST ETIKETLI KAYNAK SOZLESMESI PINIDIR**, davranis pini DEGILDIR - depoda
JS/DOM kosucusu YOK (Dalga 4'ten beri acik kalem). Davranis kaniti kontrollu A/B
tarayici + DB olcumleridir.

**DIS KONTROLU (TAM KAPSAMA, BES TUR):** P9 · P10 · P11 · premisi degisen P2 · premisi
degisen MisafirA3 -> **her turda TAM 1 ISIMLI KIRMIZI / 17 yesil**. Geri alindi, iz 0.

**5. KONTROL - BES URETIM MUTASYONU, her birinde TAM 1 ISIMLI KIRMIZI:**

| Mutasyon | Kirilan | Uretilen once-durum |
|---|---|---|
| M-P9 sosyal kaniti **FARKLI TANIMLAYICILARLA** geri koy | P9 | kusur SINIFI yakalandi |
| M-P10 sepet geri yuklemesine `byId` kapisini geri koy | P10 | katalogun gercek urunleri eleniyor |
| M-P10B favori toggle'ini misafirde YERELE dondur | P10 | cihaz-geneli favori |
| M-P11 `setAnnShip` TANIMINI kaldir, CAGRISINI birak | P11 | **MFIX-2 regresyonu BIREBIR** |
| M-DEVIR3 sekme basligini `indexOf("status=success")`e don | P11 | COD'da yanlis baslik |

Hepsi geri alindi; mutasyon izi dort dosyada da 0.

## YEREL DOGRULAMA

**333/333** `Category=Sql` · tam suitte **563 basarili / 566** (kirilan 3'un UCU DE
Docker'li `OrderEndpointTests` - yerelde Docker kapali, CI'da yesil) · Debug build
**0 Hata** · `dotnet format whitespace` ve `style --verify-no-changes` **exit 0**.

## KURGU KAYIT ENVANTERI (MFIX-3)

`musteri 79` ve `musteri 80` (GERCEK register/verify/login zinciriyle acildi) ·
`adres 46` (Trabzon, musteri 79) · `siparis 223` (COD, Confirmed, musteri 79) ·
`wishlist_items`: musteri 79 -> 947, 951, 954 | musteri 80 -> 953 ·
`cart_items` (79/80) 5 satir. MAX musteri 80 / adres 46 / siparis 223.
**Omer'in hesabi KULLANILMADI** (son siparis 211, adet 38 SABIT).
Mevcut Pending muhru (`status=0 AND id<=210`) **561429369 / 35 BIREBIR** korundu.

## MFIX-3b TANIMI (merkez kararlariyla, kuyruga)

**(a) `api-client.wishlist.toggle` SOZLESME DUZELTMESI.** Istemci GOVDE gonderiyor
(`{product_id}`), uc SORGU DIZESI bekliyor (`Toggle(int productId)`, `[FromBody]` YOK).
**CANLI KANIT: HTTP 500** (productId 0'a baglaniyor -> FK ihlali). MFIX-3'te api-client'a
dokunma yasagi vardi; dogru sozlesme api-bridge'ten cagrildi. Bu kalem istemciyi hizalar.

**(b) `variantsOf` UYDURMA RENK SECENEKLERI - ONCE OLCUM, SONRA KARAR.**
`rngOf(p.id*5153+77)` ile urune uydurma renk varyantlari uretiliyor.
**ZORUNLU ON OLCUM:** secilen renk **SIPARIS SATIRINA / DB'ye YAZILIYOR MU?**
Yaziliyorsa uydurma veri **MUSTERI KAYDINA** giriyor demektir ve kalem **AGIRLASIR**
(D-1 sinifina yaklasir). Olcumden SONRA: sokum ya da gercek-veriye baglama karari.

**(c) `toast()` IKON TIPI.** Bilesenin markup'inda SABIT onay isareti var; "giris
yapmalisin" gibi YONLENDIRME mesajlari da onay isaretiyle cikiyor. GOZ-FIX/O1 METNI
duzeltmisti, ikonu degil. `success` / `info` / `error` tipleri ayrilir.

**(d) KAMPANYA GERI SAYIMI OLCUMU.** Gece yarisina sayan geri sayimlar deterministik
(PRNG degil), bu yuzden MFIX-3'te "olay iddiasi" sayilmadi. **OLCULECEK: sure dolunca
indirim GERCEKTEN bitiyor mu?** Bitmiyorsa **SAHTE ACILIYET** -> sokum adayi.

**(e) i18n KALAN YUZEY.** api-bridge'te yorumsuz TR-karakterli kod satiri **221 -> 174**;
13'u `console.*` (gelistirici-gorunur, kapsam disi), geriye **161 KULLANICI-GORUNUR ADAY**
kaliyor. "Aday" cunku bir kismi ekrana CIKMAYAN dizgeler (`slugify` replace desenleri,
DB kategori etiketi yedegi). Kaba dagilim: form hata metni 15 · panel/blok basligi 11 ·
auth kutulari 11 · form placeholder 9 · bos durum metni 8 · misafir checkout paneli 7.

**(f) `enrichProduct` OLU DALI.** `if (d.image_url)` dali ULASILAMAZ - detay ucu
`image_url` DONDURMUYOR (canli alan listesi olculdu). Guard'li oldugu icin zarar YOK;
temizlik notu.

## KUYRUK

```
1. MFIX-B      [BACKEND] F-M1-H2 available DTO · gecersiz kupon siparis yolunda
               REDDEDILIR ya da GORUNUR UYARI · place yanitina order_number ·
               outbox Host-bos -> Failed+error
2. MFIX-3b     (a) wishlist.toggle sozlesmesi · (b) variantsOf ONCE OLCUM sonra karar ·
               (c) toast ikon tipi · (d) kampanya geri sayimi olcumu ·
               (e) i18n kalan 161 aday · (f) enrichProduct olu dali
3. FIX-1B      F4 + F8 zinciri, kara liste desenlesmesi, sentetik yazma pinleri
4. ADMIN-FIX
5. IMPORT-FIX  [KRITIK YOL - katalogda gercek urun 0; katalog gelisine gore ONE CEKILEBILIR]
6. FIX-1C      F5 · F6 · F7 · F9 · F13 · D-YAN dev-veri temizligi
7. LOG-FIX     bes ham log satiri -> KanitMaskesi
8. FIX-2       B-6 · C-1 · G5 · B-5 · D-3
9. FIX-3 / B13 kupon geri bildirimi · terk edilmis Pending TTL
```

**OMER'IN BIRLESIK DOGRULAMA TURU (12 madde) MUHUR YESILI SONRASI - KABUL KAPISI.**
Liste OMER'DE; CC kendi isini onaylayamaz.

**D-YAN TEMIZLIK LISTESINE EK:** MFIX-3'un kurgu kayitlari - musteri 79 ve 80, adres 46,
siparis 223, wishlist satirlari. MFIX-2'nin 78/45/221/222'si, MFIX-1'in 218-220'si ve
Dalga B'nin 213-217'siyle birlikte TEK temizlik isinde ele alinir.

---

