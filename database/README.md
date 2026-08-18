# Divisima — Veritabanı

Bu klasör, projenin veritabanını iki biçimde içerir: **üretim için gerçek MSSQL script'i** ve
**yerel doğrulama için çalışan bir simülasyon**.

## İçerik

```
database/
├── mssql/
│   ├── 01_schema.sql      # 43 tablo + 55 FK + index (MSSQL / T-SQL)
│   └── 02_seed.sql        # örnek veri (kategori, ürün, stok, kupon, hediye kartı)
├── sqlite_schema.sql      # aynı şemanın SQLite karşılığı (simülasyon için)
├── db_simulation.py       # GERÇEK DB simülasyonu (SQLite motorunda çalışır)
└── generate_schema.py     # entity sınıflarından her iki şemayı üreten script
```

## Üretimde MSSQL kurulumu

SQL Server'ınızda:

```bash
sqlcmd -S localhost -U sa -P <sifre> -Q "CREATE DATABASE Divisima"
sqlcmd -S localhost -U sa -P <sifre> -d Divisima -i mssql/01_schema.sql
sqlcmd -S localhost -U sa -P <sifre> -d Divisima -i mssql/02_seed.sql
```

Admin kullanıcısı seed'de **değildir**; uygulama açılışında `AdminSeeder` oluşturur (`AdminSeed`
config bölümü — şifre hash'i HMACSHA512 ile uygulama tarafında üretilir). Müşteri şifreleri de
uygulama üzerinden kayıtla oluşur.

> **Not:** Bu şema, entity sınıflarından üretilmiş **referans/başlangıç** şemasıdır. Nihai/kesin
> şema için EF Core migration üretmeniz önerilir (`dotnet ef migrations add InitialCreate`) — o
> zaman EF'in tam beklediği tablo/kolon adlarıyla birebir hizalanır. Bu script, migration boşluğunu
> pratik olarak kapatır ve DB'yi hemen ayağa kaldırmanızı sağlar.

## Simülasyon (yerel doğrulama)

`db_simulation.py`, gerçek bir SQL motoru (SQLite) üzerinde şemayı kurar, veri yükler ve iş
akışlarını **gerçek transaction + atomik UPDATE** ile çalıştırır:

```bash
python3 database/db_simulation.py
```

Doğrulanan akışlar: şema kurulumu, NOT NULL constraint uygulaması, sipariş verme (atomik stok
düşümü), overselling engeli, negatif miktar engeli, hediye kartı atomik bozdurma (compare-and-swap,
çift bozdurma engeli), mağaza kredisi atomik harcama (overdraft engeli), kupon doğrulama
(süre/ilk-sipariş/tavan), iade muhasebesi (çift iade engeli), KVKK rıza kaydı, taksit, referans
bütünlüğü.

> SQLite ve MSSQL şemaları **aynı entity sınıflarından** üretildiğinden eşdeğerdir. Simülasyon,
> MSSQL'in kendisi değildir; ancak şemanın ve iş mantığının gerçek bir SQL motorunda tutarlı
> çalıştığını kanıtlar. Üretim doğrulaması için `mssql/*.sql` script'lerini kendi SQL Server'ınızda
> çalıştırın ve ardından `dotnet build` + `dotnet test` ile uygulamayı derleyip test edin.

## Gelismis adversarial simulasyon

```bash
python3 database/advanced_simulation.py
```

Property-based test: 5 kullanici, 3000 rastgele + adversarial islem (gecersiz/asiri miktar,
overdraft, cift-bozdurma, expire-confirm cakismasi, agir churn). HER islemden sonra 9 sistem
invariant'i dogrulanir (stok>=0, rezerve<=stok, available>=0, rezervasyon-defteri=sayac, bakiye>=0,
toplam tutarli, yetim yok). Tam rezervasyon yasam dongusu modellenir: reserve->confirm/release/expire.
Bu, C# kodundaki atomik stok islemlerinin (ConfirmStockAsync/ReleaseReservedAsync/IncrementStockQuantityAsync)
tasarim dogrulugunu gercek SQL transaction'lariyla kanitlar.
