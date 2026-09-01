# MFIX-1 MUHRU - TEK GERCEK CHECKOUT (27 Agustos 2026)

**KANIT SHA: `ece00e9`** - her iki workflow yesil (`236b817..ece00e9`).
SDP v1.1'in ilk FIX dalgasi uygulamasi; muhur (`236b817`) ve duzeltme AYRI commit'lerde.

```
CI - Build & Test  run 33079719315  event=push  head_sha=ece00e9  SUCCESS
Security CI        run 33079719310  event=push  head_sha=ece00e9  SUCCESS
ALTI JOB'DA failure SEVIYELI ANNOTATION: 0   (39 annotation, HEPSI warning)
Gitleaks (secret taramasi): SUCCESS - bolum 7 kurali geregi ADIM SONUCUNDAN okundu
TestDbKurulum 1807 yeniden deneme ozeti: iki test job'inda da "HIC ATESLEMEDI (0) - retry devrede, gerekmedi"
```

**Muhur commit'i `236b817`** (docs-only, `CLAUDE.md` +269/-0) kendi turunda cift yesildi
(run 33069133327 CI + 33069133333 Security, 39 annotation / failure 0).

**ORTAM UYARISI (Divisima Eki 2.3):** olcumler `goz1` duzeneginde kosuldu ve
`api-baslat.cmd` BES arguman veriyor - `--Iyzico:UseRealSdk=false`,
`--BackgroundJobs:Enabled=false`, `--MailSettings:Host=`, `--AdminSeed:Enabled=false`,
`--RateLimit:AuthPermitLimit=100`. Odeme MOCK modda; "form donmedi" dali bu yuzden
tetiklenebiliyor ve REPRO-2 tam da onu kullaniyor. **Bunlar URUN VARSAYILANI DEGILDIR.**

## KAPANAN DORT KALEM

**F-M3a - TEK GERCEK CHECKOUT.** index.html'in MOCK checkout'u ile api-bridge'in gercek
checkout'u AYNI kaba yaziyordu (`#checkoutView`) ve tercih CIZIM SIRASINA bagliydi; mock'u
DORT yol diriltiyordu (kupon uygula 2447, kupon kaldir 2490, para birimi 2766, **dil** 2806)
ve mock CANLI KART FORMU tasiyip `coFinish()` ile **sunucuya hicbir istek atmadan**
"Order received!" diyordu. Cizim yolu **DELEGE** edildi (api-bridge `renderCheckout` /
`showCheckout`u sarmalayip eziyor), sahte kupon tablosu **SOKULDU**, kupon dogrulamasi
sunucuya baglandi. **REPRO-1 SONRA:** sahte kod -> mock GELMEDI (`coSteps=false`), gercek
checkout kaldi, "Gecersiz kod", `dvs_coupon` null, toplam **1.139,80 TL DEGISMEDI**,
cekmecede reklam metni "(REKLAM YOK)". Vakum kirici: gercek kod `E2YUZDE` -> **-113,98 TL**,
`srvAmount` SUNUCUDAN ve checkout'a TASINDI.

**F-M3b - DIL DEGISIMI.** Oturum zaten dusmuyordu; gorunurdeki dusme, `setLang`in (2793)
satir **2806**'da mock'u cagirmasi ve mock'un misafir uyarisinin `coStep1()` icinde
KOSULSUZ olmasiydi. **REPRO-3 SONRA:** TR->EN->TR, uc olcumde de `coSteps=false`,
`coSubmit=true`, **`MISAFIR_UYARISI=false`**, `loggedIn=true`, jeton yerinde.

**F-M3f - REQUEST_ID OTURUM BASINA.** Sunucu idempotency CALISIYORDU ama istemci HER TIKTA
yeni `request_id` uretiyordu -> koruma YAPISAL OLARAK ULASILAMAZDI (Omer'in turunda tek
denemeden **ALTI** Pending siparis). Anahtar OTURUM BASINA uretilip sepet degisiminde ve
BASARILI sipariste yenileniyor. **REPRO-2 SONRA (DB ile):** oturum 1'de **3 tik -> TEK
siparis 218** (`max_order` 217->218). **Vakum kirici:** gercek yeniden yuklemeden sonra
1. tik **yeni siparis 219** (farkli `request_id`), 2. tik **"Bu siparis zaten olusturulmustu
(siparis no: DVS20260827-37334419A5). YENI bir siparis olusturulmadi."**

