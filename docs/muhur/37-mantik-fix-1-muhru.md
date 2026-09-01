# MANTIK-FIX-1 MUHRU - ETKIN FIYAT / MAGAZA KREDISI / KUPON TAZELEME (29 Agustos 2026)

**KOD SHA: `b9c9ff0`** (zemin `4d8d4c2`; IKI commit TEK push: `f0f27dc` = K1-K6, `b9c9ff0` =
denetim duzeltmeleri). Bu muhur AYRI ve docs-only bir commit'tir; **kendi SHA'sini ve kendi
run kimliklerini ICEREMEZ** (tavuk-yumurta) - muhrun kendi cift yesili MANTIK-FIX-1
raporunda verilir. MFIX-1'de kurulan kalip.

```
MANTIK-FIX-1 KODU (4d8d4c2..b9c9ff0, IKI commit tek push)
  CI - Build & Test  run 33213028838  event=push  head_sha=b9c9ff0  SUCCESS
  Security CI        run 33213028751  event=push  head_sha=b9c9ff0  SUCCESS
ALTI JOB'DA failure SEVIYELI ANNOTATION: 0   (39 annotation, HEPSI warning)
ANNOTATION TABAN 39 <-> OLCULEN 39 (BIREBIR). Aile: nullable 30
  (IEntityRepository.cs 24 + EfEntityRepositoryBase.cs 6) + .github 9.
  **DIFF KESISIMI 0** - uc benzersiz annotation yolunun hicbiri push diff'inin 12
  dosyasinda YOK; kesisim komutu POZITIF KONTROLDE 1 donuyor (bilerek eslesen yol
  enjekte edildi), yani komut calisiyor. **+875 satir sunucu/DTO koduna ragmen YENI
  UYARI URETILMEDI** - dosya:satir duzeyine inmeye gerek kalmadi.
format-check UC ZORUNLU ADIM (whitespace + style + migration SENKRON): UCU DE SUCCESS
  -> "migration GEREKMEZ" iddiasinin CI kaniti budur.
Gitleaks (secret taramasi): SUCCESS - bolum 7 kurali geregi ADIM SONUCUNDAN okundu
TestDbKurulum 1807 ozeti (iki test job'inda da): "HIC ATESLEMEDI (0) - retry devrede,
  gerekmedi." · TESHIS adimi iki job'da da skipped
```

**ORTAM UYARISI (Divisima Eki 2.3):** olcumler `goz1` duzeneginde kosuldu; API surecinin
komut satirinda BES ARGUMAN var - `--Iyzico:UseRealSdk=false` · `--AdminSeed:Enabled=false` ·
`--BackgroundJobs:Enabled=false` · `--RateLimit:AuthPermitLimit=100` · `--MailSettings:Host=`.
**Bunlar URUN VARSAYILANI DEGILDIR.** Bu turda bir rig artefakti bulgu sanilmadi: K3'te
`coupon_usages` 0 kaldi cunku kullanim satirini onay olayinin isleyicisi yazar ve rig'de
arka plan isleri KAPALI.

## KESINTI ENVANTERI (dalga ON-OLCUMDE kesildi, DISKTEN kurtarildi)

Kesinti aninda **kod izi YOKTU** (calisma agaci 0 satir, stash 0) - kesinti on-olcum
fazindaydi. Kurtarma dort adimda olculdu, tahmin edilmedi:

- **DEFTER BUTUNLUK BOTU BIR KUSUR BULDU:** `a1-rapor.txt` ve `a4-rapor.txt` **0 BAYT**,
  kayitli SHA'lari (`da39a3ee5e6b`) BOS DOSYA SHA-1'inin ta kendisiydi. Yani alti defter
  satirinin HAM DAYANAGI YOKTU. **KOK SEBEP OLCULDU (ortam, urun degil):** ajan cikti
  dosyalarinin (`a*.output`) TAMAMI 0 bayt - 13/13; **NEGATIF KONTROL:** ayni dizindeki
  `b*.output` dosyalari 842 KB'a kadar DOLU. MFIX-3b'nin MK-4 turunda kaydedilen
  "transkript 0 bayt" olgusunun AYNISI.
- **SDP geregi o satirlar DAYANAKSIZ sayildi ve DORT AJANIN DORDU DE BASTAN KOSULDU**
  (SUPERSEDES ile, satir SILINMEDEN).
