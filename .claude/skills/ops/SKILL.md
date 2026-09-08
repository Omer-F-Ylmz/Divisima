---
name: ops
description: Divisima canli sunucu operasyonu — dagitim sirasi, .env, imaj tazeligi, geri donus, soft-launch kapisi, acilis gunu listesi ve sahada BEDELI ODENMIS tuzaklar (KnownProxies ag gecidi, Redis primitif ayrismasi, healthcheck araci, SMTP login, Hangfire semasi, Express/TDE). Dagitim/deploy/ops/sunucu/nginx/yedek/rollback/canli kusur iceren HER tarifin basinda yuklenir — "sadece bir konteyner yeniden baslatacagim" turu isler dahil, cunku bu dosyadaki tuzaklarin hepsi SESSIZDIR: uygulama saglikli gorunurken yanlis calisir.
---

# ops — Divisima operasyon usulu

**Bu dosyada DEGER YOKTUR, yalniz USUL vardir.** Anahtar, parola, jeton, IP ve baglanti
dizgesi buraya YAZILMAZ (SDP v1.5 · 1.12.6-b: gecici dosya da dahil). Degerler sunucuda
uretilir ve sunucuda kalir; burada yalniz **anahtar ADLARI**, komut kaliplari ve olcum
kriterleri durur.

**Iliski:** `surec` push/olcum/rapor disiplinini, `sdp` denetim protokolunu verir; bu skill
onlari TEKRAR ETMEZ. Canli sunucuya dokunan is de bir dalgadir — commit/push karari
kullanicinindir, kanit adim sonucundan okunur.

**Zemin belgeler (tek dogruluk kaynagi ONLARDIR, bu dosya OZET degil USULdur):**
`ops/deployment-checklist.md` (20 sirali IRL adim + acilis gunu) · `ops/backup-dr-runbook.md`
(yedek/RPO/RTO/migration) · `docs/muhur/57-launch-deploy.md` · `docs/muhur/58-launch-fix-5.md`.
Bir sayi ya da esik gerekiyorsa **oradan okunur**, buradan hatirlanmaz.

## 0. Iki kalici ilke

