# GOZ-1 (VITRIN KABUL TURU) ve GOZ-FIX - VITRIN DUZELTME DALGASI (26 Agustos 2026)

Zemin `9811801`. GOZ-1 YALNIZ olcumdu (kod degismedi); GOZ-FIX onun cikardigi kalemleri
kapatti. Backend'e ve oturum/jeton mekanigine DOKUNULMADI - F4/F8 FIX-1B'nin alanidir.

## OLCUM ORTAMI - IKI KALICI DERS

**(1) OTOMATIK GECIS VAR AMA PLAYWRIGHT ILE DEGIL.** Olculdu: `node`/`npx`/`npm` bu makinede
YOK (Git Bash + Windows PATH + `Program Files\nodejs` uclu tarandi), `python` MS Store
saplamasi. Kullanilan sey UYGULAMA ICI TARAYICI PANELI; izleyici kurali geregi once SONUCU
BILINEN bir sayfayla dogrulandi (scratchpad `kanit.html`: sayfa metni + konsol satiri
birebir geldi). **EKRAN GORUNTUSU ALINAMIYOR** - arac birebir "the Browser pane is not
displayed, so the page is not compositing frames" donuyor; yerlesim SAYISAL olculur
(viewport, kutu koordinatlari, `elementFromPoint`, tasma).

