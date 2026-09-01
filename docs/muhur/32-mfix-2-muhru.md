# MFIX-2 MUHRU - VITRIN DURUSTLUGU ve STOK ISTEMCISI (27 Agustos 2026)

**KANIT SHA: `2432c36`** - her iki workflow yesil (`dd8857f..2432c36`).
Bu muhur AYRI ve docs-only bir commit; kendi cift yesili MFIX-2 raporunda.

```
MFIX-2 KODU (2432c36)
  CI - Build & Test  run 33089924837  event=push  head_sha=2432c36  SUCCESS
  Security CI        run 33089924956  event=push  head_sha=2432c36  SUCCESS
MUHUR COMMITI (docs-only) kendi turunda AYRICA cift yesil - run kimlikleri raporda
ALTI JOB'DA failure SEVIYELI ANNOTATION: 0   (39 annotation, HEPSI warning)
Gitleaks (secret taramasi): SUCCESS - bolum 7 kurali geregi ADIM SONUCUNDAN okundu
TestDbKurulum 1807 yeniden deneme ozeti: iki test job'inda da "HIC ATESLEMEDI (0) - retry devrede, gerekmedi"
```

**ORTAM UYARISI (Divisima Eki 2.3):** olcumler `goz1` duzeneginde kosuldu; `api-baslat.cmd`
BES arguman veriyor - `--Iyzico:UseRealSdk=false`, `--BackgroundJobs:Enabled=false`,
`--MailSettings:Host=`, `--AdminSeed:Enabled=false`, `--RateLimit:AuthPermitLimit=100`.
**Bunlar URUN VARSAYILANI DEGILDIR.** Odeme MOCK modda - "form donmedi" dali bu yuzden
tetiklenebiliyor ve replay olcumu tam onu kullaniyor.

**AJAN KISITI (durust kayit):** bu oturumda AgentTool cagrisi yasakti, dolayisiyla L1-L3
denetci ajanlari **DAGITILAMADI**. Disiplin adimlari ELDE uygulandi: on-kayit + karar
kriteri, append-only defter (HAM/SHA dogrulamali), [YOKLUK] negatif kontrolleri,
kontrollu A/B, TAM KAPSAMA dis kontrolu, 5. kontrol.

## KAPANAN BES KALEM - KONTROLLU A/B

