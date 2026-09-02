# 44 · GUVENLIK-FIX-1 (GF-1) MUHRU — KIMLIK / OTURUM

**Zemin:** `ed1bcfe` · **Kapanis:** `189ce81` · **Tur turu:** URETIM KODU DEGISTI · **Tarih:** 2026-09-02
**Commit'ler (tek push, `ed1bcfe..189ce81`):**
`2496d6c` K1 · `6bb1638` K4+K5 · `72920f1` K2 · `df7a567` K3 · `a8f792a` K6 · `189ce81` K1-ek
**Kaynak tarif:** `42·GUVENLIK-AV-1 · BOLUMLEME ONERISI` (K9) + merkez bolumleme karari (bolum 9)
**Suit:** `Category=Sql` **367/367** · tam suit **632** (629 yesil / 3 kirmizi = bilinen Docker uclusu)

---

## 0. KAPI

```
sdp   19.910 B · surec 9.044 B — ikisi de Skill cagrisiyla yuklendi (fallback YOK)
SDP 1.12 GUVENLIK modulu: docs/muhur/42-guvenlik-av-1.md:273-477 (MK-11 b somut gerekce)
Zemin ed1bcfe = origin/main · agac 0 satir · dal main
Defter: oturum scratchpad'i gf1/ (depo DISI) · maskele.pl BU TURDA yeniden sinandi (POZ 2 / NEG 0)
```

**D1 SECILEN YOL (uc satir):** `sid` claim'i **YOK** -> secenek (1) elendi. Kara liste deposu
**VAR** + `jti` claim'i **VAR** (`JwtHelper.cs:37`) -> **secenek (2)**, migration gerekmedi.
Secenek (3) gerekmedigi icin **D1-3 DUR'u TETIKLENMEDI**.

---

## 1. DUR / COZUM ZINCIRI

On olcum, tarifin **alti kaleminin ALTISINDA** engel buldu ve dalga merkeze DUR verdi.
Merkez kararlari geldikten sonra kod yazildi.