1. **"Test yesil" ile "canlida calisir" AYNI SEY DEGILDIR.** Bu depoda en pahali kusurlarin
   tamami yerelde yesildi ve canlida kirildi. Sebep her seferinde ayni: test rig'i uretim
   bilesenini (Redis, Docker, nginx, uretim compose'u) **kullanmiyor** ve yerine gecen sey
   ayni arayuzu FARKLI temsille karsiliyor. Bu yuzden dagitim sonrasi kanit **zorunludur**,
   opsiyonel bir tur degildir.
2. **Sessiz ariza normaldir, gurultulu ariza istisnadir.** Asagidaki tuzaklarin hicbiri log'a
   hata basmaz: uygulama acilir, uclar 200 doner, konteyner ayakta gorunur. Belirti gunler
   sonra ve dolayli gelir. Bu yuzden her adimin **ayirt edici kaniti** vardir — "yaptim"
   yetmez, ayni komutun iki durumu ayirt ettigi gosterilir.

## 1. Erisim ve makine

- SSH **anahtarla**; parola girisi kapali (`PasswordAuthentication no`). sshd degisikliginden
  sonra **mevcut oturum kapatilmadan** yeni bir oturum acilir — kilitlenme kontrolu budur.
- `ufw`: **once `allow 22`, sonra `enable`.** Ters sira kilitlenmedir.
- Sunucu Ubuntu 26.04 (tarif 24.04 diyordu; sapma kayitli). Bir apt deposu eklerken suit adi
  **olculur**, dagitim adindan tahmin edilmez.
- `/etc/sysctl.conf` bu surumde **yoktur**; kalici sysctl `/etc/sysctl.d/*.conf` altina yazilir.
- Sir tasiyan dosyalar: `.env` **600 root:root** · yedek anahtari **600** · `.htpasswd` **640
  root:www-data**. Izinler dagitimdan sonra **olculur**.

## 2. Dagitim sirasi (bozulmaz)

```
sema (DDL yetkili hesap)  ->  Hangfire semasi (ayricalikli hesap)  ->  Redis ayakta
  ->  .env dogrulanir  ->  API imaji YENIDEN KURULUR  ->  /health/ready 200
  ->  set-api-origin.sh  ->  frontend kopyalanir  ->  nginx -t + reload  ->  kanit turu
```

Sira gerekceli, kolayligina degil:

- **Sema uygulamadan once**, cunku uygulama acilista migrate ETMEZ ve calisma zamani DB
  kullanicisinin DDL yetkisi YOKTUR. Migration ureten bir surum yayinlaniyorsa sema adimi
  **kod deploy'undan ONCE** kosar (expand-migrate-contract; runbook).
- **Hangfire semasi AYRI ve ZORUNLU adimdir.** Atlanirsa uygulama ilk acilista `SQL 208` ile
  **coker**. Surum `packages.lock.json`dan okunur, makinedeki klasor listesinden SECILMEZ.
- **Redis uygulamadan once**, cunku `Redis:Enabled=true` iken baglanilamazsa uygulama HIC
  ACILMAZ (sessizce bellege dusmez — dogru davranis, ama sonucu "Redis kesintisi = deploy
  blokaji"dir).
- **`set-api-origin.sh` frontend kopyalanmadan ONCE.** Ters sira vitrini localhost'a bakar
  birakir: katalog bos gelir, sebebi ekranda gorunmez.

## 3. `.env` ve imaj tazeligi

- `.env` **`source` ile dogrulanmaz.** Bosluk tasiyan degerler (`User Id=`, `Divisima <...>`)
  kabugu kirar. Dogru kanal `docker compose config` (exit 0 + cozulmemis yer tutucu **0**).
- **`--force-recreate` KOD DEGISIKLIGINI TASIMAZ.** Konteyneri yeniden yaratir, **imaji
  yeniden kurmaz**; C# degisikligi bayat imajla sessizce disarida kalir. Kod degistiyse
  **imaj yeniden kurulur**. (Bu deponun "bayat ikili" ailesi — bedeli uc kez odendi.)
  `--force-recreate` yalniz **yapilandirma** degisikligi (`.env`) icin yeterlidir.
- Dagitimdan ONCE **suruklenme (drift) olculur**: sunucudaki dosyalar ile depodakiler bayt
  duzeyinde karsilastirilir. `set-api-origin.sh` yazimi olan iki frontend dosyasi ayrisir ve
  bu **beklenendir** — dagitimdan sonra yeniden uretilir.

## 4. Sahada bedeli odenmis tuzaklar

Hepsi **sessizdir**. Her biri kendi ayirt edici olcumuyle kapanir.

| Tuzak | Belirti | Olcum / cozum |
|---|---|---|
| **`KnownProxies` ag gecidi** | hiz siniri tum site icin tek kovada, `security_events.ip_address` ag gecidi IP'si | API konteynerde + nginx konakta ise `127.0.0.1` **eslesmez** (paket kopruden girer). Deger `docker inspect ... .Gateway` ile **olculur**, tahmin edilmez; `.env`e yazilir. **Ag yeniden yaratilirsa deger degisir — adim TEKRARLANIR.** Ayirt edici kanit: iki farkli `X-Forwarded-For` ile limit tuketilir; ikincisi 429 DEGILSE bolumleme calisiyordur. |
| **Redis primitif ayrismasi** | ikinci istekten itibaren 500 (`WRONGTYPE`), ilk istek temiz | Bir cache anahtarini **yazan ile okuyan ayni primitifi** kullanmalidir (`StringIncrement` yazip `IDistributedCache` ile okumak hash<->string ayrismasi uretir). Test rig'i Redis'siz `MemoryCacheService`e duser ve ayrismayi **GOREMEZ**. Yeni bir `ICacheService` uyesi eklenirken yazan/okuyan cifti kaynaktan dogrulanir; ayirt edici deney **ARDISIK** istektir, tek istekli `curl` probu YETMEZ. |
| **Healthcheck araci imajda yok** | uygulama 200 doner, konteyner `unhealthy` | Healthcheck komutunun cagirdigi arac konteynerin **icinden** dogrulanir (`command -v`). Saglik kapisina bagli her otomasyon yanlis bilgi alir. CI bunu yakalayamaz — CI uretim compose'unu kosmuyor. |
| **SMTP login = gonderen adresi varsayimi** | mail hic gitmez ya da saglayici reddeder | `MailSettings__User` **login**dir, `MailSettings__From` **gonderen**dir. Compose varsayilani ikisini esitler; bu yalniz login==gonderen olan saglayicilarda dogrudur. Brevo gibi saglayicilar **kendi login'ini** verir — `DIVISIMA_SMTP_USER` `.env`de ACIKCA yazilir. Kanit: gercek bir mail teslim edilir (deger degil, **uzunluk/varlik** raporlanir). |
| **`MailSettings:Host` bos** | uygulama ACILMAZ (gurultulu — bu iyi taraf) | Admin kurtarma yolu buna baglidir: sifirlama jetonu DB'de **ozet** durur, ham deger okunamaz. Gercek SMTP olmadan admin kurtarilamaz. |
| **SQL Server Express** | `BACKUP ... WITH COMPRESSION` -> `Msg 1844`; TDE yok | Lisans karari kullanicinindir. "Yedekler sifreli" maddesi **yedek DOSYASI** duzeyinde karsilanir (`age`), sifresiz kopya diskte birakilmaz. **Bu TDE DEGILDIR** — `.mdf`/`.ldf` diskte sifresizdir. **ANAHTAR KAYBI = YEDEK KAYBI**: anahtarin sunucu DISINDA da kopyasi olmalidir. |
| **Recovery modeli SIMPLE** | RPO 15 dk hedefi **imkansiz** (`BACKUP LOG` -> `Msg 4208`) | `FULL` dogrulanir; `FULL`e gecisten **sonra** bir full yedek alinir — log zinciri ancak oyle baslar. |
| **SW `VERSION` bumpi** | eski onbellek servis edilir | Kod tasiyan dosyalar network-first oldugu icin bump atlanabilir; **cache-first varlik degistiyse (manifest, ikon, font) bump ZORUNLU.** Atlandiysa raporda YAZILIR. |
| **`BackgroundJobs:Enabled=false`** | uclar 200, siparis olusur, ama mail/fatura/sadakat **sessizce birikir** | `failed-jobs` listesi de **bos kalir** (mesajlar `Pending`, `Failed` degil). Kanit konfigurasyondan degil **sonuctan** alinir: gercek bir siparisten ~2 dk sonra `outbox_messages` satiri `Processed` olmali. |

**Yedek dogrulamasi ayirt edicidir:** sifreli dosya dogrudan `RESTORE VERIFYONLY`ye
verildiginde **hata vermeli** (icerik gercekten sifreli), cozulmus dosya **gecerli** demeli.
Yalniz ikincisine bakmak, adi degistirilmis sifresiz bir yedegi gecirir.

## 5. Geri donus (rollback)

- **Kod:** onceki surumun imajina donulur (blue-green / onceki tag). Migration iceren bir
  surumden geri donuluyorsa **sema geri alinmaz** — expand/contract tam da bunun icin vardir:
  eski kod yeni semayla calisabilmelidir. Kolon silen/yeniden adlandiran tek adimli bir
  migration bu yolu KAPATIR, o yuzden yazilmaz.
- **Veri:** point-in-time prosedur runbook'ta. Sira kritiktir: yedek **once yan bir isimle**
  geri yuklenip dogrulanir, veritabani **ancak ondan sonra** dusurulur. Kanitlanmamis bir
  yedege guvenerek uretimi dusurmek kurtarma degil kumardir.
- **Yapilandirma:** `.env` degisikligi geri alinip `--force-recreate` yeterlidir (kod
  degismedigi icin imaj yeniden kurulmaz).
- Her geri donusten sonra **ayni kanit turu** kosulur — geri donus de bir dagitimdir.

## 6. Soft-launch kapisi ve goz turu

Site canli olabilir ama **kapi arkasinda** olabilir (vitrin+admin Basic Auth, uc hostta
`noindex`, sitemap 404). Kapinin amaci gizlilik DEGIL, **erken ziyaretci ve indekslenme**
onlemektir; API bilincli olarak kilitsizdir (aksi halde saglayicinin sonuc POST'u ve
orkestrator problari 401 alirdi).

- **Muafiyet listesi vardir ve daraltilamaz:** tarayici `manifest.json`, service worker,
  `pwa-register.js`, `robots.txt`, ikonlar ve favicon'u **sayfadan ayri ve cogu zaman kimlik
  tasimadan** ister. Bunlar 401'lenirse PWA kurulumu ve SW kaydi **sessizce** bozulur.
  **`index.html` MUAF DEGILDIR** — muafiyet listesi genisletilirken bu ayrim korunur.
- **Kapi arkasinda tarayici olcumu yapilamaz.** Kapiyi kendi IP'n icin gecici delmek bir
  cozum degildir (denendi, izin reddedildi ve etrafindan DOLASILMADI). Tarayici kanitini
  kapiyi acan kisi ya da parolayi bilen kullanici alir; CC'nin kanadi `curl` ve sunucu
  logudur. Bu sinir raporda **adiyla** durur.
- Kapi kaldirma adimlari checklist'in **"ACILIS GUNU"** bolumundedir ve sirasi baglayicidir:
  **hukuki metinler ONCE, kapi SONRA** — ters sira, eksik metinlerle satis yapilabilen bir
  pencere birakir.

## 7. Acilis gunu ve gunluk operasyon

Tam liste checklist'tedir; burada yalniz **atlanmayacak** olanlar:

- **Hukuki metinler ON KOSUL** — alti slug, `is_active=1`, tohum metni ("temsilidir") **0**.
  Bu bir yazilim maddesi degil isletme yukumlulugudur; kod tarafi yalniz **kaydin var
  oldugunu** olcebilir, icerigin hukuki gecerliligini olcemez.
- **Test verisi temizligi** — soft-launch penceresinde canli DB'ye olcum amacli yazilan her
  satir (urun, hesap) kapi acilmadan silinir. "Defterde yaziyordu" bir dagitim adimi degildir.
- **`PaymentAfterTerminal` / `Critical` gunluk SQL sorgusu** — bu olayin **otomatik okuyucusu
  YOKTUR** (SignalR `"admins"` grubu bos, `security_events` icin okuma ucu yok). Sorgu
  kosulmazsa **elle iade gereken vaka gorulmez**. Operasyon takvimine yazilir.
- **Odeme yontemleri `.env` ile acilip kapatilamaz** — gecerli yontem kumesi **derleme zamani
  sabitidir**. "COD'u kapat" demek **misafir siparisini tumden kapatmak** demektir.
- **`AdminSeed:Enabled`** ilk giristen sonra `false`a cekilir ve parola secret'i rotate edilir.

## 8. Kanit yazma

Canli olcum de kanittir ve ayni maskeleme kurallarina tabidir: jeton/kimlik degeri raporda
**ilk 8 karaktere kirpilir**, maskeleme **uretim noktasinda** yapilir (rapor aninda degil).
Bir mailden okunan dogrulama kodu gibi degerler **kabuk degiskeninde** yasar; rapora yalniz
**uzunlugu** ya da **eslesip eslesmedigi** yazilir. Sunucudaki gecici bir dosyaya yazip
silmek de **istisna degildir** — bir kez silinmeden kalinca sahipsiz bir sir birakir.