Olcum yontemi her kalemde ayni: **ayni tarayici, ayni urunler, TEK degisken = surum.**
Yedek surum gecici olarak servis edilip olculdu, sonra yeni surum geri konup ayni olcum
tekrarlandi. Her turda SURUM DAMGASI kontrol edildi (bkz. KENDI HATALARIM #2).

### F-M9 - IKNA YUZEYLERI: UYDURMA SOKULDU, GERCEK VERIYE BAGLANDI

**SOKULENLER** (hepsi PRNG ya da sabit uydurmaydi, gercek karsiligi YOKTU):
"N kisi su an bu urune bakiyor" sayaci ve Math.random'la onu oynatan yurutucusu ·
kalip cubugu + model boyu/bedeni + "bir beden buyuk/kucuk al" onerisi ·
taksit satiri (`3 x fiyat/3`) · kumas/kalip/astar/bakim havuzlari ve uydurma urun kodu ·
sabit EU 36-46 beden tablosu ve o tabloya gore beden ONEREN hesap · "Yarin kargoda".

**GERCEK VERI ENVANTERI (0b) - karar kurali: alan VARSA ondan ciz, YOKSA satiri KALDIR:**

| Ne | Uc | Veri (bugun) | Karar |
|---|---|---|---|
| Kumas / ozellikler | **VAR** `GET /api/product-attribute/product/{id}` (ANONIM) | **0 satir** | uydurma havuz SOKULDU, blok gercek uca baglandi; bosken CIZILMIYOR |
| Beden tablosu | **VAR** `GET /api/size-guide/category/{id}` (ANONIM) | **0 satir** | sabit tablo SOKULDU; bossa urunun GERCEK bedenleri |
| Varsayilan adres sehri | **VAR** `GET /api/address` | 40 satir | teslimat buna baglandi |
| Beden basina `available` | **YOK** (`ProductStockDto` yalniz `size`+`stock_quantity`) | - | **MFIX-B (H2)** |

**A/B SONUCU** (urun 954 deri kemer + urun 937):

```
                        ONCE                              SONRA
sayac                   "12 kisi su an bu urune bakiyor"  YOK
kalip + model satiri    VAR                               YOK
taksit                  "veya 3 x 190 TL taksit"          YOK
kumas iddiasi           VAR                               YOK
"Yarin kargoda"         VAR                               YOK (gercek ucretsiz-kargo)
teslimat (MISAFIR)      "ISTANBUL icin tahmini teslimat:  "Tahmini teslimat: 2-4 is
                         28-31 Agu" + HIZLI TESLIMAT       gunu - sehrine gore degisir",
                         rozeti                            rozet YOK
teslimat (GIRISLI)      -                                 "TRABZON icin tahmini
                                                           teslimat: 31 Agu - 2 Eyl",
                                                           rozet YOK (kosul saglanmiyor)
cikistan sonra          -                                 sehir null, sehirsiz ifade
"senin bedenin"         VAR                               VAR   (KALSIN)
"Kolay Iade 14 gun"     VAR                               VAR   (KALSIN)
```

**MTUR'UN BIR BULGUSU CANLI DOGRULANDI:** aksesuar korumasi (`p.cat==='aksesuar'`) canli
slug `goz1-aksesuar` ile eslesmediginden OLUYDU - deri kemere kalip cubugu ve model
satiri geliyordu. Olcumde birebir gorulda.

**Teslimat tasarimi:** sehir YALNIZ girisli kullanicinin GERCEK varsayilan adresinden
gelir; bilinmiyorsa KESIN TARIH VERILMEZ ve "Hizli Teslimat" rozeti CIZILMEZ. Rozetin
kosullu oldugu ayrica kanitlandi - kurgu adres **Trabzon** (hizli sehir listesinde YOK)
ve girisliyken de rozet gelmedi. Cikista sehir temizleniyor (eski oturum sizmiyor).

### F-M6 - YILDIZ SATIRI KOSULLU

`"0.00 degerlendirme"` -> **bos/gri iskelet + "Henuz degerlendirilmedi"**. Sayi ve yorum
baglantisi YOK. Onceden hic yorumu olmayan urunde ust satir PUAN IDDIA EDIYOR, alt bolum
ise "yorum yok" diyordu. VITRIN-FIX-2'nin kart/cross-sell/karsilastirma korumalari (P3)
BOZULMADI - yildiz kaynagi HALA sunucunun `average_rating`/`review_count` alanlari.

### F-M7 - MODAL KARARTMAYA TIKLANINCA KAPANIYOR

`overlay.onclick` zaten `closeModal` cagiriyordu ama `#modal` katmani `#overlay`in
USTUNDE ve viewport'u kapliyor - kullanicinin "disari" sandigi yer `#modal`in KENDISI.
Depoda dort modalda (scmodal/returnModal/addrModal/cardModal) ZATEN kullanilan
`e.target===this` kalibi urun modalina da eklendi.

```
elementFromPoint(6,6) = id "modal"
  ONCE  tiklama sonrasi modal HALA ACIK
  SONRA modal KAPANDI, document.body.style.overflow BOSALDI (scroll kilidi cozuldu)
DORT KAPANIS YOLU DA AFTER'DA CALISIYOR: carpi · ESC · tarayici-geri · overlay
```

### F-M1-H3 - DETAY STOGU LISTEYI EZMIYOR + SIPARIS SONRASI TAZELEME

Kok sebep: `ProductStockDto` YALNIZ `stock_quantity` (FIZIKSEL) tasiyor; liste yolu ise
Sprint 8 madde 5'ten beri `total_stock`/`sizes` degerlerini `available` uzerinden
dolduruyor. Yani detayin toplami YANLIS, listenin toplami DOGRU - ve detay onu EZIYORDU.
Koddaki eski yorum ("liste yolunun 0 dondurdugu gercek toplam stok") o tarihten beri
BAYATTI.

```
urun 937 (DB: fiziksel 35 / rezerve 6 / SATILABILIR 29)
  ONCE   liste 29 -> detay acilinca 35   (EZILDI)
  SONRA  liste 29 -> detay acilinca 29   (ezilmedi)
Beden listesi de LISTENIN sozune uyar: tamamen rezerve beden detaydan gelse de eklenmez
(liste onu zaten disliyor - urun 932: total_stock 0, sizes []).

SIPARIS SONRASI TAZELEME (kurgu COD siparis 221, YENILEMESIZ)
  vitrin 29 -> 28    ·    DB toplam available 29 -> 28 (L bedeni 12 -> 11)   BIREBIR
```

**Tarayici onbellegi icin BIR SEY YAPILMASI GEREKMEDI - olculdu:** katalog ucu
`POST /api/product/filter`tir ve POST yanitlari onbelleklenmez; ETag'in
`private, max-age=60` basligi yalnizca GET detay ucunu etkiler, o da bosaltilan
`detailCache` yuzunden zaten yeniden istenir. En dar cozum: kendi onbellegimizi bosalt +
katalogu yeniden cek.

**DURUST SINIR:** beden **BASINA** ust sinir HALA FIZIKSEL - DTO'da `available` YOK.
Toplam artik dogru, beden bazi **MFIX-B (H2)**'de kapanir.

### MFIX-1 DEVRI - ERISILEMEZ MOCK ICERIK FONKSIYONLARI SOKULDU

MFIX-1 mock'u ERISILEMEZ kilmisti ama govdeler DURUYORDU (icinde CANLI KART FORMU ve
sunucuya HICBIR istek atmadan "Siparisin alindi" diyen `coFinish`). Silinmemelerinin tek
sebebi ADDR/CARDS ile ic ice olmalariydi; **0c haritasi o bagi cozdu** ve on bes fonksiyon
sokuldu. Korunanlar (baska yuzeylerde kullaniliyor): `cardBrand` / `brandLabel` /
`brandCls` / `luhnOk` / `fmtCardNo` / `fmtPhone` ve `var coStep`.

