# 51 · GUVENLIK-AV-2 MUHRU — DAR GUVENLIK OLCUMU (5 Eylul 2026)

**SALT OLCUMDU.** Hicbir fix yapilmadi, hicbir uretim commit'i atilmadi. Kapi ve kapanis:
`ce54d0c` = `origin/main`, `git status --porcelain` **0** (acilis ve kapanis).
Bu muhur ve ona esilk eden CLAUDE.md deltasi **docs-only** tek commit'tir.

---

## 1. RAPOR (AYNEN)

**Kapi:** `ce54d0c` = `origin/main` · agac 0 (acilis ve kapanis) · kod/config/docs degisimi **0**.

**PILOT OLCUMU:** 9 ajan (6 olcum + 3 denetci) · **636 arac cagrisi** · 2.449.227 jeton ·
235 dk ajan-zamani (paralel kosuldu). En pahali: rapor denetcisi (380k); en cok arac: S-D (93).

**DENETIM SONRASI SIDDET:** KRITIK **0** · YUKSEK **1** · ORTA/DUSUK gerisi. Uydurma aday **0**.

### EN RISKLI 10 (denetci duzeltmeleri uygulanmis)

| # | id | siddet / on kosul / durum | konum | kanal |
|---|---|---|---|---|
| 1 | **SC-1** | **YUKSEK / KIMLIKSIZ-UZAK / AKTIF** | `AuthManager.cs:325,362,398,639,922` + `AccountManager.cs:166,361` | **3** |
| 2 | **SD-7** | ORTA / KIMLIKSIZ-UZAK / AKTIF · **[VERI-BOZAN]** | `GuestCheckoutValidator` + `GuestCheckoutManager:503-504` | 4 |
| 3 | **SC-7=SE-2** (birlesti) | ORTA / tetik KIMLIKSIZ-UZAK, hasar YEREL / kanal AKTIF, yuk LATENT | `IyzicoPaymentManager.cs:235-239` | 3 |
| 4 | **SC-3** | ~~YUKSEK~~ -> **ORTA / ilgisiz / AKTIF** | `ops/serilog-siem.md` <-> `Program.cs:215-223` | ~~3~~ -> **2** |
| 5 | **SC-2** | ~~YUKSEK~~ -> **ORTA** (SC-1 ile ayni kok) | `AuthManager.cs:258-265` | 2 |
| 6 | **SD-1/SD-2** | ORTA / KIMLIKSIZ-UZAK / LATENT | `Comparison` + `Merchandising:42,69,101` + `ProductAttribute:78,98` | 3 |
| 7 | **SA-1/SA-2** | DUSUK-ORTA / YEREL-ADMIN / LATENT | `AesEncryptionProvider.cs:47-52` · rotasyon yok | 3 |
| 8 | **SB-1** | ORTA / KIMLIKSIZ-UZAK / LATENT | `AuthManager.cs:344` <-> `:354` | ~~3~~ -> **2** |
| 9 | **SC-5** | ORTA / KIMLIKLI / AKTIF | `AuditInterceptor.cs:60-84` (`audit_logs` %69,5) | 2 |
| 10 | **SE-5 + SC-6/Y-3** | DUSUK-ORTA / KIMLIKSIZ-UZAK / AKTIF | `IyzicoPaymentManager.cs:226` · EF ham istisna -> log'a PII | 2 |

**S-C kapsama matrisi (A09):** 22 olay · **10 tam bosluk** · 5 kismi · 7 tam. Tek cumlesi:
*basarili olaylar IP ile, basarisiz olaylar IP'siz, reddedilenler (403 · 404 · 429 · logout ·
webhook allowlist) hic kaydedilmiyor.*

**S-F regresyon tablosu: 10/10 TUTTU, sifir regresyon.** Pin kanali 88/88. "11 -> 12" sapmasi
**bilincli sonraki dalga karari**, regresyon degil.

### GO / NO-GO

Olcut: KRITIK **ya da** YUKSEK+KIMLIKSIZ-UZAK **ya da** `[PARA]`/`[VERI-BOZAN]` -> **BLOKER**.

- **SC-1 -> LAUNCH BLOKER** (YUKSEK + KIMLIKSIZ-UZAK). `security_events`'in IP/UA'si yapisal
  olarak olu; `serilog-siem.md`'nin bes alarm kuralindan **ucu kosulamaz**. Yardimcilar
  (`IstemciIp()`/`KisaltUserAgent()`) ayni sinifta 150 satir yukarida hazir.
- **SD-7 -> LAUNCH BLOKER.** Merkez `[VERI-BOZAN]` boyutunu ONAYLADI: anonim tek istekle
  kalici, saldirganin sectigi, kurbanin kaldiramayacagi bir `customers` satiri yaziliyor ve
  o e-posta misafir checkout'tan **surekli disaniyor**.
- Kalan kalemler -> **launch sonrasi**.

### GF-5 BOLUMLEME (merkez karari — asagida bolum 11)

---

## 2. BULGU TABLOSU — 38 -> 36 (denetci duzeltmeli)