| # | ON OLCUMDE CIKAN ENGEL | MERKEZ KARARI |
|---|---|---|
| 1 | R-G1 ayni e-postayla URETILEMEZ (409 kapisi `:84/:86`, PlaceOrder `:262`'den ONCE) | Guard 409 kapisindan da ONCE; replay yalniz e-posta eslesirse 200, degilse 400 |
| 2 | IKINCI REPLAY KOPYASI `OrderManager.cs:478-485` — K1 zarar sinifini KAPATMIYOR | Kapsama alindi: `replayed` bayragi + telafi kosulu "bu cagri siparis yazdi mi" |
| 3 | K2: kara liste kendini zehirliyor; salt-okuma onbellek uyesi YOK | `ICacheService.ExistsAsync` ONAY (iki uygulama) |
| 4 | K3: `created_at` ROTASYON anidir -> `auth_time` KOLON ister (ikinci migration) | Ikinci migration RED; dalganin TEK migration'i K3'e |
| 5 | K4: sozlesme UC yerde ihlal, tarif BIRINI aniyor | Ucu de kapsamda; kalan 11 rol/CSRF/IP 403 negatif kontrol pini |
| 6 | K5: "MapGet=0" asserti BUGUN ZATEN dogru (vakum) | Assert YAZILMAZ; uc gercek tasiyici pinlenir |
| 7 | K6: paylasilan `HashingHelper` — surum kolonu Seller'i KIRAR | ZARF onayi: surum degerin biciminden turer, migration YOK |
| 8 | Denetim: `customers.email` tekil indeks yarisi islenmeyen 500 (dalga ONCESINDE de) | K1-ek: ihlal yakalanir, 500 uretilmez |

---

## 2. L3 CIFT-KOR DAVRANIS TABLOSU (denetci kendi harness'iyle olctu)

| Davranis | ONCE `ed1bcfe` | SONRA `a8f792a` |
|---|---|---|
| R-G1(i) ayni e-posta ardisik | 2. istek **409**, e-posta kalici kilitli | **200 + `replayed:true`**, ayni `order_number` |
| R-G1(iii) farkli e-posta + ayni rid | **200, `order_number` SIZDI** + yetim musteri/adres | **400**, sizinti yok, satir yok |
| R-G1(ii-b) eszamanli farkli e-posta | kaybeden **200 + kazananin numarasi**, yetim 1 | kaybeden **400**, yetim 0 |
| R-G2 cikis / sifre degisimi | eski token **200** | **401** (ikisinde de) |
| R-G3 step-up | `auth_time` sifirlaniyor -> `/account/delete` **200, HESAP SILINDI** | tasiniyor (-1800 sn) -> **401** |
| K4 sahiplik (uc uc) | **403** "size ait degil" | **404**, yok-olanla durum VE mesaj birebir |
| K6 sifre ozeti | 64/128, giriste degismiyor | **69/16** (0x02), giriste yeniden yaziliyor |

**K1-ek L3 (ayri tur, 52 kosum):**

| | ONCE `a8f792a` | SONRA `189ce81` |
|---|---|---|
| ayni e-posta + ayni rid | 16/16 `201+500` | 15/16 `201+409` · 1/16 `200+201` |
| ayni e-posta + FARKLI rid | 10/10 `201+500` | 10/10 `201+409` |
| 5xx sayisi | **26** | **0** |
| musteri / adres / rid-siparis | 1 / 1 / 1 | 1 / 1 / 1 |
| YETIM | YOK (26/26) | YOK (26/26) |

Denetci ayirt edicilik kanitini da uretti: `IDENT_CURRENT` ile 52/52 turda `musteri +2`
olctu (basarisiz INSERT de identity tuketir) -> yaris GERCEKTEN olusuyor, olcum VAKUM DEGIL;
52/52'de `siparis +1` -> kaybeden `PlaceOrder`a HIC varmiyor.

**Denetci ifade incelemesi (G-1, kod degisikligi gerektirmez):** K1-ek commit'i "409 mevcut
semantik DEGISMEDI" diyor; **sirali** yolda dogru, **eszamanli** yolda ONCE 409 degil **500**
donuyordu. Yani gozlemlenebilir davranis 500 -> 409 olarak DEGISTI, dogru yonde.

---

## 3. PIN ve MUTASYON LISTESI

**Davranis pinleri (13):** K1 (3) · K1-ek (1) · K4 (1) · K2 (3) · K3 (2) · K6 (3)
**Kaynak-sozlesmesi pinleri (9, ISARETLI):** K4 (2) · K5 (3) · K2 (1) · K3 (1) · K6 (2)

| MUT | mutasyon | sonuc |
|---|---|---|
| MUT-1 | `RequireAuthorization()` sokuldu | TAM 1 adli kirmizi (K5a) |
| MUT-2 | `NotificationHub` `[Authorize]` sokuldu | TAM 1 adli kirmizi (K5b) |
| MUT-3 | Hangfire yetki filtresi sokuldu | TAM 1 adli kirmizi (K5c) |
| MUT-4 | ReturnManager sahiplik dali 403'e dondu | 2 adli kirmizi, sayac 11 -> 12 |
| MUT-4b | sahiplik dali BadRequest (403 URETMEZ) | YALNIZ pencere pini kirmizi, sayac pini YESIL = ayirt edici |
| MUT-5 | kara liste okuma yoluna `GetOrSetAsync` geri kondu | 3 kirmizi (kaynak + IKI davranis bacagi) |
| MUT-6 | refresh `session.auth_time` yerine tek arguman | 2 kirmizi; giris pini YESIL kaldi = ayirt edici |
| MUT-7 | `CreatePasswordHash` v1'e dondu | 3 kirmizi (zarf + zamanlama + uctan uca) |
| MUT-8 | v1 dalinin zamanlama esitleyicisi sokuldu | TAM 1 adli kirmizi |
| MUT-9 | Seller yoluna sessiz yeniden yazim eklendi | TAM 1 adli kirmizi |

**MUT-3 ilk denemede UYGULANMADI** (capa 0) ve "0 kirmizi" verdi; SUREC 5/(c) geregi bu
"lokalize" SAYILMADI, girinti olculup TEKRARLANDI. **MUT-4 once YANLIS SATIRI** vurdu
(`tail -1` baska bir daldaki `NotFound`u secti); satir dogrulanip MUT-4b ile sahiplik dali
AYRICA sinandi. Dokuz geri yuklemenin dokuzu da MD5 ile bayt-ayni dogrulandi.

---

## 4. TEK KANAL ISARETLERI (SDP 1.12.10-b — merkez karari (e))

| kalem | kanit kanali | davranis kaniti |
|---|---|---|
| K6 zamanlama esitleyicisi | TEK (kaynak sayimi + defterdeki tek seferlik olcum) | sure CI'da PINLI DEGIL (esik pini flake uretirdi) |
| K2 `RedisCacheService.ExistsAsync` | TEK (kaynak) | ortamda Redis kapali, CI'da kosucu YOK |
| K5b `NotificationHub` | TEK (yansima + kaynak) | CI'da SignalR kosucusu YOK |
| K5c Hangfire panosu | TEK (kaynak) | CI'da Hangfire kosucusu YOK |

**Zamanlama olcumu** (.NET 8 uretim kod yolu, 10 tekrar, defter `olcum/k6-zamanlama.txt`):
v2 **32,5 ms** · v1 **32,6 ms** · 0-bayt dali **32,9 ms** · v1/v2 orani **1,003** ->
merkezin 150 ms DUR esigi **ASILMADI**.
**Pinin gormedigi dal:** `HashingHelper.cs:89` `if (iterasyon <= 0) return false;` HIC
TURETME YAPMADAN doner. Uretimden ULASILAMAZ (zarfa her zaman 100k yazilir) ama "her dal
ayni maliyeti oder" iddiasi O DAL ICIN GECERSIZDIR.

---

## 5. EKSIK KANIT — IKISI DE OLCULDU

**K5c: filtre EKLENMEDI, ZATEN VARDI.** `git show ed1bcfe:.../HangfireAuthorizationFilter.cs`
-> `IsAuthenticated != true) return false` + `userType == "1"`; `Program.cs` zaten bagliydi
(gecis 1). Dalga iki dosyaya da DOKUNMADI (diff 0 satir). Merkezin "yoksa eklenir" sarti
TETIKLENMEDI.

**BILINEN SINIR — sifre degisimi TUM oturumlari iptal ETMIYOR** (olculdu,
defter `olcum/ikinci-cihaz.txt`):

```
cihaz2 sifre degisiminden ONCE   : 200
change-password (cihaz1)         : 200
cihaz1 (sifreyi degistiren) SONRA: 401
cihaz2 (IKINCI CIHAZ)       SONRA: 200   <- HALA GECERLI
DB aktif user_sessions satiri     : 0    <- refresh tarafi TAMAMEN kapaniyor
```

Yani **cikis yalniz o oturumu, sifre degisimi de yalniz SUNULAN jetonu** iptal ediyor;
ikinci cihazin access token'i en fazla 15 dk daha yasiyor. Tam coklu-cihaz iptali
`tokens_valid_from` benzeri bir KOLON ister. **HEDEF: GF-1b.**

---

## 6. GF-1 TUREV BULGULARI (merkez karari (d)) — 12 KALEM

Hicbiri GF-1'de duzeltilmedi. KANAL: K=kaynak · C=canli-API · D=DB.

| id | SIDDET | ON KOSUL | DURUM | KONUM | KANAL | HEDEF |
|---|---|---|---|---|---|---|
| GF1-B1 | YUKSEK | KIMLIKSIZ-UZAK | AKTIF | GuestCheckout replay guard'i — govde ozeti YOK | 1 (K) | **GF-3** |
| GF1-B2 | YUKSEK | KIMLIKLI | AKTIF | `change-password` oran-sinirsiz + kilitsiz + step-up'siz | 2 (K+C) | GF-1b |
| GF1-B3 | YUKSEK | YEREL | LATENT | `customers.password_reset_token` duz metin (`DivisimaDbContext.cs:301`) | 1 (K) | GF-1b |
| GF1-B4 | YUKSEK | YEREL | LATENT | `user_sessions.refresh_token` duz metin + IX unique DEGIL | 2 (K+D) | GF-1b |
| GF1-B5 | ORTA | KIMLIKLI | LATENT | Refresh rotasyonu atomik degil, CAS yok (`AuthManager` :458/:459) | 1 (K) | GF-1b |
| GF1-B6 | ORTA | KIMLIKLI | AKTIF | Cerez 30 gun vs oturum 7 gun; `AuthController.cs:207-208` yorumu YANLIS | 1 (K) | GF-1b |
| GF1-B7 | DUSUK | ADMIN | LATENT | `device` / `ip_address` HIC doldurulmuyor | 2 (K+D) | GF-1b |
| GF1-B8 | DUSUK | KIMLIKLI | AKTIF | JWT govdesinde `email` claim'i acik (jeton localStorage'da) | 1 (K) | GF-1b |
| GF1-B9 | DUSUK | YEREL | LATENT | `AccessToken.RefreshToken` / `.RefreshTokenExpiration` OLU alanlar | 1 (K) | GF-1b |
| GF1-B10 | DUSUK | KIMLIKSIZ-UZAK | LATENT | `reset-password` TOCTOU (CAS yok) — SUPHE | 1 (K) | GF-1b |
| GF1-B11 | DUSUK | KIMLIKSIZ-UZAK | LATENT | Seller login kilit oracle'i — BILINEN `00a:101`, bu turda dogrulandi | 1 (K) | GF-1b |
| GF1-B12 | DUSUK | YEREL | LATENT | `RateLimitPolitikasi.cs:70` yorumu ile `:122-123` kodu ayrisiyor | 1 (K) | GF-1b |