- **EKSIK ONCE OLCUMLERI TAMAMLANDI:** R-M4 (kupon kuculme) HIC YOKTU; R-M1b'nin C/D
  bacaklari yarimdi; R-M2/R-M3/R-M5 yoktu. Yedi kanit formatinin ONCE durumu fix'e
  baslamadan TAMAMLANDI.
- **KESINTI ONCESI URETILEN KURGU ENVANTERE ALINDI** (musteri 102-113, siparis 253-259,
  adres 65-73) - ajanlar rapor donduremeden oldu ama URETIM UCLARINDAN yazmislardi.

## UC DERIN-DUSUNME KARARI (D1 / D2 / D3)

**D2 - ETKIN FIYAT (secilen: DTO alani + istemcide TEK SINIR NOKTASI).**
Alti `ProductListResponseDto` ureticisi (`ProductManager` :433/:466 · `CollectionManager`
:158 · `SearchManager` :107/:128 · `WishlistManager` :72) TEK `CreateMap`'ten geciyor;
`ForMember` ALTISINI birden kapatir. **Zenginlestirici yolu ELENDI (A2/B1):**
`ListeyiZenginlestirAsync` PRIVATE ve yalniz iki yerden cagriliyor - hesaplanmis alan oraya
konsaydi **favoriler, arama ve koleksiyon "0,00 TL" gosterirdi**. Istemcide `mapProduct`
(api-bridge.js) TEK SINIR NOKTASI: **36 dogrudan `.price` okuyucusu** onun yazdigini
tuketiyor, yani tek yerde normalize edilince 36 tuketici DEGISMEDEN dogru olur.

**PLAN SAPMASI (merkez onayli): alan adi `sale_price` DEGIL `effective_price`.**
Gerekce KAYNAKTAN okundu: `ProductDetailResponseDto.cs:17-27` zaten `sale_price` tasiyor ve
alanin KENDI YORUMU admin formunun onu GERI YAZDIGINI soyluyor. Istemci alani artik
OKUYACAGI icin admin (HAM deger) ile vitrin (PENCERE FARKINDA deger) AYRI alanlardan
beslenmelidir - ayni alana iki anlam yuklemek Dalga B'nin "bir alanin iki anlami" sinifidir.
**A2'nin EKSEN-1 olcumu bu karari sayilarla destekledi:** `price`'i sunucuda etkin degere
cevirmek `ProductManager.cs:211` `sale_price >= price` kontrolune carpar ve **8 indirimli
urunun HICBIRI admin panelinden duzenlenemez** (HTTP 400); alternatifi ise liste fiyatinin
KALICI asagi kaymasi olurdu. **Admin kapisinin HIC ACILMAMASI bu yolun ASIL DEGERI.**

**D1 - MAGAZA KREDISI (secilen: K2-A, gorunur satir; total_price semantigi AYNEN KALIR).**
K2-B (total'i NET'e cevirmek) `OrderCancellationMoneyTests.cs:283-284` "MUHASEBE KIMLIGI"
pinini KIRAR **ve daha kotusu** `PaymentRefundTests.cs:20`'yi YESIL BIRAKARAK uretimi
tersine cevirir: tam-cuzdan sipariste `total_price=0` -> `SplitRefund` sifira-bolme yedegi
-> **TUM IADE OLMAYAN KARTA gider**; unit pin girdiyi kendisi verdigi icin bunu GORMEZ.
Yedi uretim noktasi, ALTI PINSIZ invariant, UCU `[PARA]`. Olculen zarar bir GORUNURLUK
kusurudur ve gorunur satirla kapanir. **K2-B AYRI DALGA.**

**D3 - KUPON TAZELEME (secilen: sepet IMZASINA bagli sunucuya yeniden sorma + TEK durum).**
Yalniz `min_amount` eklemek YETMEZDI: R-M4'te bayatlayan sey TUTAR'di (gosterilen 159,96,
dogrusu 31,99 - **BES KAT**). Bugunku istemci guard'i YAPISAL OLARAK OLUYDU
(`coupon.min` sabit 0). Sessiz dusurme, MFIX-B/K2'nin sunucuda kapattigi kusurun ikizi
olurdu. Imzaya baglama MFIX-3b/T1 zararini kupon ekseninde tekrarlamamak icin.

## ALTI KALEM - KONTROLLU A/B (ONCE = olcum turunun NUMARALI REPRO bloklari)