**ARITMETIK (ureten ifadeyle).** Alti defterden benzersiz ID sayimi:
`grep -ohE '\[?(AV2-)?(S[A-F]|SUPHE)-[0-9]+\]?' ham/*.md | tr -d '[]' | sed 's/^AV2-//' | sort -u | wc -l`
-> **41**. Bunun 2'si `SUPHE-1`/`SUPHE-2` (AV2 oneki tasimaz) -> **39 ID**; `SB-2` sahibi
tarafindan "BULGU SAYILMAZ" ilan edilmis -> **38 SAYILAN**. Kapsam elestirmeninin 39/38
sayilari BIREBIR yeniden uretildi (NEG kontrol `AV2-SZ-[0-9]+` -> 0).
**MERKEZ DUZELTMESI: 38 - 1 (SC-7 = SE-2 MUKERRER) - 1 (SC-2, SC-1 kokune katildi) = 36.**
Ayrica rapor denetcisinin KENDI buldugu **Y-3** ayri bulgu olarak kaydedilir (defterlerde
YOKTU) -> tabloda **36 ajan bulgusu + 1 denetci bulgusu**.

| id | siddet | on kosul | durum | konum | kanal |
|---|---|---|---|---|---|
| SC-1 | YUKSEK | KIMLIKSIZ-UZAK | AKTIF | AuthManager.cs:325,362,398,639,922 · AccountManager.cs:166,361 | 3 |
| SC-2 | ORTA (dus.) | KIMLIKSIZ-UZAK | AKTIF | AuthManager.cs:258-265 — SC-1 kokune katildi | 2 |
| SC-3 | ORTA (dus.) | ilgisiz (duz.) | AKTIF | ops/serilog-siem.md <-> Program.cs:215-223 | 2 (duz.) |
| SC-4 | ORTA | KIMLIKLI | AKTIF | AuthManager.cs:932-992 · EfUserSessionDal.cs:51-64 | 2 |
| SC-5 | ORTA | KIMLIKLI | AKTIF | AuditInterceptor.cs:60-70, :80-84, :90 | 2 |
| SC-6 | ORTA | KIMLIKSIZ-UZAK | AKTIF | ExceptionMiddleware.cs:67-68 · appsettings.json:28-36 | 2 |
| SC-7 | — | — | — | **SE-2 ILE BIRLESTI** (asagida) | — |
| SC-8 | DUSUK | KIMLIKLI | LATENT | AuthManager.cs:599-610, :731 | 2 |
| SC-9 | DUSUK | KIMLIKSIZ-UZAK | LATENT | WebhookIpAllowlistMiddleware.cs:23-29 | 1 (duz.) |
| SC-10 | ORTA | KIMLIKSIZ-UZAK | AKTIF | RequireUserTypeHandler · IyzicoPaymentManager.cs:82 · OrderManager.cs:146 · ReturnManager.cs:65 · Program.cs:405 | 2 |
| SC-11 | DUSUK | ADMIN | AKTIF | DataRetentionJob.cs:20-34 | 2 |
| SC-12 | ORTA | ADMIN | AKTIF | AuthManager.cs:837-842, :185-212 · DataRetentionJob.cs:30 | 2 |
| SC-13 | ORTA | KIMLIKLI | AKTIF | 34 DAL cagri yeri · DenetimKaydiYazAsync 3 cagri | **1 = SUPHE** |
| SC-14 | DUSUK | KIMLIKSIZ-UZAK | LATENT | SellerAuthManager · EfSellerDal.cs:29,38,47 | **1 = SUPHE** |
| SC-15 | DUSUK | KIMLIKSIZ-UZAK | AKTIF | AuditInterceptor.cs:51-52 · JwtHelper.cs:41-51 | 2 |
| SD-1 | ORTA | KIMLIKSIZ-UZAK | AKTIF | ComparisonResultDto.products : List&lt;Entity.Product&gt; | 3 |
| SD-2 | ORTA | KIMLIKSIZ-UZAK | AKTIF | MerchandisingManager:42,69,101 · ProductAttributeManager:78,98 | 3 |
| SD-3 | DUSUK | YEREL | AKTIF | AuditLogController.cs:12-17 yorumu <-> SeoController.cs:14,15 | 2 |
| SD-4 | DUSUK | KIMLIKSIZ-UZAK | AKTIF | Seo/sitemap — baseUrl kodlanmadan XML govdesine | 2 |
| SD-5 | DUSUK | KIMLIKSIZ-UZAK | AKTIF | 13 controller KANAL-2 taramasi kalanlari | 2 |
| SD-6 | ORTA | KIMLIKLI | LATENT | SignalR `Clients.User(id)` — iki kimlik ad alani ayrilmiyor | 2 |
| SD-7 | ORTA **[VERI-BOZAN]** | KIMLIKSIZ-UZAK | AKTIF | GuestCheckoutValidator:40-48 · GuestCheckoutManager:503-504 | 4 |
| SD-8 | DUSUK | KIMLIKSIZ-UZAK | LATENT | hata kodu alt-dizge cakismasi (`bu e-posta kayitli`) | 2 |
| SD-9 | DUSUK | KIMLIKSIZ-UZAK | LATENT | 31 capadan 3'u cerceve metnine bagli, pinsiz | 2 |
| SD-10 | DUSUK | YEREL | AKTIF | OrderPlacedLogHandler — Console.WriteLine, Serilog disi | **1 = SUPHE** |
| SA-1 | DUSUK | YEREL | LATENT | AesEncryptionProvider.cs:47-52 (K10 TEYIT) | 3 |
| SA-2 | ORTA | ADMIN | LATENT | anahtar rotasyonu yok -> cift sifreleme (EfEntityRepositoryBase.cs:104) | 3 |
| SA-3 | DUSUK | YEREL | AKTIF | SECURITY.md "telefon sifreli" ve "RFC 6238 TOTP" iddialari CURUDU | 2 |
| SA-4 | ORTA | KIMLIKSIZ-UZAK | LATENT | verify-2fa kodu HER DURUMDA siliyor · uc anonim (AuthManager.cs:394) | 2 |
| SA-5 | DUSUK | YEREL | AKTIF | iki 2FA mekanizmasi kopuk; tek sifreli alanin YAZICISI yok | 2 |
| SA-6 | — | — | LATENT | IModelCacheKeyFactory yok · OnModelCreating ornek alani okuyor | **1 = SUPHE** |
| SB-1 | ORTA | KIMLIKSIZ-UZAK | LATENT | AuthManager.cs:344 (CAS) <-> :354 (tam-varlik) | 2 (duz.) |
| SB-2 | — | — | — | **BULGU SAYILMAZ** — BILINEN'in canli kaniti (:503-504) | 3 |
| SUPHE-1 | DUSUK | KIMLIKSIZ-UZAK | LATENT | EfCustomerDal.cs:92-98 (atomik artis + ayri SELECT) | **1 = SUPHE** |
| SUPHE-2 | DUSUK | ADMIN | LATENT | StockManager.cs:365, :379 (DbUpdateConcurrencyException yakalanmiyor) | **1 = SUPHE** |
| SE-1 | DUSUK | ilgisiz | LATENT | SecurityHeadersMiddleware.cs:29 — OLU DIREKTIF | 2 (duz.) |
| **SE-2 (=SC-7)** | **ORTA** | tetik KIMLIKSIZ-UZAK / hasar YEREL | kanal AKTIF, yuk LATENT | IyzicoPaymentManager.cs:235-239 | **3** |
| SE-3 | DUSUK | KIMLIKLI | LATENT | InvoiceManager.cs:282 -> OrderConfirmationManager.cs:70 -> timeline | 2 |
| SE-4 | DUSUK | KIMLIKSIZ-UZAK | LATENT | GuestCheckoutValidator.cs:40-48 · GuestCheckoutManager.cs:284 | 2 |
| SE-5 | DUSUK | ilgisiz (belge) | AKTIF | SECURITY.md:30 <-> IyzicoPaymentManager.cs:226 | 2 |
| SF-1 | DUSUK | YEREL | LATENT | customers.password_reset_token — 4 miras satirda duz metin | 2 |
| **Y-3** (denetci) | ORTA | KIMLIKSIZ-UZAK | AKTIF | api-kalici.log:5886,5913,5940 — `addresses.full_name` PII (SC-6 kapsam genislemesi) | 2 |