**(2) KALICI SUREC: `Start-Process` YETMEZ, `Win32_Process.Create` DA YETMEZ.**
Olculdu: `schtasks` Last Result **`-1073741510` = `STATUS_CONTROL_C_EXIT`** - bu ortamda
kullanici oturumundaki uzun omurlu sureclere Ctrl+C gidiyor ve `^C` log dosyasina dusuyor.
Denenen ve OLEN yollar: `Start-Process -WindowStyle Hidden` · `Win32_Process.Create` ·
`cmd.exe` sarmalayicili zamanlanmis gorev. `S4U` gorev tipi **admin ister** ("Erisim
engellendi"). **CALISAN COZUM: XML ile kayitli `InteractiveToken` zamanlanmis gorev**
(`schtasks /Create /XML`). Iki tuzak daha: `/TR` **261 karakter** siniri (uzun scratchpad
yolu asiyor - XML sart) ve sema surumu (`DisallowStartOnRemoteAppSession` /
`UseUnifiedSchedulingEngine` **1.2'de YOK**, `/XML` reddediyor).
Gorev adlari: `DivisimaGoz1Api`, `DivisimaGoz1Statik`.
**BUILD ONCESI GOREV DURDURULUR** - kosan API `Divisima.*.dll`leri kilitler ve build
`MSB3027` ile duser (CLAUDE.md'de zaten yazili tuzagin gorev bicimi).

## GOZ-1 BULGULARI ve GOZ-FIX KAPANISLARI

| # | Sinif | Bulgu | Durum |
|---|---|---|---|
| G1 | YUKSEK | Izgarada UYDURMA beden stogu + YANLIS "Son N urun!" kitlik iddiasi | **KAPANDI** (F-G1) |
| G2 | ORTA | Sekme basligi "Sayfa Bulunamadi"ya YAPISIYOR | **KAPANDI** (F-G2) |
| G3 | ORTA | `#toast` dokunusu caliyor (M10 sinifi) | **KAPANDI** (F-G3) |
| G4 | DUSUK | Misafir odemede IKI secenek de `disabled`-soluk | **KAPANDI** (F-G4) |
| G5 | DUSUK | `/api/search/products` camelCase zarf (PagedResult sizintisinin 3. ornegi) | ACIK - istemci ikisini de kabul ediyor |
| G6 | DUSUK | 375 px'te 44x44 alti 99 dokunma hedefi | ACIK (Dalga 4 / M4) |
| O1 | YUKSEK | 401 alan `cart/add` yerelde "eklendi" gibi gorunuyor | **KAPANDI** (F-O1) |
| O2 | YUKSEK | "Siparisi tamamla" asili kaliyor, sayfa en alta atliyor | **KAPANDI** (F-O2) |
| O3 | DUSUK | Katalog hatasinda "Failed to fetch" kullaniciya siziyor | **KAPANDI** (F-O3) |
| O4 | ORTA | Odeme ozeti bayat kaliyor | **KAPANDI** (F-O4) |
| O5 | ozellik | Sepeti bosaltma yolu yok | **EKLENDI** (F-O5) |

### O2'NIN GERCEK KOK SEBEBI - MERKEZIN 401 HIPOTEZI OLCUMLE CURUDU

Hipotez "payment/initialize de 401 aliyor" idi. **CANLI OLCULDU: 401 YOK.**

```
POST /api/order/place        -> 201 Created
POST /api/payment/initialize -> 200 OK
coErr "" (HATA YOK) · coPayHost yuksekligi 0 px · scrollY 0 -> 648 · siparis Pending KALDI
```

Gercek sebep: `IyzicoClient.cs:84` mock modda `CheckoutFormContent` olarak **bir HTML
YORUMU** donduruyor. Eski kod onu truthy gorup gomuyor, `embedCheckoutForm` **kosulsuz**
`scrollIntoView` cagiriyor. Kullanici icin "bastim, sayfa zipladi, hicbir sey olmadi";
siparis odenmemis asili kaliyor.

**AYRICA OLCULDU - 401 YOLU ZATEN KURTARIYOR:** `api-client._request` 401'de BIR KEZ
`_tryRefresh` deneyip istegi tekrarliyor.

```
Jeton BOZUK, oturum SAGLAM : cart/add 401 -> auth/refresh 200 -> cart/add 200   KURTARIR
Oturum OLU                 : cart/add 401 -> auth/refresh 401 -> HATA FIRLAR
```

Yani konsoldaki `cart/add 401` TEK BASINA ARIZA DEGIL. Arizanin oldugu yer refresh'in de
dustugu durumdur ve orada eski davranis **rozeti 2 -> 3 artirip** toast'i **"Sepet sunucuya
yazilamadi"** metnini BASINA ONAY ISARETI koyarak gosteriyordu - basarisizlik BASARI gibi.

### F-O4 KENDI YAN ETKISINI URETTI (kayit)

Ozet tazeleme checkout HTML'ini yeniden kuruyor ve `submitOrder`in yazdigi GORUNUR hatayi
SILIYORDU (olculdu: mesaj yazildi -> sepet aynalamasi `renderCart`i tetikledi ->
`drawCheckout` yeniden cizdi -> `coErr` BOSALDI). Hata metni artik state'te
(`sonCheckoutHatasi`) tutulup her cizimden sonra geri konuyor.

### ONCE / SONRA (tarayici olcumu)

```
F-G1  izgarada BILINEN beden degeri  60/60 -> 0/60      gercekten FARKLI  53 -> 0
      gercekte stok VARKEN "0"        8   -> 0          "Son N urun!" kart 6 -> 0 (6/6 YANLISTI)
      VAKUM KIRICI: stok 3 -> "Son 3 urun!" HALA var · stok 0 -> "Tukendi" · stok null -> metin YOK
F-O2  gorunur hata YOK -> VAR (siparis numarasiyla) · scrollY 0->648 -> 0->0
F-O1  olu oturumda rozet 2->3 -> 1->1 · toast "... yazilamadi" -> "Oturumun sona erdi..."
F-G2  bozuk kategoriden SONRA #/giris "Sayfa Bulunamadi" -> "Giris · Divisima"
      (bozuk kategoride HALA "Sayfa Bulunamadi" - dogru yerde duruyor)
F-G3  tiklama hedefi DIV#toast.toast -> BUTTON#checkoutBtn (toast dugmenin USTUNDEYKEN)
F-G4  iki radyo da disabled -> kapida etkin+secili, kart tiklanabilir + sebep + #/giris
F-O3  "Failed to fetch" -> "Urunler yuklenemedi / Lutfen tekrar dene."
F-O5  yerel 2->0, rozet "0", SUNUCU 2->0 (DELETE /api/cart/clear - YENI UC ACILMADI)
```

## PINLER (2 yeni, `FrontendDokunmaHedefiTests` icine)

- `KAYNAK_SOZLESMESI_IzgaraStogu_PRNG_ile_URETILMEZ_ve_KitlikMetni_GERCEK_STOKTAN_Turer`
- `KAYNAK_SOZLESMESI_OdemeGomme_GORUNUR_ICERIK_YOKSA_Kaydirmaz_ve_GORUNUR_HATA_Yazar`

**DURUST ETIKET: ikisi de KAYNAK SOZLESMESI pinidir, DAVRANIS pini DEGILDIR** - adlari bunu
soyluyor. Yorumlar taranmadan ONCE ayiklaniyor (bu depoda "kaynak tarayan pin kendi
belgeledigi kalibi da tarar" tuzaginin bedeli iki kez odendi) ve fonksiyon govdeleri susli
parantez eslenerek cikariliyor - `rngOf` dosyada BASKA yerlerde kullanilmaya devam ettigi
icin dosya geneli tarama vakuma duserdi. Vakum kiricilar: `rngOf` en az iki yerde HALA
gecmeli · `scrollIntoView` OZELLIGI HALA durmali · govdeler bos okunmus olamaz.
Cift-anlam kiricilar: eski kosulsuz `lowS` bicimi geri gelemez · kosul kaydirmadan ONCE
gelmeli (indeks karsilastirmasi) · 401 dali AYRI ve eylem iceren metin vermeli.

**KIRILAN PIN YOK.**

### DIS KONTROLU + 5. KONTROL

DIS: her iki pinde birer assert ters -> **iki AYRI ISIMLI KIRMIZI** (her turda TAM 1),
geri alindi, flip izi 0.
5. KONTROL, IKI uretim mutasyonu - her birinde (a) dosyada mi (b) temiz build (c) lokalize:

| Mutasyon | Sonuc | Uretilen once-durum |
|---|---|---|
| M-P1 `sizeStockOf`a PRNG fallback GERI KONDU | P1 TAM 1 KIRMIZI (diger 8 yesil) | izgarada uydurma beden stogu |
| M-P2 `scrollIntoView` kosulu KALDIRILDI | P2 TAM 1 KIRMIZI (diger 8 yesil) | 0 px host'a kaydirma - sayfa en alta atlar |

Ikisi de geri alindi; `MUTASYON-MP` izi depoda **0 dosya**.

## YEREL DOGRULAMA

333/333 `Category=Sql` · tam suitte **554 basarili / 557** (kirilan 3'un UCU DE Docker'li
`OrderEndpointTests`) · Release 0 hata · whitespace + style **exit 0**.

## ACIK KALANLAR / KARARLAR

- **JS/DOM KOSUCUSU BOSLUGU ACIK.** Bu dalganin sekiz kaleminin tamami TARAYICI once/sonra
  olcumuyle kanitlandi; CI'da tutulan sey yalnizca KAYNAK KOSULU. Dalga 4'ten beri acik olan
  ayni kalem (yeni bagimlilik + `dependency-scan` kapsami; karar kullanicinin).
- **11 PENDING SIPARIS DURUYOR - SILINMEDI.** Bugun 14 siparis / 11 Pending; hepsi
  `payment_type=0` (Online) ve `e2b.sandbox@example.com` hesabindan. Bunlarin **4'u benim
  olcumlerimin urettigi** (#200-#203 - kart yolu dort kez suruldu), kalani turlarindir.
  **B13 (terk edilmis Pending'lere TTL) bu korpusun dogal tamamlayicisidir**; silme karari
  merkezden.
- **[HAVALE->FAZ 4] MOCK MODDA GORUNUR ODEME FORMU.** `Iyzico:UseRealSdk=false` iken uc
  HTTP 200 ile bir HTML YORUMU donduruyor. Istemci artik bunu ADIYLA soyluyor ama asil
  tuhaflik SUNUCU tarafinda: "basarili" bir yanit gorunur icerik tasimiyor. Aday cozumler:
  mock'un tiklanabilir sahte bir onay formu dondurmesi ya da ucun mock modda ACIKCA
  ayirt edilebilir bir alan (`is_mock: true`) tasimasi. **URETIM KODU, KAPSAM DISI.**
- **G5** (`search/products` camelCase zarfi - sizintinin UCUNCU ornegi; B2 ve K6 kapatilmisti)
  ve **G6** (44x44 alti 99 hedef) ACIK.
- **YASAL METIN VARLIGI (GOZ-1 ADIM 4):** 10 sozlesme sayfasi VAR (TR+EN, `contents`,
  footer'da 11 baglanti). **"ON BILGILENDIRME FORMU" DEPODA HIC YOK** (slug/sayfa/baglanti,
  hatta kaynakta tek gecis bile yok). **Satici kimligi 10 metnin HICBIRINDE yok**
  (unvan/vergi no/MERSIS taramasi 10/10 YOK) ve `iletisim` sayfasi kendi metniyle
  "Bu bir tasarim simulasyonudur" diyor. Ikisi de LAUNCH ONCESI IRL kalemi.

---