**K1 - ETKIN FIYAT UCTAN UCA.** Ekran <-> tahsilat farki **575,00 TL -> 0,00 TL**.
```
liste     499,90 -> 374,92 + ustu cizili 499,90 + rozet -%25
detay     499,90 -> 374,92
sepet     2.499,50 + "kargo kazandin" -> 1.874,60 + "125,40 TL kaldi"
checkout  2.499,50 -> Ara 1.874,60 / Kargo 49,90 / Toplam 1.924,50
DB        siparis 260 total_price 1.924,50 = ONCE'ki siparis 257'nin SUNUCU degeri
```
**YAN KAZANC BEDAVA KAPANDI:** A1'in "8 indirimli urunun 7'sinde `old_price` BOS -> musteri
hicbir indirim isareti GORMUYOR" bulgusu, `old` alani sinirda turetildigi icin kendiliginden
duzeldi. **R-M1b UCLUSU:** esik ALTINDA (siparis 257) TAM · **TAM SINIR (2.000,00)
ULASILAMAZ** - ampirik tam arama 0 kombinasyon (<=9 kalem) + moduler kanit; en yakin alt
1.999,80 / ust 2.000,66, bu yuzden **merkez onayiyla PIN duzeyine cekildi** · USTUNDE
(935 x2) TAM.

**K2-A - MAGAZA KREDISI GORUNUR.** DTO'ya `store_credit_used` eklendi; **AutoMapper
KONVANSIYONU esledi, ek `ForMember` GEREKMEDI**.
```
ONCE   checkout 849,80  <->  sonuc ekrani 949,80   (kredi satiri YOK)
SONRA  checkout Toplam 489,74  <->  sonuc "Kalan odeme" 489,74   BIREBIR
       sonuc ekrani ayrica: Toplam 689,74 + "Magaza kredisiyle odenen -200,00 TL"
DB     siparis 261: 639,84 + 49,90 = 689,74 · kredi 200,00 · bakiye 200,00 -> 0,00
```
Kurgu URETIM YOLUYLA kuruldu (gift-card create + redeem) - elle SQL ile kredi YAZILMADI.

**K3 - MISAFIR KUPONU.** Misafir govdesindeki sabit `coupon_code: ""` TEK KAYNAGA baglandi
ve misafir ozetine indirim satiri eklendi - **indirim cekmecenin kullandigi TEK KAYNAKTAN
hesaplanir, ikinci kupon matematigi ACILMADI**.
```
ONCE   misafir paneli kupon satiri TASIMIYOR, Toplam 509,80
SONRA  "Kupon indirimi -137,97" + Toplam 371,83
DB     siparis 262: subtotal 459,90 · indirim 137,97 · DALGAB30 · kargo 49,90 · 371,83
ZORUNLU BACAK canli: gecersiz kupon -> 400 "Gecersiz kupon kodu.", siparis OLUSMADI
```
Yan duzeltme: uye ozetindeki SABIT TURKCE "Kupon indirimi" ayni sozluk anahtarina baglandi
(AR/EN kullanici Turkce goruyordu).

**K4 - KUPON TAZELEME.** Sunucu `min_amount` doner (istemci IKINCI bir kural yazmaz);
istemci uc parca: imza kapisi + durum tekillestirme + min'in SUNUCUDAN alinmasi.
```
(1) TUTAR  sepet 3->2 kuculunce -413,91 -> -275,94; Toplam 643,86 = 919,80-275,94 BIREBIR
(2) MIN    bozulunca kupon kaldirildi + GORUNUR CEVIRILI toast tip=err
(3) IMZA   kucultmede 1 istek; ardindan IKI dil degisiminde HALA 1
           -> salt-cizim yollari istek URETMIYOR (MFIX-3b/T1 tuzagi TEKRARLANMADI)
```
**UYGULAMADA OLCUMLE BULUNAN IKI ARA DUZELTME:** (a) `min_amount` gelince index.html'in olu
guard'i CANLANDI ama **SESSIZ** calisiyordu -> guard SARMALANDI (KARAR guard'da, KONUSMA
sarmalayicida; kullanicinin KENDI "Kaldir" tiklamasi bu yoldan GECMEZ, yanlis alarm olmaz);
(b) tazeleme durumu dogru guncelliyor ama CEKMECEYI cizmiyordu - `srvAmount` 275,94'e
dondugu halde ekran HALA -413,91 yaziyordu, yani **R-M4 zarari YARIM kapanirdi**.