**TEK KANALLILAR (= SUPHE, tarife KALEM OLMAZ):** SC-13 · SC-14 · SC-9 · SD-10 · SA-6 ·
SUPHE-1 · SUPHE-2. **Hicbiri iddia gibi sunulmadi** (rapor denetcisi: SUPHE disiplini ihlali 0).

---

## 3. S-C KAPSAMA MATRISI (A09) — 22 OLAY

Iskeletin siniri OLCULDU: `ISecurityEventService` uretimde **YALNIZ IKI sinifa** enjekte
(`AccountManager.cs:48`, `AuthManager.cs:29`), toplam **YEDI** cagri yeri
(`AuthManager.cs:325,362,398,639,922` + `AccountManager.cs:166,361`; NEG `IZZZSecurityEventService` 0).

| olay | iz | not |
|---|---|---|
| login basarisiz (KAYITLI) | E | security_events, **ip/ua NULL** |
| login basarisiz (KAYITSIZ) | **H** | delta 0 (POZ kontrol: kayitli -> delta 1) |
| hesap kilitlenmesi | E | ip/ua NULL |
| sifre degisimi basarisiz | E | ip/ua NULL |
| sifre degisimi basarili | KISMEN | audit, aktor dolu |
| sifre sifirlama ISTEGI | KISMEN | aktor NULL |
| sifre sifirlama TAMAMLAMA | E | ip/ua NULL |
| refresh token REUSE | E | Critical, ip/ua NULL |
| 2FA basarisiz dogrulama | E | ip/ua NULL |
| 2FA challenge | E | ip/ua NULL |
| hesap silme | E | ip/ua NULL |
| **403 yetki reddi** | **H** | RequireUserTypeHandler'da iz cagrisi 0 |
| **404 sahiplik ihlali** | **H** | GF-1/K4 uc noktasinda iz cagrisi 0 |
| **429 rate-limit** | **H** | Program.cs:405 OnRejected geri cagrisi YOK |
| **logout** | **H** | is_active 1->0, defter deltasi 0 |
| **webhook imza hatasi** | **H** | guvenlik olayi yazilmiyor |
| **webhook IP allowlist reddi** | **H** | middleware'e ILogger enjekte EDILMEMIS |
| **satici login basarisiz / kilit** | **H** | SellerAuthManager'da iz cagrisi 0 |
| admin yazma | KISMEN | audit, negatif entity_id |
| CSV toplu import | KISMEN | govdede iz cagrisi 0 (STATIK) |
| e-posta/telefon degisimi | KISMEN | audit, [REDACTED] dogru calisiyor |
| hesap olusturma | KISMEN | negatif entity_id |

