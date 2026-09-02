# 45 · GUVENLIK-FIX-1b (GF-1b) MUHRU — OTURUM DERINLIGI (2 Eylul 2026)

Zemin `8ca6634` -> kapanis `00b012f`. Sekiz commit (K1..K10 + denetim duzeltmesi + DUR cozumu).
Tek migration `20260902090025_GF1bK3RefreshTokenOzetUnique` (K3'un).

---

## DALGA SONU RAPORU (AYNEN — merkeze verilen metin)

```
GF-1b (OTURUM DERINLIGI) - DALGA SONU RAPORU
zemin 8ca6634 -> HEAD cc25950 (7 commit, LOKAL, PUSH EDILMEDI)

== DUR (TEK) ==
K4'UN YARIS PENCERESI TAM KAPANMADI (L3 denetcisi olctu, ben dogruladim)
- CEKIRDEK SAGLAM: 48/48 turda es zamanli iki refresh'ten TAM OLARAK BIRI 200 aliyor.
  "Tek jetondan iki oturum" kusuru kapandi (M1 mutasyonu TAM 1 ISIMLI KIRMIZI verdi).
- ACIK KALAN: kaybeden istek InvalidateAllForCustomerAsync'i KAZANANIN yeni oturumu
  INSERT edilmeden ONCE kosarsa etkilenen satir 0 olur -> NE Critical alarm yazilir
  NE kazananin yeni oturumu iptal edilir. Olculdu: kapili duzenekte 23 turun 15'inde,
  K4B'nin kendi deseninde 25 turun 19'unda (aktif=1, olay=0).
- SONUC: hirsizlik sinyali TEK TURDA GARANTI DEGIL. Kaybolmuyor, GECIKIYOR (kaybeden
  eski jetonla tekrar denerse ust reuse dali atesler) - ama K4'un kendi iddiasi
  "ailenin tumu iptal" tek turda TUTMUYOR.
- PIN ETKISI: K4B bu nondeterministik cikti uzerine assert ediyor. Ana depoda 12/12,
  L3'te 8/8 yesil -> LATENT CI FLAKE.
- COZUM TASARIM KARARI ISTER (merkez): (a) kaybeden yolda alarmi KOSULSUZ yaz
  (tek kaynak korunur, parametreyle) mi, (b) oturum aile/kusak kolonu (migration) mi.
  KOD YAZILMADI - karar bekliyor.

== OLCUMLER (ureten ifadeleriyle, HAM defter: scratchpad/defter/gf1b-olcumler.txt) ==
Test envanteri (--list-tests): 8ca6634 -> 632 | cc25950 -> 642 | delta +10, KAYBOLAN 0
  (tarifteki "taban 631" SUPERSEDED isaretliydi; kendi olcumum 632)
UC ARDISIK DOGRULAMA (cc25950, SERI, worktree'siz):
  TUR1 Sql 376/376 · tam 639/642    TUR2 aynisi    TUR3 aynisi
  kirilan listeler UC TURDA BIREBIR (diff bos):
    OrderEndpointTests.PlaceOrder_ConcurrentRequests_NoOverselling
    OrderEndpointTests.PlaceOrder_InsufficientStock_Returns400_And_NoPartialData
    OrderEndpointTests.PlaceOrder_ValidCart_Returns201_And_DecrementsStock
  KOK SEBEP HAM CIKTIDAN: "Docker is either not running or misconfigured" - BILINEN
Release build 0 Hata · Debug 0 Hata · whitespace exit 0 · style exit 0
CR=0 TAB=0 sonda-bosluk=0 (degisen her .cs; TAB sayimi tr -cd, grep -P bu ortamda duser)

== KALEM KALEM (DALGA ICI DENETIM 1) ==
K1  coklu-cihaz access token iptali (revoked_before esigi, migration YOK)  1e50930
    PIN K1B x3 (2 davranis + 1 kaynak)  · kirmizi-once: yedek+elle geri alma
K2  change-password: kilit + step-up + auth hiz siniri                      b44dc72
    PIN K2B x2 · rig 1000'e cekildi (7 sinifin ZATEN kullandigi desen, test DISLAMA DEGIL)
K3  refresh + reset jetonlari DB'de SHA-256 HEX ozet + filtreli UNIQUE      b857fd3
    PIN K3B · TEK migration (20260902090025)
K4  rotasyon ATOMIK (DeactivateIfActiveAsync CAS)                           994d954
    PIN K4B (3 kanal) · kirmizi-once TAM 1 ISIMLI ("to be 1 ... but found 2")
    -> DUR: 2./3. kanal nondeterministik (yukari bak)
K5  cerez omru = oturum omru (OturumOmru.RefreshGun tek kaynak)             8a48db5
    ONCE olculdu: cerez 30 gun / oturum 7 gun = 23 GUN fazla yasiyordu
    PIN K5B · kirmizi-once "cerez (30,00) ve oturum (7,00) ... but found 551.99 saat"
K6  user_sessions.device / ip_address dolduruluyor (IHttpContextAccessor)   8a48db5
    ONCE: iki kolon da SEMADA vardi, HICBIR uretim yolu yazmiyordu
    PIN K6B (yalniz DEVICE) · L3 dogruladi: gonderilen UA BIREBIR, 250->200 kirpma dogru
K7  DEVIR -> GF-2a (kod YOK, iddia duruyor)
K8  DUSURULDU (merkez karari, yalniz yorum)
K9  RateLimitPolitikasi bayat yorumu duzeltildi (davranis degisikligi YOK)  8a48db5
K10 sifre sifirlama jetonu es zamanli da TEK KULLANIMLIK (CAS)              e30893c
    ONCE-DURUM GERCEK KOD UZERINDE 3 KEZ olculdu, 3/3: OK sayisi 2 (beklenen 1)
    PIN K10B (3 kanal) · L3 bagimsiz 12/12 ONAY, cekincesiz
DENETIM DUZELTMELERI                                                        cc25950

== YARIM KALAN / MERKEZ KARARI BEKLEYEN (DALGA ICI DENETIM 2) ==
1. DUR (K4 yaris penceresi) - yukarida
2. DENETIM IZI KAYBI: ExecuteUpdateAsync AuditInterceptor'i ATLAR (ChangeTracker
   uzerinden calisiyor). K10 sonrasi BASARILI SIFRE SIFIRLAMA artik audit_logs satiri
   BIRAKMIYOR ve ResetPassword security_events de YAZMIYOR (olculdu) -> olay IZSIZ.
   K4 sonrasi tek-oturum kapatma da audit uretmiyor. Kod siniri geregi DUZELTILMEDI.
3. ResetPassword K1'in iptal esigini YAZMIYOR (AccountManager change-password ve
   AuthManager logout-all yaziyor). Sifirlama en kritik sifre degisimidir; saldirganin
   access token'i 15 dk daha yasar. K1 kapsaminda goruldu ama K1 KAPANMISTI - karar merkezin.
4. DEV DB (DivisimaDb): K3 migration'i UYGULANMADI. Olculdu, varsayilmadi:
   user_sessions kolon=9 (kapi kaniti TUTTU) · refresh_token indeksi unique=0 filtre=YOK
   son migration 20260901230439_GF1K3UserSessionAuthTime
   ENGEL VAR MI: NOT NULL 342 satir, mukerrer grup 0 (Turkish_CI_AS) ve 0 (BIN2)
   -> filtreli UNIQUE TEMIZ uygulanir.
5. GOZLEM (denetci): IndexPagingAndSessionTests.cs:320 hala elle AddDays(7) yaziyor
   (TEST fiksturu, uretim degil) · AuthManager.Logout tek-oturum kapatmada hala
   check-then-act (K4'un iddiasi acikca ROTASYON'la sinirli, YANLIS IDDIA YOK)
   · expires_at DateTime.Now (yerel) / cerez UtcNow - bugun zararsiz, L3 ofset
   duzeltmesiyle gercek farki 0,0002 saat olctu; UTC bekleyen gelecek okuyucu 3 saat yanilir.

== YAN ETKI TARAMASI (DALGA ICI DENETIM 3) ==
ICacheService yeni uyeler -> Memory + Redis IKISI de guncellendi; tuketiciler
  CacheTokenBlacklist, CacheUserTokenRevocation, Program.cs kaydi
refresh_token (DB) yazan/okuyan uretim: AuthManager (ozet yazar) + EfUserSessionDal x2
  (ozet arar). BASKA YOK.
SellerAuthManager:101 `refresh_token` bir DTO ALANIDIR; SellerAuthManager user_sessions'a
  HIC YAZMAZ (grep: UserSession/_userSessionDal 0 gecis) -> K3 hashing'i Seller'i
  ETKILEMEZ. DOKUNULMAZ korundu, OLCULDU.
password_reset_token: AuthManager (ozet yaz + teshis oku), EfCustomerDal (CAS),
  AccountManager (silmede null) - tutarli
`new AuthManager(` ile ELLE kurulan yer: 0 -> istege bagli ctor parametresi kirmaz

== KENDI HATALARIM (DALGA ICI DENETIM 4) ==
1. MK-4b IHLALI (BENIM): L3 denetcisi test kosarken ben de tam suit kostum -> 188
   kirmizi. Kok sebep OLCULDU: Login failed 68 · timeout 192 · already exists 22 ·
   Cannot open database 4 · deadlock 3 = PAYLASILAN TEST DB CAKISMASI, kod kaynakli
   DEGIL. Kosum GECERSIZ sayildi, denetciler bitince SERI yenilendi.
2. K4 yorumu "IKINCI KOPYA ACILMADI" diyordu; KOD ONU YALANLIYORDU (kural uygulamasi
   1 -> 2 olmustu). Ustelik "acmadigini soyleyen" bir yorumla. Duzeltildi: tek kaynak
   YenidenKullanimiIsleAsync.
3. K9 yorumunda TARIHSEL aritmetik uydurmaydi: "o gun DOGRUYDU" ve "-2/+1 -> 9'da kaldi"
   (9-2+1=8). IKI denetci BAGIMSIZ buldu. Olculen zincir: d434906^ 10/6 · d434906 8/6
   (yorum burada yazildi, "9" dedi) · b44dc72 9/7 · bugun 9/7. Yorum ZINCIRLE degistirildi.
4. CAPA KIRLENMESI 3. VAKA: (2)'nin duzeltme yorumu taranan dizgeyi METIN olarak tasidi
   ve kendi sayimini 1 yerine 2 gosterdi. Yeniden yazildi.
5. K3 commit mesaji BAYAT SAYI tasiyor: "Category=Sql 371/371" yaziyor, o commit bir
   test EKLEDIGI icin 372 olmaliydi. Gecmis YENIDEN YAZILMAZ (force-push yasak) -
   MUHRE HATA KAYDI.

== PIN DURUSTLUGU (DALGA ICI DENETIM 5) ==
Dalgada eklenen 10 pin; L3 dordunu MUTASYONLA sinadi:
  K4B  M1 (CAS kosulu kaldirildi)        -> TAM 1 ISIMLI KIRMIZI  (ama 2./3. kanal DUR)
  K5B  M2 (cerez AddDays(30))            -> ISIMLI KIRMIZI, 551.99 saat
  K6B  M3 (device = null)                -> ISIMLI KIRMIZI
  K6B  M3b (ip_address = null)           -> 6/6 YESIL = VAKUMDU -> ASSERT KALDIRILDI,
       ad daraltildi (K6B_OTURUM_SATIRI_ISTEGIN_CIHAZINI_TASIR). IP yarisinin DAVRANIS
       KANITI YOK; sebebi olculdu (TestServer RemoteIpAddress uretmiyor) ve teste yazildi.
  K10B M4 (jeton temizligi kaldirildi)   -> TAM 1 ISIMLI KIRMIZI
Kaynak-sozlesme pini: yalniz K1B_IPTAL_YOLU_CACHE_ASIDE_KULLANMAZ (davranis kaniti
  ayni kalemin K1B davranis pinlerinde).
L3 EK GOZLEM: jeton tek-kullanimligi SifrePolitikasiTests'te TEK PINE bagli (K10B).

== BOZDUKLARIM (DALGA ICI DENETIM 6) ==
KALDIRILAN TEST: 0 (git diff ile olculdu). Hicbir pin bilincli kirilmadi.
DEGISTIRILEN PIN: K6B'nin IP asserti KALDIRILDI - ama o assert ZATEN HICBIR SEYI
korumuyordu (mutasyonla kanitli vakum), yerine korunan sey YOK cunku korunan sey YOKTU.
GF-1 pinleri (225/231 cagri bicimi) AYAKTA: IssueSessionAndTokenAsync imzasi DEGISMEDI.

== SIR HIJYENI ==
Eklenen satirlarda ciplak deger taramasi (suzgec POZ/NEG sinandi: POZ 1, NEG 0):
6 eslesme, hepsi TANIMLAYICI (migration adi, pin adlari, test User-Agent'i,
Status401Unauthorized). Jeton/hash/anahtar YOK. Iki denetci bagimsiz ayni sonuca vardi.

== MUHRE TASINACAKLAR ==
- Stash dersi (untracked dosyalarda olcum yedegi + elle geri alma KALICI USUL) - hata kaydi
- AYNI-SANIYE JETON PENCERESI: iat saniye cozunurlukludur ve kosul kasitli olarak "<";
  K1B ikinci-cihaz pini bu yuzden Task.Delay(1100) tasir -> BILINEN satiri
- MK-4b ihlali (yukarida hata 1) ve 188 kirmizinin kok sebebi
- K3 commit mesajindaki bayat 371 sayisi
- Capa kirlenmesi 3. vaka
- Denetci maliyeti: 3 denetci, 180 arac cagrisi, 4 bulgu (3 kapatildi, 1 DUR)

== DURUM ==
Calisma agaci TEMIZ (git status 0 satir) · HEAD dal uzerinde (main, MK-10) ·
worktree'ler KALDIRILDI · 7 commit LOKAL, PUSH EDILMEDI.
PUSH ve DUR kararlari MERKEZDEN bekleniyor.
```

---

## DUR ve MERKEZ COZUMU (F1-F4, commit `00b012f`)

**F1 — KAYBEDEN CAS YOLUNDA ALARM KOSULSUZ.** Tek kaynak korundu
(`YenidenKullanimiIsleAsync`, `alarmKosulsuz` parametresi). Pasif-jeton yolu KOSULLU
kalir (tekrar deneyen mesru istemci admin bildirimini SPAM'a cevirirdi); CAS yarisi
kaybi KOSULSUZ yazar — yaris kaybi "ayni jeton ayni anda iki kez sunuldu"nun TEK BASINA
kesin kanitidir ve tekrar denemeyle URETILEMEZ. `InvalidateAll` best-effort kalir.

**OLCUMLE ZORLANAN SAPMA (merkez tarifinden ayrildi — gerekcesi kayitli).**
Tarif K4B'yi "tam 1x200 + alarm >= 1" olarak daraltmayi soyluyordu. **Alarm kanali da
deterministik cikmadi:** uc ardisik tam suit kosumunun IKINCISINDE pin
`RefreshTokenReuse ... but found 0` ile kirildi (`Category=Sql` kosumlarinda 3/3 yesil —
yani yalniz yuk altinda ayrisiyor). Kok sebep kirmizi kosumun KENDI verisinden okundu:
kaybeden istek her zaman CAS yoluna DUSMEZ; kazananin CAS'i commit olduktan ama yeni
oturum INSERT edilmeden once okursa **pasif-jeton** yoluna gider ve orada alarm hala
kosulludur, supurulecek aktif oturum da YOKTUR. F1'in kosulsuz alarmi YALNIZ CAS dalini
kapsiyor. Cozum: es zamanli pin YALNIZ deterministik kanali tutar (tek basari); alarm +
zincir iptali + denetim izi, ayni kurali YARISSIZ kosulda ureten yeni bir pine tasindi
(`K4B_SIRALI_YENIDEN_KULLANIM_ALARM_YAZAR_ve_ZINCIRI_IPTAL_EDER`). Iki pin 8 ardisik
kosumda 8/8 yesil.

**F2 — CAS YOLLARINDA DENETIM IZI ELLE YAZILIR.** Olculen once-durum: `AuditInterceptor`
`ChangeTracker` uzerinden calisir, `ExecuteUpdateAsync` `SaveChanges`i ATLAR — basarili
sifre sifirlama HICBIR `audit_logs` satiri birakmiyordu ve `ResetPassword` guvenlik olayi
HIC yazilmiyordu (olay TAMAMEN IZSIZ). Yeni soyutlama ACILMADI: mevcut
`IAuditLogDal.AddAsync`; `AuditInterceptor._ignored` zaten `AuditLog`u disliyor, yazim
kendini tetiklemez; interceptor'a DOKUNULMADI. Uc yer: reset basarisi
(`Customer`/`password_reset`), rotasyon kapatmasi (`UserSession`/`session_rotated`),
zincir iptali (`UserSession`/`chain_revoked`).

**F3 — SIFIRLAMA DA BIR SIFRE DEGISIMIDIR.** `ResetPassword` artik toplu access-token
iptal esigini yazar. Once yalnizca change-password ve logout-all yaziyordu; "sifremi
unuttum" tam da hesabin ele gecirildigi durumda kullanilan yoldur ve refresh tarafi
kapatilirken saldirganin ACCESS token'i 15 dk daha calisiyordu.

**F4 — `IndexPagingAndSessionTests` fiksturu `AddDays(7)` yerine `OturumOmru.RefreshGun`.**

**KAPANIS OLCUMLERI (`00b012f`, SERI, worktree'siz — uc tur BIREBIR):**
`Category=Sql` **378/378** · tam suit **641/644** (3 kirmizi = bilinen Docker uclusu, kirilan
listeler uc turda `diff` ile ayni) · Release build **0 Hata** · `dotnet format whitespace`
exit **0** · `style` exit **0** · degisen her `.cs`: CR 0 / TAB 0 / sonda-bosluk 0.
Test envanteri `8ca6634` **632** -> `00b012f` **644** (+12, KAYBOLAN 0).
**SAPMA NOTU (MK-3):** tarif B7 icin "taban 376/642" diyordu; o deger `cc25950` anina aitti.
DUR cozumu iki pin daha ekledigi icin kapanis degeri **378/644**'tur ve B7'ye OLCULEN deger
yazildi, tarifteki degil.

---

## L3 DAVRANIS DENETCISI TABLOSU (worktree `gf1b-wt/d1`, HEAD `e30893c`)

| Kalem | Karar | Olcum |
|---|---|---|
| R-1 rotasyon atomik mi | KISMI ITIRAZ | tek basari 48/48 ONAY; zincir iptali + alarm NONDETERMINISTIK (kapili 8/23, K4B deseni 6/25) |
| R-2 cerez omru = oturum omru | ONAY | gercek fark 0,0002 saat (TZ ofseti duzeltilerek); 23 gun DEGIL |
| R-3 device / ip_address | KISMI ITIRAZ | `device` BIREBIR dogru, 250->200 kirpma dogru; `ip_address` dort yolda da NULL, pini VAKUM |
| R-4 reset jetonu es zamanli | ONAY (cekincesiz) | 12/12: 1x200, hesapta 1 gecerli sifre, ucuncu 400, jeton null |
| R-5 kapsam disi kirilma | ONAY | 3/642 = bilinen Docker uclusu; ara kosumdaki 176 kirmizi paylasilan test DB cakismasi |

Rapor denetcisi (`d2`) ve kural-uyum denetcisi (`d3`): kapsam · migration · MK-9 · MK-10 ·
sir sizintisi **UYUMLU**. Iki ITIRAZ (K4 "ikinci kopya", K9 tarihsel aritmetik) ve iki kucuk
bulgu (ctor imzasi anlatilmadi, K6B adi fazla soz veriyor) — hepsi kapatildi.

---

## PIN ve MUTASYON TABLOSU (10 + F kalemleri)

| Pin | Tur | Mutasyon | Sonuc |
|---|---|---|---|
| K1B_SIFRE_DEGISIMI_IKINCI_CIHAZI_DA_DUSURUR | davranis | esik yazimi | ISIMLI KIRMIZI |
| K1B_IPTALDEN_SONRA_ALINAN_YENI_JETON_CALISIR | davranis | — | cift-anlam kirici |
| K1B_IPTAL_YOLU_CACHE_ASIDE_KULLANMAZ | kaynak | MK-6 uygulandi | ISIMLI KIRMIZI |
| K2B_CHANGE_PASSWORD_BES_YANLISTA_KILITLENIR | davranis | kilit kontrolu | ISIMLI KIRMIZI |
| K2B_MIRAS_OTURUMDA_STEP_UP_YENIDEN_GIRIS_ISTER | davranis | — | fail-closed |
| K3B_DB_DE_DUZ_METIN_JETON_TUTULMAZ | davranis | ozetleme | ISIMLI KIRMIZI |
| K4B_..._TEK_BASARI_ve_ALARM | davranis | M1 CAS kosulu | TAM 1 ISIMLI KIRMIZI |
| K4B_SIRALI_YENIDEN_KULLANIM_ALARM_YAZAR... | davranis | MUT-F1 alarm kosulu tersine | TAM 1 ISIMLI KIRMIZI |
| K5B_CEREZ_OMRU_OTURUM_OMRUYLE_AYNI_ANDA_BITER | davranis | M2 AddDays(30) | 551,99 saat farkla KIRMIZI |
| K6B_OTURUM_SATIRI_ISTEGIN_CIHAZINI_TASIR | davranis | M3 device=null | ISIMLI KIRMIZI |
| K10B_AYNI_SIFIRLAMA_JETONU_ESZAMANLI_TEK_KEZ | davranis | M4 jeton temizligi | TAM 1 ISIMLI KIRMIZI |
| R1b9_SIFIRLAMA_IZ_BIRAKIR_ve_ESKI_ACCESS... | davranis | MUT-F2 / MUT-F3 | iki ayri TAM 1 ISIMLI KIRMIZI |

EMEKLI ASSERT: K6B'nin `(ip == null || ip.Length <= 64)` asserti — `nvarchar(64)` kolonda
HICBIR degerle kirilamaz (M3b ile kanitlandi: `ip=null` sabitlense bile 6/6 yesil). VAKUM.

---

## GF1b-B13 — HEX OZET, VARYANT KABULU KAPANDI

`user_sessions.refresh_token` ve `customers.password_reset_token` artik SHA-256 **hex**
tutuyor (base64 DEGIL). Gerekce OLCULDU: veritabani collation'i `Turkish_CI_AS` ve
base64 alfabesi buyuk/kucuk harf ayrimi tasir — `CI` collation altinda ayni jetonun
harf duzeni degistirilmis **varyantlari** de eslesirdi (etkin entropi ~227 bit'e duser).
Hex yalniz `[0-9a-f]` uretir; case-folding entropi kaybi YOKTUR (256 bit korunur).
Filtreli UNIQUE indeks (`[refresh_token] IS NOT NULL`) ayni turda eklendi.

---

## HATA KAYDI (BES + BIR)

1. **MK-4b IHLALI (benim):** L3 denetcisi test kosarken ana depoda tam suit kostum ->
   188 kirmizi. Kok sebep OLCULDU (Login failed 68 · timeout 192 · already exists 22 ·
   Cannot open database 4 · deadlock 3) = paylasilan test DB cakismasi, kod kaynakli DEGIL.
   Kosum GECERSIZ sayildi, denetciler bitince SERI yenilendi.
2. **K4 "IKINCI KOPYA ACILMADI" iddiasi YANLISTI** ve kod onu yalanliyordu (kural
   uygulamasi 1 -> 2 olmustu) — ustelik acmadigini SOYLEYEN bir yorumla. Rapor denetcisi
   olcup yakaladi. Tek kaynaga indirildi.
3. **K9 tarihsel aritmetigi UYDURMAYDI** (`9-2+1=8`, ve "o gun dogruydu" olcumle curudu).
   IKI denetci BAGIMSIZ buldu. Olculen zincir yaziya gecirildi.
4. **CAPA KIRLENMESI 3. ve 4. VAKA:** (2)'nin ve F3'un duzeltme yorumlari taranan
   dizgeleri METIN olarak tasidi ve kendi sayimlarini bir fazla gosterdi. Ikisi de yeniden
   yazildi (`grep` sonuclari 1 ve 2'ye dondu).
5. **STASH DERSI (K1, kalici usul):** `git stash push -- <yol>` untracked dosyalari
   BIRAKIR; kirmizi-once geri almasi fixli kod uzerinde kostu ve YALANCI "0 kirmizi"
   verdi. `(a) mutasyon indi mi` kontrolu 6 (!=0) gosterdi ve SUREC 5/(c) geregi bu
   "lokalize" SAYILMADI. Bundan sonra: **olcum yedegi + elle geri alma + md5 dogrulamasi.**
6. **K3 COMMIT MESAJI BAYAT SAYI TASIYOR:** `b857fd3` "Category=Sql 371/371" yaziyor;
   o commit bir test EKLEDIGI icin dogrusu 372 idi. Kural-uyum denetcisi statik sayimla
   yakaladi. **Gecmis yeniden yazilmaz (force-push yasak)** — kayit burada durur.
7. **ORTAM DERSI:** `audit_logs.action` kolonu **20 karakter** (`DivisimaDbContext.cs:620`).
   F2'nin ilk yaziminda `"session_chain_revoked"` (21) kullanildi ve SQL Server
   "String or binary data would be truncated" ile DUSTU; K4B pini yakaladi. Eylem adlari
   kisaltildi + `EylemEnUzun` kirpmasi eklendi.

---

## BILINEN (bes)

1. **AYNI-SANIYE JETON PENCERESI.** `iat` claim'i SANIYE cozunurluklu ve iptal kosulu
   KASITLI olarak `iat < esik`. Jeton ile esik ayni saniyeye duserse jeton iptal EDILMEZ —
   esigin kendi anini kapsamamasi icin bilincli secim. Pinler bu yuzden `Task.Delay(1100)`
   tasir (K1B ikinci cihaz, R1b9); beklemesiz kosumda eski jeton 200 doner (OLCULDU).
2. **MIRAS OTURUMDA STEP-UP YENIDEN GIRIS ISTER.** GF-1 oncesi acilmis oturumlarda
   `auth_time` NULL'dur ve K-7 karari geregi bu "BILINMIYOR" demektir (fail-closed):
   hassas islemde kullanici yeniden giris yapar. Geriye donuk doldurma YAPILMADI.
3. **342 OLU OTURUM.** K3 geriye donuk ozetleme YAPMADI (merkez karari); dev DB'deki
   mevcut duz metin `refresh_token` satirlari ozet aramasiyla ESLESMEZ ve fiilen olu
   oturuma doner. Launch oncesi kabul.
4. **IP DAVRANIS KANITI YOK.** `user_sessions.ip_address` uretimde `IHttpContextAccessor`
   uzerinden doldurulur ama `WebApplicationFactory` test sunucusunda `RemoteIpAddress`
   uretilmez — L3 dort ayri yoldan olctu, DORDUNDE DE null. Kolon siniri (64) kaynak
   duzeyinde kirpma ile korunuyor. Uctan uca kanit gercek Kestrel/proxy ister -> GF-3.
5. **K4 GECIKMELI AILE IPTALI.** Es zamanli yarista kaybeden, kazananin INSERT'inden once
   kosarsa aile iptali VE alarm o turda gerceklesmeyebilir; kaybeden eski jetonla ikinci
   kez denedigin de pasif-jeton yoluna duser ve zincir O ZAMAN kapanir. Kalici cozum
   **GF-3**: rotasyon TEK DB transaction'i (CAS + INSERT birlikte commit), kaybeden CAS'i
   ancak commit sonrasi gorur ve supurme kazananin satirini DA kapsar.

---

## KURGU ENVANTERI

**GF-1b HICBIR YENI KURGU KAYDI URETMEDI.** Testler ayri CI/sinif veritabanlarinda kostu;
dev DB'ye (`DivisimaDb`) YALNIZ OKUMA yapildi. Olculdu (ureten ifadeleriyle):

```
SELECT COUNT(*), MAX(id) FROM customers;                          -> 149 / 169
SELECT COUNT(*), MAX(id) FROM user_sessions;                      -> 342 / 342
SELECT MAX(id) FROM orders;                                       -> 286
SELECT COUNT(*), SUM(CAST(id AS bigint))
  FROM orders WHERE status = 0 AND id <= 210;                     -> 35 / 3837
```

MAX musteri **169** (tarifteki `gf1b.<n>@example.com` hesaplarindan HICBIRI acilmadi) ·
siparis **286** · Pending(id<=210) **35/3837** — MK-3 uclusu BIREBIR tuttu. m10 icerik
duzeyinde DOKUNULMADI.

### DEV DB MIGRATION (push sonrasi, cift yesilden SONRA)

`dotnet ef database update --project Divisima.Dal --startup-project Divisima.API` -> exit 0,
`Applying migration '20260902090025_GF1bK3RefreshTokenOzetUnique'. Done.`
Bayat-ikili tuzagi icin ONCE `dotnet build Divisima.API` (0 Hata) kosuldu; `--no-build`
KULLANILMADI. API sureci kosmuyordu (DLL kilidi yok).

**KAPI AYIRT-ETME KANITI (ayni sorgu, iki durum):**

```
ONCE  : indeks IX_user_sessions_refresh_token  unique=0  filtre=YOK
        son migration 20260901230439_GF1K3UserSessionAuthTime
SONRA : indeks IX_user_sessions_refresh_token  unique=1  filtre=([refresh_token] IS NOT NULL)
        son migration 20260902090025_GF1bK3RefreshTokenOzetUnique
```

**VERI KORUNDU:** `user_sessions` **342** satir / MAX **342** (migration oncesi de 342) ·
kolon sayisi **9** (degismedi). Engel on-olcumu tutmustu: NOT NULL 342 satirda mukerrer
grup 0 (`Turkish_CI_AS`) ve 0 (`Latin1_General_BIN2`).

**D-YAN — m10 OTURUM DURUMU:** `customer_id = 10` icin `user_sessions` **toplam 19,
aktif 0**. Yani Omer'in hesabinda CANLI oturum YOK; K3'un geriye donuk ozetleme YAPMAMASI
bu hesap icin bir davranis degisikligi URETMEZ. (m10 icerik duzeyinde DOKUNULMADI.)

**MK-3 UCLUSU MIGRATION SONRASI DA BIREBIR:** musteri MAX **169** · siparis MAX **286** ·
Pending(status=0 AND id<=210) COUNT **35** SUM **3837**.

---

## DENETCI MALIYETI (SDP 1.10)

3 denetci (L3 davranis · rapor · kural-uyum), her biri KENDI worktree'sinde (MK-4b),
toplam **180 arac cagrisi** / ~549k alt-ajan jetonu. **4 bulgu**: 3 kapatildi (`cc25950`),
1 DUR merkeze gitti ve `00b012f` ile cozuldu. Iki denetci AYNI bulguyu (K9 aritmetigi)
BAGIMSIZ olarak buldu — capraz dogrulama. L3'un DUR bulgusu, ana akisin ve diger iki
denetcinin GORMEDIGI tek kalemdi; bedeli bir dalga uzamasi, kazanci CI'da yalanci yesil
verecek bir pinin ve garanti edilmeyen bir hirsizlik sinyalinin yakalanmasi.