**F-M8 - DURUST SIPARIS NUMARASI.** Iki siparis ucu de yalniz sayisal id donuyor; istemci
artik `order_number`i cekiyor, cekemedigi yerde **UYDURMUYOR**. "Form donmedi" mesaji
`"Siparisin 207 numarasiyla..."` -> **`"Siparisin DVS20260827-4DF7BEBF4F numarasiyla..."`**;
misafir sonuc ekrani `#212` -> **"Siparis numaran e-postanla paylasilacak. / Referans: 219"**
(`#id` bicimi YOK); iade listesi `order_number` basiyor.

## DEFER YARISI - CIFT-KOR DENETCININ AVI (SDP'nin DEGER KANITI)

**L3 cift-kor denetcisi, ana akisin sonuclarini GORMEDEN, gercek bir acik buldu.**
`api-bridge.js` **`defer`** ile yukleniyor (`index.html:3229`) ve inline script **2862**'de
acilista `router()` cagiriyor; `renderCheckout`/`showCheckout` sutun-0 global. Yani sayfa
**DOGRUDAN `#/odeme`** ile acilirsa (yenileme, yer imi, callback 302 donusu) EZME HENUZ
OLMAMIS olur ve orijinal govde MOCK'u cizer - sepet doluysa canli kart formu ve `coFinish`e
bagli dugme DOM'a girer; api-bridge hic yuklenmezse **KALICI** kalir.

**IKINCI SAVUNMA HATTI:** mock **KAYNAKTA** etkisizlestirildi - `renderCheckout` govdesi notr
bir yer tutucu yazip **ERKEN DONUYOR**. Erisilemezlik artik api-bridge'in YUKLENMESINE bagli
DEGIL. api-bridge ezmesi KORUNDU; o, dort dirilis yolunun GERCEK checkout'u tazelemesini
sagliyor. **IKI KATMAN AYRI IS YAPIYOR.** Soguk acilis olculdu: `dvs_cart` DOLU, sayfa
dogrudan `#/odeme` ile yeniden yuklendi (marker kayboldu = gercek soguk acilis) - **ALTI
ornekte de** (T0 ve T+600..3000 ms) `coSteps=false`, `coCardNo=false`, `placeOrder=false`;
gercek misafir checkout T0'DAN ITIBAREN ekranda, **mock HIC GORUNMEDI**.

**DERS:** prompt duzeyindeki cift-kor izolasyonu bu bulguyu uretti; SDP v1.1 madde 1.9 bunu
TEKNIK izolasyona (ayri calisma dizini) yukseltti.

## PIN ZAAFI DERSI - "KIRMIZI YOK" ONCE PIN SUPHESIDIR

5. kontrolun **M-P5b** mutasyonu (kaynaktaki erken donus yorumlandi) **KIRMIZI VERMEDI**.
Kuralin (a) ve (b) adimlari once kosuldu: mutasyon dosyaya INDI, build **0 Hata**. Yani
sonuc "mutasyon lokalize" DEGIL, **PIN ZAYIF** demekti - duz `IndexOf("return;")` mock
govdesindeki BASKA bir `return`'u (bos sepet dali) buluyordu. Pin **SATIR KOMSULUGUNA**
cevrildi (yer tutucudan SONRAKI satir kosulsuz `return` olmali) ve mutasyon TEKRARLANDI ->
**TAM 1 ISIMLI KIRMIZI**.