**TAM BOSLUK: 10 · KISMEN: 5 · TAM: 7.**

---

## 4. S-F REGRESYON TABLOSU — 10/10 TUTTU

| # | kapanis | ONCE (muhurden) | SONRA (bugun) | kanal | sonuc |
|---|---|---|---|---|---|
| 1 | `44·GF-1·K1` `[VERI-BOZAN]` | farkli e-posta + ayni rid -> 200, order_number SIZDI | **400** genel mesaj; satir sayimi 166/155/105 BIREBIR, yetim 0 | 3 | TUTTU |
| 2 | `45·GF-1b·K1` `[OTURUM]` | ikinci cihaz cikistan sonra 200 | logout-all sonrasi cihaz1 **401**; sonraki yeni jeton 200 | 2 | TUTTU |
| 3 | `44·GF-1·K4` | sahiplik 403 -> 404; kalan 11 | 404 + yok-olanla **govde birebir**; kimliksiz 401; yuzey 9+3=**12** | 3 | TUTTU |
| 4 | `44·GF-1·K6` `[OTURUM]` | 64/128 -> 69/16, 0x02, 100k | m174 uretim yolundan **69\|16**; DB 69/16=3, 64/128=142, 0/0=6 | 3 | TUTTU |
| 5 | `45·GF-1b·K3` `[OTURUM]` | SHA-256 hex + filtreli UNIQUE | indeks `unique=1 · filter=(refresh_token IS NOT NULL)`; yeni 14 satir 64-hex | 3 | TUTTU |
| 6 | `45·GF-1b·K5` `[OTURUM]` | cerez 30g / oturum 7g = 23g fazla | cerez `expires` ile `expires_at` **ayni saniye**, `RefreshGun=7` | 3 | TUTTU |
| 7 | `46·GF-2a·K3` | 10 girdi (4 POZ / 6 NEG) | tarayicida **12/12**; `svg+xml`/`//`/`javascript:`/`file:`/BUYUK varyant -> bos | 3 | TUTTU |
| 8 | `47·GF-3·K9` | 400x20 sonra 429 = 21. istek | **1..20 -> 400, 21..24 -> 429**; kova paylasimi; kapsam disi 200x5 | 3 | TUTTU |
| 9 | `48·GF-2b·K3` `[PARA]` | 429 -> kupon sepetten kalkiyordu | 400/404/422 kaldirir; **429/500/409 SEPETTE KALIR** | 3 | TUTTU |
| 10 | `48·GF-2b·K4` `[VERI-BOZAN]` | "400/409'da yenile" -> cift siparis | `api-bridge.js:2277` tek kaynak, iki cagri yeri de oradan | 2 | TUTTU |

