# KAPANIS KAYDI - KALITE SUPURMESI KAPANDI (23 Agustos 2026)

**LAUNCH'I BLOKE EDEN TEKNIK KALEM KALMADI.**

Bes olcum dalgasi ve karsiliklarindaki duzeltme dalgalari, artik yesil bir CI ile
kapandi. Kapanisi kanitlayan son SHA: **`dbaa763`** (her iki workflow tamamen yesil,
alti job'da failure seviyeli annotation SIFIR).

## KAPANAN DALGALAR

| Dalga | Konu | Duzeltme commit'i / durum |
|---|---|---|
| Dalga 1 | Envanter + tarama (B1..B9) | DALGA-1-FIX |
| Dalga 2 | Mantik / invariant denetimi (B10..B14) | DALGA-2-FIX + veri temizligi (7 iptal faturasi) |
| Dalga 3 | Performans (P1..P5) | DALGA-3-FIX |
| (C) Guvenlik | IDOR / tutar / mass assignment / enjeksiyon / yaris (G1..G9) | GUVENLIK-FIX + GUVENLIK-FIX-2 |
| Dalga 4 | Mobil + capraz cihaz (M1..M11) | M10/M11-FIX (`77c0308`) + DALGA-4-FIX-2 / M1 (`dbaa763`) |

**Dalga 4'un UC LAUNCH-BLOKE kaleminin UCU DE kapandi:**
- **M10** - "Sepeti Onayla" mobilde hic calismiyordu (delege handler'in kati hedef
  karsilastirmasi ripple ink yuzunden dusuyordu). GERCEK CIHAZDA dogrulandi.
- **M11** (+ M3) - cerez bari odeme sayfasinin TEK eylem dugmesini ve alt navigasyonu
  ortuyordu (`.ck-panel{display:flex}` HTML `hidden`'i eziyordu). GERCEK CIHAZDA dogrulandi.
- **M1** - storefront API adresi ve CSP origin'leri kaynakta sabit gomuluydu ve elle
  senkron tutuluyordu. Tek kaynak + dagitim betigi + calisma ani tutarlilik guard'i.

**MOBIL SATIN ALMA UCTAN UCA SURULDU** (kullanicinin telefonu, Android/Opera 384x694):
sepet -> "Sepeti Onayla" -> `#/odeme` -> **Iyzico kart formu mobilde yuklendi**
(kart no / ay-yil / CVC / 3DS + tutar). Bu, kapanisin saha kanitidir.

## ACIK KALANLAR (HICBIRI LAUNCH'I BLOKE ETMIYOR)

**TEKNIK DEFTER**
- **SUPHELI #14** - `X-Api-Version` ayristirilamazsa TUM API blanket 400 veriyor.
  Kapsam Sprint 8'de webhook yolu icin DARALTILDI ve pinlendi; genel cozum
  **LAUNCH SONRASI** (bkz. SUPHELI DAVRANISLAR).
- **SUPHELI #20** - varsayilan-kapali yetki kurali controller'larla sinirli; bugun
  BOSLUK YOK (olculdu) ve bosluk testte kapatildi.

**GUVENLIK**
- **G4** - satici girisi refresh token'i GOVDEDE donuyor (`SellerAuthManager.cs:101`).
  Bugun ERISILEMEZ (`sellers` 0 satir, kayit kapali/403). **Satici modulu acilmadan
  ONCE ZORUNLU ON KOSUL** - ikinci on kosul (kilit kontrolu sirasi) ile birlikte
  KARARLAR bolumunde.

**DALGA 4 - BLOKE ETMEYENLER**
- **M2** 376 px altinda header aksiyon kumesi tasiyor (gercek cihazda DOGRULANMADI -
  emulasyon kaniti gecerli).
- **M4** dokunma hedefleri 44x44 altinda (sepette `-`/`+` gercek cihazda sorunsuzdu).
- **M5** `autocomplete` eksik, `<form>` elementi yok (onem derecesi DUSURULDU - telefon
  parola kaydetmeyi onerdi, klavyede "Git" tusu var).
- **M6 / M7** PWA standalone kalemleri - kisayol standalone ACMADIGI icin **OLCULEMEDI**;
  "test edilmedi, bloke etmez" olarak kapatildi.
- **M8** service worker `VERSION` E3'ten beri bumplanmadi. Offline testi kullanici
  karariyla ATLANDI (oncelik degil); **VERSION bump'i DAGITIM KURALI olarak
  `ops/deployment-checklist.md`'de**.
- **M9** alt navigasyon etiketleri 9.5 px.

**ERTELENENLER**
- **B5** - 150 API ucunun 100'u HTTP duzeyinde test gormuyor (ayri kapsam dalgasi).
- **B13** - terk edilmis Pending siparislere TTL yok (17 siparis, hepsi >24 saat;
  rezervasyonlar serbest, stok/kupon guvende - politika URUN karari).
- **B8**, **P4**, **P2-inline-bolme** ve KARARLAR'daki launch-sonrasi defterin tamami
  (gift-card expiry, 2FA enrollment ucu, step-up `auth_time`, loyalty oransal geri alma
  + referral clawback, Dashboard tam-tablo agregalari, sabit-zamanli kayit, RFC 2606
  ust alan adlari, Turkce klavyede yazilan e-posta, istemci onbellegi, cikisli
  kullaniciya dogrudan giris katmani, JS/DOM test kosucusu).

## KAPANISTA KAYDA DEGER UC SEY

1. **GERCEK CIHAZ TURU EMULASYONUN GOREMEDIGINI GOSTERDI.** M10 emulasyonda CURUK
   gorundu: sentetik `.click()` dogrudan butona gider, o an ripple ink YOKTUR. Gercek
   dokunusta ink DOM'a girer ve click hedefi O olur. Kok sebep ancak cihazda gorundu.
2. **CI'DA JS/DOM PINI YOK.** Tarayici semantigi (hit-test, CSS ozgullugu,
   `elementFromPoint`) bu suitte dogrulanamiyor; 13 kaynak/hesap pini
   (`FrontendDokunmaHedefiTests` 7 + `ApiOriginTekKaynakTests` 6) sozlesmeyi tutuyor ve
   `frontend/test/mobil-erisilebilirlik.js` olcumu tekrarlanabilir kiliyor. Kalici cozum
   launch-sonrasi defterde (yeni bagimlilik + `dependency-scan` kapsami).
3. **DAGITIM ARTIK BIR ADIM ISTIYOR.** `ops/set-api-origin.sh` kosulmadan yapilan bir
   yayin, storefront'u localhost'a bakar halde birakir. Bu SESSIZ DEGIL: calisma ani
   guard'i ekrana kirmizi uyari basar ve `--verify` exit 1 doner. Checklist maddesi
   `ops/deployment-checklist.md`'de.

---