**KAPANAN ACIK SORU (listeye GIRMEZ, negatif sonuc):** reset jetonunun `Turkish_CI_AS`
altinda catisma riski — `SecureTokenGenerator.cs:9-15` alfabesi URL-guvenli base64,
32 bayt = 43 karakter, CI collation harf basina 1 bit yer, kalan ~216 bit -> RISK IHMAL
EDILEBILIR. Rapor denetcisi olctu ve kapatti.

**GF-1'DE KAPANANLAR (tabloya girmez):** A-jeton A-1 (`RevokeAsync` 0 cagri) + A-2 (kara
liste zehirlenmesi) -> K2 · C-hash B-3/B-5 (adaptif KDF yok + 64/128 pinsiz) -> K6.

---

## 7. CC HATALARI (on kalem + curuyen indeks)

1. **D2'yi YANLIS sonuclandirdim** — "`created_at` yeter, migration gerekmez" dedim;
   `IssueSessionAndTokenAsync` HER cagrida yeni satir ekliyor, yani `created_at` ROTASYON
   anidir. Kaynaktan duzeltildi, K3 kolon aldi.
2. **K1'in ikinci replay kopyasini** deftere KAYDETTIM ama fix-sonrasi kalinti yola
   BAGLAMADIM — Secenek-3 onerim eksik verildi. Kapsam elestirmeni bagladi.
