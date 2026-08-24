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

## 2. Hedefler

- **RPO (max veri kaybi):** 15 dakika (log backup sikligina bagli).
- **RTO (max kesinti):** 1 saat (full + differential + log zinciri geri yukleme).

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
> Bu depoda 12 migration vardir; `InitialCreate` en eskisidir. (Bu satir uzun sure
> "Bu projede henuz migration yok" diyordu - on iki migration BAYATLAMISTI.)
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
