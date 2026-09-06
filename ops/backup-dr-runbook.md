# Divisima — Yedekleme, Felaket Kurtarma ve Migration Runbook

## 1. Yedekleme stratejisi (SQL Server)

| Tur | Siklik | Saklama | Amac |
|-----|--------|---------|------|
| Full backup | Gunluk (03:00) | 30 gun | Tam geri yukleme temeli |
| Differential | 6 saatte bir | 7 gun | Full sonrasi degisiklikler |
| Transaction log | 15 dakikada bir | 3 gun | Point-in-time recovery |

- Yedekler ayri bir depolama hesabina (farkli bolge) kopyalanmali — sunucu kaybinda erisim.
- Yedekler **sifreli** olmali (TDE veya backup encryption).
- Ayda bir **restore tatbikati**: yedekten yeni ortama geri yukleyip dogrula (yedek ise yaramiyorsa yedek degildir).

### ON KOSUL — RECOVERY MODELI **FULL** OLMALI (D6 tatbikatinda OLCULDU)

Yukaridaki tablonun **ucuncu satiri** (transaction log, 15 dk) ve bolum 3'teki point-in-time
proseduru, veritabani **FULL recovery** modelindeyse calisir. **SIMPLE modelde ikisi de
IMKANSIZDIR** — olculdu:

```
recovery modeli = SIMPLE
BACKUP LOG DivisimaDb ...
  -> Msg 4208: The statement BACKUP LOG is not allowed while the recovery model is SIMPLE.
```

SIMPLE modelde gercek RPO, **son full/differential yedekten bu yana gecen suredir** —
gunluk 03:00 full ile **24 saate kadar veri kaybi** demektir, 15 dakika DEGIL.

**DAGITIMDA DOGRULANACAK (zorunlu):**

```sql
SELECT DATABASEPROPERTYEX('Divisima','Recovery');   -- FULL donmeli
ALTER DATABASE Divisima SET RECOVERY FULL;          -- degilse
BACKUP DATABASE Divisima TO DISK='...full.bak';     -- FULL'e gecisten SONRA log zinciri
                                                    -- ancak bir full yedekle BASLAR
```

