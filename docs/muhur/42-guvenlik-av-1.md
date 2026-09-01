# 42 · GUVENLIK-AV-1 MUHRU — TAM-OWASP SALT OLCUM (ULTRACODE PILOTU)

**Zemin:** `c6721b7` · **Tur turu:** SALT OLCUM (FIX YOK, uretim kodu DEGISMEDI) ·
**Tarih:** 2026-09-01 · **SDP:** v1.2 kosuldu, bu muhurle **v1.3**'e cikar (1.12 eklendi) ·
**MK-11 c usulu** (ARSIV-1 P2 kalibi): tam metin BURADA, CLAUDE.md yalniz delta alir.

---

## 0. KAPI

**Acilis:** HEAD = origin/main = `c6721b7` · dal `main` · agac 0 · stash 0 · tek worktree ·
`dotnet build` **0 Hata** (1436 uyari; `grep " Hata"` ile bakildi — `tail -1` ALDATIR) ·
MK-3 uclusu ve MAX'lar merkez tarifiyle **birebir**.

**Kapanis:** agac **0** · HEAD degismedi, yeni commit yok · MK-3 uclusu **birebir ayni** ·
Pending(`status=0 AND id>210`) **10'da sabit** · para/stok/siparis/fatura yuzeyine **tek
satir yazilmadi** (43 tablo supuruldu, 28'i sifir).

**CI (S3 suzgeci, `head_sha=c6721b7`):** `total_count=2` — `CI - Build & Test` (2 job) +
`Security CI` (4 job). `"conclusion":"failure"` **iki dosyada da 0** · cancelled 0 ·
NEG kontrol `"conclusion":"ZZZfailure"` -> 0.

**Sapma-1 (K8):** merkez tarifi "dort run yesil" diyordu; olculen **iki run / alti job**.
Merkez oncul duzeltmesi: *"dort run" ARSIV-1'in iki push'uydu; kapi HEAD'in iki run'idir.*
Kayda gecti, DUR sebebi sayilmadi.

**Sapma-2 (K8, KABUL):** depo ici `scratchpad/` **`.gitignore`'da DEGIL**
(`git check-ignore` ciktisi BOS; `git status` -> `?? scratchpad/`). Orada defter tutmak
"agac 0" kapisini KIRARDI -> defter oturum scratchpad'ine (depo DISI) tasindi, `gav1/` adi
korundu; tasima sonrasi `git status --porcelain` = **0**.

**Olculen calisma ortami (SDP 1.7/3 — ZORUNLU ANMA):**
```
P1  PID 36288  http://localhost:5000   5 arguman:
    --Iyzico:UseRealSdk=false --AdminSeed:Enabled=false --BackgroundJobs:Enabled=false
    --RateLimit:AuthPermitLimit=100 --MailSettings:Host=
P2  PID 38312  http://localhost:5001   4 arguman (RateLimit override YOK = urun varsayilani)
```
**BUNLAR URUN VARSAYILANI DEGILDIR (B-18).** Ikisi de `ASPNETCORE_ENVIRONMENT=Development`,
ayni DB (`localhost/DivisimaDb`). Statik vitrin `:5173`. Port tahsisi: patlama olcumleri
YALNIZ P2 (Redis KAPALI -> limiter surec-ici ve porta ozel). Tur sonunda iki surec de
DURDURULDU (kosan API `dotnet build`i MSB3027 ile kirar).
`/api/products` -> **404** olculdu; rota adlari tahminle tutmuyor (SDP 1.7/2 ornegi olarak
ajanlara verildi).

---

## 1. MERKEZ KARARLARI (K1..K10)

| # | Karar |
|---|---|
| **K1** | **GAV1-C-1 YUKSEK KABUL** — mekanizma denetci duzeltmesiyle yazilir: `RevokeAsync` **olu degil**; `TokenBlacklistMiddleware.cs:31` **okuma tarafi CANLI** (`Program.cs:599` ile boru hattina kayitli), ama **yazma tarafi uretimde 0 cagri** ve `user_sessions`ta **`jti` kolonu YOK** -> kara liste hic yazilmadigi icin okuma her zaman `false` doner: **yapisal olu kod**. |
| **K2** | **GAV1-E-1 -> ORTA**, ve **IKIYE BOLUNUR**: **E-1a** `docker-compose.yml:57` degeri (zaten PUBLIC — dosyanin 56. satiri "yalniz yerel gelistirme icindir" diyor; **asil kusur**: bilinen-public bir deger `Program.cs:84-86` placeholder listesinde YOK ve `Program.cs:76` uzunluk kapisini (51 >= 32) geciyor, yani uretim fail-fast'i GECIRIYOR). **E-1b** CI degeri FARKLI (`ci.yml:67` / `security.yml:223`); kacis `ci.yml:65-69` ve `security.yml:221-225`'te **bilincli ve YAZILI** -> **KABUL**, bulgu degil. |
| **K3** | **GAV1-C-2 ve GAV1-C-3 -> BILINEN.** C-2 kaynagi `00a:108` (launch sonrasi defteri: "step-up `auth_time` refresh'te sifirlanmasi"), C-3 kaynagi `00a:101` (GUVENLIK-FIX-2 eki: `SellerAuthManager.Login` kilit kontrolu SIFRE DOGRULAMASINDAN ONCE). **C-2 GF-1'de KALIR** (bilinen olmasi fix kapsamini daraltmaz), **C-3 BIRIKTIR**'e gider. |
| **K4** | **GAV1-A-1 -> `[MANTIK]`**, guvenlik bulgusu degil. VITRIN-KALAN'in **7. kalemi** olur; `LOWER()` sarmalayici (CLAUDE.md 6c KALICI yasagi) ihlali de bu kaleme dahildir. |
| **K5** | **SEC-D ic sirasi ON KOSULA gore duzeltilir:** `D-7, D-8` (KIMLIKSIZ-UZAK) > `D-5, D-9` (KIMLIKLI) > `D-2, D-3, D-4, D-10, D-11` (ADMIN). (Denetci itirazi #6: 11 bulgunun 10'u ORTA -> siralama ekseni cozunurluk kaybetmisti.) |
| **K6** | **Kural-uyum denetcisinin bulgusu (ciplak jeton) ORTA-AKTIF KABUL**; **M0 ile KAPANDI** (bkz. bolum 7). |
| **K7** | **ULTRACODE PILOT SONUCU:** 2.472.858 alt-ajan jetonu · 71 dk · 10 ajan · 35 bulgu · kapsam <=27/40. MANTIK-AV-1 fan-out'unun (~520-540k) **~4,5 kati**. **Ikinci kullanim karari YOK**; **GUVENLIK-AV-2 ultracode'suz ve DAR** kosulacak. |
| **K8** | Sapma-1 = **merkez onculu** ("dort run" ARSIV-1'in iki push'uydu; kapi HEAD'in iki run'idir) — kayit. Sapma-2 (defter konumu) — **KABUL**. |
| **K9** | **GUVENLIK-FIX bolumleme ONERISI** (GF-1..GF-6 + BIRIKTIR + AV-2) kayda gecer; **karar merkezden, SONRAKI TUR**. |
| **K10** | **Comparison/Collection HAM ENTITY suphesi** + **AES `Decrypt` istisna yutma suphesi** -> **GUVENLIK-AV-2'nin BAS KALEMLERI**. |

---

## 2. BULGU OZETI

**35 bulgu — 0 KRITIK · 2 YUKSEK · 19 ORTA · 14 DUSUK.**
OWASP dagilimi: A01 2 · A02 1 · A03 9 · A04 4 · A05 8 · A06 5 · A07 3 · A08 1 · A09 2 · A10 0.
**`[PARA]` sinifi bulgu SIFIR** — 35 bulgunun hicbiri para akisina dokunmuyor
(rapor denetcisinin kapsam gozlemi; MANTIK-AV-1'de PARA kalemi listeden DUSMUSTU, burada
dusen degil **hic dogmayan** bir sinif).

### EN RISKLI 10 (olcut: OTURUM > DURUSTLUK > MANTIK; esitlikte kanal sayisi, sonra on kosul)

1. **C-1** YUKSEK · KIMLIKLI · AKTIF · **3 kanal** — `AuthManager.cs:605-622` (Logout),
   `:597` (ResetPassword), `AccountManager.cs:135` (ChangePassword). Ucu de yalniz
   `user_sessions.is_active`'e dokunuyor -> **access token IPTAL EDILMIYOR**; calinan jeton
   ~15 dk tam read+write yasiyor. **B-02'nin SINIRINI genisletiyor.**
2. **D-7** ORTA · KIMLIKSIZ-UZAK · AKTIF — `index.html:5`, `admin.html:5`. Vitrin CSP'si
   `unsafe-inline` + `unsafe-hashes` + `blob:`; 11 satir ici script yuzunden kaldirilamiyor
   (nonce/hash YOK). **Diger sekiz XSS bulgusunun CARPANI.**
3. **E-1** (K2 ile ORTA) · YEREL · LATENT · **tek kanal** — `docker-compose.yml:57` +
   `Program.cs:76`, `:84-86`. Commit'li JWT anahtari her iki uretim fail-fast kapisini da
   geciyor (51 bayt >= 32; placeholder listesinde yok).
4. **D-1** ORTA · YEREL · AKTIF — `api-bridge.js:2613,2628,3413`, `index.html:3412`.
   DOMPurify yuklu ve **fail-closed sarmalayici VAR** ama **131 sink'in 1'inde** cagriliyor;
   `admin.html` purify'i **hic yuklemiyor** -> yanlis kapsam hissi.
5. **D-8** ORTA · KIMLIKSIZ-UZAK · AKTIF · tek kanal — `admin.html:17` (etiket), `:16`
   (deponun KENDI "YAPILACAK" yorumu), `:5` (CSP `cdn.jsdelivr.net`'e izin veriyor).
   Chart.js CDN'den `integrity=` OLMADAN -> CDN yaniti degisirse **admin oturumu**.
6. **F-1** ORTA · KIMLIKSIZ-UZAK · AKTIF — `CouponController.cs:81-82`,
   `GiftCardController.cs:25-26,32-33`, `SearchController.cs:18-19`,
   `ProductReviewController.cs:29-30`. Kimlik/para uclarinda siki limit YOK (yalniz global
   100/dk/IP); **kupon yaniti gecerli/gecersizi AYIRT EDIYOR** -> anonim kod enumerasyonu.
7. **B-1** ORTA · KIMLIKLI · AKTIF · 3 kanal — `ReturnManager.cs:66-67` (403 dali) vs
   `:62-63` (404 dali). Sahiplik ihlalinde **403**, yoklukta 404 -> siparis **VARLIGI**
   siziyor; `SecureControllerBase.cs:23-24`'un "tek sozlesme = 404" beyanina **AYKIRI**.
   Siparis ICERIGINE erisim YOK.
8. **C-2** ORTA · KIMLIKLI · AKTIF (**BILINEN**, K3) — `JwtHelper.cs:43-44`,
   `AuthManager.cs:364-385,459`. `auth_time` **her jeton uretiminde** tazeleniyor (refresh
   dahil) -> step-up "son 10 dk giris" vaadi **ETKISIZ**.
9. **E-3** ORTA · YEREL · AKTIF — `SmtpMailService.cs:42,81`, `ExceptionMiddleware.cs:29`,
   `ops/serilog-siem.md` (politika boslugu). Musteri e-postasi log'a **duz** yaziliyor;
   SIEM belgesinde **hicbir PII/maskeleme kurali yok** (KVKK).
10. **E-2** ORTA · YEREL · LATENT — `IyzicoClient.cs:195-196,198`. Iyzico odeme jetonu
    **KanitMaskesi'nden GECMEDEN** log'a yaziliyor — maskenin *cagrilmasi gereken ama
    cagrilmayan* yeri. **Kuralin kendi ailesinin yeni vakasi.**

---

## 3. 35 BULGUNUN TAM TABLOSU

**KANAL sayimi olcutu:** `REPRO` = en az IKI bagimsiz kanal (kaynak + canli-API / DB /
tarayici / arac) · `STATIK` = TEK kanal (kaynak) · `SUPHE` = tek kanal, **IDDIA DEGIL**.
Kanal harfleri: K=kaynak · C=canli-API · D=DB · B=tarayici · T=arac.
**KURAL:** tek kanalli bir bulgu, cok kanalliyla AYNI siddet sirasina KONMAZ.

| ID | SIDDET | ON KOSUL | DURUM | OWASP | KANIT | KANAL | KONUM (dosya:satir) | BILINEN? | MERKEZ |
|---|---|---|---|---|---|---|---|---|---|
| C-1 | YUKSEK | KIMLIKLI | AKTIF | A07 | REPRO | 3 (K+C+D) | `AuthManager.cs:605-622`, `:597`; `AccountManager.cs:135` | B-02 sinir genisletme | **K1 KABUL** |
| C-2 | ORTA | KIMLIKLI | AKTIF | A07 | REPRO | 2 (K+C) | `JwtHelper.cs:43-44`; `AuthManager.cs:364-385`, `:459` | **BILINEN `00a:108`** | **K3** GF-1'de kalir |
| C-3 | DUSUK | KIMLIKSIZ-UZAK | LATENT | A07 | STATIK | 1 (K) | `SellerAuthManager.cs:72` vs `:75`; `SellerAuthController.cs:48-56` | **BILINEN `00a:101`** | **K3** BIRIKTIR |
| C-4 | ORTA | YEREL | LATENT | A02 | STATIK | 1 (K) | `HashingHelper.cs:10-26` | hayir | — |
| B-1 | ORTA | KIMLIKLI | AKTIF | A01 | REPRO | 3 (K+C+D) | `ReturnManager.cs:66-67` vs `:62-63`; `SecureControllerBase.cs:23-24` | hayir | — |
| B-2 | ORTA | YEREL | LATENT | A01 | STATIK | 1 (K) | `Program.cs:640`, `:635-639`; `SecurityHardeningTests.cs:626`, `:645`, `:629` | B-06 **sinir genisletme** | — |
| A-1 | DUSUK | KIMLIKSIZ-UZAK | AKTIF | A04 | REPRO | 3 (K+C+D) | `SearchManager.cs:37`, `:45` | CLAUDE.md 6c'nin AKSI | **K4 -> `[MANTIK]`** |
| A-2 | DUSUK | KIMLIKSIZ-UZAK | LATENT | A04 | STATIK | 1 (K) | `SearchManager.cs:53`, `:55`, `:91`, `:100` | hayir | — |
| A-3 | DUSUK | ADMIN | LATENT | A03 | **SUPHE** | 1 (K) | `PriceDropManager.cs:92`; `StockNotificationManager.cs:95`; `SmtpMailService.cs:56` | hayir | IDDIA DEGIL |
| D-7 | ORTA | KIMLIKSIZ-UZAK | AKTIF | A05 | STATIK | 1 (K) | `index.html:5`; `admin.html:5` | hayir | **K5: 1.** |
| D-8 | ORTA | KIMLIKSIZ-UZAK | AKTIF | A05 | STATIK | 1 (K) | `admin.html:17`, `:16`, `:5` | hayir | **K5: 2.** |
| D-5 | ORTA | KIMLIKLI | LATENT | A03 | STATIK | 1 (K) | `index.html:2583`, `:2584`; `api-client.js:161-162`; `api-bridge.js:3103,3137,3252,3259,3468,3503` | B-11 capraz | **K5: 3.** |
| D-9 | ORTA | KIMLIKLI | LATENT | A03 | STATIK | 1 (K) | `api-bridge.js:2293-2318`, `:2320`, `:2258`; `IyzicoPaymentManager.cs:174` | hayir | **K5: 4.** |
| D-1 | ORTA | YEREL | AKTIF | A03 | STATIK | 1 (K) | `index.html:3412`; `api-bridge.js:2613`, `:2628`, `:3413`; `admin.html` (purify 0) | hayir | — |
| D-6 | ORTA | YEREL | LATENT | A05 | STATIK | 1 (K) | `service-worker.js:72-80`; `caches.` 0 gecis (4 dosya) | hayir | — |
| D-2 | ORTA | ADMIN | LATENT | A03 | REPRO | 2 (K+B) | `index.html:1682`, `:1686`; celiskili ikiz `:2052` | hayir | **K5: 5.** |
| D-3 | ORTA | ADMIN | LATENT | A03 | STATIK | 1 (K) | `index.html:1682` (`style=`) | hayir | **K5: 6.** |
| D-4 | ORTA | ADMIN | LATENT | A03 | REPRO | 2 (K+B) | `index.html:1684`, `:1686`, `:3122`; ikiz `admin.html:742` | hayir | **K5: 7.** |
| D-11 | ORTA | ADMIN | LATENT | A03 | STATIK | 1 (K) | `admin.html:230`, `:448`; kacisli ikizi `:233` | hayir | **K5: 8.** |
| D-10 | DUSUK | ADMIN | LATENT | A03 | STATIK | 1 (K) | `index.html:2663`; ikizler `:2630`, `api-bridge.js:1904` | hayir | **K5: 9.** |
| E-1 | ~~YUKSEK~~ **ORTA** | YEREL | LATENT | A05 | STATIK | **1 (K)** | `docker-compose.yml:57`; `Program.cs:76`, `:84-86` | hayir | **K2 IKIYE BOLUNUR** |
| E-2 | ORTA | YEREL | LATENT | A09 | STATIK | 1 (K) | `IyzicoClient.cs:195-196`, `:198` | hayir | — |
| E-3 | ORTA | YEREL | AKTIF | A09 | REPRO | 2 (K+C) | `SmtpMailService.cs:42`, `:81`; `ExceptionMiddleware.cs:29`; `ops/serilog-siem.md` | B-09 KOMSU, AYRI | — |
| E-4 | DUSUK | KIMLIKSIZ-UZAK | LATENT | A05 | STATIK | 1 (K) | `Program.cs:568`; `ops/infra/nginx.conf:26`; `ops/infra/divisima-security-headers.conf:31` | hayir | — |
| E-5 | DUSUK | YEREL | LATENT | A05 | STATIK | 1 (K) | `Program.cs:84-86`; `appsettings.json:19,23,24,43` | hayir | E-1 ile KOMSU (itiraz #7) |
| E-6 | DUSUK | KIMLIKLI | LATENT | A05 | STATIK | 1 (K) | `ETagMiddleware.cs:33`, `:62`, `:91-92`; `ProductController.cs:109-110` | hayir | — |
| F-1 | ORTA | KIMLIKSIZ-UZAK | AKTIF | A04 | REPRO | 2 (K+C) | `CouponController.cs:81-82`; `GiftCardController.cs:25-26,32-33`; `SearchController.cs:18-19`; `ProductReviewController.cs:29-30` | hayir | — |
| F-2 | DUSUK | KIMLIKSIZ-UZAK | LATENT | A05 | REPRO | 2 (K+C) | `Program.cs:339-342`, `:366-374`, `:477-493`, `:555`; `RedisRateLimitMiddleware` | B-11 capraz | kosullu (loopback) |
| F-3 | DUSUK | ADMIN | LATENT | A04 | STATIK | 1 (K) | `ProductController.cs:47-54`; `ProductManager.cs:106-175` | hayir | — |
| G-1 | ORTA | YEREL | LATENT | A06 | STATIK | 1 (K) | `security.yml:113-128`; `dependabot.yml:24-25`; `Divisima.Bussiness.csproj:8`; `Divisima.API.csproj` | B-07 **sinir genisletme** | — |
| G-2 | ORTA | YEREL | AKTIF | A06 | REPRO | 2 (K+T) | `Directory.Build.props` (NuGetAudit ailesi HIC TANIMLI DEGIL); tum `Divisima.*/*.csproj` | hayir | — |
| G-3 | ORTA | KIMLIKSIZ-UZAK | LATENT | A06 | STATIK | 1 (K) | `security.yml:42-146`, `:156`; `dependabot.yml:12,40,53` | hayir | — |
| G-4 | DUSUK | YEREL | LATENT | A08 | STATIK | 1 (K) | `Dockerfile:2`, `:15`; depo koku (lock/NuGet.config/Directory.Packages.props/global.json YOK) | hayir | — |
| G-5 | DUSUK | YEREL | AKTIF | A06 | STATIK | 1 (K) | `security.yml:145-146` (`\|\| true`); `Divisima.Core.csproj:22`; `Divisima.API.csproj:11` | hayir | — |
| G-6 | DUSUK | YEREL | LATENT | A06 | STATIK | 1 (K) | `security.yml:73`, `:58-61`; `Divisima.IntegrationTests.csproj` | hayir | — |

**KANAL DAGILIMI:** 3 kanal **3 bulgu** (C-1, B-1, A-1) · 2 kanal **8 bulgu** · 1 kanal
**24 bulgu** (biri SUPHE). **En riskli 10'un 6'si tek kanallidir** — bu, siralamada acikca
belirtilir ve fix onceliginde **cok kanallilar ONCE** gelir.

### YETKI MATRISI OZETI
40 controller (`SecureControllerBase` **taban sinif**, controller degil — ilk sayim 41'di,
denetci duzeltti) + **0 minimal-API ucu** + 1 hub + 3 health ucu.
`[Authorize]` kullanan controller **0**; gercek mekanizma `RequireUserType` (33 dosya) +
`SecureControllerBase` (17 dosya). Kapsam elestirmeni bu mekanizmayi ILK taramada YANLIS
aradi ve **kendi hatasini kayda gecirdi** (K2/10).
Controller DISI bes kayit: `Program.cs:640` `MapControllers().RequireAuthorization()` ·
`:641` `MapHub<NotificationHub>("/hubs/notification")` · `:646/:648/:652` health
(`AllowAnonymous`). **`MapGet/MapPost/... sayisi = 0`** -> B-06 / SUPHELI #20 bugun
**LATENT** (capraz dogrulama). Hub kendi `[Authorize]`'i ile korunuyor (`NotificationHub.cs:9`,
`:17-19`) -> **SAGLAM**, ama fallback'e DEGIL kendi ozniteligine dayaniyor.
**Sahiplik kontrolsuz uc BULUNAMADI**: dinamik A/B'de 200 donen kalem yok; tek sahiplik
kusuru **B-1** (403/404 sozlesme ayrismasi, varlik sizintisi).

### XSS SINK OZETI
**131 uretim sink** — `index.html` 53 · `admin.html` 30 · `api-bridge.js` 48.
Rapor denetcisi **bagimsiz sayimla BIREBIR** dogruladi
(`grep -coE 'innerHTML[[:space:]]*[+]?=[^=]'`).
DOMPurify cagri sayisi: `index` 1 · `bridge` 7 · `admin` **0**.
Kacissiz + kullanici-verili sink'ler: **D-2, D-3, D-4, D-5, D-10, D-11**.
**Tekrarlayan desen:** ayni alan bir yerde `esc()`'li, birkac satir otede **ciplak** —
"ayni kuralin ikinci kopyasi" ailesinin XSS yuzeyindeki karsiligi.
MK-7 notu: `innerHTML=` **bosluksuz** bicimi de eslesmeli; bu kacirilma bu depoda daha once
olculdu (aile sayaci 4. vaka) ve suzgec POZ/NEG sinandi.

### SECRETS SAYIMI (DEGER YAZILMADI)
`appsettings.json` (tracked): **CHANGE_ME** — gercek deger YOK.
`appsettings.Development.json`: `.gitignore:84` ile **takip DISI**;
`TokenOptions:SecurityKey` uzunluk **64**, `Encryption:Key` uzunluk **44**,
Iyzico `ApiKey`/`SecretKey` uzunluk **0**.
**Commit'li TEK gercek deger:** `docker-compose.yml:57` (`TokenOptions__SecurityKey`,
uzunluk **51**); dosyanin 56. satiri "yalniz yerel gelistirme icindir, gercek secret
DEGILDIR" diyor -> **zaten public, yeni maruziyet yok**. Bu deger E-1a'nin konusu.
Uretim placeholder dedektoru yalniz `TokenOptions:SecurityKey`'i tariyor (**E-5**).

### BAGIMLILIK
`NuGetAudit=true` ama **`NuGetAuditMode=direct`** + `TreatWarningsAsErrors=false`
(`dotnet msbuild -getProperty` ile **arac kanalindan** dogrulandi) -> **gecisli** zafiyetler
restore/build'de hicbir uyari uretmiyor.
`dependency-scan` yalniz dotnet'i tariyor (`security.yml:42-146`); CodeQL `languages: csharp`;
dependabot ekosistemleri nuget + github-actions -> **vitrin JS (DOMPurify, Chart.js) hicbir
otomatik kanalda gorulmuyor** (G-3).
Deprecated kapisi `|| true` ile **bilerek kirilmaz**; dort paket deprecated (G-5).
Test projesinin **uc YUKSEK gecisli advisory'si** PROD listesi disinda kaldigi icin hicbir
kapiyi kirmiyor; tek gorunur kanali **anonim okunamayan job log'u** (G-6).
Docker imajlari digest'siz; lock / `NuGet.config` / `Directory.Packages.props` / `global.json`
**hicbiri yok** (G-4).
**AutoMapper 12.0.1 (B-07 karari KORUNUYOR):** advisory'nin ETKILENEN ARALIGI kayitli
gerekceyle ortusmuyor; 16.0.x'e bump advisory'yi **KAPATMAZ** ama `ALLOW_IDS` ayni GHSA'yi
listeledigi icin **kapi yesil kalir ve zafiyet KAPANDI SANILIR** (G-1).

---

## 4. DENETCI ITIRAZ TABLOSU (21 itiraz — uc denetci de **KISMI**)

Dagilim: **1 CURUK · 2 ZAYIF · 2 SIDDET-YANLIS · 1 KONUM-YANLIS · 15 EKSIK.**
**HICBIR BULGU OZUYLE CURUMEDI** — curuyen sey bir bulgu degil, bir **KAPANIS IDDIASIYDI**.

| # | SONUC | HEDEF | Merkez notu |
|---|---|---|---|
| 1 | EKSIK | C-3 `BILINEN?` alani "B-08 komsu, oracle YENI" | **K3** — `00a:101`'de KAYITLI |
| 2 | EKSIK | C-2 `BILINEN?` alani "hayir (B-listesinde yok)" | **K3** — `00a:108`'de KAYITLI |
| 3 | **ZAYIF** | C-1 mekanizma tarifi: "RevokeAsync olu; middleware yalniz `is_active` bakar" | **K1** ile duzeltildi |
| 4 | **KONUM-YANLIS** | E-1 KONUM'u UC dosyayi ayni bulguya bagliyor | **K2** ile bolundu (compose degeri != CI degeri) |
| 5 | **SIDDET-YANLIS** | E-1 SIDDET: YUKSEK (tek kanal + LATENT + YEREL) | **K2** -> ORTA |
| 6 | **SIDDET-YANLIS** | SEC-D siddet ekseni (11 bulgunun 10'u ORTA) | **K5** — ic sira ON KOSULA gore |
| 7 | EKSIK | E-1 ve E-5 ayri bulgular (YUKSEK ve DUSUK) — ayni placeholder kapisi | tabloda KOMSU isaretlendi |
| 8 | EKSIK | Bulgu semasi uyumu: 7 ajanin yalnizca 2'si ORTAK KURAL bolum 7 semasini kullandi | **SDP 1.12'ye girdi** (sema zorunlulugu) |
| 9 | EKSIK | `10-secd.md:282-283` `admin.html:106` `API_BASE = localStorage[...]` yapisal bulguya yukseltilmemis | denetci KENDI olcumuyle eledi: `connect-src` CSP keyfi host'a fetch'i ENGELLIYOR -> yukseltmeme SAVUNULABILIR, eksik olan GEREKCE |
| 10 | EKSIK | **BILESIK ETKI** hicbir defterde birlesik yazilmamis: B-01 + D-7 + C-1 | **bu muhurde birlestirildi** (bolum 5) |
| 11 | EKSIK | TARIF KUSURU — denetci prompt'undaki iki dosya yolu bir dizin yukarisi; 8. defter anilmiyor | **CC hatasi 1** |
| 12 | EKSIK | **A09 bu turda FIILEN OLCULMEDI** | **AV-2 kapsamina** |
| 13 | EKSIK | 40 controller'in **13'u** hicbir defterde ANILMIYOR | **AV-2 kapsamina** |
| 14 | EKSIK | **SIFRELEME (at-rest) yuzeyi SIFIR kapsamli** — AKTIF ve 2FA sirrina bagli | **K10 -> AV-2 BAS KALEM** |
| 15 | EKSIK | **2FA / TOTP yuzeyi SIFIR kapsamli** — SEC-C kapsam cumlesinde "2FA" kelimesi YOK | **AV-2 kapsamina** |
| 16 | EKSIK | SSRF ekseni SAHIPSIZ | elestirmen **KENDI kapatti**: 7 cagri yerinde hedef URL sabit/config -> TEMIZ |
| 17 | EKSIK | TOCTOU ekseni 1/8 defter; `ExecuteUpdateAsync` (atomik CAS) **0/8** | **AV-2 kapsamina** |
| 18 | EKSIK | Turun "SECRET DEGERI BASILMADI" kural uyumu | **K6 / M0** ile kapandi |
| 19 | **CURUK** | **SEC-B kapanis iddiasi:** "Sir/jeton: hicbir tam deger yazilmadi; jetonlar ilk 8 karaktere kirpildi" | **YANLISTI** — dokuz dosyada tam deger vardi. **M0 bunu KAPATTI.** |
| 20 | EKSIK | KURGU ENVANTERI tamligi — `user_sessions` (9 satir) ve `review_helpful_votes` (1) beyan edilmemis | bolum 8'e **eklendi**, D-YAN'a devredildi |
| 21 | **ZAYIF** | "access token omru 15 dk" kaleminin BAGIMSIZ CAPRAZ DOGRULAMA olarak sunulmasi | SEC-D, SEC-C'nin sonucunu **GORMUS**; TEYIT'tir, bagimsiz kanal DEGILDIR |

### DENETCILERIN "OLCEMEDIM" LISTESI (SDP 1.11.10-d — ana akisa geri doner)
Ana agacin index'i DOGRUDAN okunamadi (harness reddi; dolayli kanit: index mtime **17:28**
< tur baslangici **19:57**, 19:00'dan yeni gevsek nesne **0**, dedektor `-newermt 2020` ile
**2608** dondurerek POZ kontrollu) · "yalniz SELECT" iddiasi `created_at` tabanli supurgeyle
TAM kapatilamadi (**var olan satirin GUNCELLENMESI gorunmez**) · C-1'in "540 sn kalan omur"
canli olcumu · D-6 service worker davranisi · SEC-F patlama serileri (port tahsisi) ·
E-2'nin ATESLENEN hali (mock mod acik, B-18) · CI kosucusunun `NuGetAuditMode` varsayilani ·
ajan transkriptleri (MK-4a'nin transkript kanali bu turda da YOKTU).

---

## 5. BILESIK ETKI (itiraz #10 geregi — burada BIRLESTIRILDI)

Uc parca uc ayri defterde AYRI AYRI olculdu, **birlesigi hicbir yerde yazilmamisti**:

```
B-01  access token localStorage'da (hibrit model - KABUL EDILMIS KARAR, degismez)
D-7   vitrin CSP'si unsafe-inline tasiyor  -> enjekte edilen HTML SCRIPT OLARAK CALISIR
C-1   calinan access token IPTAL EDILEMIYOR -> kurban logout/sifre degisimi yapsa bile
      ~15 dk daha gecerli
=>    Herhangi bir XSS (D-2/3/4/5/10/11'den biri) -> script calisir (D-7) ->
      localStorage'daki jeton okunur (B-01) -> kurban FARK ETSE BILE oturumu
      kapatamaz (C-1).
```
**Bu zincir SOMURU BETIGI OLARAK YAZILMADI** (SDP 1.12.5). Kayit amaci: **GF-1 ve GF-2'nin
BAGIMSIZ DALGALAR OLMADIGINI** gostermektir — ikisinden yalniz birini yapmak zinciri kirmaz.
`admin.html`'in **purify'i hic yuklemedigi** ve **CDN script'ini SRI'siz cektigi** (D-8)
dusunulurse, ayni zincirin **ADMIN** ayagi daha kisadir.

---

## 6. SDP 1.12 — GUVENLIK TURU MODULU (TAM METIN; SDP v1.3'e girer)

Bu modul SDP CEKIRDEK'e **1.12** olarak girer. GEZGIN TURU (1.11) kalibinin kardesidir:
1.11 "kullanici gibi dolas" der, 1.12 "**saldirgan gibi say**" der. Ikisi de SALT OLCUMDUR.
Her madde GUVENLIK-AV-1'de OLCULEN bir surtunmeye dayanir; dayanmayan madde YAZILMADI.

### 1.12.1 NE ZAMAN KOSULUR
Guvenlik turu, bir urun yuzeyi "islevsel olarak kabul edildi" sayildiktan SONRA ve
**LAUNCH'tan ONCE** kosulur. Gezgin turundan FARKI: gezgin **niyetli kullanicinin** yolunu
yurur, guvenlik turu **niyeti olmayan yolu** arar — kimsenin gitmesi beklenmeyen, bu yuzden
hic olculmemis yol.
Tetikleyiciler: kimlik/oturum kodu degistiginde · yeni bir dis entegrasyon baglandiginda ·
yetki modeli genisledeginde · bir bagimlilik dalgasindan sonra · **her launch oncesi bir kez**.

### 1.12.2 SINIF ve SIDDET
**Yeni siddet sinifi `[GUVENLIK]`**, 1.6'daki listeye eklenir ve onlarla **DIK** kesisir:
bir bulgu hem `[PARA]` hem `[GUVENLIK]` olabilir.
`[GUVENLIK]` bulgusu **UC eksende birden** etiketlenir; ucu de zorunludur:

| Eksen | Degerler | Neden zorunlu |
|---|---|---|
| **SIDDET** | KRITIK · YUKSEK · ORTA · DUSUK | hasarin buyuklugu |
| **ON KOSUL** | KIMLIKSIZ-UZAK · KIMLIKLI · ADMIN · YEREL | **erisilebilirlik** |
| **DURUM** | AKTIF · LATENT | bugun somurulebilir mi |

**KURAL:** siddet ON KOSULDAN BAGIMSIZ VERILEMEZ. `ADMIN` on kosullu bir kalem `KRITIK`
olamaz — admin zaten o yetkiye sahiptir; olsa olsa **yatay** yetki yukseltmesi ya da
denetim izi kaybi olarak degerlendirilir.
**SIDDET GEREKCESI ZORUNLU**; gerekcesiz siddet rapor denetcisi tarafindan DUSURULUR.
*(Bu turda olculdu: E-1 SIDDET-YANLIS, SEC-D ekseni SIDDET-YANLIS.)*

### 1.12.3 HER UC ICIN 6'LI GUVENLIK LISTESI
Dokunulan **HER uc** icin alti soru AYRI AYRI sorulur ve deftere yazilir; "ilgisiz" de bir
yanittir:
1. **KIMLIK** — kim cagirabiliyor?
2. **YETKI-SAHIPLIK** — kaynagin sahibi mi? Sahiplik yukleminin **dosya:satir**'i nerede?
3. **GIRDI** — dogrulama var mi (validator · uzunluk · tur · aralik · allowlist)?
4. **CIKTI-SIZINTI** — yanit fazla sey soyluyor mu (PII · ic hata · sayim · enumeration)?
5. **DURUM-REPLAY** — ayni istek iki kez atilirsa (idempotency · request_id · CSRF)?
6. **HIZ-KOTUYE KULLANIM** — limit var mi, **hangi kaynaktan**?

Alti soru da yazildigi icin **KAPSAM MATRISI (uc x soru) MEKANIK dolar** ve neyin
OLCULMEDIGI gorunur. Matris **kelime sayimiyla URETILMEZ**; ajanlarin KENDI kapsam
tablolarindan derlenir.
**Her ISTEMCI YUZEYI icin DORTLU:** **SINK** (nereye yaziliyor) · **KACIS** (nasil
temizleniyor) · **CSP** (tarayici ne kadar kisitliyor) · **JETON** (o yuzeyden ne calinabilir).

### 1.12.4 IKI KANAL KURALI (yetki icin ZORUNLU)
**Statik oznitelik taramasi TEK BASINA YETMEZ.** Bir ucun korunup korunmadigi iki ayri
katmanda kararlasir ve **ikisi de okunmadan karar verilemez**:
- **KANAL-1** — controller/route oznitelikleri: kim cagirabilir.
- **KANAL-2** — manager/DAL katmanindaki **sahiplik yuklemi**: cagiran, kaynagin sahibi mi.

Yetki matrisinde bu ikisi **AYRI SUTUNDUR**; KANAL-2 sutunu "YOK" olan her kaynak-sahipli
uc **IDOR ADAYIDIR**.
**Gerekce (yapisal):** `[Authorize]` kimlik dogrular ama sahiplik dogrulamaz; iki soruyu tek
sutuna sikistiran her matris, **kimligi olan** bir saldirganin baskasinin kaydini okumasini
GORMEZ.
*(Bu turda olculdu: `[Authorize]` kullanan controller sayisi 0 — gercek mekanizma
`RequireUserType` + `SecureControllerBase`; yalniz KANAL-1 taransaydi matris BOS cikardi.)*

### 1.12.5 SOMURU KANITI DISIPLINI — "EN DAR REPRO"
Guvenlik turunun ciktisi **calisan bir exploit DEGILDIR**. Kanit birimi:
`EN DAR REPRO = bir istek + bir sonuc`
- **Zincirleme somuru YAZILMAZ.** Iki bulgunun birlikte daha agir sonuc verdigi NOT edilir
  (bkz. bolum 5), ama zinciri kosan betik URETILMEZ.
- **Yikici yuk YOK.** Klasik SQLi/komut yukleri gonderilmez; yerine **ayirt edici ama
  ZARARSIZ** girdi (LIKE kacisi icin tek `%`; tur hatasi icin gecersiz tur).
- **Yikici olani KAYNAKTAN okunur.** Canli olcum hasara yol acacaksa (siparis durumu
  degistirecekse, hesap kilitleyecekse) olcum **STATIK**e dusurulur ve boyle etiketlenir.
- **Kanit turu ETIKETLENIR:** `REPRO` · `STATIK` · `SUPHE`.
  **`SUPHE` bir IDDIA DEGILDIR** ve fix dayanagi OLAMAZ.

### 1.12.6 SIR HIJYENI — OLCUM ANINDA, RAPOR ANINDA DEGIL
Depo PUBLIC; olcum ciktisi da depoya girebilir. CLAUDE.md'nin *"maskeleme URETIM NOKTASINDA
yapilir"* kurali guvenlik turunda **daha da sikidir**, cunku turun kendisi sir uretir.
- **Sirrin DEGERI hicbir yere yazilmaz.** Yerine **DORTLU**:
  `alan adi | dosya:satir | UZUNLUK | desen adi`.
- Jeton/GUID **ilk 8 karaktere kirpilir**. JWT icin yalniz header/payload'in **alan adlari**
  ve kimlik-disi degerleri (`alg` `exp` `iss` `aud`); **imza parcasi ASLA**.
- **`user-secrets list` KOSULMAZ**; yerine `AddUserSecrets` cagrisinin VARLIGI okunur.
- Git gecmisi taramasi **YALNIZ SAYI** dondurur (`desen X: N commit`), eslesen satir DEGIL.
- **Sir taramasi UZANTI FARKETMEKSIZIN tum olcum agacini kapsar** — `.json` ve `.log` ham
  API dokumleri de sir tasir.
- **MERKEZ EKI (AV-1 karari):** *sir hijyeni **DISKE YAZMAYI da kapsar** — ham yanit dokumu
  **maskelenerek** yazilir.* Ajan ortak kurali **uc parcayi birden** icermelidir:
  **(a)** "basilmaz" · **(b)** "**diske yazilmaz**" · **(c)** **maske araci** (kirpma
  yardimcisi, ajanin elinde).
  *(Gerekce OLCULDU: AV-1'de kural yalniz (a)'yi tasiyordu; sonuc dokuz dosyada ciplak canli
  jeton — 6 access JWT + 3 refresh, biri ADMIN, oturumlar 7 gun gecerli ve besi `is_active=1`.
  Ajanin KENDI kapanis iddiasi "jetonlar ilk 8 karaktere kirpildi" diyordu ve **CURUK** cikti.)*

### 1.12.7 DUZENEK ARTIFAKTI <-> URUN KUSURU AYRIMI
**Guvenlik turunun EN BUYUK YANLIS-POZITIF KAYNAGI olcum duzenegidir.**
**KURAL — HER GUVENLIK RAPORU IKI SUTUN TASIR:**

| | ne olculdu | nerede karara baglandi |
|---|---|---|
| **CANLI** | duzenegin O ANKI davranisi | argumanlar + ortam ANILARAK |
| **URETIM DALI** | kodun uretimde ne yapacagi | KAYNAKTAN okunarak |

Bir bulgu ancak **URETIM DALI** sutunu da doldurulduktan sonra bulgudur.
Ornekler (olculmus): `Secure` cookie bayragi Development'ta kapali olabilir ·
`UseDeveloperExceptionPage` yalniz Development dalinda · rate limit komut satirindan
override edilmis · **Redis kapali oldugu icin dagitik limiter dali HIC KOSMUYOR**.
- **KOROLLER — IKI SURECLI OLCUM.** Bir bayrak olcumu maskeliyorsa, **AYNI ikiliden IKINCI
  bir surec** o override olmadan baslatilir; ikisi raporda AYRI SUTUNDUR.
- **KOROLLER — PAYLASILAN DURUM SERILESTIRILIR.** `Redis:Enabled=false` iken hiz limiti
  **surec-ici**dir; iki surec AYRI limiter tasir. Bu, patlama olcumunu tek surece hapsetmenin
  hem GEREKCESI hem IMKANIDIR. (MK-4b'nin guvenlik turundaki karsiligi.)

### 1.12.8 BILINEN/KABUL EDILMIS RISK LISTESI — ZORUNLU ON ADIM
1.11.4'un guvenlik karsiligi, **ama daha sert**: guvenlik turunda "bilinen risk" cogu zaman
**VERILMIS BIR KARARDIR**. Bunu bilmeyen bir ajan **KARARI BULGU SANIR**.
- Liste **numaralanir** (`B-01`...) ve **TUM ajanlara AYNEN** verilir (SDP 1.8).
- Listedeki sey **BULGU DEGILDIR**; bagimsiz yeniden kesfedilirse **"BILINEN — capraz
  dogrulama"** etiketiyle TEK SATIR yazilir.
- **SINIRINI genisleten kisim YENI BULGUDUR.**
- Her madde **kaynak atfi** tasir (muhur adi + baslik ya da `00a:satir`), boylece uydurma bir
  "B numarasi" denetlenebilir.
- **LISTE BAYAT OLABILIR ve bu ACIKCA YAZILIR.**
- **MERKEZ EKI (AV-1 karari):** liste **B8 fragmanlarindan KURULMAZ**; `00a`/`00b` **tam
  metni** okunarak kurulur. *(Gerekce OLCULDU: AV-1'de liste arsiv ozetinin ilk-cumle
  fragmanlarindan kuruldu; `00a:101` ve `00a:108` DISARIDA kaldi ve IKI bulgu (C-3, C-2)
  yanlis olarak "YENI" sayildi.)*

### 1.12.9 KAPSAM ELESTIRMENI — GUVENLIK TURUNDA ZORUNLU UYE
Gorevi ikiye cikar:
1. **OWASP TOP 10 x AJAN ESLEME MATRISI** — on kategorinin her biri hangi ajana dustu, ne
   kanit uretti, hangi alt-baslik BOS kaldi. **A04** ve **A09** yapisal olarak bos kalmaya
   EGILIMLIDIR; bu iki kategori icin "kapsandi" demek AYRICA gerekcelendirilir.
   *(AV-1'de olculdu: A04 BOS KALMADI (2 etiketli bulgu), **A09 FIILEN BOS KALDI**.)*
2. **HICBIR AJANA DUSMEYEN UC/DOSYA OLCUMU** — depodaki TUM uclarin listesi cikarilir, ajan
   defterlerinde ANILANLAR dusulur; **kalan liste KAPSAM BOSLUGUDUR ve adiyla raporlanir.**
   Suzgec POZ/NEG sinanir.
   **Elestirmen kendi olcutunun sinirini de yazar:** "anilma != olculme" — bu sayim **UST
   SINIRDIR**, gercek kapsam daha dusuk olabilir.
Ayrica **tarifin kendisinin kor noktasini** arar: is mantigi kotuye kullanimi · yarisma
kosullari · SSRF · sifreleme anahtari kullanimi · PII yasam dongusu — bunlar klasik
"OWASP x katman" bolumlemesinin **ARASINDAN duser**.

### 1.12.10 DENETIM KAPISI (uc denetci, hepsi ayri worktree — MK-4b)
| Denetci | Olctugu sey |
|---|---|
| **KAPSAM ELESTIRMENI** | 1.12.9 |
| **KURAL-UYUM** | kod diff 0 · yikici yuk 0 · sir degeri basilmadi **ve DISKE YAZILMADI** · MK-4a/MK-5 · BILINEN etiketleri kaynakli · kurgu deseni · **URETIM IMZASI** |
| **RAPOR DENETCISI** | (a)-(f) + **KANIT GUCU TABLOSU** + siddet/on-kosul tutarliligi + curuyen bulgu |

**1.12.10-a URETIM IMZASI (guvenlik surumu).** "Yalniz okuma yapildi" DOGRUDAN gozlenemez.
Olculen sey: turda olusan satirlarin **URETIM YOLUNDAN** geldigi. Guvenlik turunun kendine
ozgu imzasi **kimlik kaydidir**: uretim yolundan acilan hesap `password_hash` **ve**
`password_salt` tasir, e-posta deseni kurgudur, `created_at` turun saat araligindadir; ayrica
kayit ucunun urettigi **yan etki zinciri** (bu depoda: musteri basina TAM 3 `consent_records`)
bulunur. Elle bir `INSERT` bunlarin tumunu tutturamaz.
**Kesin kanit degildir, IKI KANALLI guclu kanittir.**
**SINIRI DA YAZILIR:** `created_at` tabanli supurge **var olan bir satirin GUNCELLENMESINI
GORMEZ**; bu bosluk yalniz **dokunulmaz kayitlarin ICERIK karsilastirmasiyla** kapatilir
(1.11.10-b).

**1.12.10-b KANIT GUCU TABLOSU (ZORUNLU).** Her YUKSEK/KRITIK bulgu icin **kac BAGIMSIZ
KANALDAN** dogrulandigi yazilir: `kaynak` · `canli-API` · `DB` · `tarayici` · `arac`.
**Tek kanalli bir bulgu, cok kanalliyla AYNI siddet sirasina KONMAZ.**
Gerekce: guvenlik bulgulari "makul gorunmeye" en yatkin bulgu sinifidir — bir kod satiri tek
basina okundugunda korkutucu, canli olculdugunde etkisiz cikabilir (ve tersi).
**BIR AJANIN BASKA BIR AJANIN SONUCUNU GORDUGU olcum BAGIMSIZ KANAL SAYILMAZ; TEYITTIR.**
*(AV-1'de olculdu: SEC-D, SEC-C'nin 15 dk olcumunu gormustu — itiraz #21.)*

**1.12.10-c SIDDET DENETIMI.** Rapor denetcisi her bulgunun **siddet <-> on kosul**
tutarliligini olcer ve **AKTIF/LATENT ayriminin OLCULMUS mu VARSAYILMIS mi** oldugunu belirler.

### 1.12.11 FIX DALGASI ESLEMESI
Guvenlik turu **SALT OLCUMDUR**; fix baslatmaz.

| Bulgu sinifi | Hedef dalga |
|---|---|
| KRITIK · **KIMLIKSIZ-UZAK** · AKTIF | **ACIL** kendi dalgasi, digerlerinden ONCE |
| YUKSEK · **KIMLIKLI** · AKTIF (IDOR · oturum · para) | ilk fix dalgasi |
| **LATENT** (kod var, yol bugun ulasilmaz) | **kok basina** tek dalga |
| on kosul **ADMIN** ya da DUSUK | biriktirilip tek pakette |
| Bagimlilik (A06) | **ayri dalga** — sema/API kirilmasi riski farkli bir sinif |
| SORU listesi | merkeze; karar sonrasi dalgaya |

**LATENT KALEMLER KOK BASINA GRUPLANIR** (1.11.7'nin guvenlik karsiligi).
**ZINCIR KURALI:** bolum 5'teki gibi birbirini besleyen bulgular **AYNI dalgada ya da
BILINCLI SIRAYLA** yapilir; zincirin yalniz bir halkasini onarmak zinciri KIRMAZ.
Dalga bolumlemesi **MERKEZDEN**; guvenlik turu yalniz siniflandirir ve onceliklendirir.
**SIRALAMA KENDI OLCUTUNE UYMAK ZORUNDADIR.**

### 1.12.12 GUVENLIK TURUNUN KENDI KOR NOKTALARI (durust kayit zorunlulugu)
Rapor su **DORDUNU** ACIKCA yazar:
1. **Duzenek sinirlari** — hangi kod dali bugunku bayraklarla HIC KOSMADI.
2. **Kosulmayan yollar** — yikici oldugu icin STATIK'e dusurulen olcumler.
3. **Onlenen yanlis bulgular** — "bulgu sandim, olcunce degilmis". **RAPORDAN SILINMEZ**;
   turun kalibrasyonu bu kayitlarla yapilir.
4. **Kanit kanali kapalilari** — SARIF/CodeQL/artefakt gibi imza isteyen kanallar (anonim 401)
   ve bunlarin nasil telafi edildigi.

### 1.12.13 OLCULEN MALIYET (kalibrasyon icin — her turda raporlanir)
Ajan sayisi · faz sayisi · tur suresi · plan sapmasi · ara kapi bulgusu · denetci itirazi ·
**curuyen bulgu orani**. Sonuncusu guvenlik turunun ozgun olcutudur: curuyen bulgu orani
yuksekse tur **fazla agresif**, sifirsa **fazla temkinli**.
*(AV-1: 35 bulgu, curuyen bulgu **0**; curuyen tek sey bir KAPANIS IDDIASIYDI. Oran 0/35 ->
tur **temkinli** tarafta; siddet dagiliminda 0 KRITIK bunu destekliyor.)*

---

## 7. M0 — MASKELEME KAYDI (merkez karari K6)

**Yapilan:** `scratchpad/secb/` altindaki **dokuz** dosyadaki **dokuz** jeton ilk 8 karaktere
kirpildi (`XXXXXXXX…[maskelendi]`); her dosyanin sonuna
`MASKELEME KAYDI: 2026-09-01 · 1 jeton · AV-1 merkez kararı K6` satiri eklendi.
**Denetci defterlerine (`20-*.md`) DOKUNULMADI** (mtime 21:06-21:21, maskeleme sonrasinda
degismedi).

**"DOKUZ JETON" SAYISI DOGRULANDI (varsayilmadi):** `login1/login2/adminlogin.json`
dosyalarinin `refresh_token` **alani BOS** (uzunluk 0, md5 = bos dize) — refresh jetonu
**httpOnly cookie**'den geliyor (B-01 hibrit modeliyle tutarli) ve `.raw` dosyalari
Set-Cookie'den cikarilmis. JSON'daki access JWT ile `.txt` dosyasindaki **AYNI** (md5
esitligi). Yani **9 dosya = 9 gecis = 6 farkli deger** (3 access + 3 refresh).

**SUZGEC SINAMASI (SDP 1.7/1) — maskeleyici KARAR ICIN KULLANILMADAN ONCE sinandi:**
```
POZ girdi (jwt.io ornek jetonu + 43 karakterlik b64url kosusu) -> JETON=2
NEG girdi (duz Turkce cumle, uzun kosu yok)                    -> JETON=0
```

**SONRAKI OLCUM:**
```
CIPLAK JETON (sinama POZ girdileri HARIC)  : 0    <- hedef 0      TUTTU
MASKELEME KAYDI tasiyan secb/ dosyasi      : 9    <- hedef 9      TUTTU
```
**43-KARAKTERLIK EslesMELERIN SINIFLANDIRILMASI (yalanci pozitif elemesi):** tum scratchpad
agacinda 250 eslesme; **249'u** `C--Users-pc-Desktop-smart-Divisima-Solution` — yani harness
**dizin slug'i**, ki uzunlugu **TAM 43 karakter** (olculdu, tahmin edilmedi). Kalan 1 kendi
POZ kontrolum. **Bu ailede gercek jeton 0.**
Ciplak JWT tasiyan iki dosya kaldi: `gav1/sinama/poz.txt` ve `kural-sinama/poz.txt` — ikisi
de **bilerek yerlestirilmis SAHTE POZ girdileridir** ve dedektorun calistigini kanitlar
(SDP 1.7/1 geregi SILINMEZ).

**Dosya kendi iceriginden turetilerek ustune YAZILMADI** (CLAUDE.md kalici kurali): cikti
`<dosya>.yeni`ye yazildi, dokuz dosyanin her biri icin `bayt>0` + `JWT=0` + `b64_43=0` +
`maske>=1` + `kayit=1` **DOGRULANDI**, ancak ondan sonra `mv` yapildi. Kalan `.yeni` = 0.

**`user_sessions` 331-339 IPTAL EDILMEDI** (merkez karari): yerel DB, kurgu hesaplar,
`expires_at = 2026-09-08`. **D-YAN'a devredildi.**

---

## 8. KURGU ENVANTERI ve MK-3

**MK-3 UCLUSU — ZEMIN ve KAPANIS BIREBIR AYNI** (ureten ifadeleriyle):
```
SELECT COUNT(*), MAX(id) FROM orders WHERE customer_id = 10;                  -> 38 / 211
SELECT COUNT(*), MIN(id), MAX(id), SUM(CAST(id AS bigint))
  FROM orders WHERE status = 0 AND id <= 210;                                 -> 35 / 9 / 210 / 3837
SELECT COUNT(*), SUM(total_price), STRING_AGG(CAST(status AS varchar),',')
  FROM orders WHERE customer_id = 74 AND id BETWEEN 234 AND 237;              -> 4 / 4698,60 / 0,0,1,1
```
Kural-uyum denetcisi ayni uclyu **BAGIMSIZ** olctu; **tek hane sapma yok**.

**MAX'lar:** musteri **158 -> 168** (+10) · siparis **286 -> 286** (0) ·
adres **118 -> 119** (+1) · fatura **119 -> 119** (0) ·
Pending (`status=0 AND id>210`) **10 -> 10** (**yeni Pending URETILMEDI**).

**YENI MUSTERILER (10/10 `gav1.` onekli · URETIM IMZALI: `password_hash` 64 B +
`password_salt` 128 B dolu · `created_at` 20:23-20:27 · musteri basina TAM 3
`consent_records`):**
```
159 gav1.secc.1   160 gav1.secb.1   161 gav1.secb.2   162 gav1.secc.2   163 gav1.secc.3
164 gav1.secc.n1  165 gav1.secc.n2  166 gav1.secc.n3  167 gav1.secc.n4  168 gav1.secc.n5
(hepsi @example.com)     `id > 158 AND email NOT LIKE 'gav1.%'` -> 0
```

**YAN ETKI ZINCIRI (43 tablo supuruldu, 28'i SIFIR):**
```
consent_records 30 · user_sessions 9 (id 331-339) · audit_logs 85 · security_events 11 ·
outbox_messages 21 · addresses 1 (id 119) · wishlist_items 2 · carts 1 · cart_items 1 ·
product_reviews 1 · review_helpful_votes 1 · price_drop_subscriptions 1 ·
stock_notification_requests 1 · gift_cards 1
```
**SIFIR KALAN KRITIK YUZEY:** `orders` · `order_items` · `payments` · `invoices` ·
`invoice_items` · `products` · `product_stocks` · `stock_movements` · `stock_reservations` ·
`coupons` · `coupon_usages` · `sellers` · `shipments` · `return_requests` ·
`loyalty_transactions` · `store_credit_transactions`.

**MEVCUT KAYITTA DEGISIKLIK:** kurgu admin **118** (`mf2.k1admin@example.com`) sifresi
**URETIM YOLUNDAN** sifirlandi (`forgot-password` -> jeton `customers.password_reset_token`'dan
SELECT -> `reset-password` -> `login`); elle SQL UPDATE YOK. AdminSeed **ACILMADI**.

**DOKUNULMAZ — musteri 10 (Omer):** `updated_at` **NULL** · `password_reset_token` **NULL** ·
hash 64 / salt 128 · siparisler 38 satir, id 14..211, `SUM(total_price)=52789,20` · adres 1 ·
bu turda uretilen oturum/audit/security/outbox satiri **0**.
`outbox_messages` payload'inda `%e2b.sandbox%` -> **0** (gercek kullaniciya tek e-posta
kaydi bile uretilmedi). **SAYI YETMEZ kuralina uyularak ICERIK de olculdu (1.11.10-b).**

**ENVANTER BOSLUGU (itiraz #20):** `user_sessions` (9 satir, id 331-339) ve
`review_helpful_votes` (id 1) **hicbir ajan envanterinde beyan edilmemisti**. Hepsi kurgu
hesaplara ait, zarar yok — **yazilmayan bir sey vardi**. Bu muhurde beyan edildi.

**D-YAN'a devredilenler:** musteri **159-168** · adres **119** · `user_sessions` **331-339**
(iptal edilmedi) · `review_helpful_votes` **1** · yukaridaki 14 satirlik yan etki tablosu.

---

## 9. PILOT OLCUMU (ultracode ilk ve — K7 geregi — TEK pilotu)

```
Ajan sayisi        : 10 (7 kesif SEC-A..G + 3 denetci) — PLAN DISI AJAN ACILMADI
Faz                : 2 (KESIF paralel -> BARIYER -> DENETIM paralel)
Sure               : 4.253.021 ms = 70 dk 53 sn (tek Workflow cagrisi)
Alt-ajan jetonu    : 2.472.858
Arac cagrisi       : 715
Hata / bos sonuc   : 0 / 0
Bulgu              : 35 (0 KRITIK · 2 YUKSEK · 19 ORTA · 14 DUSUK)
Kapsam             : <= 27/40 controller (UST SINIR — "anilma != olculme")
Curuyen bulgu      : 0/35
Teknik izolasyon   : DENETIM fazi 3/3 ayri worktree (hepsi c6721b7, locked) — MK-4b
                     KESIF fazi UYGULANMADI (ana agac); ampirik olculdu
Izolasyon (ampirik): kosegen POZ kontrol 8/8 TUTTU · NEG `sec-z` 0 · kosegen disi 27 atif:
                     25'i kapsam siniri referansi, 2'si BASKA AJANIN SONUCUNA (itiraz #21)
Defter             : 8 kesif HAM + 5 denetci HAM = 13 dosya, 0 BAYT YOK (MK-5 TUTTU)
ON-KAYIT           : 7/7 defterde PLAN OLCUMDEN ONCE · PLANSIZ OLCUM KALEMI 0
BILINEN etiketi    : defterlerde 16 farkli B-xx; kaynakta OLMAYAN numara 0 (uydurma YOK)
/context           : slash komutu, bu oturumda calistirilamadi (son bilinen 90,7k)
/cost              : GORUNMUYOR
```
**KIYAS ve KARAR (K7):** MANTIK-AV-1 fan-out'u ~520-540k jeton kullanmisti; bu tur
**~4,5 kati**. **Ikinci ultracode kullanimi karari YOK**; **GUVENLIK-AV-2 ultracode'suz ve
DAR** kosulacak.

---

## 10. CC HATALARI (ana akis — durust kayit, 6 kalem)

1. **`args.scratch` KOKU YANLIS VERILDI.** Ortak kural ve kapi defteri `<S>/gav1/` altinda,
   ajanlara `<S>/` kokü soylendi. **Uc denetci de bagimsiz** olarak PLAN-SAPMA / TARIF KUSURU
   diye yazdi (itiraz #11). Ajanlar dosyalari BULDU (B-xx atiflari calisiyor), kayip yok; ama
   kesif defterleri `gav1/` yerine kokte olustu ve **sekizinci defter** (`10-secd-p2.md`)
   denetci prompt'unda hic anilmadi.
2. **B-01..B-18 LISTESI EKSIKTI — IKI BULGU YANLIS SINIFLANDI.** `00a:101` ve `00a:108`
   listede YOKTU -> **C-3 ve C-2 "YENI" sayildi**, oysa depoda KAYITLI (itiraz #1, #2).
   **KOK SEBEP:** B listesini CLAUDE.md'nin **ARSIV OZETINDEKI fragman ilk cumlelerinden**
   kurdum; `docs/muhur/00a-sira-kararlar.md` **TAM METNINI okumadim**.
   -> **SDP 1.12.8 MERKEZ EKI** bu hatadan dogdu.
3. **ORTAK KURAL "DISKE YAZMA"YI KAPSAMIYORDU.** Bolum 1 "secret degeri BASILMAZ (rapora,
   deftere, konsola)" diyordu; **ham yanit dokumlerinin DISKE yazilmasi kapsanmadi** ->
   dokuz dosyada ciplak canli jeton (itiraz #18, #19). CLAUDE.md'nin "maskeleme URETIM
   NOKTASINDA" ilkesi ajan promptuna **ARAC SEVIYESINDE inmemisti**.
   -> **SDP 1.12.6 MERKEZ EKI** bu hatadan dogdu; **M0** ile kapandi.
4. **Heredoc dususu.** `cat > ... <<'EOF'` ile ortak kural metni yazilamadi
   ("unexpected EOF"); **MK-8** geregi dosya aracina gecildi. Kayip yok, bir tur harcandi.
5. **Cok alanli `grep -oE` deseni 2 dk zaman asimina dustu** (journal'da non-greedy 20k);
   alan-alan cikarima gecildi. **Ilk sayim 34 dedi, DOGRUSU 35** — bir bulgu birlesik desende
   kaciyordu. **Suzgec sinamasi olmasa rapor eksik cikacakti** (SDP 1.7/1'in bu turdaki
   ikinci kazanci; birincisi kapsam elestirmeninin `MapHub` kacirmasiydi).
6. **Kapi tarifi sapmasi:** merkez "dort run" dedi, olculen **iki run / alti job** (K8).
7. **MUHUR TURU — `cd docs/muhur` KABUKTA KALICI OLDU.** Arsiv bayt sayimi icin girilen dizin
   sonraki cagrida da gecerliydi ve butce olcumu `CLAUDE.md: No such file or directory` ile
   **tumden dustu** (yedi alt-olcumun HEPSI bos dondu). Mutlak yola gecilerek yeniden
   olculdu. **MK-2 ailesi** ("git komutu calistiran her cagri CWD'yi ONCE dogrular") —
   bu vaka kuralin git DISINA, **her dosya-yolu bagimli olcume** genisledigini gosteriyor.
   Kayip yok (olcum tekrarlandi), bir tur harcandi.
   **AYRICA — BU DUSUS BIR SEYI ISPATLADI:** hata `2>/dev/null` ile YUTULMADIGI icin
   goruldu; yutulsaydi butce olcumu "0 B" doner ve **"BUTCE GECTI" YALANCI SONUCU**
   uretilebilirdi. SDP 1.7/1'in "hata yutan yedek KARAR BESLEYEMEZ" maddesinin bu turdaki
   ucuncu kazanci.

**Ilk raporda bir atif hatasi yapildi ve burada duzeltildi:** "curuyen tek sey C-1'in
mekanizma tarifi" yazilmisti; dogrusu **C-1 mekanizma tarifi ZAYIF (#3)**, **CURUK olan
SEC-B'nin kapanis iddiasidir (#19)**.

---

## 11. KAPSAM BOSLUKLARI (AV-2 girdisi)

**A09 (Logging/Monitoring) FIILEN OLCULMEDI:** 3 anilma, tek defter (SEC-E), 0 etiketli bulgu.
Denetim kaydinin ASIL yuzeyleri de **sifir kapsamli**: `AuditLogController` ·
`DenetimGizlilik` · `DenetimRedaksiyonu`.

**SIFIR KAPSAMLI ve AKTIF yuzeyler:**
- **Sifreleme at-rest** — `AesEncryptionProvider` / `EncryptedConverter` / `IEncryptionProvider`
  **0 defter**. `Program.cs:230` DI kayitli; `DivisimaDbContext.cs:306` `tfSecret`
  **SIFRELI saklaniyor**. **SUPHE (K10):** `AesEncryptionProvider.cs:47-52` `Decrypt` **TUM
  istisnalari yutuyor** (`catch { return cipherText; }`, gerekce "kademeli gecis") ->
  AES-GCM'in **kurcalama tespiti (auth tag) bu katmanda SESSIZLESIYOR**.
  *(Ayni dosyanin `:19` satirindaki "anahtar bos ise SHA256 tureme" dali BULGU DEGILDIR —
  `Program.cs:88-96` prod'da `throw` ile engelliyor ve gerekcesi yorumda YAZILI.)*
- **2FA / TOTP** — `TotpService` / `ITwoFactorService` **0 defter**; `Program.cs:229` DI
  kayitli. SEC-C'nin kapsam cumlesinde **"2FA" kelimesi YOK** — yapisal bosluk.
- **Yarisma kosulu / TOCTOU** — 1/8 defter; **`ExecuteUpdateAsync` (atomik CAS) 0 defter**,
  CLAUDE.md'nin KAYITLI tuzagi olmasina ragmen.
- **Olay isleyicileri** — dort `OrderPlaced*Handler` **0 defter** (PII tasiyan yollar).

**40 CONTROLLER'IN 13'U (%32,5) HICBIR DEFTERDE ANILMIYOR:**
`AuditLog` · `Category` · `Collection` · `Comparison` · `Content` · `Merchandising` ·
`ProductAttribute` · `RecentlyViewed` · `Recommendation` · `Referral` · `Seo` · `SizeGuide` ·
`Stock`. Bunlarin **dordu TAMAMEN ANONIM** (`Comparison`, `Merchandising`, `Recommendation`,
`Seo`); yazma fiili olan tek kalem `Comparison` (1 POST, `ProductComparisonManager.cs:27-29`
ile 2..4 arasi **SINIRLI** — onlenen yanlis bulgu).
**SUPHE (K10):** `ComparisonResultDto.products` tipi `List<Entity.Product>` — anonim cagirana
**HAM ENTITY** donuyor (`seller_id`, `vat_rate`, `sale_start`/`sale_end`, `is_active`);
ayni desen `CollectionManager.cs:160`. `AuditLogController.cs:12-17` kendi yorumunda ayni
kusur sinifini FAZ-0/K6'da duzelttigini soyluyor — Comparison/Collection o duzeltmenin
**DISINDA kalmis olabilir**.
**Stok yuzeyi** (SDP'ye gore L3 sinifi) HIC DENETLENMEDI; `StockController` sinif duzeyinde
`[RequireUserType(Admin)]` tasiyor (kaynak okundu, satir 13) — **bugun saglam gorunuyor, ama
bunu OLCEN bir ajan YOKTU**.

**SSRF ekseni SAHIPSIZDI**, kapsam elestirmeni **KENDISI kapatti**: `HttpClient` kullanan 7
dosyanin hepsinde hedef URL **sabit ya da config** (`GibEInvoiceProvider` ·
`FcmPushNotificationService` · `NetgsmSmsService` · `DefaultCarrierProvider` ·
`TurnstileCaptchaValidator` · `PaymentController` · `Program.cs`) -> **kullanici-kontrollu
hedef URL YOK, TEMIZ** (`[YOKLUK]`, NEG kontrollu).

**ONLENEN YANLIS BULGULAR (1.12.12/3 — SILINMEZ):** Comparison DoS (2..4 siniri var) ·
SMS parametre enjeksiyonu (`NetgsmSmsService.cs:37-39` hepsi `Uri.EscapeDataString`) ·
AES bos-anahtar turemesi (prod'da `throw`) · `IdempotencyFilter` "olu kod" sanildi, gercekte
tek atif bir YORUM icinde -> kapsam boslugu SAYILMAZ.

**TURUN KENDI KOR NOKTALARI (1.12.12):** `Redis:Enabled=false` -> **dagitik limiter dali HIC
KOSMADI** · uretim ters-proxy'si yok -> XFF guveni yalniz kaynaktan · tarayici panelinde
ekran goruntusu alinamiyor (DOM SAYISAL olculdu) · CodeQL/Gitleaks SARIF ve artefaktlari
anonim erisime KAPALI (401/403) -> depo taramasiyla telafi edildi · BILINEN listesi tur
basinda YENIDEN OLCULMEDI (ve **eksik cikti**, CC hatasi 2).

---

## 12. GUVENLIK-FIX BOLUMLEME ONERISI (K9 — YALNIZ ONERI, KARAR MERKEZDEN)

```
GF-1  OTURUM          : C-1 + C-2
      Kok: iptal YAZMA tarafi hic cagrilmiyor (jti kolonu yok) + auth_time her uretimde
      tazeleniyor. B-02 bu dalgada kapanir. NOT: GF-2 ile ZINCIRLI (bolum 5).
GF-2  ISTEMCI XSS     : D-7 · D-8 · D-5 · D-9 · D-1 · D-2 · D-3 · D-4 · D-11 · D-10
      Kok: CSP unsafe-inline (CARPAN) + esc() tutarsizligi. KOK BASINA TEK DALGA.
      Ic sira K5 geregi ON KOSULA gore.
GF-3  SIZINTI / LOG   : E-2 + E-3 (+ B-09 failed-jobs)
      Kok: maskeleme CAGRI NOKTASI eksik (KanitMaskesi var, cagrilmiyor).
GF-4  LIMIT           : F-1 + F-2 + A-2
GF-5  TEDARIK ZINCIRI : G-1 · G-2 · G-3 · G-4 · G-5 · G-6   (D-8'in SRI yarisi GF-2'de)
      AYRI DALGA - sema/API kirilma riski farkli bir sinif.
GF-6  YAPILANDIRMA    : E-1a · E-4 · E-5 · E-6 · B-2         (E-1b KABUL, bulgu degil)
BIRIKTIR (tek paket)  : A-3 (SUPHE) · C-3 (BILINEN 00a:101) · C-4 · D-6 · F-3
VITRIN-KALAN 7. kalem : A-1 (K4 - [MANTIK], LOWER() 6c ihlali dahil)
GUVENLIK-AV-2 (dar olcum, ultracode YOK):
      at-rest sifreleme (AES Decrypt istisna yutma - K10) · 2FA/TOTP ·
      TOCTOU + ExecuteUpdateAsync · A09 denetim kaydi yasam dongusu · olay isleyicileri ·
      13 anilmayan controller (Comparison/Collection HAM ENTITY suphesi - K10) · Stock yuzeyi
```

---

## 13. DEFTER (HAM — depo DISI, oturum scratchpad'i)

```
gav1/00-ORTAK-KURAL.md          ajanlara verilen TEK ortak kural metni (SDP 1.8)
gav1/00-PLAN-VE-KAPI.md         kapi olcumleri + dagitim plani (SDP 1.4)
gav1/30-sdp-guvenlik-modulu.md  SDP 1.12 taslagi (tam metni bu muhurde, bolum 6)
gav1/40-KAPANIS.md              kapi kapanisi + pilot + CC hatalari
gav1/maskele.pl                 M0 maskeleyici (POZ/NEG sinanmis)
gav1/sinama/{poz,neg}.txt       M0 suzgec sinama girdileri (SILINMEZ)
10-{seca,secb,secc,secd,secd-p2,sece,secf,secg}.md    kesif HAM (8 dosya)
20-{kapsam,kapsam-olcumler,kural,kural-ek,rapor}.md   denetci HAM (5 dosya)
```

**FIX BASLATILMADI. URETIM KODU DEGISMEDI.**