**Pin kanali:** dort kaynak-sozlesme sinifi, `--no-build --no-restore` -> **88/88, exit 0**.
**BIRIM FARKI, REGRESYON DEGIL:** K4'un "11" degeri `44·GF-1` ANINA aittir; `45·GF-1b·K2`
change-password ucuna login kilidini tasidi -> 8->9, toplam **12**. Karar pinin KENDI
govdesinde yazili (`GuvenlikFix1SozlesmeTests.cs:104-113`, "GF-1b GUNCELLEMESI: 11 -> 12,
BILINCLI"). Sahiplik sozlesmesi (404) DEGISMEDI.
**Secilmeyen 4 aday ve gerekcesi:** `46·GF-2a·K8` (gercek Chrome ister, tek kanalda kalirdi) ·
`47·GF-3·K5` (Production'a kosullu, Development'ta ateslemez) · `47·GF-3·K6` (muhrun kendi
KANIT GUCU bolumu TEK KANALLI ilan ediyor) · `50·GF-4·K5` (para/veri/oturum ekseninde degil,
en siki pinli).

---

## 5. KAPSAM MATRISI — 40 CONTROLLER x TUR (KUMULATIF, SDP 1.12.10-v1.4)

**Bolunme DOGRULANDI:** depoda `ls Divisima.API/Controllers/*Controller.cs` -> **40**;
13 + 17 + 10 = 40; artik **her iki yonde de 0** (`comm -23` ve `comm -13` bos);
**AV-1 kor ∩ AV-2 kor = 0** (`comm -12` bos).

| Kume | Sayi | Controller'lar |
|---|---|---|
| **AV-1'de KOR, AV-2'de OLCULDU** | 13 | AuditLog · Category · Collection · Comparison · Content · Merchandising · ProductAttribute · RecentlyViewed · Recommendation · Referral · Seo · SizeGuide · Stock |
| **AV-1'de OLCULDU, AV-2'de KOR** | 17 | Address · AdminCustomer · Cart · Dashboard · Device · Invoice · Loyalty · PriceDrop · ProductImage · ProductQuestion · Return · Seller · SellerAuth · Shipment · StockNotification · StoreCredit · Wishlist |
| **IKI TURDE DE KAPSANDI** | 10 | Account · Auth · Coupon · GiftCard · GuestCheckout · Order · Payment · Product · ProductReview · Search |

**ANA BULGU: KAPSAM GENISLEMEDI, YER DEGISTIRDI.** 40 controller'in **30'u en az bir turda
kor kaldi**. Bu olgu SDP 1.12.10'a v1.4 eki olarak girdi: *kapsam matrisi KUMULATIFTIR;
sonraki AV turu onceki turun KOR KUMESINDEN baslar.*

**UC DUZEYINDE:** altili liste (SDP 1.12.3) uygulanan uc **45 / 151 = %29,8**; anilma
(UST SINIR) 53/151. **106 uc KAPSAM BOSLUGU.** Elestirmenin kendi siniri: *"anilma !=
olculme; bu sayim UST SINIRDIR."*
**En agir uc bosluk:** `POST api/order/place` (kimlikli siparis, `[PARA]`+`[VERI-BOZAN]`) —
MISAFIR ikizini UC ajan olctu, kimlikli aslini **SIFIR** · `POST api/product-image/upload`
(tek dosya-yukleme ucu; **IKI TUR UST USTE** bos) · `[PARA]` demeti **32 uc**, altili 0.

**OWASP x AJAN (elestirmenin atfi; defterlerde "OWASP" kelimesi 0 kez geciyor):**
A01 KISMEN · A02 KAPSANDI · **A03 BOSLUK (istemci)** · A04 KISMEN · A05 KISMEN ·
**A06 BOSLUK** · A07 KAPSANDI(musteri)/BOSLUK(satici) · A08 KISMEN · **A09 KAPSANDI** ·
**A10 BOSLUK**.

---

## 6. BILINEN / KABUL EDILMIS RISK — B-01..B-30 DURUMLARI

Liste `00a`/`00b` **TAM METNINDEN** kuruldu (SDP 1.12.8 merkez eki: B8 fragmanlarindan DEGIL)
ve TUM ajanlara AYNEN verildi (SDP 1.8).

| Durum | Kalemler |
|---|---|
| **CAPRAZ DOGRULANDI, DEGISMEDI** | B-02 (Seller kapali) · B-11 (JS/DOM kosucusu yok) · B-13 (misafir 409) · B-14 (failed-jobs PII) · B-17 (hibrit jeton) · B-25 (30 dk webhook) · B-26 (Webhook:AllowedIps BOS) · B-28 (varsayilan-kapali kural) · B-30 (vitrin CSP) |
| **BAYAT CIKTI -> KAPANDI** | **B-27** `/api/payment/callback` rate limit disinda — **BUGUN KAPALI**. Iki bagimsiz kanal: kaynak (`PaymentController.cs:29` sinif duzeyi `[EnableRateLimiting("payment")]`) + canli sinir kosulu (**1-10 -> 302, 11+ -> 429**), rapor denetcisi ve kural-uyum denetcisi AYRI AYRI kostu. |
| **SINIRI GENISLETILDI (yeni bulgu dogdu)** | B-26 -> SC-9 (allowlist DOLDURULDUGUNDA reddin izsiz kalmasi) · B-13 -> SD-7 (409, mesru siparisten degil **BASARISIZ ISTEKTEN** doguyor) |
| **ANILMADI (bu turun kapsami disinda)** | B-01 · B-03..B-10 · B-12 · B-15 · B-16 · B-18..B-24 · B-29 |

**BILINEN'DEN CIKAN (kapandi):** `45·GF-1b` "IP davranis kaniti YOK" — gercek Kestrel'de
`ip_address='::1'`, `device='curl/8.12.1'` (7/7 satir).
**UYDURMA B NUMARASI: 0.** Kullanilan benzersiz numaralar B-01 B-02 B-11 B-13 B-14 B-17
B-25 B-26 B-27 B-28 B-30; aralik disi **0**. Dort satir atfi (`00a:54`, `00a:107`,
`00b:229`, `00b:247`) kaynaktan BIREBIR dogrulandi.

---

## 7. DENETCI RAPORLARI (MK-4b: ayri worktree + ayri test DB + ayri scratchpad, ucu de `ce54d0c`)

### 7.1 KAPSAM ELESTIRMENI (SDP 1.12.9)
- **A09 GERCEKTEN KAPANDI**, "anildi" degil — uc olcutle: (1) AV-1'de **0** etiketli bulgu ->
  AV-2'de **17**; (2) 22 satirin **6'si canli delta**; (3) AV-1'in adiyla saydigi uc
  sifir-kapsam yuzeyi (`AuditLogController` · `DenetimGizlilik` · `DenetimRedaksiyonu`)
  ucu de kapandi.
- **A04 "KAPSANDI" DENEMEZ:** yedi bulgu dustu ama hicbir ajanin gorev metninde tasarim/
  is-mantigi ekseni YOKTU; yedisi de baska eksenlerin ARTIGI. `is mantigi` 0 anilma.
- **CIFT SAYIM 3 vaka:** `ExecuteUpdateAsync` 34-cagri envanteri IKI KEZ (S-B + S-C, ayni
  ureten ifade — olumlu okuma: bagimsiz teyit) · odeme jetonu sink'i IKI BULGU (SC-7 = SE-2) ·
  uc yiginlasmasi (`auth/login` ve `guest-checkout/place` ucer ajan).
- **IKI AJANIN ARASINDAN DUSENLER:** Seller modulu (bes uc sahipsiz) · outbox'in UC PARCASI
  uc ayri yere dustu ve **kimse ayrimi yapmadi** (yazan tarafa HIC bakilmadi) · `request_id`
  uc ajanda ama **hepsi misafir ucunda**.
- **PROVENANS KAYBI:** `42·AV-1`'in kuyruk satirindaki "· Stock yuzeyi" CLAUDE.md'ye
  TASINMAMIS (olculdu: muhur 1, CLAUDE.md 0). **Bu muhurle GERI KONDU.**
- Ayrica olculdu: AV-1'in "dort `OrderPlaced*Handler`" sayisi **bir fazla** (biri arayuz;
  gercek isleyici UC) · S-D'nin kendi basligi "30 uc" diyor, tablosu 35 ve depo da 35.

