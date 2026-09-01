# KAPANIS KAYDI - KOD TARAFINDA LAUNCH'I BLOKE EDEN IS KALMADI (25 Agustos 2026)

**KANIT SHA: `f9634cc`** - her iki workflow tamamen yesil, **alti job'da failure seviyeli
annotation SIFIR** (adim bazinda + annotation duzeyinde dogrulandi). Retry gorunurluk adimi
iki job'da da okundu: `TestDbKurulum: 1807 ... HIC ATESLEMEDI (0)`.

## KAPANAN FAZLAR

| Faz | Konu | Kapanis kaniti |
|---|---|---|
| **Kalite supurmesi** (Dalga 1-4 + Guvenlik) | Envanter/tarama · mantik-invariant · performans · IDOR/tutar/enjeksiyon/yaris · mobil ve capraz cihaz | `dbaa763` - M10/M11/M1 dahil uc launch-bloke kalem kapandi, mobil satin alma GERCEK CIHAZDA uctan uca suruldu |
| **Kapsama denetimi** | Kirik halkalarin cikarilmasi (mail zinciri, operasyon yuzeyi, yayin altyapisi, gercek veri) | LAUNCH-FIX dalgalarina donusturuldu |
| **LAUNCH-FIX Dalga A** | Ilk musteri zinciri: mail altyapisi · sifremi unuttum · sifre politikasi · misafir checkout · tek para birimi | `8818f19` |
| **LAUNCH-FIX Dalga B** | Operasyon yuzeyi: admin panelinin HIC ACILMAMIS bes ekrani (B1..B5) | `8e46337` |
| **LAUNCH-FIX Dalga C** | Yayin altyapisi: storefront'u sunan tanim · gorsel kaliciligi · ilk admin · arka plan is hatalari · paylasim/sitemap · Update transaction'i | `d5993ea` |
| **D-SEMA + D-SEMA-FIX** | Tek dogruluk kaynagi EF migrations; `01_schema.sql` uretilen artefakt, 47 FK migration'a tasindi, CI'ya kayma kapisi | `452d9ea` + `4a0bfa0` |
| **LAUNCH-FIX Dalga D** | Gercek veri provasi: D1 gorsel sizintisi · D2 yetim stok + FK · D3 gercek olcek (+D3-FIX) · D4 idempotency · D5 rate limit · D6 yedek/geri donus | `2bc53c5` (ayrinti "DALGA D KAPANIS KAYDI") |
| **Taksonomi** | Gezinme menusu veritabanindan uretiliyor; taninmayan rota artik 404 | **`f9634cc`** |

Arada iki CI kirmizisinin kok sebebi de olcumle bulundu ve kapatildi: **Hangfire yarisi**
(`cd51a52` - test host'lari dakikalik outbox isiyle yarisiyordu) ve **`model` kilidi**
(`10d794d` - gereksiz 47. veritabani bes BASKA sinifi dusurmustu).

## ACIK KALANLAR - TEK LISTE

**STAGING'DE OLCULECEK (bu makinede arac YOK, durust kayit):**
- **Canli Redis turu** - dagitik kilit, blacklist, idempotency'nin Redis yolu, dagitik rate
  limit sayaci. (Docker/Redis yok; fail-fast davranisi belgelendi.)
- **k6 yuk turu** (`ops/load-test/k6-smoke.js`) - k6 kurulu degil; elle harness ile olculdu.
- **Eksik indeks esigi** - 403 urunde DMV'nin CANLILIGI bile gosterilemedi (kasitli indekssiz
  sorgular da oneri uretmedi; uc sorgulari kosum basina 10-18 mantiksal okuma yapiyor).
  **KORLEMESINE INDEKS EKLENMEZ** - gercek katalog hacminde yeniden okunur.

**LAUNCH SONRASI:**
- **SUPHELI #14** - `X-Api-Version` ayristirilamazsa TUM API blanket 400 doner. Kapsam
  webhook yolu icin DARALTILDI ve pinlendi; genel cozum acik.
- **SUPHELI #20** - varsayilan-kapali yetki kurali controller'larla sinirli. **Bugun BOSLUK
  YOK** (olculdu: 150 action'in tamami acikca isaretli) ve bosluk TESTTE kapatildi.
- **GUVENLIK DALGASI 2 / #1 - MISAFIR CHECKOUT ENUMERATION: KABUL EDILEN RISK**
  (karar 25 Agustos 2026). Kod DEGISMEZ - kayitli e-posta 409, kayitsiz 201 kalir.
  Gerekce (G2 deseni bir SIPARIS ucunda neden elendi) ve YENIDEN DEGERLENDIRME
  TETIKLEYICILERI (misafir checkout'a KART eklenirse VEYA rate limit topolojisi/kovasi
  degisirse) GUVENLIK-FIX-4 bolumunde. Riskin SINIRI pinli: 409 yolu musteri/adres/siparis/
  rezervasyon/outbox satiri YAZMAZ - kanal bir kaynak tuketimi vektorune DONUSEMEZ.
  NOT: ayni dalganin #2'si (cop COD siparisi) GUVENLIK-FIX-4'te KAPANDI (kanonik posta
  kutusu ekseninde esik guard'i, 429, yan etki sifir).

**SATICI MODULU ACILMADAN ONCE - ZORUNLU ON KOSUL:**
- **G4** - satici girisi refresh token'i GOVDEDE donuyor (`SellerAuthManager.cs:101`).
  Musteri tarafindaki httpOnly cerez sozlesmesi satici tarafina tasinmali.
- **Kilit kontrolu sirasi** - `SellerAuthManager.Login` kilidi SIFRE DOGRULAMASINDAN ONCE
  kontrol ediyor (musteri tarafinda SUPHELI #19 olarak kapatilan oracle'in aynisi).
- **Iki `seller_id` FK'si** - `products.seller_id` ve `order_items.seller_id` (D-SEMA-FIX'te
  bilincli olarak ERTELENDI, modul kapali oldugu icin).
Ucu de bugun ERISILEMEZ: `sellers` tablosu 0 satir, kayit kapali (403).

**IRL (kod isi DEGIL):**
- **Gercek mail turu** - gercek SMTP hesabiyla teslim edilebilirlik (SPF/DKIM/DMARC), spam
  klasoru, gonderen adi/adresi, gercek origin'li baglantilar. Yerel yakalayiciyla
  **govde + alici + link** duzeyinde kanitlandi; **TESLIMAT** duzeyi kanitlanmadi.
- **Gercek katalog aktarimi** (Zuhredeki verisi) - taksonomi isi tam da bunun on kosuluydu.
- Domain karari · canli Iyzico basvurusu · hosting/DNS.

**BLOKE ETMEYEN DEFTER** (Dalga 4 M2/M4/M5/M6/M7/M8/M9 · B5 uc kapsami · B13 terk edilmis
Pending siparislere TTL · P2 inline bolme · P4 istemci onbellegi · gift-card expiry ·
2FA enrollment · step-up `auth_time` · loyalty oransal geri alma + referral clawback ·
Dashboard tam-tablo agregalari · sabit-zamanli kayit · RFC 2606 ust alan adlari · Turkce
klavyede yazilan e-posta · cikisli kullaniciya dogrudan giris katmani · JS/DOM test kosucusu)
ilgili bolumlerinde ayrintisiyla duruyor.

---

