# FLAKE-FIX - ADI OLAN FLAKE KAPANDI (25 Agustos 2026)

Zemin: `677e9ee`. TEK KALEM: `BackgroundJobs:Enabled=false` iken Hangfire DEPOLAMA
yapilandirmasinin da atlanmasi.

## OLCUM (kod degismeden)

### Hangfire yuzey envanteri - TAM LISTE

```
Program.cs:396  AddHangfire(... UseSqlServerStorage ...)   KOSULSUZ   <- KUSUR
Program.cs:418  AddHangfireServer()                        bayrakli
Program.cs:625  app.UseHangfireDashboard("/hangfire", ...)  KOSULSUZ   <- (b) sinifi
Program.cs:634-641  RecurringJob.AddOrUpdate x7            bayrakli
Services/HangfireAuthorizationFilter.cs                    yalniz dashboard'da kullanilir
```

### Tuketiciler - IKISI DE OLCULDU

- **Enqueue yolu YOK.** `IBackgroundJobClient` / `IRecurringJobManager` uretim kodunda
  **HIC enjekte edilmiyor** (tarandi: 0 gecis). Outbox dispatcher yalniz
  `RecurringJob.AddOrUpdate<OutboxProcessor>` ile zamanlaniyor (zaten bayrakli) ve testler
  isleyiciyi **DOGRUDAN** cagiriyor (`ProcessPendingAsync`) - Hangfire'a hic dokunmadan.
- **`GetFailedJobs` HANGFIRE'DAN BAGIMSIZ.** `DashboardManager.GetFailedJobs` ->
  `_outboxDal.GetListAsync(m => m.status == Failed)` - **outbox tablosunu DOGRUDAN okur**,
  Hangfire storage/IMonitoringApi KULLANMAZ. Yani operatorun gercek yuzeyi bu isten
  ETKILENMEZ.

### Bayragin bugunku semantigi

```
false veren TEK yer : Divisima.IntegrationTests/TestHostConfig.cs:74
etkiledigi host     : TestHostConfig.Apply -> 42 cagri yeri
uretim/gelistirme   : anahtar YOKSA varsayilan TRUE (Program.cs), example.json da true
Cerez_Secure sinifi : IKINCI bir host aciyor - `new CookieFactory("Production")`
                      (RefreshCookieContractTests.cs:317) ve o da TestHostConfig uyguluyor,
                      yani bayragi false AMA depolamayi YINE kuruyordu.
```

### KARAR TABLOSU (bayrak false iken Hangfire tipine dokunan her yol)

| Yol | Bugun | Sinif |
|---|---|---|
| `AddHangfireServer()` | zaten kapali | (a) |
| `RecurringJob.AddOrUpdate` x7 | zaten kapali | (a) |
| `AddHangfire` + `UseSqlServerStorage` | **ACIK - SQL'e baglaniyor** | DUZELTILECEK |
| `app.UseHangfireDashboard` | **ACIK - calisma aninda JobStorage cozer** | **(b)** |
| `IBackgroundJobClient` enqueue | **YOK** (0 enjeksiyon) | - |
| `GetFailedJobs` (admin ucu) | Hangfire'dan **BAGIMSIZ** | - |
| `OutboxProcessor` | testlerde DOGRUDAN cagriliyor | - |