### 7.2 KURAL-UYUM DENETCISI (SDP 1.12.10)
7 maddenin **6'si ONAY**, biri KOSULLU:
- **Kod diff 0:** porcelain 0, HEAD = origin/main, dal `main`. Desen-bagimsiz kanit:
  `.git/logs/HEAD` ve `.git/index` mtime'i **19:13:41'de donmus** (tur 22:31'de basladi).
- **Yikici yuk 0:** SQLi dedektorunun 16 eslesmesinin **16'si yanlis pozitif**; kilitlenmis
  hesap yok (en yuksek sayac 4, o da GAV-1'den); `/api/order/place` tur penceresinde **hic
  yok**; `payments` 0 satir; S-B'nin prob DB'si dusurulmus.
- **MK-4a/MK-5 ONAY:** 6/6 defter basligi tasiyor, 6/6 dosya 0 bayt DEGIL (boyutlar birebir),
  hepsi scratchpad'de (worktree ICINDE DEGIL).
- **Arac yasaklari ONAY:** `curl -I` 0 · PAT talebi 0 · rig tur icinde **1 kez** baslatildi
  ve o **MERKEZE ait** (ORTAK-KURAL yazilmadan once) · depoda `dotnet build` KOSULMADI
  (API/Core DLL mtime 3 Eylul 15:43'te sabit).
- **KOSULLU: sir hijyeni.** Beyan edilen uc kayma (`.vtok`, `.tA`, `se_rid.tmp`) **ucu de
  gercekten silinmis**. Ama ajanlar artefaktlari **%TEMP% kokune** yazmis ve orada
  **`/tmp/.ses` SILINMEDEN KALMIS** — sahipsiz (alti defterin hicbiri anmiyor).
  **-> MERKEZ KARARI: SILINDI (bolum 9).**
- **OLCUT DUZELTMESI:** gorevdeki "musteri basina TAM 3 `consent_records`" olcutu
  **EVRENSEL DEGIL**; 175'in 0 consent'i ihlal degil (misafir checkout'tan dogmus, consent
  yalniz register yolunda yaziliyor). *Verilen olcut de bir IDDIADIR ve sinanir.*

### 7.3 RAPOR DENETCISI (SDP 1.3 (a)-(f) + 1.12.10-b/c)
**DORT SAYAC: UYDURMA ADAYI 0 · SAYI UYUSMAZLIGI 6 · CAPRAZ CELISKI 2 · SIDDETI DUSURULEN 2.**
- **(a) 0:** 375 ayrik `dosya:satir` iddiasindan sabit tohumlu (`srand(20260904)`) **16
  orneklem** + ~40 hedefli, hepsi ONAY. Yalniz iki 1-satir kaymasi (SDP 1.6: itiraz degil).
- **SIDDET DUSURMELERI:** `SC-2` YUKSEK->ORTA (kok cift sayimi + 2 kanal) · `SC-3`
  YUKSEK->ORTA **ve ON KOSUL `KIMLIKSIZ-UZAK` -> `ilgisiz`**: *"kimse okumuyor diye
  KIMLIKSIZ-UZAK yazilmis"* — oysa o eksen **kim TETIKLEYEBILIR** sorusunu yanitlar; siddet
  TAM DA O EKSENDEN sisirilmis. Ayrica kanal 3 degil 2 ve **yanlislayicisi depo DISINDA**.
- **KANAL DUZELTMESI 6:** ortak kok — depodan okunan yapilandirma ya da ikinci bir grep
  AYRI KANAL sanilmis. Hicbiri bir SUPHE'yi iddiaya donusturmedi.
- **Y-3 — DENETCININ KENDI BULDUGU (hicbir defterde YOK):** S-D'nin `guest_name`=151
  olcumu, SC-6'nin teshis ettigi maskesiz EF kanalindan log'a **UC YENI SATIR** dusurdu ve
  sizan alan `customers.phone` DEGIL **`addresses.full_name`** — bir INSAN ADI, duz metin,
  diske (`api-kalici.log:5886, 5913, 5940`).
- **Y-4 CELISKI:** S-A "sayac sifirlaniyor" <-> S-B "sifirlama geri aliniyor" — ayni
  satirlar, ters sonuc. Denetcinin kaynak okumasi **S-B'yi dogruluyor**; SB-1'i ZAYIFLATMAZ,
  GUCLENDIRIR. **-> MERKEZ: kayit, S-B dogru.**
- **TAKDIR (kanal disiplini):** S-E, muhur `01-oturum-devri.md:503` tarihsel olcumunu
  *"[muhur = TEYIT, bagimsiz kanal SAYILMADI]"* diye ACIKCA disarida birakti.

---

## 8. PILOT OLCUMU (SDP 1.12.13 — kalibrasyon)

| Ajan | Jeton | Arac cagrisi | Sure (dk) |
|---|---|---|---|
| S-A at-rest + 2FA | 227.551 | 51 | 19,3 |
| S-B TOCTOU/CAS | 254.620 | 68 | 27,0 |
| S-C A09 | 265.750 | 82 | 28,8 |
| S-D controller + olay isleyici | 241.195 | 93 | 24,0 |
| S-E odeme/3DS | 262.800 | 65 | 24,9 |
| S-F regresyon | 300.190 | 83 | 21,5 |
| Kapsam elestirmeni | 248.641 | 61 | 27,8 |
| Kural-uyum | 267.876 | 84 | 34,6 |
| Rapor denetcisi | 380.604 | 49 | 27,3 |
| **TOPLAM** | **2.449.227** | **636** | **235 (ajan-zamani)** |

Alti olcum ajani **worktree'siz**, ana agacta SALT OKUMA kostu; MK-4b bu turda **denetci
duzeyinde** uygulandi (uc worktree). Rapor denetcisinin ampirik izolasyon olcumu: kosegen
disi capraz atif **hepsi ya zorunlu kurgu tablosu ya kapsam siniri notu — hicbiri baska
ajanin SONUCUNA dayanmiyor** (NEG `AV2-ZZ-` 0/6).

---

## 9. KURGU ENVANTERI

**Dort kalici kayit, dordu de URETIM YOLUNDAN:**
`av2.sb.1`(**172**) · `av2.sf.1`(**174**) · `av2.sd.2`(**175**) · `av2.sc.1`(**177**).

**URETIM IMZASI (1.12.10-a):** dort satirin dordunde de `DATALENGTH(password_hash)` = **69**
ve `(password_salt)` = **16** -> GF-1/K6 v2 zarfi. Elle bir `INSERT` bunu tutturamaz.
`created_at` 22:50:29–22:52:37 = turun saat araliginda.

**173 ve 176 — SAF IDENTITY BOSLUGU, YETIM SATIR DEGIL.** Kural-uyum denetcisinin kendi
olcumu: `customers` 173 -> 0 satir, 176 -> 0 satir; yetim taramasi `customer_id IN (173,176)`
-> `addresses` 0 · `orders` 0 · `consent_records` 0 · `user_sessions` 0 · `carts` 0.
Basarisiz/geri alinmis INSERT'lerin tukettigi identity degerleridir.

**175 MISAFIR YOLUNDAN DOGDU (D-YAN satiri).** `av2.sd.2@example.com` register'dan DEGIL,
anonim `POST /api/guest-checkout/place` ile olustu (SD-7'nin 151-karakter reprosu); bu yuzden
`consent_records` **0** tasiyor ve o e-posta misafir checkout'tan **kalici olarak disaniyor**.
Uc kanal ortustu: DB · kaynak (`AuthManager.cs:201-206`) · canli log (register 201 sayisi
TAM 3 = 172/174/177).

**MAX'LAR (kapanis):** musteri **177** · urun **955** · siparis **286** · adres **119** ·
fatura **119** · `user_sessions` **369** (+13 = 11 login + 2 refresh ile BIREBIR aciklaniyor).
**MK-3 UCLUSU uc olcumde de: 35 / 9 / 210 / 3837.** `orders`'a Pending URETILMEDI.
Omer'in hesabi (musteri 10) KULLANILMADI.

**`/tmp/.ses` SILINDI (merkez karari).** Silme oncesi olculdu: 53 bayt, 2 satir,
md5 `b3deb40f85da1e2140109e3fbd6c7057`, mtime `2026-09-04 22:45:42`.
**DORTLU (deger YAZILMAZ):** `satir 1 | /tmp/.ses:1 | UZUNLUK 13 | epoch-ms, yalniz rakam` ·
`satir 2 | /tmp/.ses:2 | UZUNLUK 36 | TAM GUID, BUYUK HARF hex`.
Canli kimlik bilgisi DEGILDI (`orders.request_id` · `order_number` ·
`user_sessions.refresh_token` · `email_verification_token` · `password_reset_token` — besinde
de 0 esleme). Silme dogrulandi (NEG kontrol: var olan bir dosya ayni dedektorle VAR dondu).
**KAYIT:** `%TEMP%` kokunde **19 ajan artefakti daha duruyor**; kural-uyum denetcisi bunlari
tarayip **gercek sir 0** olctu (JWT 0 · anahtar+deger 4 eslesmenin dordu de KAYNAK KODU ·
e-posta yalniz kurgu). Tarif yalniz `.ses`'i silmeyi soyledi; **digerleri silinmedi** ve
SDP 1.12.6-(b)'nin v1.4 eki bundan sonra o yolu YASAKLIYOR.

---

## 10. CC / AJAN HATALARI

**ANA AKIS (merkez tarafi) — 1:**
Rig'i "kalkmadi" sandim. `/api/health` sorguladim, **404** dondu (o uc YOK) ve PowerShell
`Invoke-WebRequest` 404'te istisna atti; ayrica `Get-Process -Name "Divisima.API"` BOS dondu
cunku surec adi **`dotnet`**. **Iki dedektor de POZ KONTROLSUZDU** (SDP 1.7/1 ihlali).
API zaten kosuyordu; dogru uc `/health` (200). Kural-uyum denetcisi BAGIMSIZ dogruladi:
22:36:23 baslatmasi ve onu izleyen uc 404 **bana ait**. -> CLAUDE.md B6 dersi.

**AJANLAR (secme):**
1. **S-F — sir hijyeni 1.12.6-(b), IKI KEZ.** Dogrulama jetonunu ve bir access token'i
   scratchpad'e YAZDI, kullanimdan sonra sildi (denetci silindigini BAGIMSIZ dogruladi).
   Kok sebep: kabuk degiskeni cagrilar arasi yasamiyor. -> **SDP v1.4 eki**.
2. **S-E — ayni sinirda kayma** (`se_rid.tmp`, silinmis).
3. **KAPSAM ELESTIRMENI — `grep -oiF` BU KABUKTA OLU DEDEKTOR.** 27 ankrajlik taramada
   "27/27 sifir" verdi; POZ kontrolle yakalandi ve **SSRF disindaki 26 sonuc YANLISTI**.
   -> **Suzgec kutuphanesi S6**.
4. **KURAL-UYUM — rig log'unu tek gunluk sandi.** `awk '/^\[2[23]:/'` yedi gune yayilan
   log'da yanlis pencere uretti ve bir an "order/place 201 var" sandi; **DB ile celisince**
   satir araligiyla (5808..6225) sabitleyip duzeltti. *Zaman damgasi TARIH TASIMIYORSA saat
   suzgeci PENCERE DEGILDIR.*
5. **RAPOR DENETCISI — `wwwroot` sayimini once WORKTREE'de yapti**, 0 dosya gordu ve S-E'ye
   yanlis yere ITIRAZ edecekti; `wwwroot/uploads` gitignore'lu. Ana agacta olcup duzeltti.
6. **S-D — capa supurmesini once yalniz `Messages.cs`'e surdu**, 11 "olu" capa cikti;
   korpusu genisletince **3**'e dustu. Raporlansa bulgu **3,7 kat abartilmis** olacakti.
7. **S-E — `request_id` "zaman damgasi" hipotezi CURUDU** (`api-bridge.js:2281`
   `crypto.randomUUID()`; timestamp-benzeri degerler onceki dalgalarin fikstur'leri).
8. **KURAL-UYUM — MK-7 capa ailesine yeni vaka:** `-name "*rid*"` deseni `api-b`**rid**`ge`'i
   esledi (10/10 yanlis pozitif).

---

## 11. MERKEZ KARARLARI

**DENETCI 7 KARARI — HEPSI ONAYLANDI:**
1. **SC-7 = SE-2 BIRLESIR** — tek bulgu: tetik `KIMLIKSIZ-UZAK`, hasar `YEREL`, **kanal
   AKTIF / yuk LATENT**, kanal 3.
2. **SC-2, SC-1 kokune katilir** (SDP 1.11.7 kok birlestirme).
3. **SC-3: ORTA / `ilgisiz`** (siddet ve on kosul birlikte duzeltildi).
4. **SD-7'ye `[VERI-BOZAN]` eklenir; Y-3 AYRI BULGU** (SC-6 kapsam genislemesi:
   `addresses.full_name`).
5. **B-27 KAPANDI** — B9 BILINEN listesine "KAPANDI (AV-2, 4 Eyl)" notu. **`00b` arsivi
   DEGISMEZ (MK-11/d).**
6. **ON KOSUL TANIMI SDP'ye girer** -> 1.12.2 v1.4 eki.
7. **Y-4 KAYIT** — S-B'nin okumasi dogru.

**ACIK KALEMLER — KAPATILANLAR:**
- `/tmp/.ses` **SILINDI** (bolum 9, silme oncesi dortlu + md5 kayitli).
- INDEX'e **GF-4 AD CAKISMASI UYARISI** eklendi (GF-3'tekiyle ayni bicim):
  `18-guvenlik-fix-4.md` = **Agustos 2026** (Sprint donemi, `00b` #22'yi kapatan) ·
  `50-guvenlik-fix-4.md` = **4 Eylul 2026** (tedarik zinciri).
- **"· Stock yuzeyi"** B9 kuyruk satirina GERI kondu (provenans).

**SDP SKILL v1.3 -> v1.4** (`.claude/skills/sdp/SKILL.md`):
19.928 -> **22.321 B** (+2.393) · 342 -> 377 satir · md5
`6b06a7db140db5a45f13d723353be695` -> **`489eecfba6501a66f4aa403c516d8972`** · CR 0.
Uc ek: **1.12.2** (on kosul = KIM TETIKLEYEBILIR) · **1.12.6-(b)** (gecici dosya/sil-yaz
dahil YASAK; scratchpad disi hicbir yol) · **1.12.10** (kapsam matrisi KUMULATIF).
1.12'nin TAM METNI arsivde kalir (`42·AV-1`, MK-11/d — **arsiv DEGISTIRILMEDI**); skill
govdesindeki v1.4 bolumu onu **degistirerek tamamlar** ve celiskide **skill gecerlidir**.

**GF-5 BOLUMLEMESI (launch ONCESI, TEK DALGA):** A09 iz/atif (SC-1 + SC-2 + SC-4 + SC-10 +
SC-13) · misafir yolu butunlugu (SD-7 + SE-4 + `:503-504` atomiklestirme) · maske
(SC-7=SE-2 + SC-6/Y-3 + SE-3 + SC-12) · imzasiz webhook (SE-5).
**GF-5 BU TURDA BASLATILMADI.**

---

**AV-2 KAPANDI — `ce54d0c` zemininde, SALT OLCUM.**
Kuyrukta sirada: **GF-5** (launch oncesi) -> **AV-3 DAR** -> **LAUNCH GO/NO-GO turu**.