3. **K1 (ii) pini ilk halinde OLCMUYORDU** — geri alinmis kodda 3/3 yesil kaldi.
4. **Telafinin kirlenmis change-tracker yuzunden sessizce dustugunu** once GORMEDIM
   (olculen once-durum: musteri=2 adres=2 siparis=1).
5. **403 sayiminda ikinci deseni** (`StatusCodes.Status403Forbidden`) KACIRDIM -> merkezin
   "11" sayisiyla gereksiz ayrisma. Iki desen birlikte 8+3=11.
6. **Capa kirlenmesi (1):** kaldirdigim mesaj sabitlerinin ADINI, o adlari tarayan belgeye
   yazdim. ARSIV-2'de KAYITLI dersin tekrari.
7. **Capa kirlenmesi (2):** "kara liste `GetOrSetAsync` kullanmamali" pinini, yasakladigi
   dizgeyi aciklamasinda tasiyan dosyaya kurdum -> pin mutasyondan ONCE kirmizi verdi.
   Cozum aciklamayi kirpmak DEGIL, olcumu KODLA sinirlamak oldu (`KodSatirlari`).
8. **MUT-4'te YANLIS SATIRI** mutasyona ugrattim (`tail -1` baska bir dali secti).
9. **K6 suresini YANLIS CALISMA ZAMANINDA olctum** — PowerShell/.NET Framework 236 ms verdi
   ve merkezin 150 ms esigini asiyor gorundu; uretim .NET 8 yolunda 32 ms. Duzeltilmeseydi
   merkeze GEREKSIZ DUR gidecekti.