**ADDR/CARDS: TOHUMLAR BOSALTILDI, CIZICILER SILINMEDI.** Olculen zarar: ADDR IKI SAHTE
ADRESLE, CARDS IKI SAHTE KAYITLI KARTLA tohumluydu (degerler bilerek buraya YAZILMIYOR -
yorumun kendisi taramayi kirletir). api-bridge Hesabim'i tumden ezdigi icin bu ciziciler
YALNIZ api-bridge yuklenmezse calisir - ve o yolda kullaniciya sahte adres/kart
gosterirlerdi (MFIX-1'deki defer yarisiyla AYNI SINIF). Silmek `renderAccount`u
ReferenceError'a dusururdu; **bosaltmak DURUST BOS DURUMU gosterir** - MFIX-1'in ikinci
savunma hatti kalibi.

**[YOKLUK] ALTI TARAMA DA 0** (yorumlar ayiklanmis halde): rngOf ikna ureticileri · sayac ·
sabit beden tablosu · taksit · uydurma havuzlar · on bes mock fonksiyon adi.
**NEGATIF KONTROL:** `rngOf` 2, `SIZES_FOR` 11, `coStep` 2, `trustBlock` 2, `pdRateHTML` 2 -
tarama gercekten calisiyor.

## MFIX-1'IN ACIK UCU KAPANDI - TEK OLCUM

MFIX-1 raporu "buton `finally` davranisi korundu" iddiasini **OLCULMEDI** olarak
isaretlemisti (kaynakta dogru, davranis olculmemis). Bu turda OLCULDU:

```
kart yolu, mock modda "form donmedi"
  1. tik -> siparis DVS20260827-412122AF04 uretildi, buton disabled=FALSE geri dondu
  2. tik -> REPLAY mesaji: "Bu siparis zaten olusturulmustu (siparis no: ...).
            YENI bir siparis olusturulmadi. Odeme su an baslatilamiyor; ..."
  SONRASINDA: coSubmit.disabled = FALSE
              etiket "Siparisi tamamla" (geri donmus)
              getComputedStyle(...).pointerEvents = "auto"
              HIT-TEST: elementFromPoint(buton merkezi) = coSubmit'IN KENDISI
  -> BUTON GERCEKTEN TIKLANABILIR.
```

**YAN KAZANC:** replay mesajinin ON EKI korunmus (saglayici metni SONRA eklenmis) - yani
MFIX-1'in (a) yarisi ("replay mesaji ezilmiyor") IKINCI KEZ dogrulandi.

## PINLER - ve PIN PREMISI DEGISIKLIKLERI (MERKEZ ONAYLI)

`FrontendDokunmaHedefiTests` **13 -> 15 `[Fact]`** (SIFIR-DDL sinif; yeni veritabani
ACILMADI - `10d794d` dersi):
- **P7** `KAYNAK_SOZLESMESI_IknaYuzeyleri_PRNG_Uretilmez_ve_GercekVeriYoksaSatirYok`
- **P8** `KAYNAK_SOZLESMESI_DetayStogu_Listeyi_Ezmez_ve_SiparisSonrasiTazeleme`

**DURUST ETIKET: ikisi de KAYNAK SOZLESMESI pinidir**, davranis pini DEGILDIR (depoda
JS/DOM kosucusu yok - Dalga 4'ten beri acik kalem). Davranis kaniti yukaridaki A/B
olcumleridir.

### BILINCLI DEGISTIRILEN IKI PIN - MERKEZ ONAYLI

Ikisinde de **ASSERT DEGERLERI degil PREMIS** degisti; ikisinin de sebebi **EMREDILEN
SOKUM**:

1. **P5'in vakum kiricisi.** MFIX-1'de "mock uretici HALA govdede (sokum degil,
   erisilemezlik)" diyordu ve O GUN DOGRUYDU - uretici ADDR/CARDS ile ic ice oldugu icin
   silinememisti. MFIX-2'de merkez SOKUMU acikca emretti ve 0c haritasi bagi cozdu;
   eski assert bugun **SOKULMEMIS olmasini SAVUNURDU**. Yerine daha guclu iddia kondu:
   govde **URETIM IZI TASIMAZ** + govde **bos okunmus olamaz**.
2. **`HICBIR_YENI_EYLEM_HANDLERI_...` izinli listesi** `{giftChk, cmpDiffChk}` ->
   `{cmpDiffChk}`. `giftChk` MOCK CHECKOUT'un hediye paketi adimindaydi ve sokuldu.
   **KURAL DEGISMEDI** (kati `e.target.id` yalniz change-olayli checkbox'ta guvenli);
   liste, mesru bir uyesi kaldirildigi icin daraldi.

**KALICI KURAL NOTU:** bir pinin PREMISI degistiginde bu **HER ZAMAN** raporda gerekceli
olarak yazilir ve muhurde **merkez onayiyla** kayda gecer. Assert degerini degistirmeden
premisi sessizce kaydirmak, pini yalanci yesile cevirmenin en sinsi yoludur.

## DIS KONTROLU + 5. KONTROL

**DIS (TAM KAPSAMA, orneklem YOK):** P7 -> **TAM 1 ISIMLI KIRMIZI** (14 yesil);
P8 -> **TAM 1 ISIMLI KIRMIZI** (14 yesil). Her turda YENIDEN DERLEME; flip'in dosyaya
indigi grep ile dogrulandi; geri alindi (iz 0).

**5. KONTROL:**
- **M-P7** (fit uretici geri kondu) -> **TAM 1 ISIMLI KIRMIZI**, 14 yesil - LOKALIZE.
- **M-P8 *** BIR PIN ZAAFI YAKALADI - DORDUNCU VAKA *** ** Stok ezmesi **FARKLI BIR
  BICIMDE** geri kondu (reduce ile toplam) ve P8 **KIRMIZI VERMEDI**. Kuralin (a) ve (b)
  adimlari once kosuldu: mutasyon dosyaya **INDI**, build **0 Hata** -> yani "mutasyon
  uygulanmadi" DEGIL, **PIN ZAYIFTI**: assert **ESKI LITERAL BICIMI** ariyordu, KUSUR
  SINIFINI degil. Pin **KACISSIZ ve BICIMDEN BAGIMSIZ** hale getirildi (govdeden bosluk
  ayiklanip duz dizge araniyor) ve mutasyon TEKRARLANDI -> **TAM 1 ISIMLI KIRMIZI**.
  Pin artik farkli bicimde yazilmis AYNI KUSURU da yakaliyor.

**KURAL NOTU (ikinci sahada isleyisi):** "kirmizi yok -> ONCE pin suphesi" refleksi
MFIX-1'de yazilmisti; MFIX-2'de **ikinci kez** is gordu. Sira: (1) mutasyon dosyaya indi
mi, (2) build temiz mi, (3) **PIN yeterince keskin mi**. Ucuncusu atlanirsa zayif bir pin
"lokalize" diye RAPORLANIR ve koruma sanilan sey aslinda YOKTUR. Bu, 5. kontrolun bir
pini eledigi **DORDUNCU** vakadir (oncekiler D2, FIX-1A, MFIX-1).

**KACIS-KAYBI AILESI - DORDUNCU ORNEK.** P8'in ILK dis turu `perl` ile yapilmisti ve perl
HEM flip'i koyarken HEM geri alirken regex'in ters bolularini YEDI; assert hicbir seyle
eslesmeyen bir desene dondu. O turda gorulen kirmizi **GERCEKTI ama YANLIS SEBEPTENDI**,
dolayisiyla tur **GECERSIZ** sayildi: regex TUMDEN kaldirildi (kacissiz cozum) ve hem dis
hem mutasyon turu **Edit araciyla TEKRARLANDI**. Ailenin onceki uyeleri: heredoc'ta
ters bolu dususu, `printf`'te satir sonu kacisi, guard'a gomulen regex.

## YEREL DOGRULAMA

**333/333** `Category=Sql` · tam suitte **560 basarili / 563** (beklenti 558->560 **BIREBIR
tuttu**; kirilan 3'un UCU DE Docker'li `OrderEndpointTests` - yerelde Docker kapali, CI'da
yesil) · Debug build **0 Hata** · `dotnet format whitespace` ve `style --verify-no-changes`
**exit 0**.

## KAPSAM DISI UC YENI BULGU - OLCULDU, DUZELTILMEDI

Kapsam SABITTI; ucu de ayni sekilde raporlandi ve karar merkeze birakildi.

1. **[DURUSTLUK] SOSYAL KANIT BILDIRIMI UYDURMA SATIN ALMA ILAN EDIYOR**
   (`index.html:3072-3084`): on dort uydurma isim, on iki sehir, uydurma dakika ve
   **YESIL ONAY ISARETIYLE** "X bu urunu satin aldi - N dk once". Ilk gosterim 25 sn
   sonra, sonra 90-150 sn'de bir; urun `Math.random` ile seciliyor.
   **D-1 (sahte yorumlar, LAUNCH BLOKERI) ile AYNI SINIF - hatta daha agir: yorum bir
   GORUS, bu bir OLAY IDDIASIDIR.** `rngOf` DEGIL `Math.random` kullandigi icin P7'nin
   taramasi bunu YAKALAMAZ.
2. **[DURUSTLUK] `MOCK_ORDERS` HALA TOHUMLU** (`index.html:2696`): uydurma siparis
   numaralari, tarihler, durumlar. Tuketicileri `accOrders` ve `openReturn`; ikisi de
   api-bridge'in ezdigi `renderAccount`tan cagriliyor - ADDR/CARDS ile **AYNI SINIF**.
   Ayni tek satirlik tedavi uygulanabilirdi; merkez YALNIZ ADDR/CARDS dedigi icin
   TUTARLILIK adina dokunulmadi.
3. **[DURUSTLUK/UX] BASARILI COD SIPARISTE SEKME BASLIGI "Odeme Tamamlanamadi" DIYOR.**
   Canli olculdu (siparis 221, `status=cod`): ekran "Siparisin alindi" derken
   `document.title` TERSINI soyluyor. Kok sebep: `api-bridge.js:2663` basarili sayma
   olcutu YALNIZ `status=success` ariyor, oysa AYNI DOSYADA `renderPaymentResult`
   (`:1784`) `success` **VEYA** `cod` diyor - iki kod yolu "basarili"yi FARKLI
   tanimliyor. Dalga 1 / B9 duzeltmesinde girmis. TEK KOSULLA duzelir.

## KURGU KAYIT ENVANTERI (MFIX-2)

`musteri 78` (kurgu hesap, dogrulanmis) · `adres 45` (Trabzon, musteri 78) ·
`siparis 221` (COD, Confirmed, 1649.80 - tazeleme olcumunun fixture'i) ·
`siparis 222` (Online, Pending - mock modda odeme formu donmedigi icin odenmemis kaldi;
replay olcumunun fixture'i). `max_musteri` 78 · `max_adres` 45 · `max_order` 222.
**Omer'in hesabi (musteri 10) KULLANILMADI**; degerleri SABIT (son siparis 211, adet 38).
Mevcut Pending muhru **561429369 / 35 BIREBIR** korundu.

## KUYRUK GUNCELLEMESI

**MFIX-2 KAPANDI.** MFIX-3'un kapsami **UC YENI KALEMLE BASA GENISLEDI** (bu dalganin
kapsam disi bulgulari):

```
1. MFIX-3   (a) SOSYAL KANIT BILDIRIMI SOKUMU - index.html:3072-3084, uydurma
                satin-alma iddialari [LAUNCH BLOKERI SINIFI, D-1'den AGIR: olay
                iddiasi]. Math.random kullandigi icin P7 taramasi YAKALAMIYOR ->
                MFIX-3'te [YOKLUK] + PIN GENISLETMESI gerekir.
            (b) MOCK_ORDERS tohumu bosaltilir (ADDR/CARDS tedavisi, index.html:2696)
            (c) sekme basligi success|cod kosulu (api-bridge.js:2663 <-> :1784 uyumu)
            + KALAN KAPSAM DEGISMEDI: F-M4 (misafir sepeti) · F-M5 (hesaba-ozgu
              favori) · F-M2 (api-bridge bypass'i sozluge + AR 2 anahtar) ·
              F-M3g (istemci query duzeltmesi)
2. MFIX-B   [BACKEND] F-M1-H2 available DTO · gecersiz kupon siparis yolunda
            REDDEDILIR ya da GORUNUR UYARI · place yanitina order_number ·
            outbox Host-bos -> Failed+error
3. FIX-1B   F4 + F8 zinciri, kara liste desenlesmesi, sentetik yazma pinleri
4. ADMIN-FIX
5. IMPORT-FIX   [KRITIK YOL - katalogda gercek urun 0]
6. FIX-1C   F5 · F6 · F7 · F9 · F13 · D-YAN dev-veri temizligi
7. LOG-FIX  bes ham log satiri -> KanitMaskesi
8. FIX-2    B-6 · C-1 · G5 · B-5 · D-3
9. FIX-3 / B13   kupon geri bildirimi · terk edilmis Pending TTL
```

**D-YAN TEMIZLIK LISTESINE EK:** MFIX-2'nin kurgu kayitlari - musteri 78, adres 45,
siparisler 221 ve 222. MFIX-1'in 218-220'si ve Dalga B'nin 213-217'siyle birlikte TEK
temizlik isinde ele alinir.

---