**K5 - BULTEN VAADI.** Dort anahtar (`nl_title`, `nl_sub`, `nl_btn`, `nl_done_s`) x uc dil
+ dort satir ici HTML varsayilani. Olcum **`applyI18n()` CAGRILDIKTAN SONRA** yapildi cunku
MFIX-1'in ilk duzeltmesini ETKISIZ KILAN sey tam o daldi ("yapilmis gorunup calismayan
duzeltme" ailesi, kaynagi KENDI onceki dalgam). SONRA: uc dilde "10%"/"%10" eslesmesi **0**.
VAKUM KIRICI: pencere SILINMEDI, `nlModal` ve anahtarlar YERINDE.

**K6 - KARGO ESIGI METNI.** **PREMIS ONCE DOGRULANDI:** karsilastirma HER YERDE ZATEN `>=`
(`OrderManager:293`, `api-bridge:1534/:1695`, `index.html:2606`) -> **SAF METIN ISI, DAVRANIS
RISKI YOK**. SONRA: tr "2.000 TL ve uzeri" · en "of TL2,000 and above" · ar
"بقيمة ₺2,000 فأكثر"; kalan "over"/"فوق" eslesmesi **0**. CIFT-ANLAM KIRICI KORUNDU:
`ann_free_ship` esigi HALA `{tutar}` ile parametrik.
**KAPSAM NOTU:** merkez `ben_ship_s` + uc anahtar demisti; olcumde `sh_ship_txt` ve
`ann_free_ship`'in EN/AR karsiliklarinin da "over"/"فوق" dedigi gorulup ayni semantige
cekildi - aksi halde AYNI URUNDE IKI SEMANTIK kalirdi.

## MF-2 ONCESI BILINCLI ARA DURUM (merkez sarti geregi ACIKCA)

**SART-2'NIN ONCULU OLCUMLE DUZELTILDI - MERKEZ KABUL ETTI.** Sart "K2-A sonrasi yeni
siparislerin faturasi NET toplamdan uretilecek" diyordu; olculdu: `InvoiceManager.cs:76`
`order.total_price` (**BRUT**) kullaniyor ve K2-A o semantige DOKUNMADI.
```
Kredi tasiyan DORT TARIHSEL SIPARISIN GERCEK faturalari (invoices 81-84):
  invoices.total 949,80 = orders.total_price · matrah 863,45 + KDV 86,35 = 949,80
MATRAH / KALEM FARKI: YOK
```
Kredi bir **ODEME ARACIDIR**, fiyat indirimi degil - matrahi dusurmez;
`OrderManager.cs:578` fatura HTML'inde zaten ayri bir odeme satiri var.
**InvoiceManager KODUNA DOKUNULMADI (sart aynen korundu).**
**ASIL RISK MF-2'DE:** K2-B `total_price`'i NET'e cevirirse `:76` onu **SESSIZCE takip eder
ve KDV EKSIK BEYAN EDILIR**. Bugun boyle bir pin YOK.

## PINLER - ALTI YENI, HEPSI KIRMIZI-ONCE KANITLI

| Pin | Sinif | Ne tutar |
|---|---|---|
| **P18** | `StorefrontCatalogContractTests` | liste ETKIN fiyati doner ve DETAYLA AYRISMAZ |
| **P19** | `FrontendDokunmaHedefiTests` | istemci sinirinda etkin fiyat normalizasyonu |
| **P20** | `ResultOverloadPinTests` | siparis detayi krediyi doner, TOTAL SEMANTIGI DEGISMEZ |
| **P21** | `MisafirCheckoutTests` | misafir kuponu sunucuya TASINIR, gecersiz kupon 400 |
| **P22** | `FrontendDokunmaHedefiTests` | TEK kupon durumu + imza kapili tazeleme + `ulasildi` ayrimi |
| **P23** | `FrontendDokunmaHedefiTests` | dayanaksiz vaat YOK (rozet DAHIL) + esik metni |

**PREMIS DEGISIKLIGI (merkez onayli):** `StorefrontCatalogContractTests` tohumuna IKI URUN
eklendi (pencere ACIK + pencere KAPALI). Gerekce olculdu: kaynakta `sale_price` ATAYAN tohum
**0**'di (11 gecisin 6'si ikili dosya, 1 assert, 1 yorum, 3 CSV basligi) - **NEGATIF KONTROL**
`color_hex` 68 gecis / 40 atama. Tohum genisletilmeden K1'in davranis pini **VAKUM** olurdu.
MFIX-B/K1'in `7/0 -> 10/3` emsali.

**DIS KONTROLU (TAM KAPSAMA, orneklem YOK):** alti pinin her birinde bir assert ters
cevrildi -> her turda **TAM 1 ISIMLI KIRMIZI**. Geri alindi, flip izi 0.