10. **MK-5 boslugu:** zamanlama ve suit sayilari bir sure DEFTERE YAZILMADI (yalniz konusma
    baglaminda kaldi). Rapor denetcisi "uydurma adayi" olarak isaretledi; ikisi de ureten
    ifadeleriyle deftere alindi, zamanlama YENIDEN olculdu.
11. **CURUYEN IDDIA — `customers.email` tekil indeksi.** K1 commit'i ve test yorumu
    "tekil indeks YOK (`DivisimaDbContext.cs:290`)" diyordu. **YANLIS:** indeks
    `DivisimaDbContext.cs:320`'de (`HasIndex(c => c.email).IsUnique()`) ve
    `01_schema.sql`'de (`CREATE UNIQUE INDEX [IX_customers_email]`) VARDIR — dalga
    ONCESINDE de vardi. Ilk olcum `Entity<Customer>` blogunu 30 satirlik pencereyle
    taradigi icin indeksi BES SATIR farkla kacirdi; `:290` bir KOLON ESLEMESIDIR.
    **Dogrusu: e-posta tekil indeksi `:320`'de vardi.** Test yorumu K1-ek commit'inde
    DUZELTILDI; K1 commit metni DEGISTIRILMEDI (rewrite yok).

**AJAN HATASI:** on olcum ajani C, `appsettings.Development.json` okurken `grep -A6`
penceresinin `TokenOptions:SecurityKey` satirina tastigini ve degerin TERMINALDE gorundugunu
kendi raporladi. Maruziyet olculdu: dosya git'te izlenmiyor ve `.gitignore:84` ile yok
sayiliyor -> depoya GIRMEDI. Dortlu kayit: alan `TokenOptions:SecurityKey` ·
`Divisima.API/appsettings.Development.json:6` · uzunluk 64 karakter · desen "JWT imza
anahtari (gelistirme)". "Maskeleme uretim noktasinda" dersinin 4. vakasi — yine uretim kodu
degil, KANIT OKUMA ani.

---

## 8. KURGU ENVANTERI

```
MAX musteri 168 · siparis 286 · adres 119 · fatura 119 · Pending(id>210) 10 · user_sessions 339
  -> 42·GUVENLIK-AV-1 kapanisiyla BIREBIR
MK-3 UCLUSU (ureten ifadeleriyle):
  SELECT COUNT(*), MAX(id) FROM orders WHERE customer_id = 10;                  -> 38 / 211
  SELECT COUNT(*), MIN(id), MAX(id), SUM(CAST(id AS bigint))
    FROM orders WHERE status = 0 AND id <= 210;                                 -> 35 / 9 / 210 / 3837
  SELECT COUNT(*), SUM(total_price), STRING_AGG(CAST(status AS varchar),',')
    FROM orders WHERE customer_id = 74 AND id BETWEEN 234 AND 237;              -> 4 / 4698,60 / 0,0,1,1
```

**KOD FAZI GELISTIRME VERITABANINA HIC YAZMADI** — tum kosumlar sinif basina AYRI test
veritabanlarinda yapildi; yukaridaki MAX'lar ve MK-3 uclusu push ANINDA birebir tuttu.

**KAPANIS FAZI (goz1) — TEK YENI KURGU KAYDI, URETIM YOLUNDAN:**

```
migration uygulandi : user_sessions.auth_time kolonu 0 -> 1 (NEG kontrol 0)
                      339 mevcut satirin HEPSI NULL - geriye donuk doldurma YOK
musteri 169  gf1.1@example.com   (register -> verify-email -> login, ELLE SQL YOK)
  password_hash 69 B · password_salt 16 B   <- K6 v2 ZARFI URETIM YOLUNDA CANLI DOGRULANDI
  consent_records 1 · orders 0
user_sessions 339 -> 342 (giris + refresh rotasyonu)
`id > 168 AND email NOT LIKE 'gf1.%'` -> 0     (uretim imzasi)
MAX siparis 286 · adres 119 · fatura 119 · Pending(id>210) 10  -> DEGISMEDI
Omer (musteri 10) `updated_at` NULL — DOKUNULMADI
```

**MAX musteri 168 -> 169.** D-YAN'a devredilen: musteri **169** (`gf1.1@example.com`) +
`consent_records` 1 + `user_sessions` 340-342.

