# 56 · LAUNCH GO/NO-GO + LAUNCH-FIX-1 (LF-1)

**Tarih:** 6 Eylul 2026 · **Zemin:** `b711c5e` (= GF-6 kapanisi, muhur 55)
**Kapanis:** LF-1 commit zinciri (asagida) · **Karar:** **GO** (bolum 3)

Bu muhur UC parcadir:
1. **LAUNCH HAZIRLIK OLCUMU raporu — AYNEN** (salt olcum turu, `b711c5e` zemini). Tek satiri
   degistirilmedi; o turun kendi olcumleri, kendi durust sinirlari ve kendi "MUHUR 56
   YAZILMADI" notu dahil BIREBIR duruyor.
2. **LAUNCH-FIX-1 (LF-1)** — o olcumun buldugu uc dagitim blokerinin (BL-1/BL-2/BL-3)
   kapatilmasi ve K1/K2/K5/K6/K9'un durum degisikligi.
3. **GO karari + BILINEN K1-K10 DURUM sutunuyla** (SDP 1.12.8: kapali kalem yeni bulguyu
   BASTIRMAZ).

---

## 1. LAUNCH HAZIRLIK OLCUMU RAPORU (AYNEN — 6 Eylul 2026, zemin `b711c5e`)

> Asagidaki blok, salt-olcum turunun raporunun DUZ METIN karsiligidir ve **degistirilmeden**
> alinmistir. Icindeki satir numarali atiflar O ANIN zeminine (`b711c5e`) aittir; LF-1 ayni
> dosyalarin bir kismini degistirdi, dolayisiyla **bu bolumdeki satir numaralari BAYATTIR ve
> bilerek boyle birakilmistir** (MK-11/d: capa, yazildigi ani kaydeder; duzeltmesi bolum 2'de).

```
LAUNCH HAZIRLIK OLCUMU — SALT OLCUM, KOD/CONFIG DEGISMEDI
zemin ve kapanis b711c5e (= origin/main) · agac 0 · worktree 1 · 2 ajan (kapsam elestirmeni + denetci)

(1) LAUNCH BLOKER — DAGITIM TARAFI (uc kalem)

BL-1  Cookies:Domain  [OTURUM]  SESSIZ  — EN AGIR
  ZINCIR (dort halka, kaynaktan olculdu):
    ops/infra/nginx.conf:10  server_name api.divisima.com
    ops/infra/nginx.conf:85  server_name divisima.com www.divisima.com      -> AYRI HOST
    AuthController.cs:296-297  domain BOS ise o.Domain SET EDILMEZ           -> host-only cerez
    frontend/api-client.js:215 csrf_token document.cookie'den okunur          -> OKUYAMAZ
    AntiforgeryMiddleware.cs:26-35  guvensiz metot + refresh_token cerezi + Bearer YOK -> 403
    frontend/api-client.js:285-292  refresh cagrisi TAM BU uclu (govdesiz, credentials:include, Bearer YOK)
  SONUC: her kullanici access token'in 15 dk omru dolunca oturumunu kaybeder ve YENILEYEMEZ.
  AGIRLASTIRICI: anahtar appsettings.json'da 0 gecis · fail-fast kapisinda DEGIL (Iyzico:CallbackUrl
    GURULTULU duser, bu SESSIZ duser) · deployment-checklist'te "Cookies" iceren onay kutusu 0.
  KACIS YOLU YOK: csrf govdeye konmuyor (bilincli karar).
  NOT: kodun KENDI yorumu bunu zaten soyluyor ("OLCULEN kisit - varsayim degil") - eksik olan
  YAPILANDIRMA KAPISI ve KONTROL LISTESI MADDESI.

BL-2  PROD SABLONU ve DAGITIM ARTEFAKTI YOK
  appsettings.Production.example.json YOK (yalniz appsettings.json 2.080 B + Development.example 9.408 B).
  Tek compose GELISTIRME icin: ASPNETCORE_ENVIRONMENT: Development · sa parolasi ·
    "yalniz yerel gelistirme icindir, gercek secret DEGILDIR" diye ISARETLI JWT anahtari.
  Prod compose / systemd unit / deploy betigi YOK. ops/ altinda iki betik: rotate-secrets.sh, set-api-origin.sh.
  ALTI config bolumu YALNIZ Development.example'da: Api · BackgroundJobs · Cookies · ForwardedHeaders ·
    GuestCheckout · RateLimit — checklist bunlardan BIRINI belgeliyor.
  Dockerfile URETIM SEKILLI (pinli digest · USER divisima non-root · EXPOSE 5000 · ortam zorlanmiyor).

BL-3  KRITIK OLAY ALARM KAPSAMI DISINDA  (belge kusuru, kod dogru)
  SECURITY.md:62 ve ops/serilog-siem.md:54-56 ONIKI tip sayiyor; GF-6'nin ekledigi
  PaymentAfterTerminal (Critical) ve ProductImportRejected LISTEDE YOK.
  serilog-siem.md alarm tablosu bu listeden kuruluyor -> gunluk elle kontrol icin onerilen
  en kritik olay alarm kapsami disinda BELGELENMIS durumda. GF-6'nin KENDI artigidir.

(2) OMER IRL ADIM LISTESI (sirali · her adim tek satir + KANIT NASIL ALINIR)

 1 Alan adi + DNS: divisima.com, www, api.divisima.com A/AAAA kaydi   | kanit: dig +short her uc ad
 2 TLS sertifikasi: /etc/ssl/divisima/fullchain.pem + privkey.pem     | kanit: openssl x509 -noout -dates
 3 SQL Server kur; veritabanini COLLATE Turkish_CI_AS ile YARAT       | kanit: DB ICINDEN DATABASEPROPERTYEX(DB_NAME(),'Collation')
 4 Recovery model FULL + AUTO_CLOSE OFF                               | kanit: sys.databases recovery_model_desc, is_auto_close_on=0
 5 Uygulama icin en az yetkili DB kullanicisi (ops/db/least-privilege.sql) | kanit: betigi kos, cikti 0 hata
 6 TokenOptions:SecurityKey uret (>=32 bayt)                          | kanit: acilista fail-fast SESSIZ gecerse dogru
 7 Encryption:Key uret: openssl rand -base64 32 (TAM 32 bayt)         | kanit: acilista "TAM 32 bayt" hatasi GELMEZSE dogru
 8 ConnectionStrings:DivisimaDb doldur                                | kanit: /health/ready 200
 9 **Cookies:Domain = ".divisima.com" YAZ (BL-1)**                    | kanit: giris sonrasi tarayici konsolu: document.cookie icinde csrf_token GORUNMELI
10 ForwardedHeaders:KnownProxies = LB/nginx IP'leri                   | kanit: security_events RateLimitExceeded detayinda ip alani GERCEK istemci IP'si
11 MailSettings:* gercek SMTP (Host bos ise ACILIS DUSER)             | kanit: sifre sifirlama maili GELMELI (admin kurtarma yolu BUNA BAGLI)
12 Iyzico canli: ApiKey · SecretKey · BaseUrl · UseRealSdk=true       | kanit: sandbox degil canli BaseUrl; test odemesi 3D akisini tamamlamali
13 **Iyzico:CallbackUrl = https://api.divisima.com/... (fail-fast ZORUNLU)** | kanit: bos/HTTP ise uygulama ACILMAZ - acilmasi kanittir
14 CallbackUrl origin'i storefront CSP form-action listesiyle AYNI mi | kanit: tarayici konsolunda CSP ihlali OLMAMALI (E2b'de yasandi)
15 Storefront:BaseUrl = https://divisima.com                          | kanit: odeme sonucu #/odeme/sonuc adresine YONLENDIRMELI
16 ops/set-api-origin.sh ile vitrin API origin'ini yaz                | kanit: betigin --verify modu EXIT 0
17 Redis kur + Redis:Enabled=true, Connection                         | kanit: Redis erisilemezse uygulama ACILMAZ (D5) - acilmasi kanittir
18 BackgroundJobs:Enabled=true (rezervasyon suresi dolanlar temizlensin) | kanit: Hangfire recurring job listesi; stok available KALICI dusuk KALMAMALI
19 AdminSeed ile ILK admini ac, sonra Enabled=false'a CEK             | kanit: admin girisi 200; ikinci acilista yeni admin OLUSMAMALI
20 GUNLUK: security_events'te PaymentAfterTerminal/Critical SORGULA   | kanit: SELECT * FROM security_events WHERE event_type='PaymentAfterTerminal' AND severity='Critical'
   (UYARI: bu olayin OKUYUCUSU YOK - controller'da security_events gecisi 0, SignalR admins BOS)

(3) BILINEN / KABUL (launch SONRASI · GF-7 kuyrugu)

 K1 CAPTCHA TAMAMEN OLU (T3-1 dogrulandi): ICaptchaValidator uc yerde gecer (arayuz · sinif ·
    Program.cs:326 DI); ValidateAsync URETIMDE 0 CAGRI. Captcha:Enabled=true NO-OP.
    AGIRLASTIRICI: Captcha:SecretKey fail-fast'in yedi zorunlu anahtarindan biri — hicbir sey
    yapmayan ozellik icin secret girmeden uygulama ACILMIYOR.
 K2 KEY VAULT OLU: Program.cs:309 KOSULSUZ ConfigurationSecretProvider kaydediyor;
    AzureKeyVaultSecretProvider HIC KAYITLI DEGIL; ISecretProvider TUKETICISI 0.
    "Kod dokunulmadan kasaya gecis" (SECURITY.md:53) YANLIS. secret-rotation.yml 90 gunluk
    cron'la uygulamanin OKUMADIGI bir kasaya yaziyor — KARAR GEREKIR.
 K3 SA-2 ANAHTAR ROTASYONU: tek anahtar; Decrypt cozemedigi degeri SESSIZCE ciphertext olarak
    donduruyor; re-encryption job .cs'te 0. Yaricap 1 kolon (two_factor_secret).
    SECURITY.md:106 bunu DOGRU anlatiyor; kusur uyarinin betige ve checklist'e TASINMAMASI.
 K4 T4-F2 (kayip guncelleme) — GF-7 ILK KALEM, gerekceli LAUNCH BLOKER istisnasi (55·GF-6 §5.1).
 K5 IDOR olay kapsami UC BELGEDE YANLIS: SECURITY.md:62 · serilog-siem.md · ve URETIM KAYNAGINDA
    ISecurityEventService.cs:14 "Order+Payment" diyor; gercek IyzicoPaymentManager->order,
    OrderManager->address. CLAUDE.md B8 bunu AV-3'te duzeltmis, ucune de ulasmamis.
 K6 runbook "12 migration" (satir 111 ve 161) — GERCEK 15 (ModelSnapshot HARIC; DB'deki
    __EFMigrationsHistory 15 satir ile BIREBIR).
 K7 nginx api.divisima.com blogu ortak baslik dosyasini INCLUDE ETMIYOR; fark Content-Security-Policy.
 K8 Hangfire dashboard PRATIKTE ACILAMAZ: filtre user_type=="1" istiyor ama JWT localStorage'da,
    cerezde DEGIL -> tarayici gezintisinde httpContext.User KIMLIKSIZ. Guvenli taraf (deny).
 K9 Serilog logs/ icin compose'da VOLUME YOK (yalniz uploads_data) -> konteyner yenilenince loglar GIDER.
 K10 OTLP kodu ve alert kurallari VAR ama checklist'te otlp|prometheus|grafana 0 gecis.

(4) DENETIM

KAPSAM ELESTIRMENI (zorunlu rol, CLAUDE.md B6): 37.278 B rapor · F1..F10 fark · pwd+HEAD beyan edildi.
  DOGRULANANLAR (fark YOK): sema 56 FK / 45 tablo · Migrate() yok · set-api-origin.sh --verify EXIT 0 ·
  nginx 3 server block · BackgroundJobs varsayilani true · pentest-checklist'in bes [x] CI iddiasi 5/5 dogru.
  Kendi hatasi 2 (onlenen yanlis bulgu, ikisi de olcumle curutuldu).
DENETCI: 55.291 B rapor · 16 ONAY · 1 AGIR ITIRAZ (I-1) · 1 OLCEMEDIM · 3 YENI BULGU · 1 KURAL IHLALI.
  ITIRAZ KABUL EDILDI: olay tipi 12 DEGIL 14 (ternary ilk-argumani kacirildi - MK-7 ailesi;
  yanlis sayi SECURITY.md'nin 12'siyle cakisip UC KANALLI SAHTE MUTABAKAT uretti).
  YENI BULGULAR KABUL: B-1 (BL-3) · B-2 (K5'in ucuncu kopyasi URETIM KAYNAGINDA) · B-3 (K7).
  KURAL IHLALI: kapsam elestirmeni %TEMP% kokune uc dosya birakmis (ke_base/ke_dev/ke_fk_uniq) —
  icerik olculdu, jeton 0; ANA AKIS TARAFINDAN SILINDI. Aile en az DORDUNCU vakasinda.
  SAPMA (kayit): EF probu ANA AGACTA derledi (MK-4b) — izlenen dosyaya etki 0 (git status 0).
DEFTER ZAYIFLIGI (denetci tespiti, kabul): 17 kalem ama [PLAN] satiri 1, HAM: atifi 1, SHA 0 —
  SDP 1.5 defter butunluk botu bu defter uzerinde KOSTURULAMAZ.

OLCEMEDIKLERIM (DURUST SINIR)
  Docker YOK, nginx YOK bu makinede -> docker compose config · imaj build · nginx -t OLCULEMEDI.
  Kapsam elestirmeninin bulgularinin TAMAMI L1 kaynak okumasi; canli kanit YOK.
  F3 zinciri kaynaktan dort halkada kapatildi ama UCTAN UCA CANLI kosum yapilmadi.

TEMIZLIK VE ZEMIN
  agac 0 · git diff b711c5e..HEAD BOS · worktree 1 · LaunchProbeDb dusuruldu ·
  kurgu kayit URETILMEDI (MK-3 uclusu 35/9/210/3837; yedi MAX AV-3 tabaniyla AYNI) ·
  %TEMP% denetci artigi 0 · sir 0.

MUHUR 56 YAZILMADI — tarif "KABUL sonrasi" diyor. GF-7 BASLATILMADI.
```

---

## 2. LAUNCH-FIX-1 (LF-1) — DAGITIM ARTEFAKTLARI

**Zemin `b711c5e` -> kapanis `6a4a4b3`, dort commit (amend YOK):**

```
2776e2b  LF-1: dagitim artefaktlari (K1-K5) + sozlesme pinleri
ae11041  LF-1 pin duzeltmesi: captcha YOKLUK iddiasina BILINEN-POZITIF negatif kontrolu
1910794  LF-1 pin duzeltmesi: SIEM alarm capasi UST DIZGEYE kordu (MUT-15)
6a4a4b3  LF-1: GF-3/K5 pini LF-1 kararlarina hizalandi (BOZDUKLARIM kaydi)
```

**Kapsam (13 dosya · +1059 / -32):** yalniz dagitim artefaktlari, belgeler ve pinler.
**DOKUNULMAZLARA DOKUNULMADI:** is mantigi · frontend · `Seller*` · migration · nginx
(K7 -> GF-7) · captcha ve Key Vault URETIM KODU.

### 2.1 K1 (BL-1) — `Cookies:Domain` uretimde fail-fast

`Program.cs`in uretim kapisina (`!IsDevelopment()` dali) ADANMIS bir kontrol eklendi:
deger bos ya da yalniz bosluksa uygulama **ACILMAZ**; mesaj hem SEBEBI hem BICIMI verir
(ust alan adi ornegi). Anahtar ayrica yer-tutucu/deny-list dongusune girdi (yedi anahtar).

**KIRMIZI-ONCE (olculdu, tahmin degil):** LF-1 oncesi Production + bos deger ile host
SORUNSUZ ACILIYORDU — `appsettings.json`da "Cookies" bolumu **hic yok** (grep 0 satir).

**NEDEN ACILIS KAPISI:** ariza SESSIZ. `/health` 200 doner, belirti dagitimdan **15 dakika
sonra** ve TUM kullanicilarda AYNI ANDA cikar. `Iyzico:CallbackUrl` gurultulu duser; bu
dusmez — kapinin kendisi bu sessizligin telafisi.

**Pinler:** `ConfigFailFastTests.Uretimde_CookiesDomain_BOSSA_UYGULAMA_ACILMAZ` (Theory: bos
dize ve yalniz-bosluk) · `Developmentta_CookiesDomain_BOS_ise_UYGULAMA_ACILIR` (VAKUM KIRICI:
kapinin YALNIZ uretim bacaginda kostugu ayrica olculur) · POZ ayagi
`Uretimde_GECERLI_HTTPS_CallbackUrl_ile_UYGULAMA_ACILIR` (tam yapilandirmayla host
GERCEKTEN aciliyor).

### 2.2 K2 — `Captcha:SecretKey` fail-fast'ten CIKARILDI

**Olculen gerekce (T3-1 yeniden dogrulandi):** `ValidateAsync` uretim kodunda **0** yerden
cagriliyor; `Captcha:Enabled` bir NO-OP. Olmayan bir ozellik icin gercek secret dayatmak
dagitimi **hicbir sey korumadan** bloke ediyordu.

`SECURITY.md`de **dort** yanlis satir durustlestirildi: bot korumasi satiri
("register/forgot/riskli login" diyordu — captcha adimi YOK) · uretim kontrol listesindeki
`Captcha:Enabled=true` maddesi · entegrasyon tablosunun Captcha satiri · ayni tablonun
Secrets satiri (K5). **KOD DOKUNULMADI** — kader karari (baglamak ya da silmek) GF-7'de.

### 2.3 K3 (BL-2) — uretim sablonu + uretim compose'u

`Divisima.API/appsettings.Production.example.json` (YENI, 172 satir): Development.example'in
**tum** bolumleri + uretime ozgu olanlar; her bolumun tek satirlik "nereden alinir"
aciklamasi; degerler `CHANGE_ME` ya da guvenli varsayilan.

`docker-compose.prod.yml` (YENI, 109 satir): `ASPNETCORE_ENVIRONMENT: Production` ·
secret'lar ortamdan ve **eksikse-patlat** bicimiyle (`:?` sonek) · `sa` parolasi ve DB
servisi **BILINCLI OLARAK YOK** (yonetilen SQL Server, en az yetkili kullanici) ·
`logs_data` + `uploads_data` VOLUME (K9) · Redis + healthcheck · API yalniz
`127.0.0.1:5000`e baglanir (dis yuze nginx bakar).

**GITLEAKS TUZAGI (GF-3 dersi):** yer-tutucular kisa ve dusuk entropili secildi. `CHANGE_ME`
**dokuz** karakterdir; `generic-api-key` kurali en az **on** karakterlik deger arar ve onu
ESLEYEMEZ. Bu olcut bir pinle sabitlendi (bolum 2.6).

### 2.4 K4 (BL-3) — olay tipleri, alarm tablosu, kapsam, sayilar

- Guvenlik olay tipi sayisi **12 -> 14** (`SECURITY.md` · `ops/serilog-siem.md`). Onceki 12,
  basit bir cagri capasinin bir **ternary**'nin ilk argumanini gorememesinden geliyordu
  (`AccountLocked` · `ChangePasswordFailed`) — MK-7 ailesi.
- SIEM alarm tablosuna iki satir: **`PaymentAfterTerminal`** (severity `Critical`, herhangi
  bir olusumda ELLE IADE) ve **`ProductImportRejected`** (3/gun).
- `IdorAttempt` kapsami UC yerde birden duzeltildi (`SECURITY.md` · `serilog-siem.md` ·
  `ISecurityEventService.cs` yorumu): kapsam MANAGER adiyla degil **YAZILAN KAYNAK** adiyla
  tanimlanir — `IyzicoPaymentManager` kaynagi `order`, `OrderManager` kaynagi `address`.
- `ops/backup-dr-runbook.md`: migration sayisi **15** (ureten ifadeyle, iki yerde).
- `ops/deployment-checklist.md`: **20 sirali IRL adimi**, her satirda KANIT sutunu; ayrica
  `Cookies:Domain` onay kutusu ve **GUNLUK `PaymentAfterTerminal` sorgusu** maddesi.

### 2.5 K5 — zamanlanmis rotasyon DEVRE DISI

`.github/workflows/secret-rotation.yml`: zamanlama KALDIRILDI, yalniz elle tetikleme kaldi.
**Olculen gerekce:** uygulamada KASA OKUYUCUSU YOK — `Program.cs` kosulsuz
`ConfigurationSecretProvider` kaydeder, `AzureKeyVaultSecretProvider` **hicbir yerde kayitli
degil**, `ISecretProvider` tuketicisi **0**. Kasada donen anahtar uygulamaya ULASMAZ; is
akisi guvenlik saglamiyor ama "anahtarlarim donuyor" YANILGISI uretiyordu. `SECURITY.md`in
secret maddesi ayni turda durustlestirildi. **KOD DOKUNULMADI.**

### 2.6 PINLER ve MUTASYON KANITI (MK-6)

`LaunchFix1SozlesmeTests.cs` (YENI, 534 satir) + `ConfigFailFastTests.cs` eklemeleri.
**On yedi mutasyon kosuldu**; her birinde (a) yazildi mi (`git diff --numstat`), (b) build
EXIT KODU **ayri** kontrol, (c) beklenen ISIMLI kirmizi, (d) olcum yedeginden geri alma +
`git status --porcelain` = 0. `git checkout` / `git stash` **kullanilmadi**.

```
MUT-1   K1 kapisi etkisizlestirildi               -> 2 kirmizi (Cookies Theory'sinin iki hali)
MUT-2   liste kompozisyonu LF-1 ONCESINE dondu    -> 2 kirmizi (kompozisyon + captcha davranis pini)
MUT-3b  sablondan BackgroundJobs bolumu dusuruldu -> 1
MUT-4b  yer-tutucu uzun + yuksek entropili        -> 1
MUT-5   compose ortami Development yapildi        -> 1
MUT-6   compose'a sa parolasi geri kondu          -> 1
MUT-7   checklist 20. IRL adimi silindi           -> 1
MUT-8   runbook migration sayisi 15 -> 16         -> 1
MUT-9   secret-rotation.yml'e zamanlama geri kondu-> 1
MUT-10  kod ONBESINCI olay tipi uretti            -> 1
MUT-11  "URETILMIYOR" listesindeki ad koda girdi  -> 1
MUT-12  kasa saglayicisi DI'ya kaydedildi         -> 1
MUT-13  SECURITY.md'nin eski captcha maddesi      -> 1
MUT-14  bir bolumun aciklama satiri silindi       -> 1
MUT-15  alarm olayinin adi bozuldu (UST DIZGE)    -> 0  ** PIN KUSURU, asagida
MUT-15b alarm satirlari GERCEKTEN silindi         -> 1  (capa sikilastirildiktan sonra)
MUT-16  arayuz yorumu yanlis kapsama dondu        -> 1
MUT-17  uretim kapisi Development'ta da kosuyor   -> 1
```

**MUT-15 — PININ KENDISI KUSURLU CIKTI (kayit):** ciplak bir alt-dizge capasi, olayin adini
bir UST DIZGEYE ceviren mutasyonla **0 kirmizi** verdi — `Contain` ust dizgeyle de tatmin
olur. Yani pin, korudugu olayin adinin BOZULMASINA **kordu**. Capa belgenin HAM metninden
kopyalanip (MK-7) ALARM SATIRI bicimine sikilastirildi ve MUT-15b tam 1 kirmizi verdi.
Bu, "ankrajli mukerrer taramasi" ailesinin bu depodaki bir sonraki vakasidir.

### 2.7 DALGA ICI DENETIM

**1. KALEM KALEM.** K1..K5'in hepsi sevk edildi ve her biri en az bir ISIMLI kirmiziyla
pinli (yukaridaki tablo). Kaniti olmayan kalem YOK.

**2. YARIM KALAN.** Yok. Tarifin butun kalemleri kapandi. Tarif disinda kalan tek sey
`gitleaks`in YERELDE kosulamamasidir (bolum 2.8).

**3. YAN ETKI TARAMASI.** `hassasAnahtarlar` listesini degistirmek GF-3'un
`K5_YER_TUTUCU_TARAMASI...` pinini KIRDI — **tam suit dogrulamasinda yakalandi, tahminle
degil**. Pin LF-1 kararlarina hizalandi (madde 6). `TestHostConfig.UretimAsgariAyarlari`ya
`Cookies:Domain` eklenmeseydi TUM uretim-bacagi pinleri kirmizi olurdu; deger TEK KAYNAKTAN
veriliyor. Belge tuketicileri (`SECURITY.md` · `serilog-siem.md` · `ISecurityEventService.cs`)
uc kanalda birden tarandi ve olay tipi kumesi artik KODDAN TURETILIYOR.

**4. KENDI HATALARIM (dort).**
- **(a) BAYAT IKILI — UCUNCU VAKA.** MUT-2'nin geri alinmasindan sonra **build yapmadan**
  MUT-3/MUT-4 kosuldu; `--no-build` MUT-2'nin ikilisini calistirdi ve iki mutasyonun da
  ciktisinda ACIKLANAMAYAN ikinci bir kirmizi cikti. **Aritmetikle yakalandi** (mutasyonun
  aciklayabilecegi kirmizi 1 iken 2 gorundu), tesadufle degil. Ikisi de TAZE ikiliyle
  yeniden kosuldu (MUT-3b/MUT-4b) ve tam 1 kirmizi verdi.
- **(b) YASAK-BICIM ASSERT'I KENDI DUZELTMESIYLE TETIKLENDI.** Eski yanlis ifadeyi yasaklayan
  bir `NotContain` yazildi ve 1 kirmizi verdi: uc dosya da o ifadeyi tasiyor — IDDIA olarak
  degil, **"YANLISTI" diyen kendi duzeltme cumlesi icinde**. Yasak bicim yerine POZITIF
  bicim pinlendi (kapsam kaynak adiyla yazilmis olmali).
- **(c) MUT-15: pinin capasi ust dizgeye kordu** (bolum 2.6).
- **(d) YOKLUK IDDIASI NEGATIF KONTROLSUZ YAZILDI.** "Captcha cagri yeri 0" pini once
  kontrolsuz kondu; "0 sonuc" ile "tarayici bos calisti" AYNI ciktiyi verir. Ayni tarayici
  artik once GERCEKTEN cagrilan bir metotla BILINEN-POZITIF sinamadan geciyor (`ae11041`).

**5. PIN DURUSTLUGU.** LF-1 pinlerinin cogu **KAYNAK-SOZLESME** pinidir ve bunu saklamiyor:
kalemlerin buyuk kismi uygulama DISINDA yasiyor (sablon, compose, is akisi, dort belge) ve
"davranisi" ancak gercek bir dagitimda gozlenir. **DAVRANIS ayagi olan tek kalem K1'dir** ve
o ayak `ConfigFailFastTests`te gercek host acilisiyla olculur (Production'da ACILMIYOR,
Development'ta ACILIYOR). Olay tipi pini arada durur: listeyi ezberlemez, **uretim
kaynagindan turetir** — kod bir tip eklerse belgeler guncellenene kadar KIRMIZI kalir.

**6. BOZDUKLARIM (tek kalem).**
`GuvenlikFix3SizintiSozlesmeTests.K5_YER_TUTUCU_TARAMASI_TEK_DONGUDE_TUM_HASSAS_ANAHTARLARA_Uygulanir`
— `Captcha:SecretKey` ariyordu. **KORUNAN SEY AYNI KALDI:** "yer-tutucu taramasi TEK dongude,
listelenen HER anahtara TAM 1 kez uygulanir" + yedi uye. **DEGISEN: kumenin kompozisyonu**
(`Captcha:SecretKey` cikti, `Cookies:Domain` girdi) ve ikisi de merkez karari. Kompozisyonun
kendisi ayrica pinlendi ve MUT-2 ile sinandi.

### 2.8 DOGRULAMA ve DURUST SINIRLAR

**UC ARDISIK TAM DOGRULAMA (birebir ayni, `6a4a4b3` uzerinde):**
```
Release build            EXIT 0 · 0 Hata              (uc kosumda da)
--filter "Category=Sql"  415 / 415 / 0 atlanan        (uc kosumda da)
tam suit                 826 basarili / 829 toplam    (uc kosumda da, kirilan UC AD AYNI)
```
Uc kirmizi: `OrderEndpointTests.PlaceOrder_{ValidCart...,InsufficientStock...,ConcurrentRequests...}`.
**AYIRT EDICI DENEY:** yalniz bu uc test kosuldugunda da uc kirmizi veriyor ve hata metni
`DotNet.Testcontainers.Builders.DockerUnavailableException` — sebep LF-1 DEGIL, bu makinede
**Docker YOK**. AV-3 tabaniyla ayni bilinen ucludur.

**Bicim kapilari (MK-9):** her checkpoint commit'inden ONCE whitespace ve style kapilari
kosuldu -> **ikisi de EXIT 0**.

**GITLEAKS YERELDE KOSULAMADI — OLCEMEDIM (durust sinir).** `gitleaks` bu makinede kurulu
degil (`command -v gitleaks` -> bos) ve Docker da yok, yani konteynerle de kosturulamadi.
Tarifin "Gitleaks yerelde sablonlara karsi" sarti **KARSILANAMADI**. Yerine konan (ve
YETERSIZ oldugu bilinen) kanit:
- `appsettings.Production.example.json`: `generic-api-key` olcutunu (deger uzunlugu >= 10 VE
  Shannon entropisi >= 3.5) tetikleyen aday **0**.
- `docker-compose.prod.yml`: iki aday var, ikisi de kural disi — bir URL yolu
  (`5000/health/ready`) ve Redis imajinin PUBLIC digest'i (`ff02b58f…`; `docker-compose.yml`de
  ZATEN var ve bugune kadar `secret-scan`i kirmadi).
- Ayni olcut bir PINE gomuldu
  (`UretimSablonundaki_GIZLI_BENZERI_DEGERLER_GITLEAKS_ESIGININ_ALTINDA`) ve MUT-4b ile
  sinandi — ileride "daha gercekci gorunen ornek deger" yazma girisimi CI'dan ONCE, YERELDE
  kirmizi verir.

**Kesin kanit push sonrasi `secret-scan` ADIM SONUCUDUR** — annotation'dan DEGIL (surec
skill'i: gitleaks bulgusunu `warning` seviyesinde basar, job YESIL gorunur).

---

## 2A. F-TURU — DENETCI BULGULARININ KAPATILMASI

Denetim PUSH'TAN ONCE kosuldu ve **yedi bulgu** cikardi; biri **AKTIF**ti. CLAUDE.md
"Denetim bulgu cikarirsa: duzeltme karari KULLANICININ, PUSH BEKLER" der - push BEKLETILDI,
merkez F-turunu verdi, bulgular kapatildi. Kapanis commit'i `b382065`.

### B-1 (tek AKTIF) — checklist, K2/K5'in KALDIRDIGI isi EMREDIYORDU

LF-1 `ops/deployment-checklist.md`e **kirk iki satir ekledi** ama BAYRAK TABLOSUNA ve
SECRET BASLIGINA **dokunmadi**. Sonuc: ayni depoda `SECURITY.md` "captcha bayragi etkisiz,
acmak sahte guvence uretir" derken checklist operatore
`Captcha:Enabled -> **true** (gercek Turnstile secret)`, `Vault:Enabled -> **true**` ve
`## Secret'lar (Key Vault'a)` diye **emrediyordu**. Operatorun eline aldigi belge
CHECKLIST'tir; yani K2 ve K5 **fiilen yarim kalmisti** ve hicbir pin bunu goremiyordu
(captcha pini yalniz `SECURITY.md`ye bakiyordu).

Iki bayrak "PRODUCTION'DA ACILACAK" tablosundan **cikarildi** ve
**"URETIMDE ACILMAZ — OLU OZELLIKLER"** basligina tasindi (olculen gerekceleriyle). Secret
basligi `## Secret'lar (env/compose - appsettings'te ASLA)` oldu, altina "kasaya YAZMAYIN,
okuyucu yok" notu kondu ve listeye `Cookies--Domain` girdi. Tabloya ayrica
`BackgroundJobs:Enabled` satiri eklendi (AV-3'un rezervasyon birikmesi).

### B-2 / B-3 — iki KOR capa (denetci mutasyonla gosterdi)

- **B-2:** `Contain("PaymentAfterTerminal")` bir UST DIZGEYLE tatmin oluyordu. Denetcinin
  MUT-B'si (adi `PaymentAfterTerminalXYZ` yapmak) **0 kirmizi** verdi. Bu, ana akisin
  MUT-15'te SIEM tarafinda kapattigi sinifin **checklist tarafinda hayatta kalan** ornegiydi.
  Onemi: 20. adimin SQL'indeki olay adi bozulursa pin YESIL kalir ve operatorun gunluk
  sorgusu SESSIZCE 0 satir doner — o sorgu, bu olayin **TEK OKUYUCUSUDUR**.
- **B-3:** `Contain("YANLIŞ")` korudugu seye OZGU DEGILDI; `SECURITY.md`de bu kelimenin IKI
  gecisi var ve IdorAttempt duzeltmesi SILINSE BILE K5'in kasa maddesindeki gecis asserti
  doyuruyordu. Denetcinin MUT-C'si **0 kirmizi** verdi.

Ikisi de **sinir karakterli / ifadeye bitisik** capaya cevrildi. **AYNI IKI MUTASYON
YENIDEN KOSULDU ve artik TAM 1 ISIMLI KIRMIZI veriyor** (FM-2 · FM-3) — kapanisin kaniti
budur, "duzelttim" beyani degil.

### B-4 — uretim compose'u kendi checklist'ine kol vermiyordu

`docker-compose.prod.yml`de `AllowedOrigins__* · ForwardedHeaders__KnownProxies__* ·
AdminSeed__* · Webhook__AllowedIps__*` gecisi **0/0/0/0** idi. Kritik olan CORS: imaja
gomulu `appsettings.json` listesi `http://localhost:5173` **tasiyor** ve bu bir DIZI
oldugu icin **yalnizca INDEKSLI** ortam degiskeniyle (`AllowedOrigins__0`) ezilir — duz bir
`AllowedOrigins` degiskeni **hicbir sey yapmaz**, yani operator verdigini SANIP vermeyebilir.

Dort anahtar grubu compose'a eklendi **ve** `Program.cs`e K1 kalibinda kucuk bir kapi kondu:
**uretimde liste yerel bir origin tasiyorsa uygulama ACILMAZ**, mesaj indeksli bicimi
ogretir. Gerekce ayni sinif: ariza SESSIZ — yanlis CORS hicbir log satiri uretmez, yalnizca
saldiri aninda "calisir" (yerelde kosan bir sayfa, kurbanin oturumuyla API'ye gidip yaniti
OKUR). Pin POZ/NEG: `localhost` ve `127.0.0.1` ile ACILMAZ, gercek origin'le ACILIR.

### B-5 / B-6 / B-7

- **B-5:** `ConfigurationSecretProvider.cs` yorumu hala "Production'da
  `AzureKeyVaultSecretProvider` ile degistirilir - arayuz ayni, kod dokunulmaz" diyordu —
  K5'in `SECURITY.md`de YANLIS diye isaretledigi iddianin **uretim kodundaki ikinci
  kopyasi** ("AYNI KURALIN IKINCI KOPYASI" ailesi). Yorum gercege dondu ve **pinlendi**.
- **B-6:** `NotContain("MSSQL_SA_PASSWORD")` BEDAVA DOGRUYDU (onceki assert onu mantiken
  kapsiyor) — **silindi**, yerine gerekce yorumu kondu.
- **B-7:** iki tarayicinin kapsami dardi. Olay tipi cikarimi artik `Divisima.Dal` ve
  `Divisima.Entity`yi, captcha cagri taramasi `Divisima.Core`u da kapsiyor. **Kapsamin
  kendisi de sinandi**: `TarayiciKapsami_BILINEN_POZITIFLE_SINANDI` her taranan dizinde
  bilinen bir cagriyi arar (bir dizin listeye yazilip da fiilen taranmazsa sonuc yine
  "0 bulundu" olurdu — **ayni korluk, bu kez GORUNMEZ bicimde**), ayri bir test de uydurma
  capayi bulmadigini gosterir.

### F-TURU MUTASYONLARI (yalniz DEGISEN pinler)

```
FM-1  checklist'e eski "Turnstile secret" emri geri kondu     -> 1 (DagitimListesi_OLU_OZELLIKLERI_...)
FM-2  denetcinin MUT-B'si: olay adi UST DIZGE yapildi         -> 1 (ONCE 0 IDI)
FM-3  denetcinin MUT-C'si: IdorAttempt duzeltmesi silindi     -> 1 (ONCE 0 IDI)
FM-4  AllowedOrigins kapisi etkisizlestirildi                 -> 2 (Theory'nin iki hali)
FM-5  Divisima.Core'a GERCEK bir ValidateAsync CAGRISI eklendi-> 1 (B-7 oncesi GORUNMEZDI)
FM-6  ConfigurationSecretProvider yorumu eski yanlisa dondu   -> 1
```
Her birinde (a) `git diff --numstat`, (b) build EXIT KODU **ayri** kontrol, (c) isimli
kirmizi, (d) olcum yedeginden geri alma + `git status --porcelain` = 0.
**FM-2 ve FM-3, denetcinin BIREBIR ayni mutasyonlaridir** — once 0, simdi 1.

### F-TURU DOGRULAMASI (uc ardisik, birebir)
```
Release build            EXIT 0 · 0 Hata              (uc kosumda da)
--filter "Category=Sql"  415 / 415 / 0 atlanan        (uc kosumda da)
tam suit                 834 basarili / 837 toplam    (uc kosumda da, kirilan UC AD AYNI)
```
Uc kirmizi yine bilinen Docker uclusu (`DockerUnavailableException`, log'da 6 gecis).
Bicim kapilari commit'ten ONCE: whitespace EXIT 0 · style EXIT 0.

---

## 3. GO KARARI

**KARAR: GO — dagitim Omer'de.**

**OLCUT (`51·AV-2`, `53·AV-3`/(B) ile daraltilmis):** bir kalem launch bloker sayilir ancak
`KRITIK` **∨** (`YUKSEK` + `KIMLIKSIZ-UZAK`) **∨** `[PARA]`/`[VERI-BOZAN]` ise **ve**
DAVRANIS KANITI varsa.

**Bu olcute gore bugun ACIK launch bloker YOKTUR:**

| Kaynak | Kalem | Durum |
|---|---|---|
| `52·GF-5` | SD-7 misafir butunlugu · SC-1 A09 iz/atif | KAPANDI (GF-5) |
| `53·AV-3` | T1-B1 uye replay · T1-B2 adressiz siparis · T1-B4 COD parasiz "odenmis" | KAPANDI (GF-6) |
| `55·GF-6` | S-1 iptal edilmis siparisi dirilten callback · T4-F1 cift iade `[PARA]` | KAPANDI (GF-6/F2, F3) |
| LAUNCH OLCUMU | BL-1 `Cookies:Domain` `[OTURUM]` | **KAPANDI (LF-1/K1)** |
| LAUNCH OLCUMU | BL-2 prod sablonu/artefakti yok | **KAPANDI (LF-1/K3 + F-turu/B-4)** |
| LAUNCH OLCUMU | BL-3 kritik olay alarm kapsami disinda | **KAPANDI (LF-1/K4 + F-turu/B-2)** |

**GEREKCELI ISTISNA — T4-F2 (kayip guncelleme, `[VERI-BOZAN]`):** GF-7'nin ILK kalemidir ve
launch blokeri SAYILMAMASI `55·GF-6 §5.1`de gerekcelendirilmistir (migration ister; GF-6'da
kirmizi-once denendi). Karar DEGISMEDI, burada YENIDEN ANILIYOR ki GO karari onu BASTIRMASIN.

**DENETIM SONUCU:** tek denetci (L2, ayri kok, `6a4a4b3` beyanli) **yedi bulgu** cikardi,
**hicbiri launch bloker olcutunu karsilamiyordu**; yedisi de F-turunda kapatildi. Denetcinin
korlugu acan iki mutasyonu (MUT-B/MUT-C) F-turundan sonra **birebir yeniden kosuldu** ve
artik kirmizi veriyor.

**GO'NUN SARTLARI (bunlar Omer'in isidir, kodun degil):**
1. `ops/deployment-checklist.md`teki **20 sirali IRL adimi** sirasiyla ve KANIT sutunuyla
   yurutulur. 9. adim (`Cookies:Domain`) ve 18. adim (`BackgroundJobs:Enabled=true`)
   ATLANAMAZ — birincisi BL-1'in, ikincisi AV-3'un rezervasyon birikmesinin karsiligidir.
2. **CORS INDEKSLI verilir:** `AllowedOrigins__0` / `AllowedOrigins__1`. Duz bir
   `AllowedOrigins` degiskeni HICBIR SEY yapmaz; yerel origin kalirsa uygulama ACILMAZ.
3. **GUNLUK** `PaymentAfterTerminal` / `Critical` SQL sorgusu operasyon takvimine yazilir.
   Bu olayin SIEM okuyucusu YOKTUR ve SignalR alarmi BOS gruba gider; tek gercek okuyucu
   elle kosulan sorgudur.
4. **`Captcha:Enabled` ve `Vault` bayraklari ACILMAZ** — ikisi de olu ozelliktir; acmak
   yalnizca sahte guvence uretir (checklist artik bunu ADIYLA soyluyor).
5. Ilk admin `AdminSeed` ile acildiktan SONRA bayrak `false`a cekilir.
6. Push sonrasi CI'nin **cift yesili** beklenir; `secret-scan` sonucu ADIM DUZEYINDEN okunur.

---

## 4. BILINEN / KABUL EDILMIS RISK — K1..K10, DURUM SUTUNLU

**SDP 1.12.8:** liste DURUM sutunuyla kurulur; **KAPALI bir kalem YENI bir bulguyu
BASTIRMAZ.** Durumlar LAUNCH HAZIRLIK OLCUMU'nun (bolum 1) K1..K10'una gore guncellenmistir.

| # | Kalem | DURUM | LF-1 + F-turunda ne oldu / kalan |
|---|---|---|---|
| **K1** | Captcha tamamen olu (`ValidateAsync` uretimde 0 cagri) | **KISMEN KAPANDI** | Agirlastirici KALKTI: `Captcha:SecretKey` fail-fast'ten cikti. `SECURITY.md`in dort yaniltici satiri **ve** (F-turu/B-1) `deployment-checklist.md`in "true (gercek Turnstile secret)" emri duzeltildi — checklist artik "URETIMDE ACILMAZ" diyor. **KOD HALA OLU** — sil ya da bagla karari **GF-7**. |
| **K2** | Key Vault olu (`ISecretProvider` tuketicisi 0) | **KISMEN KAPANDI** | `secret-rotation.yml` zamanlamasi KALDIRILDI; `SECURITY.md`, checklist secret basligi (F-turu/B-1) **ve** `ConfigurationSecretProvider.cs` yorumu (F-turu/B-5) durustlestirildi. **OKUYUCU HALA YOK** — bagla ya da iskeleti sil karari **GF-7**. |
| **K3** | SA-2 anahtar rotasyonu (tek anahtar; `Decrypt` cozemedigini SESSIZCE geri veriyor) | **ACIK** | LF-1 kapsaminda DEGILDI. Uretim sablonunun `Encryption` bolumu bunu ACIKCA yaziyor. **GF-7** (SC-12 ile birlikte). |
| **K4** | T4-F2 kayip guncelleme `[VERI-BOZAN]` | **ACIK — GF-7 ILK KALEM** | Gerekceli launch bloker istisnasi (`55·GF-6 §5.1`). Migration ister. |
| **K5** | IDOR olay kapsami UC belgede yanlis ("Order+Payment") | **KAPANDI** | Ucu de duzeltildi ve pinli; F-turu/B-3'te capa **ifadeye bitisik** hale getirildi (once kordu). |
| **K6** | Runbook "12 migration" (gercek 15) | **KAPANDI** | Pin sayiyi DEPODAKI GERCEK dosya sayisiyla karsilastirir — runbook BAYATLAYAMAZ. |
| **K7** | nginx `api.divisima.com` blogu ortak baslik dosyasini INCLUDE etmiyor (fark: CSP) | **ACIK** | nginx bu dalgada **DOKUNULMAZ** ilan edildi. **GF-7**. |
| **K8** | Hangfire dashboard pratikte acilamaz (JWT localStorage'da, cerezde degil) | **ACIK** | Guvenli taraf (deny). **GF-7**. |
| **K9** | Serilog `logs/` icin compose'da VOLUME yok | **KAPANDI** | `docker-compose.prod.yml`de `logs_data` **ve** `uploads_data`. Eski gelistirme compose'u DEGISMEDI. |
| **K10** | OTLP/alert kurallari var ama checklist'te gecmiyor | **ACIK** | Olculdu: checklist'te `otlp\|prometheus\|grafana` gecisi **0** (F-turu sonrasi da). Izleme yigininin baglanmasi **GF-7**. |

**AYRICA ACIK KALAN (bu turda DEGISMEDI):** `frame-src` SUPHELISI · SignalR `"admins"`
grubu BOS · vitrin CSP `'unsafe-inline'` KABUL EDILMIS RISK · `00b:229` webhook IP
allowlist BAGLAYICI · `00b:197` (#14) ve `00b:313` (#20).

---

## 5. OLCEMEDIKLERIM (DURUST SINIR — UC KANAL)

Bunlar "yapilmadi" degil, **YAPILAMADI**; yerine ne konuldugu ve kesin kanitin NEREDEN
gelecegi ayri ayri yazilidir.

**1. GITLEAKS — yerelde KOSULAMADI.** `gitleaks` kurulu degil (`command -v` bos; denetci
ayrica `gitleaks version` -> exit 127 ve chocolatey/go yollarini da yokladi) ve **Docker da
yok**, yani konteynerle de kosturulamadi. POZ kontrol: ayni kabukta `command -v dotnet`
DOLU doner. Tarifin "Gitleaks yerelde sablonlara karsi" sarti **KARSILANAMADI**.
*Yerine konan (yetersizligi bilinen) kanit — IKI BAGIMSIZ KANAL, ayri ifadelerle:* ana akis
ve denetci `generic-api-key` olcutunu (deger uzunlugu >= 10 **∧** Shannon entropisi >= 3.5,
gizli-benzeri anahtar adinin yaninda, `[0-9a-zA-Z._=-]` karakter kumesi) elle uyguladi ve
**iki yeni dosyada da aday 0** buldu. Denetci olcutu KALIBRE ETTI: GF-3'u kiran deger
**4.415** (CLAUDE.md'nin kaydettigi 4.35-4.42 bandi) · gercek GUID **3.867** · `CHANGE_ME`
**9 karakter / 2.948** — yani kural onu ESLEYEMEZ. Olcut ayrica bir PINE gomuldu
(`UretimSablonundaki_GIZLI_BENZERI_DEGERLER_GITLEAKS_ESIGININ_ALTINDA`, MUT-4b ile sinandi).
**Varsayilan kural setinin diger kurallarini IKIMIZ DE olcemedik.**
**KESIN KANIT: push sonrasi `secret-scan` ADIM SONUCU** — annotation'dan DEGIL (gitleaks
bulgusunu `warning` seviyesinde basar, job YESIL gorunur; surec skill'i).

**2. DOCKER — bu makinede YOK.** `docker compose config` kosulamadi; `${VAR:?zorunlu}`
davranisi **YAML metninden okundu**, calistirilarak dogrulanmadi. Ayni sebeple imaj build'i
ve uc `OrderEndpointTests` kirmizisi de burada olculemiyor (ucu de
`DockerUnavailableException`, ayirt edici deneyle LF-1'den bagimsiz oldugu gosterildi).

**3. NGINX — bu makinede YOK.** `nginx -t` kosulamadi; K1 zincirinin nginx halkasi
**kaynaktan** dogrulandi (iki `server_name` blogu), CANLI 403 uretilmedi. Yeni artefaktlarin
nginx/TLS ile uctan uca uyumu **olculmedi**. K7 zaten GF-7'de.

**Ayrica:** gercek bir dagitim gozlenmedi — LF-1'in TUM kalemleri depo artefakti duzeyinde
dogrulandi; tek DAVRANIS ayaklari K1 (Cookies:Domain) ve F-turu/B-4 (AllowedOrigins) fail-fast
kapilaridir ve onlar gercek host acilisiyla olculur.

---

## 6. GF-7 KUYRUGU (launch SONRASI, sirali)

1. **T4-F2** kayip guncelleme (rowversion) — `[VERI-BOZAN]`, migration ister.
2. **Captcha:** sil ya da bagla (K1).
3. **Key Vault:** okuyucuyu bagla ya da iskeleti sil (K2).
4. **Kargo kaydi** (Shipping entegrasyonu).
5. **K7 nginx** ortak baslik include'u.
6. **K8 Hangfire** dashboard erisimi.
7. **SA-2** anahtar rotasyonu + **SC-12** outbox payload sifreleme (K3; SC-12 once SA-2'yi ister).
8. **K10 OTLP/alert** checklist'e ve izleme yigini.
9. **Raporlama siteleri** (`security_events` okuyucusu — SC-3).
10. **frontend `odeme/sonuc` i18n.**
11. AV-3'un 6b/6c/6d kalani · olu/yaniltici yuzey grubu · SB-1 · SD-1/SD-2/SD-4 ·
    VITRIN-KALAN (10 kalem) · FIX-1B · ADMIN-FIX · IMPORT-FIX · FIX-1C · LOG-FIX · FIX-2 ·
    FIX-3/B13.