**5. KONTROL:** her kalem icin uretim mutasyonu; her birinde (a) mutasyonun dosyaya indigi
grep ile, (b) TEMIZ BUILD hata sayisiyla dogrulandi, (c) kirmizi cikmadiginda ONCE
"mutasyon uygulanmadi" suphesi elendi. Hepsi geri alindi, iz 0.

**KACIS-KAYBI AILESI - ALTINCI ORNEK:** P22'de `"\s+"` heredoc'ta `"\s+"`ya indi ve C#
**CS1009** verdi; `sed`/`perl` duzeltmeleri de ayni kacisi yedi. **KACISSIZ COZUME gecildi**
(regex yerine duz `Replace` zinciri). Kayitli dersin bir kez daha dogrulanmasi.

## DENETIM (MK-4) - UC DENETCI, IKISI GERCEK KUSUR BULDU

**CELISKI AVCISI - DORT AKTIF/LATENT KUSUR + UC BAYAT YORUM, DORDU DE ANA AKISIN KENDI
ISINDE.** Dordu de bagimsiz dogrulandi, duzeltildi ve A/B ile olculdu:
- **C1** misafir ozeti KARGO-BEDAVA kuponunu YOK SAYIYORDU; uye paneli kontrolu YAPIYORDU -
  **dalganin KENDI ICINDE asimetri**, K3'un erisilebilir kildigi. A/B: ekran 509,80 -> 459,90;
  DB siparis 263 `shipping_cost=0.00` `total=459.90` BIREBIR.
- **C2 "TEK KUPON DURUMU" IDDIAM YANLISTI:** cekmeceden "Kaldir" paneli temizlemiyordu **ve**
  panelin kendi uygulama yolu YALNIZ `checkoutState`'e yaziyordu -> **panelden uygulanan kupon
  tazeleme kapsamina HIC girmiyordu, R-M4 yeniden ureiliyordu.**
- **C3** K5 METIN vaadini kaldirdi, **GORSEL** vaadi birakti: 58px `%10` rozeti.
- **C5** AG HATASI "kupon artik gecersiz" diye raporlaniyordu - gecici kesintide **GECERLI**
  kupon dusuruluyor ve sebep YANLIS soyleniyordu. `{ulasildi, gecerli, veri}` ayrimi geldi
  (4xx = sunucu karar verdi -> kaldir; 0/5xx = ulasilamadi -> DOKUNMA). **P22 GUCLENDI.**

**L3 CIFT-KOR - BES ONAY + BIR ITIRAZ, ve IKI PIN BOSLUGUNU URETIM MUTASYONUYLA GOSTERDI:**
- **P19 BEDAVA DOGRUYDU (AGIR):** `Contain("effective_price")` dizgesi `mapProduct` govdesinde
  DORT satirda geciyordu. L3 `price` alanini K1 ONCESINE dondurdu ve **TUM SUIT 575/578 ile
  TEMIZ DURUMLA BIREBIR AYNI kaldi** -> K1'in ISTEMCI YARISI PINSIZDI. Assert ALAN BAZLI
  yapildi, mutasyon TEKRARLANDI -> **TAM 1 ISIMLI KIRMIZI**.
- **P23 kapsami DARDI:** L3 rozeti "%10 INDIRIM" yapip vaadi BUYUTTU, P23 YESIL kaldi
  (karsit kontrol kirmizi verdi -> pin kendi kapsaminda calisiyordu). Rozet ADIYLA taranir
  hale getirildi, mutasyon TEKRARLANDI -> KIRMIZI.
- **BULGU ORTUSMESI:** L3'un tek itirazi (K5 rozeti) celiski avcisiyla AYNI bulguydu -
  **IKI BAGIMSIZ KANAL**.

**KURAL-UYUM - M1-M8 UYUMLU, M9 UYUMSUZ (sebep BENIM DAGITIM HATAM).**
M6'da **URETIM IMZASI** kullanildi: 10 siparisin 10'unda kalem + rezervasyon + tarihce +
fatura zinciri var, siparis numarasi bicimi 10/10 uyuyor, `user_type=1` TAM 1 hesap -
elle bir `INSERT` bu zinciri URETMEZ. M7'de muhurler **ICERIK** duzeyinde dogrulandi (kabul
turu kayitlari SAAT DAMGASINA kadar). M8: commit'te ve 749 eklenen satirda sir **SIFIR**;
tek sapma scratchpad'deki ciplak JWT tasiyan olcum dosyasiydi - **SILINDI** (bolum 1: olcum
araclari YAZMA ANINDA maskeler).