---

## 8b. R-G3 goz1 KANIT SATIRI (dev DB, migration uygulandiktan SONRA)

API `goz1` duzeneginde BES ARGUMANLA ayrik baslatildi (teyit edildi):
`--Iyzico:UseRealSdk=false --AdminSeed:Enabled=false --BackgroundJobs:Enabled=false
--RateLimit:AuthPermitLimit=100 --MailSettings:Host=`

```
giris -> user_sessions.auth_time NULL DEGIL          (kolon uctan uca CALISIYOR)
oturumun giris ani 30 dk GECMISE cekildi             (guncellenen satir 1)
POST /api/auth/refresh (Cookie + X-CSRF-Token)       -> success:true
yeni jetonun auth_time claim'i = 1788329931
beklenen (gecmise cekilen deger)= 1788329931
FARK = 0 sn                                          -> TASINDI (sifirlansaydi ~1800)
yeni jetonun YASI = 1847 sn                          -> step-up saati SIFIRLANMADI
yeni oturum satiri auth_time  = 2026-09-02T06:18:51  -> zincir SONRAKI rotasyona da tasiniyor
```

**OLCUM HATASI (kayit):** ilk cikarici `"auth_time":"[0-9]+"` (TIRNAKLI) ariyordu ve BOS
dondu; claim JWT govdesinde TIRNAKSIZ SAYIDIR (`"auth_time":1788329931`). POZ/NEG sinamasi
(POZ 1 / NEG 0) hatayi YAKALADI ve suzgec duzeltildi — capa EZBERDEN yazilmisti (MK-7).

---

## 9. BOLUMLEME KARARI (merkez metni — KAYIT blogu)

```
GF-1 KIMLIK/OTURUM [backend, migration olasi]: DV1 (BAS) · C-1 · C-2 · B-1 · B-2 · C-4
GF-2a ISTEMCI KACIS [frontend]: D-1 · D-2 · D-3 · D-4 · D-5 · D-10 · D-11 · D-6 · D-8
GF-3 SIZINTI/YAPILANDIRMA/LIMIT [backend config]: E-2 · E-3 · B-09 failed-jobs · E-1a ·
     E-5 · E-4 · E-6 · F-1 · F-2 · A-3
GF-2b CSP [frontend, D-7]: 11 satir ici script disa + unsafe-inline/unsafe-hashes/blob sokum
GF-4 TEDARIK ZINCIRI [CI/paket]: G-2 · G-5 · G-6 · G-4 · G-3 · G-1 = 12.0.1 KALIR
BILINEN/KABUL EDILMIS RISK: C-3 (00a:101) · D-9 · E-1b · Webhook:AllowedIps bos · hibrit jeton
BASKA KUYRUGA: A-2 -> VITRIN-KALAN 8 · F-3 -> IMPORT-FIX
SIRA: GF-1 -> GF-2a -> GF-3 -> GF-2b -> GF-4 -> GUVENLIK-AV-2 -> VITRIN-KALAN -> FIX-1B
```

**GF-1 KAPANDI** (`189ce81`). Kuyruga **GF-1b** eklendi (GF-2a'nin ONUNE): 11 turev bulgu
+ coklu-cihaz iptali hedefi. **GF1-B1** (govde ozeti) **GF-3**'e devredildi.

---

## 10. KOD DIFF

```
32 dosya, +4820 / -47  (ed1bcfe..a8f792a) + 3 dosya, +168 / -43 (K1-ek)
uretim 22 dosya · test 6 dosya · migration 3 dosya (EF uretimi) · db script 1 dosya
TEK MIGRATION, TEK KOLON: user_sessions.auth_time datetime2 NULL
```

**MK-9 bagimsiz dogrulandi:** ayri bir worktree'de BES checkpoint'in BESINDE de
`whitespace` exit 0 / `style` exit 0.

**Denetim sonucu:** kural-uyum **8/8 UYDU, ihlal yok** (kapsam disi dosya 0, dokunulmaz
0 satir, sir 0). Rapor denetcisi **20 iddiayi birebir dogruladi**, 1 curuyen iddia + 2 asiri
iddia + 12 rapora girmeyen bulgu cikardi — hepsi bu muhurde karsilandi.