**TEK (b) DASHBOARD'DUR ve URUN DAVRANISINI DEGISTIRMEZ** - bu yuzden durup sorulmadi:
uretim varsayilani `true` (dashboard aynen kayitli), bayragi `false` yapan TEK yer
TestHostConfig, testler `/hangfire`i HIC cagirmiyor (tarandi: 0), ve operatorun gercek
yuzeyi zaten `/hangfire` DEGIL - o, tek kimlik semasi JwtBearer oldugu icin tarayicidan
ERISILEMEZ (DALGA C / C4'te olculdu) ve nginx'te ayrica `allow 10.0.0.0/8` ile kilitli.

## DUZELTME

`AddHangfire` + `AddHangfireServer` TEK bir `if (arkaPlanIsleri)` blogunda; dashboard ve
recurring kayitlari da AYNI bayrakta. Bayrak `false` iken Hangfire'a ait **HICBIR DI kaydi**
yapilmaz -> `IGlobalConfiguration` **AKTIVE EDILEMEZ** -> havuz tukenmesi **YAPISAL OLARAK**
olusamaz. "Daha az olasi" degil, IMKANSIZ. Bayrak `true` davranisi DEGISMEZ.

## ONCE / SONRA (canli, iki yol da surulda)

```
BAYRAK TRUE (varsayilan)          BAYRAK false (BackgroundJobs__Enabled=false)
  API acildi            OK          API acildi            OK
  /hangfire      -> 401             /hangfire      -> 404   (dashboard kayitli DEGIL)
  HangFire tablosu -> 11            failed-jobs    -> 401   (uc CALISIYOR, Hangfire'dan
  recurring-jobs   -> 7                                      BAGIMSIZ)
```

**YAN KAZANC OLCULDU:** tam suit suresi **~1 dk 06 sn -> ~45 sn**. Test host'lari artik
Hangfire icin SQL'e hic baglanmiyor.

## PINLER (`ArkaPlanIsleriIzolasyonTests`, +2 - VERITABANI ACMAZ)

- **p1 `BAYRAK_FALSE_ISE_HANGFIRE_DI_KAYDI_HIC_YOK_DEPOLAMA_KURULMAZ`** - DAVRANIS pini,
  DETERMINISTIK. Kayitlar **AKTIVE EDILMEDEN** gozlenir: `IServiceCollection`, Program.cs'in
  kayitlarindan SONRA yakalanir ve `Hangfire.` ile baslayan TIP ADI aranir.
  **`GetService<IGlobalConfiguration>()` CAGIRILMADI - bilincli:** kayit VARSA o cagri tam da
  olcmek istedigimiz SQL baglantisini KENDI ACARDI; pin, olctugu zarari URETIRDI.
  Vakum kirici: yakalanan kayit sayisi > 100 olmali.
- **p2 `HICBIR_HANGFIRE_CAGRISI_BAYRAGIN_DISINDA_KALMAZ`** - KAYNAK SOZLESMESI pini
  (durust etiket). `if (arkaPlanIsleri)` bloklari susli parantez esleyerek cikarilir ve
  `AddHangfire(` / `AddHangfireServer(` / `UseHangfireDashboard(` / `RecurringJob.AddOrUpdate`
  cagrilarinin HEPSININ blok ICINDE oldugu dogrulanir. Yorum satirlari AYIKLANIR (bu dosya
  Hangfire'i onlarca kez yorumda aniyor - "kaynak tarayan pin kendi belgeledigi kalibi da
  tarar" tuzaginin bedeli depoda iki kez odendi). Tek satirlik `if` govdesi KABUL EDILMEZ.
  Vakum kirici: en az iki blok ve her desen kaynakta GERCEKTEN bulunmali.

**NEDEN p2 DAVRANIS PINI DEGIL (durust kayit):** bayrak TRUE bir test host'u bu suitte ayaga
kaldirilamaz - o host Hangfire depolamasini kurar, SQL'e baglanir ve GELISTIRICININ
veritabanina recurring job tanimi yazar; yani pinin KENDISI, kaldirmaya calistigimiz zarari
uretirdi. Bayrak TRUE davranisinin DAVRANIS kaniti yukaridaki canli olcumdur
(`/hangfire` 401 + 11 tablo + 7 recurring job).

**YENI SQL SINIFI ACILMADI** (10d794d dersi): iki pin de mevcut SIFIR-DDL sinifina eklendi.

## DIS KONTROLU (TAM KAPSAMA) + 5. KONTROL

**DIS - ORNEKLEM YOK, HER YENI PIN TEK TEK:**
```
p1 ters -> BAYRAK_FALSE_ISE_HANGFIRE_DI_KAYDI_HIC_YOK_DEPOLAMA_KURULMAZ   KIRMIZI
p2 ters -> HICBIR_HANGFIRE_CAGRISI_BAYRAGIN_DISINDA_KALMAZ                KIRMIZI
```
Ikisi de geri alindi, 4/4 yesil.

**5. KONTROL - M1 (kosul kaldirildi, depolama HER ZAMAN kurulur):**
p1 **DETERMINISTIK KIRMIZI** ve mesaj OLCULEN AILEYI birebir uretti:
```
Expected hangfireKayitlari to be empty ... but found {"Hangfire.JobStorage",
  "Hangfire.JobActivator", ..., "Hangfire.IGlobalConfiguration"}
```
`Hangfire.IGlobalConfiguration` - yigin izindeki `activating λ:Hangfire.IGlobalConfiguration`
tipinin TA KENDISI. p2 de kirildi (mutasyonda `AddHangfire(` gercekten blok DISINDA).
Geri alindi; `[MUTASYON]` izi depoda **0 dosya**.

**SURECTE YASANAN (kayit):** M1'in ILK denemesi `perl` ile yapildi ve **Program.cs'i BOZDU**
(using blogu birlestirildi, build **82 hata**). Test o turda bayat ikililerle kosup 1 kirmizi
verdi - yani sonuc GECERSIZDI. Kuralin **(b) TEMIZ BUILD** adimi bunu yakaladi; dosya
yedekten geri alindi (`git diff` ile yalniz amaclanan degisiklik dogrulandi) ve mutasyon
**Edit araciyla** tekrarlandi. **DERS: cok satirli C# bloklarinda `perl -0pi` yerine hassas
duzenleme kullanilir; her mutasyondan sonra build hata sayisi OKUNUR.**

## YEREL DOGRULAMA

**Ardisik UC tam suit - UCU DE TEMIZ:**
```
1/3  537 basarili / 540  43 sn   Cerez_Secure kirmizi: 0   Hangfire/havuz izi: 0
2/3  537 basarili / 540  46 sn   Cerez_Secure kirmizi: 0   Hangfire/havuz izi: 0
3/3  537 basarili / 540  47 sn   Cerez_Secure kirmizi: 0   Hangfire/havuz izi: 0
```
(kirilan 3'un UCU DE Docker'li `OrderEndpointTests` - yerelde Docker kapali, CI'da yesil)
Release 0 hata · whitespace + style **exit 0**.

**TABAN:** GUVENLIK-FIX-4'te ayni suit UC KEZ kosulmus ve **1 kirmizi / 2 temiz** vermisti.
Simdi 3/3 temiz. **DURUST SINIR:** uc kosum, 3'te-1 taban icin guclu ama KESIN kanit degildir;
kesin kanit MEKANIZMANIN kendisidir - Hangfire DI kaydi YOKSA `IGlobalConfiguration` aktive
EDILEMEZ ve o istisna OLUSAMAZ (p1 bunu deterministik olarak pinliyor).

## DEFTER

- **ADI OLAN FLAKE KAPANDI.** Kok sebep GUVENLIK-FIX-4'te ILK KEZ olculdu
  (`Autofac ... activating λ:Hangfire.IGlobalConfiguration` -> `max pool size was reached`),
  cozum bu dalgada. Guvenlik dalgasindaki eski "mesaj YAKALANAMADI" kaydina ve GUVENLIK-FIX-4
  bulgu kaydina capraz referans verildi.
- **GUVENLIK-FIX-4'e OZEL CI RE-RUN POLITIKASI KAPANDI.** O politika ("kirmizi yalniz
  Cerez_Secure ise bir kez yeniden calistir") tek bir push icin verilmisti ve gerekcesi
  duzeltilmemis bir flake'ti. Artik `Cerez_Secure_...` kirmizisi flake DEGIL, bu duzeltmenin
  BASARISIZLIK KANITIDIR: re-run istenmez, durulur ve olculur.

## PUSH RAPORU `60ecc93` - HER IKI WORKFLOW TAMAMEN YESIL (KAPANIS)

Push `677e9ee..60ecc93` (tek commit -> tek push). Adim bazinda + annotation duzeyinde
dogrulandi; iki run da dogru commit uzerinde kostu
(`head_sha` alani `60ecc93`, `event = push`).

**CI - Build & Test (run 32837426216) - TAMAMEN YESIL.**
`build-and-test`: `.NET 8 kurulumu` / `Bagimliliklari geri yukle` / `Derle (Release)` /
`SQL Server hazir mi` / **`SQL gerektiren testler (ATLANMAMALI)`** / **`Testler + coverage`** /
`TestDbKurulum - 1807 yeniden deneme ozeti` / **`Coverage raporunu yukle`** hepsi SUCCESS;
`TESHIS` skipped.
`format-check`: **iki ZORUNLU adim** (whitespace + style) ve
**`Model ile migration'lar SENKRON mu (ZORUNLU)`** SUCCESS.

**Security CI (run 32837426245) - TAMAMEN YESIL.**
`tests` (`Entegrasyon testleri` DAHIL, TESHIS skipped) / `dependency-scan` (RAPOR + KAPI +
kullanimdan kalkmis paket) / `codeql` SUCCESS.
**`secret-scan` -> `Gitleaks (secret taramasi)` SUCCESS** - bolum 7 kurali geregi ADIM
SONUCUNDAN okundu; "Leaks detected" satiri YOK.

**ANNOTATION: ALTI JOB'IN HICBIRINDE `failure` SEVIYESI YOK.** `format-check` ANNOTATION'DAN
okundu (job sonucundan DEGIL). Bulunan her annotation `warning` ve HEPSI ONCEDEN VARDI:
Node.js 20 deprecation, CodeQL Action v3 deprecation, ve
`Divisima.Core/DataAccess/*` nullable uyarilari. **Bu commit YENI uyari uretmedi.**

**RETRY GORUNURLUGU (ayri, iki job'da da anonim okundu):**
`TestDbKurulum: 1807 yeniden denemesi bu kosumda HIC ATESLEMEDI (0) - retry devrede,
gerekmedi.` Yani `model` kilidi bu kosumda HIC ATESLEMEDI; retry duran emniyet agi
olarak yerinde.

**ASIL SORU - `Cerez_Secure_HER_ORTAMDA_ISARETLI_OrtamGuardi_YOK` KIRILMADI.** Kanit uc
kanaldan: (1) `Testler + coverage` ve `Entegrasyon testleri` SUCCESS - `set -o pipefail`
devrede oldugu icin tek bir kirmizi test adimi dusururdu; (2) `TESHIS` adimi IKI JOB'DA DA
skipped (yalniz `if: failure()` kosar); (3) alti job'da failure seviyeli annotation 0.
**Bu push'a ozel RE-RUN YASAGI HIC DEVREYE GIRMEDI** - kirmizi olmadi, dolayisiyla
"duzeltmenin basarisizlik kaniti" durumu dogmadi.

**DURUST SINIR - TEST SAYISI ANONIM KANALDAN TEYIT EDILEMEZ.** 540 rakami okunamaz (job
log'u 403, Summary imza ister, annotation yalniz `Failed` satiri tasir - dordu de daha once
olculdu). Kanit ADIMLARIN SUCCESS OLMASIDIR: suit tumuyle kostu ve tek bir test kirilmadi.
Buradan cikan tek gecerli CIKARIM: yerelde Docker kapali oldugu icin kirilan **3
`OrderEndpointTests` kosucuda GECTI** - aksi halde adim kirmizi olurdu. Sayinin kendisi
okunamadi, kosumun temizligi okundu.

**GUVENLIK DALGASI 2 HATTI TAMAMEN KAPANDI:** GUVENLIK-FIX-3 `f800afe` ·
GUVENLIK-FIX-4 `677e9ee` · FLAKE-FIX `60ecc93` - **ucu de cift yesil**
(her iki workflow da SUCCESS, failure seviyeli annotation 0).

---