**SURUM SINIRI (D6'da olculdu):** tatbikat ortami **SQL Server Express Edition**'di ve
Express **backup compression** ile **TDE** DESTEKLEMIYOR (`Msg 1844: BACKUP DATABASE WITH
COMPRESSION is not supported on Express Edition`). Yani yukaridaki "yedekler sifreli olmali"
maddesi Express'te KARSILANAMAZ; uretim Standard/Enterprise olmalidir.

## 2. Hedefler

- **RPO (max veri kaybi):** 15 dakika — **KOSULLU**: yalnizca FULL recovery + 15 dakikada bir
  log yedegi varsa. Bu on kosul bolum 1'de ve dagitim checklist'inde dogrulanir.
  **D6 tatbikatinda ORTAM SIMPLE oldugu icin bu hedef DOGRULANAMADI** (log yedegi alinamadi).
- **RTO (max kesinti):** 1 saat — **UST SINIR**. D6 tatbikatinda dev ortaminda uctan uca
  **6,4 saniye** olculdu (dusurme 1,7 sn + geri yukleme 0,5 sn + uygulama ayaga kalkma 4,2 sn;
  80 MB / 19 MB yedek). Uretim donaniminda, gercek veri hacminde ve differential+log zinciriyle
  bu sure BUYUR; 1 saatlik hedef makul bir tavan olarak KORUNUYOR.
  **SINIR (durust kayit): tatbikat DEV ortaminda yapildi, uretim donaniminda RTO FARKLI olabilir.**

## 3. Geri yukleme prosedueru (point-in-time)

```sql
-- 1) Son full backup
RESTORE DATABASE Divisima FROM DISK='...full.bak' WITH NORECOVERY, REPLACE;
-- 2) Son differential
RESTORE DATABASE Divisima FROM DISK='...diff.bak' WITH NORECOVERY;
-- 3) Log yedekleri (hedef ana kadar sirayla)
RESTORE LOG Divisima FROM DISK='...log1.trn' WITH NORECOVERY;
RESTORE LOG Divisima FROM DISK='...log2.trn' WITH STOPAT='2026-07-20T14:30:00', RECOVERY;
```

## 3b. TATBIKAT — YAPILDI (D6), TEKRARLANABILIR ADIMLAR

Bolum 1 "ayda bir restore tatbikati" diyordu ama tatbikat **hic yapilmamisti**. D6'da yapildi;
asagidaki adimlar aynen tekrarlanabilir. **SIRA KRITIK: yedek once YAN BIR ISIMLE geri
yuklenip DOGRULANIR, veritabani ancak ondan sonra dusurulur** — kanitlanmamis bir yedege
guvenerek uretim veritabanini dusurmek, kurtarmayi denemek degil kumar oynamaktir.

```sql
-- 1) Yedek al + dogrula
BACKUP DATABASE Divisima TO DISK='...\Divisima_drill.bak' WITH INIT, FORMAT, CHECKSUM;
RESTORE VERIFYONLY FROM DISK='...\Divisima_drill.bak' WITH CHECKSUM;   -- "backup set is valid"

-- 2) YAN ISIMLE geri yukle (yedegin GERCEKTEN ise yaradiginin kaniti)
RESTORE DATABASE Divisima_DrillRestore FROM DISK='...\Divisima_drill.bak'
  WITH MOVE 'Divisima' TO '...\Divisima_DrillRestore.mdf',
       MOVE 'Divisima_log' TO '...\Divisima_DrillRestore_log.ldf', RECOVERY;
-- invariant sorgularini BURADA kostur; asil veritabaniyla BIREBIR ayni cikmali

-- 3) Asil tatbikat
ALTER DATABASE Divisima SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE Divisima;
RESTORE DATABASE Divisima FROM DISK='...\Divisima_drill.bak' WITH RECOVERY;

-- 4) Uygulamayi ayaga kaldir, /health 200 bekle, invariantlari TEKRAR kostur
```

**OLCUM SABLONU** (D6'da elde edilen degerler ornek olarak):

| Adim | D6 (dev, 80 MB DB / 19 MB yedek) |
|---|---|
| Yedek alma | 330 ms (2425 sayfa, 0,068 sn saf yedek suresi) |
| VERIFYONLY | gecerli |
| Yan geri yukleme | 466 ms |
| Veritabanini dusurme | 1.693 ms |
| Geri yukleme | 503 ms |
| Uygulama ayaga kalkma (`/health` 200) | 4.185 ms |
| **TOPLAM KESINTI (RTO)** | **6,4 saniye** |
| Veri tutarliligi | 11 invariant sorgusu, ONCE ile SONRA **BIREBIR AYNI** |
| Uygulama dogrulamasi | katalog 200 · kategori 200 · **gercek giris 200** · my-orders 200 |

Uygulama, kesinti penceresini durust olcmek icin **ON DERLENIR** (`--no-build` ile baslatilir);
uretimde yayinlanmis ikili zaten hazirdir, `dotnet run`in derleme adimi RTO'ya girmez.

**MIGRATION'LARIN GERCEK VERIYLE KOSMASI da D6'da dogrulandi:** uretilen idempotent script ile
kurulan bos bir veritabanina (`56 FK / 45 tablo + __EFMigrationsHistory`, o gun 12 migration kaydi;
**BUGUN 15** - LF-1/K4'te yeniden olculdu, asagidaki nota bakin)
`dotnet ef database update` uygulandi -> **"No migrations were applied. The database is already
up to date."** ve sayilar DEGISMEDI. Yani `database/mssql/01_schema.sql` ile migration'lar
AYNI semayi uretiyor; D-SEMA'nin iddiasi olcumle KANITLANDI.

## 4. Migration stratejisi (sifir kesinti — expand/contract)

Migration'lar **geriye uyumlu** olmali (blue-green deploy sirasinda eski + yeni kod ayni sema ile calisir):

1. **Expand:** yeni kolon/tablo EKLE (nullable veya default'lu). Eski kod etkilenmez.
2. **Deploy:** yeni kod devreye alinir (hem eski hem yeni semayi okur).
3. **Backfill:** veri tasima/doldurma (arka planda, kucuk batch'ler).
4. **Contract:** eski kod tamamen gidince eski kolon/kisitlar kaldirilir (ayri migration).

Yikici degisikliklerden (kolon silme/yeniden adlandirma tek adimda) KACIN — rollback'i imkansizlastirir.

### Migration uretimi
```bash
dotnet ef migrations add <Ad> --project Divisima.Dal --startup-project Divisima.API
dotnet ef database update --project Divisima.Dal --startup-project Divisima.API
# Geri alma:  dotnet ef database update <OncekiMigration>
```

#### SEMA ISLEMLERI UYGULAMA CONFIG'I GEREKTIRMEZ (D-SEMA-FIX)

**Kurtarma sirasinda migration komutlarini kosmak icin uygulamanin secret'larina IHTIYACINIZ
YOKTUR.** Yalnizca veritabani baglantisi gerekir ve o da tek bir ortam degiskeniyle verilir:

```bash
export ConnectionStrings__DivisimaDb="Server=<sunucu>;Database=Divisima;User Id=<ddl_yetkili>;Password=<...>;TrustServerCertificate=True;"
dotnet ef database update --project Divisima.Dal --startup-project Divisima.API
```

`Divisima.Dal/DivisimaDesignTimeDbContextFactory` bunu saglar; baglanti dizgesini su sirayla
cozer: **(1)** `ConnectionStrings__DivisimaDb` ortam degiskeni -> **(2)**
`Divisima.API/appsettings.Development.json` -> **(3)** `Divisima.API/appsettings.json`
(`CHANGE_ME` yer tutucusu GECERLI SAYILMAZ) -> **(4)** bilerek BAGLANILAMAZ bir yer tutucu.
Dorduncu basamak yalnizca baglanmayan komutlar (`migrations add`, `migrations script`,
`has-pending-model-changes`) icindir; `database update` oraya duserse **gurultulu patlar** -
sessizce yanlis bir veritabanina YAZMAZ.

**NEDEN BOYLE BIR NOT VAR - OLCULDU:** fabrika eklenmeden ONCE `dotnet ef ...` komutlari
DbContext'i elde etmek icin baslangic projesinin HOST'unu calistiriyor, dolayisiyla
`Program.cs`'in fail-fast blogunu da tetikliyordu. Yani bir **SEMA** islemi, uygulamanin TAM
URETIM CONFIG'INI - `TokenOptions:SecurityKey` dahil - sart kosuyordu. Ayricalikli bir
bastion'da sema kurtarmaya calisan operator, JWT anahtariyla hicbir ilgisi olmayan bir is icin
`FATAL: Config - TokenOptions:SecurityKey eksik` ile karsilasirdi. Bu bir CI adiminda ortaya
cikti ama asil bedeli **tam da bu runbook'un anlattigi kurtarma yolunda** olurdu.

> **SEMANIN TEK DOGRULUK KAYNAGI `Divisima.Dal/Migrations`'dir** (D-SEMA karari).
> Bu depoda **15** migration vardir; `InitialCreate` en eskisidir.
> **URETEN IFADE (sayi ezberden yazilmaz - LF-1/K4):**
> `ls Divisima.Dal/Migrations/*.cs | grep -v Designer | grep -v ModelSnapshot | wc -l` -> **15**
> ve bos bir veritabanina uygulandiktan sonra `SELECT COUNT(*) FROM __EFMigrationsHistory` -> **15**
> (iki bagimsiz kanal, LF-1 oncesi launch olcum turunda `LaunchProbeDb` ile dogrulandi:
> 46 tablo · 15 satir · "Done."). `DivisimaDbContextModelSnapshot.cs` bir migration DEGILDIR,
> sayima GIRMEZ - dosya sayisi 16'dir.
> (Bu satir uzun sure "Bu projede henuz migration yok" diyordu, sonra "12"de BAYATLADI -
> IKI KEZ. Bu yuzden artik UREten IFADEYLE yazili.)
>
> **.NET araci olmayan bir ortamda** (felaket kurtarma, ayricalikli bir bastion) sema
> `database/mssql/01_schema.sql` ile kurulur. O dosya URETILMIS bir artefakttir
> (`dotnet ef migrations script --idempotent` ciktisi), elle duzenlenmez ve idempotenttir -
> ayni script iki kez kosulabilir.
>
> ```bash
> sqlcmd -S <sunucu> -d Divisima -b -f 65001 -i database/mssql/01_schema.sql
> ```
> `-b` ve `-f 65001` ZORUNLUDUR; gerekcesi dosyanin basindaki baslik blogunda (bayraksiz
> koşumda script'in yarisi calismasa bile sqlcmd EXIT 0 doner).
>
> Sema kurulumu AYRICALIKLI bir adimdir: uygulamanin calisma zamani DB kullanicisinin DDL
> yetkisi YOKTUR ve uygulama acilista migrate ETMEZ (bkz. `ops/deployment-checklist.md` ->
> "Veritabani semasi").

## 5. Felaket senaryolari

| Senaryo | Aksiyon |
|---------|---------|
| DB sunucu kaybi | Yedekten yeni sunucuya restore (RTO 1s); connection string guncelle |
| Bolge kesintisi | Ikincil bolgedeki yedekten ayaga kaldir; DNS/trafik yonlendir |
| Hatali deploy | Blue-green: onceki surume aninda geri don (trafik switch) |
| Veri bozulmasi | Point-in-time restore (bozulma oncesi ana) |
| Ransomware | Sifreli + immutable yedekten temiz ortama restore |
| **Redis kaybi** | **Uygulama ACILMAZ** (asagi bak) - once Redis'i ayaga kaldir, sonra uygulamayi baslat |

### Redis erisilemezse uygulama ACILMAZ (D5 - OLCULDU)

`Redis:Enabled=true` iken baglanti kurulamazsa `Program.cs` acilista
`StackExchange.Redis.RedisConnectionException` firlatir ve surec baslamaz. **Sessizce
in-memory'ye DUSMEZ.** Bu DOGRU davranistir - dagitik kilit ve merkezi sayac olmadan acilan
bir sunucu, koruma varmis gibi davranirdi (cift odeme, kacan rate limit).

Kurtarma sirasindaki sonucu: **once Redis, sonra uygulama.** Uygulama acilmiyorsa ve hata
metni `redis` iceriyorsa sorun sema ya da JWT DEGILDIR.

Acil durumda Redis'siz ayaga kaldirmak gerekirse `Redis:Enabled=false` ile baslatilabilir -
ama o zaman kilit/sayac/blacklist **sunucu-basina** olur; **TEK INSTANCE** ile kosulmalidir,
aksi halde cift islem ve kacan rate limit riski dogar.

> Rate limit esikleri (`RateLimit:AuthPermitLimit` / `PaymentPermitLimit` /
> `GlobalPermitLimit`) her iki yolda da okunur (D5). Once Redis yolu bu degerleri HIC
> okumuyordu ve auth kovasi kaynakta sabit 5'ti.
