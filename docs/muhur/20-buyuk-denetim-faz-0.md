# BUYUK DENETIM - 8 FAZLI PLAN ve FAZ 0 KAPANISI (25 Agustos 2026)

Kalite supurmesi ve LAUNCH-FIX dalgalari kapandiktan sonra acilan **butunsel denetim**.
Envanter (salt olcum) once cikarildi; FAZ 0 o envanterin isaretledigi temizlik kalemlerini
KARAR TABLOSUNA cevirip uyguladi.

## FAZ PLANI (8 faz)

| Faz | Konu |
|---|---|
| **Faz 0** | **Envanter temizligi** (bu commit) - K1..K7 |
| Faz 1 | Kimlik & hesap yasami |
| Faz 2 | Vitrin & alisveris (para-oncesi) + **SIFIR-TESTLI 7 ALAN** + anonim olu uclar tam denetimi |
| Faz 3 | Para cekirdegi |
| Faz 4 | Admin & operasyon |
| Faz 5 | Kesisen altyapi |
| Faz 6 | Veri katmani |
| Faz 7 | Kapanis |

Envanterin cikardigi sayilar (zemin `3870d6d`): 40 controller / **151 uc** (17'si anonim POST,
8'i gercek anonim YAZMA) · 47 manager + 47 arayuz (**olu arayuz YOK**) · 45 entity / 45 tablo /
**56 FK** (tamami RESTRICT) / 29 UNIQUE (8'i filtreli) / 12 migration · 76 test sinifi / ~540 test ·
49 yapilandirma anahtari · **61 olu uc** (frontend hicbirini cagirmiyor) · frontend->API yon
farki **0** (cagrilan ama olmayan uc YOK).

## FAZ 0 KAPANISI - K1..K7 KARARLARI

| # | Karar | Ne yapildi |
|---|---|---|
| **K1** | (b) **KALDIR** | `ETagMiddleware`'in onek listesinden `/api/sizeguide` silindi. ILK COMMIT'ten (`df91863`) beri HIC eslesmiyordu: gercek rota `api/size-guide`, eslesme `StartsWithSegments` ile SEGMENT SINIRLI. Canli olculdu (size-guide ETag YOK / product-category-collection ETag VAR + 304). Duzeltmek yerine kaldirildi cunku SizeGuide uclari OLU YUZEY (ETag kazanci 0) ve duzeltmek `no-store`'u `private, max-age=60`'a GEVSETIRDI. |
| **K2** | (a) **SINIF DUZEYINE TASI** | `[EnableRateLimiting("payment")]` uc action'dan `PaymentController` sinif duzeyine tasindi. Davranis degismezligi uc ayakla olculdu (3/3 action zaten isaretliydi · `[DisableRateLimiting]` depoda 0 · iki mevcut 429 pini). Eksik olan **initialize 429 pini** eklendi. |
| **K3** | (b) **KOD DEGISMEDI, PIN** | Filtre ifadeleri siniflandirildi: 8 filtreli indeksin **yalniz** `UX_store_credit_referee_reward` METIN LITERALINE bagli; `UX_loyalty_transactions_order_earn` ise SAYISAL ENUM sabitine (`[type] = 0`). Literalin sabit/DbContext/migration/`01_schema.sql` dortlusunde BAYT-BIREBIR esit oldugu olculdu ve pinlendi. |
| **K4** | (a) **KOD DEGISMEDI, BELGE** | `DivisimaDbContext`'teki dislama yorumu **4 -> 6 entity**'ye tamamlandi (Seller ve ProductQuestion eksikti), her biri TEK satir gerekceyle. Olculen guvenlik/veri boslugu **SIFIR**. |
| **K5** | (a) **SIL** | 4 olu DTO + `frontend/pwa/`'nin 4 olu dosyasi silindi (**48 satir C# + 4 dosya**). `pwa/README.md` KALDI (arsiv notu eklendi) - `GuvenlikFix3SozlesmeTests` deny-kurali kapsam pini o yolu ariyor. |
| **K6** | (a) **IS KATMANINA TASI** | `IAuditLogService` + `AuditLogManager` + `AuditLogListItemDto` + `AuditLogPagingListResponseDto`. Controller artik DAL degil SERVIS enjekte ediyor. |
| **K7** | (a) **OZNITELIK TEK KAYNAK** | `RedisRateLimitMiddleware` kovayi ONCE endpoint metadata'sindaki `EnableRateLimitingAttribute.PolicyName`'den alir; metadata yoksa `KapsamSec` YEDEK. Cozumleme TEK SAF fonksiyonda (`RateLimitPolitikasi.KovaSec`). |

### K4 - ALTI ENTITY'NIN GEREKCESI (ozet; tamami DbContext'te)

`GiftCard` (is_active = tuketildi + soft-delete; filtre denetimden gizlerdi) ·
`ProductStock` (dokuz okumanin dokuzu da filtreli) ·
**`UserSession` (filtre eklemek IKI seyi bozar: G1 dondurulmus-jeton tespiti + DataRetentionJob)** ·
**`CustomerDevice` (filtre eklemek `device_token` UNIQUE IHLALI uretir - reaktivasyon yolu filtresiz okuyor)** ·
`Seller` (her okuma 403 korumali) · `ProductQuestion` (bayrak YAZ-BIR-KEZ).

### K3 - KUPLAJ BILINCLI KABUL EDILDI (defter)

Metin literali kuplaji KODDA duruyor; pin yalnizca SESSIZ kalmasini engeller.
**9. bir `reason` turu eklendiginde** K3-(a) gundeme gelir: `reason` yerine `reason_code`
byte kolonu (migration + geri doldurma + 8 yazma sitesinin dokunulmasi).

### K6 - ILGINC AYRINTILAR

- Uc bugun **HIC CAGRILMIYOR** (`api-client.js:570` `auditLogs()` tanimli, cagiran yok;
  `admin.html`'de denetim ekrani yok) -> hizalamanin kirilma riski SIFIR, en ucuz an buydu.
- Sizinti **DALGA B / B2 defekt sinifinin IKINCI ORNEGIYDI** (repository tipi `PagedResult<T>`
  HTTP'ye cikiyordu -> camelCase `{items,totalCount,...}` vs deponun snake_case konvansiyonu).
- **Autofac KONVANSIYONEL DEGIL** (olculdu: `RegisterAssemblyTypes` yok, her servis tek tek
  `RegisterType<X>().As<IY>()`), bu yuzden `AuditLogManager` icin ACIK kayit satiri zorunluydu.
- **HARNESS BULGUSU (durust kayit):** p-k6b'nin kurgusu `AuditInterceptor`'a DAYANAMAZ -
  `DalgaBFactory` `DbContextOptions` kaydini kaldirip `AddDbContext(...)` ile YENIDEN kuruyor
  ve o kayit `.AddInterceptors(...)` TASIMIYOR (uretim kaydi tasiyor). Yani `audit_logs` bu
  suitte BOS kalir. Pin denetim satirlarini DOGRUDAN kuruyor; olctugu sey UCUN SOZLESMESI.

### K7 - BILINCLI DAVRANIS DEGISIKLIGI (rapora ve deftere)

`guest-checkout/place`, `price-drop/subscribe|unsubscribe`,
`stocknotification/subscribe|unsubscribe`, `seller/auth/login|register`,
`auth/reset-password|resend-verification|verify-2fa|logout|refresh` uclari **dagitik tarafta
artik `global` degil `auth` kovasini PAYLASIR**. Etkin limit ZATEN 10 idi (iki yolun minimumu);
degisen sey paylasimin SIKILASMASI - ve bu, oznitelik tarafinin ZATEN yaptigi sey.
**[NOT]#9 bu degisiklikle YAPISAL OLARAK KAPANDI:** `reset-password` / `resend-verification` /
`verify-2fa` / `logout` / `refresh` artik dagitik tarafta da auth kovasinda (oznitelik
`AuthController` sinif duzeyinde).

### ADIM 0 - K7'NIN IKI PARCALI ON DOGRULAMASI (gecici tani, commit'e GIRMEDI)

```
(i)  EnableRateLimitingAttribute.PolicyName public okunabilir  -> DERLEYICI KANITI: 0 error CS
(ii) Gercek boru hattinda, RedisRateLimit middleware KONUMUNDA:
       /api/auth/login            endpointNull=False  policy=auth
       /api/guest-checkout/place  endpointNull=False  policy=auth
       /api/payment/webhook       endpointNull=False  policy=payment
       /api/product/get/1         endpointNull=False  policy=-
       /api/olmayan-yol           endpointNull=TRUE   policy=-    <- YEDEK YOL SART
       /health                    endpointNull=False  policy=-
```
Sebep olculdu: uygulama `app.UseRouting()`i ACIKCA cagirmiyor, yonlendirme boru hattinin
BASINA ekleniyor (Sprint 8 madde 9 bulgusunun ta kendisi). Ayni desen `IdempotencyMiddleware`de
ZATEN kullaniliyor.

## PINLER (10 yeni)

`Faz0SozlesmeTests` (6, **VERITABANI ACMAZ** - 10d794d dersi): p-k1a olu onek yapisal yasak ·
p-k3 dort artefaktta bayt-birebir literal + enum kuplaji · p-k7a metadata oncelikli ·
p-k7b metadata yoksa yedek · p-k7c eslesmeyen yol -> global · p-k7-EK middleware gercekten
metadata okuyup saf fonksiyona veriyor (ve `KapsamSec`i DOGRUDAN cagirmiyor).

Davranis pinleri MEVCUT host'lara eklendi (yeni SQL sinifi ACILMADI):
`StorefrontCatalogContractTests` +1 (**p-k1b - ETag'in ILK davranis pini**: product ETag VAR,
If-None-Match -> 304 + 0 bayt, size-guide ETag YOK) · `PaymentCallbackRedirectTests` +1
(p-k2 initialize 11. istek 429) · `DalgaBOperasyonTests` +2 (p-k6a 401/403/200 yetki kapisi,
p-k6b snake_case zarf + `tableName` filtresi + `created_at DESC` + DTO alanlari).

**KIRILAN PIN YOK.**

## DIS KONTROLU (TAM KAPSAMA) + 5. KONTROL

**DIS - ORNEKLEM YOK, 10 PININ HER BIRI TEK TEK:** 6 (Faz0) + 4 (davranis) ->
**10/10 AYRI ISIMLI KIRMIZI**. Geri alindi, 32/32 yesil.

**5. KONTROL - URETIM MUTASYONLARI:**

| Mutasyon | Sonuc | Uretilen once-durum |
|---|---|---|
| **M1** olu onek `/api/sizeguide` GERI KONDU | p-k1a KIRMIZI (1 pin) | K1 oncesi kaynak. **p-k1b YESIL KALDI - bu, onegin gercekten OLU oldugunun IKINCI, bagimsiz kanitidir** |
| **M2** middleware `KapsamSec`e donduruldu | p-k7-EK KIRMIZI (1 pin) | K7 oncesi: iki ayri el yazmasi |
| **M3** manager `PagedResult<AuditLog>` dondurdu | p-k6b KIRMIZI (1 pin), mesaj: `{"items","totalCount","page","size","totalPages"}` | **B2'de olculen camelCase sizintisinin BIREBIR aynisi** |
| **M4** sinif duzeyi `[EnableRateLimiting]` kaldirildi | **KIRMIZI VERMEDI** - (a) ve (b) gecti, yani mutasyon UYGULANDI | asagi |
| **M4b** M4 + yedek yolun `/payment/` eslesmesi de kapatildi | **3 PIN BIRDEN KIRMIZI** (initialize 401, callback 302, webhook 404 - hicbiri 429) | M4'un neden sessiz kaldigini AYRISTIRDI |

**M4/M4b - DURUST SONUC:** M4'un kirmizi vermemesi bir pin zaafi DEGIL, **K7'nin yedek yolunun
OLCULMUS etkisidir**: oznitelik dusse bile `KapsamSec` `/payment/` ayni 10/dk'yi uyguluyor.
M4b bunu ayristirdi. Yani p-k2 "initialize payment kovasinda" SOZLESMESINI tutar; o sozlesmeyi
hangi mekanizmanin (oznitelik mi yedek yol mu) tuttugunu AYIRT ETMEZ - ve bu, K2'nin
"etkin limit korunur" iddiasinin ampirik kanitidir.

Tum mutasyonlar geri alindi; kod tarafinda `[MUTASYON]` izi **0** (kalan 9 gecis CLAUDE.md'nin
tarihsel kayitlarindir).

## [NOT] HAVALELERI (FAZ 0'da DOKUNULMADI)

| [NOT] | Konu | Havale |
|---|---|---|
| #1 | `ProductQuestion.is_active` yaz-bir-kez | **K4'te KAPANDI** (deftere yazildi) |
| **#2** | **`GET /api/product-question/product/{id}` ANONIM ve HAM ENTITY donuyor - `customer_id` + `answered_by` disari acik** | **FAZ 2 - ONCELIKLI** |
| #3 | AuditLog ham entity + `PagedResult` sizintisi | **K6'da KAPANDI** |
| #4 | `api-client.auditLogs()` tanimli ama cagrilmiyor; admin'de denetim ekrani yok | FAZ 4 |
| #5 | ETag middleware `no-store`'u `private, max-age=60` ile eziyor - bilincli mi? | FAZ 5 |
| #6 | Storefront filtresi `stock_quantity > 0`, zenginlestirme `available` - tutarsizlik | **FAZ 2** |
| #7 | `Seller:RegistrationEnabled` uc ayri yerde okunuyor, tek kaynaktan turemiyor | FAZ 5 |
| #8 | `GiftCard.is_active` IKI anlam tasiyor (soft-delete + tuketildi) | FAZ 6 |
| #9 | Redis yolunda `reset-password`/`resend`/`verify-2fa`/`logout`/`refresh` global kovasinda | **K7'de YAPISAL OLARAK KAPANDI** |
| #10 | `user_sessions` 158 satirin 65'i pasif; birikme orani olculmedi | FAZ 6 |

## YEREL DOGRULAMA (FAZ 0)

Release/Debug **0 hata** · tam suitte **547 basarili / 550** (taban 540 + 10 yeni pin;
kirilan 3'un UCU DE Docker'li `OrderEndpointTests` - yerelde Docker kapali, CI'da yesil) ·
whitespace + style **exit 0**.