## CC'NIN HATALARI

**DALGA ICI: 9** - (1) uc denetciyi AYNI worktree'ye gonderdim (M9'un sebebi) · (2) "TEK
KUPON DURUMU" iddiam yanlisti (C2) · (3) P19'u bedava-dogru yazdim · (4) P23'un kapsamini dar
tuttum · (5) "40 okuma" saydim, dogrusu **36** (onceki sayim tanimin kendisini ve ilgisiz
okumalari sayiyordu) · (6) "eski guvenlik uyarisi bu dalgada karsilandi" iddiasini GERI
CEKTIM (kosulu gerceklesmedi) · (7) `index.html:2117` bayat yorumu · (8) **SUPERSEDES
[A4][3]:** "`ship_s` sozlukte MUKERRER" dedim - **CURUDU**; `ship_s:` deseni `ben_ship_s`
ve `tr_ship_s` ICINDE de esliyor, ankrajli `[,{]ship_s:` -> **0**. Desen bilinen-NEGATIF
girdiyle SINANMAMISTI (SDP 1.7/1 ihlali); A2 hakliydi · (9) `--no-build`/bayat-ikili
tuzagina yeniden dusme riski her mutasyon turunda temiz build ile elendi.

**PUSH+MUHUR TURU: 5** (merkez ek tarifi **4** sayiyordu; **5.si EK GELDIKTEN SONRA olustu**,
sapma raporlandi). Besi de KARAR ONCESI yakalandi:
1. **Run sayaci ic ice nesneleri sayiyordu** - `^      "id":` deseni `jobs` nesnelerini de
   yakaliyordu; bilinen-negatif girdi 0 yerine **1** dondu. `run_number` alanina cevrildi.
2. **ASCII ozet filtresi** - `Basarili!|Basarisiz!` yazilmisti, ham cikti Turkce
   (`Başarısız! - Başarısız: 3, Başarılı: 575 ... Toplam: 578`); **HICBIR SEY eslesmedi** ve
   tur 1'in sayilari BOS geldi. **ASCII/Turkce yuklem ailesinin 3. vakasi** (ilk kayit
   `0655178`) - ders CLAUDE.md'de KAYITLIYKEN yinelenen dusus. `Toplam:` capasina cevrildi.
3. **awk cikaricinin ic ice JSON'da bozulmasi** - depo id'sini ve aktor adini run alani
   saniyordu; **karar icin HIC kullanilmadi**, EMEKLI edildi.
4. **Bir negatif kontrolun YANLIS DOSYADA kosulup "TUTAR" yazilmasi** - kendisi yakalandi,
   dogru dosyalarla TEKRARLANDI (ucu de dogru sekilde TUTMAZ dondu).
5. **Annotation ucunu TAHMIN ETTIM:** `/actions/jobs/{id}/annotations` -> **HTTP 404**, ve
   naif okuma **"0 annotation"** derdi (taban 39 iken). Dogru yol **CLAUDE.md bolum 1'de
   ZATEN YAZILI**: `/check-runs/{id}/annotations`. Yakalatan sey sayinin tabanla celismesiydi.
   **SDP 1.7/2 ihlali** - rota KAYNAKTAN okunur, tahmin edilmez.

## KALICI KURALLAR (bu dalgadan)

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

**MK-5 - HER ON-OLCUM AJANI RAPORUNU KENDI HAM DOSYASINA YAZAR.** Harness'in cikti dosyasina
GUVENILMEZ ve boyutunun 0 olmadigi ajan tarafindan DOGRULANIR. Gerekce OLCULDU: bu dalganin
kesintisinde ajan cikti dosyalarinin **13/13'u 0 bayt** cikti (negatif kontrol: ayni dizinde
`b*.output` 842 KB'a kadar dolu) ve alti defter satiri DAYANAKSIZ kaldi; MFIX-3b'nin MK-4
turunda **AYNI olgu** yasanmisti. Rapor "yalnizca konusma baglaminda" var olursa defterin
HAM/SHA butunlugu YAPISAL OLARAK saglanamaz.