**KALICI KURAL NOTU:** bir uretim mutasyonu beklenen pini kirmiyorsa sira sudur -
(1) mutasyon dosyaya indi mi, (2) build temiz mi, (3) **PIN yeterince keskin mi**. Ucuncusu
atlanirsa zayif bir pin "lokalize" diye RAPORLANIR ve koruma sanilan sey aslinda YOKTUR.
Bu, 5. kontrolun bir pini eledigi **UCUNCU** vakadir (oncekiler D2 ve FIX-1A).

**IKINCI DERS (kacis kaybi, UCUNCU KEZ):** pin duzeltilirken regex kacisi IKI KEZ kayboldu -
heredoc ters boluyu dusurdu (CS1009), sonra `printf` satir sonu kacislarini gercek satir
sonuna cevirdi. Cozum regex'i TUMDEN kaldirmak oldu; kacissiz bir cozum varsa o tercih edilir.

## PINLER / DIS / MUTASYON

`FrontendDokunmaHedefiTests` **11 -> 13 `[Fact]`** (SIFIR-DDL sinif; yeni veritabani
ACILMADI - `10d794d` dersi):
- **P5** `KAYNAK_SOZLESMESI_MockCheckout_Dirilemez_ve_TekGercekCheckout`
- **P6** `KAYNAK_SOZLESMESI_RequestId_OturumBasina_ve_SahteKuponTablosu_Yok`