**MK-6 - KAYNAK-SOZLESME PINLERI MUTASYONLA SINANIR.** Bir pin yalnizca kaynak metnini
tariyorsa, "kirmizi-once" kaniti YETMEZ: aranan dizgenin **BASKA bir baglamda da** gecip
gecmedigi, korunan alani ONCEKI haline donduren bir uretim mutasyonuyla gosterilir.
Gerekce OLCULDU: P19'un `Contain("effective_price")` asserti BEDAVA DOGRUYDU (dizge
`mapProduct` govdesinde DORT satirda geciyordu); alan K1 oncesine dondurulunce **TUM SUIT
575/578 ile temiz durumla BIREBIR AYNI** kaldi - yani duzeltmenin istemci yarisi PINSIZDI.
Assert ALAN BAZLI yapilinca ayni mutasyon TAM 1 ISIMLI KIRMIZI verdi.

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

## KURGU ENVANTERI (D-YAN'a)

**MUSTERI 102-116 (15):** 102 `mfix1.once` (ana kurgu) · 103/104/107 ajan kurgulari ·
105/106/108-111 `a3g1..a3g6` (misafir) · 112/113 `a2kapi`/`a2kontrol` ·
**114 `mfix1.k2admin` (`user_type=1`, hediye karti uretimi icin)** ·
**115 `mfxk3.tuzak` (K3 YETIM MISAFIR - gecersiz kupon reddinin biraktigi satir)** ·
116 `mfxk3.ab`.
**SIPARIS 253-263 (11, TAMAMI Confirmed, YENI PENDING YOK):** 257/260 K1 A/B · 261 K2 A/B
(kredi 200,00) · 262 K3 A/B (kupon DALGAB30) · 263 C1 A/B (kargo-bedava kuponu) · kalanlar
ajan kurgulari. **ADRES 65-75.** **KUPON `MFXK4MIN700`** (Yuzde 30, min 700, limitsiz -
URETIM YOLUNDAN, `POST /api/coupon/add`). **HEDIYE KARTI 1** (`C0715A89...`, 200,00,
kullanildi).

**A3'UN KUPON ENVANTERI (rapora not):** `L3GLOBAL` **TUKENMIS** (`usage_limit=1`, bir odenmis
siparis) · `L3SURESI`/`MFXEXP` suresi dolmus · `L3PASIF` pasif · `L3MINTUTAR` min 999999 ·
uc SIFIR DEGERLI kupon (`E2TEST`/`DALGABOLCUM`/`PANELDEN30`, D-YAN artiklari). **K4 icin
uygun kupon YOKTU** - tespit dogruydu, kupon uretildi.

**MK-3 MUHURLERI (URETEN IFADELERIYLE):**
```
SELECT COUNT(*), MAX(id) FROM orders WHERE customer_id = 10;
  -> 38 / 211                                   SABIT
SELECT COUNT(*), MIN(id), MAX(id), SUM(CAST(id AS bigint))
  FROM orders WHERE status = 0 AND id <= 210;
  -> 35 / 9 / 210 / 3837                        DEGISMEDI
SELECT COUNT(*), SUM(total_price), STRING_AGG(CAST(status AS varchar),',')
  FROM orders WHERE customer_id = 74 AND id BETWEEN 234 AND 237;
  -> 4 / 4698,60 / 0,0,1,1                      ICERIKLE korundu
```
Omer'in hesabi ve kabul turu kayitlari **KULLANILMADI, SILINMEDI**; mevcut Pending
siparislere DOKUNULMADI.

## DOKUNULMAYANLAR / DEVIR

**MF-2 [FATURA] - IKI KALEM:**
- **(a)** `InvoiceManager.cs:76`'nin **BRUT** toplama bagi MF-2'de ACIK HALE GETIRILIP
  **PINLENECEK**. Bugun boyle bir pin YOK ve K2-B geldiginde `:76` yeni semantigi SESSIZCE
  takip eder -> **KDV EKSIK BEYAN**.
- **(b)** `invoices` tablosu krediyi **HIC KAYDETMIYOR**, yalniz HTML gosteriyor - MF-2
  kapsam adayi.