**DURUST ETIKET: ikisi de KAYNAK SOZLESMESI pinidir**, davranis pini DEGILDIR (depoda
JS/DOM kosucusu yok - Dalga 4'ten beri acik kalem). Davranis kaniti REPRO 1/2/3 tarayici ve
DB olcumleridir. Vakum kiricilar: gercek ciziciler HALA VAR - dort dirilis yolu HALA ORADA
(duzeltme "cagiranlari sil" DEGIL "hedefi etkisizlestir") - `couponUI`/`removeCoupon` HALA
VAR - [YOKLUK] taramasinin NEGATIF KONTROLU var (`cp_apply` 3 bulundu). Cift-anlam
kiricilar: cizim YALNIZ `odeme` rotasinda - `showCheckout` CIZMEZ (cift cizim olmasin) -
tik basina anahtar ureten eski bicim GERI GELEMEZ.

**DIS KONTROLU (TAM KAPSAMA):** her iki pinde birer assert ters -> **TAM 1 isimli kirmizi**.
**5. KONTROL:** M-P5 (api-bridge ezmesi kaldirildi), M-P5b (kaynaktaki erken donus
kaldirildi), M-P6 (request_id tik basina donduruldu) -> **ucu de TAM 1 kirmizi / 12 yesil**;
hepsi geri alindi, `MUTASYON-MP` / `DIS-FLIP` izi **0**.

**SUIT:** 333/333 `Category=Sql` - tam suitte **558 basarili / 561** (beklenti 556->558
BIREBIR tuttu; kirilan 3'un UCU DE Docker'li `OrderEndpointTests`) - Release 0 hata -
whitespace + style **exit 0**.

## v1.1 MADDE-VARLIK DOGRULAMASI (`236b817` muhrundeki metne karsi)

| # | v1.1 maddesi | Durum | Kanit satiri |
|---|---|---|---|
| 1 | `plan` alani TUM ajan semalarinda zorunlu | **VAR** | 8625 (`SEMA KURALI (v1.1)`) |
| 2 | Anlik goruntu AYRI kayit turu (on-kayit disi) | **VAR** | 8601 (`ANLIK GORUNTU`) |
| 3 | Tek ortak kural metni (kural simetrisi) | **VAR** | 8676-8677 (`1.8 KURAL SIMETRISI`) |
| 4 | Ayirt edici deney kalibi | **VAR** | 8668 (`AYIRT EDICI DENEY`) |
| 5 | Ortam/komut-satiri olcumu ZORUNLU ILK ADIM | **VAR** | 8663 (`CALISMA ORTAMI OLCULUR`) |
| 6 | Satir-kaymasi-itiraz-degildir notu | **VAR** | 8653 (bulgu paketine gomulu not) |
| 7 | Cift-kor TEKNIK izolasyon (ayri calisma dizini) | **VAR** | 8682-8684 (`1.9 IZOLASYON`) |

**YEDISI DE VAR - metne EKLEME GEREKMEDI.** Kayit: MFIX-1 raporunda "yedi changelog
maddesi islenmis" ifadesi rapor denetcisi tarafindan **desteksiz** bulunup kaldirilmisti;
o gun elimde 1-7 numarali bir liste YOKTU ve muhurde de numarali liste YOK. Bu turda merkez
maddeleri acikca listeledi, dolayisiyla dogrulanabilir bir onerme olustu ve madde madde
dogrulandi. **O gunku kaldirma DOGRUYDU** (kanit yoktu); bugun kanit URETILDI.

## "OLCEMEDIM" KAPANISI

Rapor denetcisinin isaretledigi iddianin **IKI YARISI** var ve ayri sonuclaniyor:
- **(a) "Replay mesaji EZILMIYOR" = KAPALI.** REPRO-2'de olculdu: yeniden yuklenen oturumda
  2. tikta **EKRANA ULASAN** metin replay mesajinin kendisiydi. Mock modda odeme baslatma
  dali AYNI istekte kosuyor; odeme hatasi metni replay metnini EZSEYDI ekranda o gorunurdu.
  Gozlem iddiayi FIILEN kanitliyor (kaynak karsiligi `api-bridge.js:1625-1629`).
  **Yeni olcum yapilmadi - var olan olcum yeniden okundu.**
- **(b) "Buton `finally` davranisi korundu" = HALA OLCULMEDI.** Kaynakta dogru
  (`api-bridge.js:1633-1635`), ama dugmenin ekranda gercekten kullanilabilir hale geldigi
  OLCULMEDI. **Tek acik nokta budur.**

## DEFTER NOTLARI

- **MOCK ICERIK FONKSIYONLARI ERISILEMEZ AMA DURUYOR** (`coStep*`, `coFinish`, `coVal`,
  `addrItemHTML`, `coData`). Silinmeleri `ADDR` (11 gecis, `delivCity()` 1899 + Hesabim) ve
  `CARDS` (9 gecis, Hesabim Kartlarim) ile IC ICE oldugu icin bu dalgada YAPILMADI ->
  **MFIX-2 SOKUM KALEMI**.
- **`E2YUZDE` `used_count` Pending'de ARTMIYOR** - sayac onayda artiyor (bilgi notu; kusur
  degil, Sprint 8 madde 1'in turetme tasariminin sonucu).
- **BULTEN PENCERESI VAADI KALDIRILDI.** On olcum, sahte kupon reklamini kapsam metninde
  ANILMAYAN UCUNCU bir yuzeyde daha buldu: `index.html:3019` bulten acilir penceresi
  `HOSGELDIN` kodunu kayit karsiligi VAAT EDIYORDU, veritabaninda karsiligi YOK.
  [YOKLUK] uc yuzeyde de **0** (dort uydurma kod dizgesi), negatif kontrol: `cp_apply` 3.
- **KURGU ENVANTERI:** `orders 218, 219, 220` (musteri 74, online, Pending, **ucu de ayri
  `request_id`**) - `payments 40, 41, 42` - yeni musteri/adres YOK (max 77 / 44 sabit) -
  Omer'in hesabi KULLANILMADI - mevcut Pending muhru `561429369 / 35` BIREBIR korundu.

## KUYRUK GUNCELLEMESI

**MFIX-1 KAPANDI** (`ece00e9`, cift yesil). Kuyrugun 1. maddesi dustu; kalan sira
"KUYRUK (MTUR sonrasi, merkez karari)" bolumundeki haliyle gecerlidir ve **siradaki her
sey MERKEZDEN** baslatilir - MFIX-2 dahil.

**D-YAN TEMIZLIK LISTESINE EK:** musteri 74'un MFIX-1 kurgu siparisleri **218, 219, 220**
(online, Pending) ve `payments 40, 41, 42`. Ayni listedeki 213-217 ile birlikte tek
temizlik isinde ele alinir.

---