**MF-3 [KVKK/HESAP] - K3'UN ULASILABILIR KILDIGI TUZAK (DUZELTILMEDI, PINLENDI):**
gecersiz kupon -> **400 ama musteri satiri OLUSMUS** -> ayni misafir KUPONSUZ tekrar
denerse **409 "Bu e-posta kayitli"**. TEK YANLIS KUPON KODU o e-postayi misafir checkout'a
**KALICI KAPATIYOR** ve musteri giris de yapamaz (parola rastgele). Kok:
`GuestCheckoutManager.cs:173` musteriyi `PlaceOrder`'dan ONCE yaziyor. **K3 tuzagi
YARATMADI, ULASILABILIR KILDI.** MANTIK-AV-1 dortlusundeki bilinen GEZA-2 bulgusunun
KESKINLESMIS hali.
**MF-3 SARTLARI:** (a) musteri+adres yazimi `PlaceOrder` BASARISINA baglanacak (transaction
ya da erteleme) · (b) cozumde **IKINCI kupon dogrulama noktasi ACILMAZ** ("ayni kuralin
ikinci kopyasi" - bu depoda 7 kez bedeli odendi) · (c) **409 semantigi YENIDEN ACILMAZ**
(GUVENLIK-2/#1 kabul edilmis karar) - satir hic yazilmazsa 409 sorunu zaten DOGMAZ ·
(d) K3'un bu dali ulasilabilir kildigi gercegi MF-3 tarifinin GEREKCESINE girer.

**A2'NIN SERBEST AV BULGULARI (bu dalgada DOKUNULMADI):** B3 `[MANTIK]` "Indirim" suzgeci UC
yerde `old_price`'a bakiyor -> canli 8 yerine 2 urun gosteriyor · B6 `[VERI-BOZAN LATENT]`
admin formu `sub_category_id` GONDERMIYOR, her guncellemede NULL'lanir (bugun dolu urun 0) ·
B4 `mapProduct.cart` alani OLU (0 tuketici, TEMIZLIK ADAYI).

**A3'UN (C) BULGUSU:** `OrderManager.cs:209-211` yorumu "sira Validate ile AYNI" diyor,
`usage_limit` icin YANLIS - `[KOZMETIK]`.

**DURUST KAYIT - ISIMSIZ FLAKE:** denetciler kosarken alinan BIR `Category=Sql` kosumunda
**338/339** gorundu; ADI YAKALANMADI (grep deseni mesaji disarida birakti). Ayni anda alinan
tam suit 575/578 (yani 4 degil 3 kirmizi) - **TUTARSIZ**. Worktree kaldirildiktan sonra iki
ardisik kosum 339/339. En olasi aciklama paylasilan test veritabanlari (kural-uyum M2-2'de
`already exists` cakismasi olctu) **ama BU ISPAT DEGIL**.

**ORTAM DERSLERI (kalici):** `sqlcmd` bu ortamda **QUOTED_IDENTIFIER kapali** baslar ve
filtreli indeksi olan tabloya `UPDATE` **Msg 1934** ile duser -> **`-I` bayragi ZORUNLU** ·
`gift-card` rotasi **TIRELI** (`api/gift-card`) ve `GiftCardCreateDto` **yalniz `amount`**
tasir · `schtasks` Git Bash'ten cagrilinca yol cozumleme bozulur, **PowerShell** uzerinden
cagrilir · build ONCESI API sureci DURDURULUR (MSB3027/MSB3021 DLL kilidi), SONRASINDA
yeniden baslatilir ve bes arguman TEYIT EDILIR. Dordu de KAYNAKTAN okunarak cozuldu.

**DERS - "A/B'NIN YAKALADIGINI KAYNAK OKUMASI YAKALAYAMAZDI":** K4'un IKI ara duzeltmesi de
(sessiz guard · cizilmeyen cekmece) YALNIZCA KONTROLLU A/B ile gorulebildi. Kaynak sozlesmesi
pinleri ikisini de YESIL gecerdi. **Kaynak pini "ne yazildigini", A/B "ne oldugunu" olcer.**

## KUYRUK

KUYRUK: MANTIK-FIX-2 [FATURA] → MF-3 [KVKK/HESAP] → MF-4 [VİTRİN+i18n] → GÜVENLİK-AV-1 (ilk
ultracode pilotu; 'ultracode' kelimesini prompt'a merkez ekler) → GÜVENLİK-FIX paketi →
FIX-1B → ADMIN-FIX → IMPORT-FIX → FIX-1C → LOG-FIX → FIX-2 → FIX-3/B13.

**D-YAN TEMIZLIK LISTESINE EK:** MANTIK-FIX-1'in kurgu kayitlari (musteri 102-116, siparis
253-263, adres 65-75, kupon `MFXK4MIN700`, bir hediye karti) · **musteri 115 YETIM-MISAFIR
ailesine** (gecersiz kupon reddinin biraktigi, siparissiz, `email_verified=0` satir).
Onceki dalgalarin kayitlariyla (musteri 74-101, siparis 213-252) birlikte TEK temizlik
isinde ele alinir; **fatura siparisten ONCE** (`invoices -> orders` FK'si RESTRICT).

---

