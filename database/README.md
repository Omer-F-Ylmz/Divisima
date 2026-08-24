# Divisima — Veritabanı

**Şemanın tek doğruluk kaynağı `Divisima.Dal/Migrations` klasörüdür (EF Core migrations).**
Bu klasördeki `mssql/01_schema.sql` onun **üretilmiş dağıtım çıktısıdır** — elle düzenlenmez.

## İçerik

```
database/
├── mssql/
│   ├── 01_schema.sql      # ÜRETİLMİŞ (dotnet ef migrations script --idempotent) - elle düzenlemeyin
│   └── 02_seed.sql        # örnek veri (kategori, ürün, stok, kupon, hediye kartı) - elle bakımlı
├── sqlite_schema.sql      # ESKİ simülasyonların şeması (aşağıdaki uyarıya bakın)
├── db_simulation.py       # SQLite üzerinde iş akışı simülasyonu
├── advanced_simulation.py # property-based adversarial simülasyon
└── concurrent_stress_sim.py
```

## Üretimde MSSQL kurulumu

```bash
sqlcmd -S <sunucu> -E -Q "CREATE DATABASE Divisima COLLATE Turkish_CI_AS"
sqlcmd -S <sunucu> -E -d Divisima -b -f 65001 -i mssql/01_schema.sql
sqlcmd -S <sunucu> -E -d Divisima -b -f 65001 -i mssql/02_seed.sql
```

**`-b` ve `-f 65001` bayrakları ZORUNLUDUR — ikisinin de bedeli ölçüldü (D-ŞEMA):**

- **`-b`** — bir ifade patlarsa sqlcmd sıfır dışı kod döndürsün. Bu bayrak olmadan
  script'in yarısı çalışmasa bile `EXIT 0` döner ve operatör "başarılı" görür.
- **`-f 65001`** — dosya UTF-8. Kod sayfası verilmezse Türkçe karakterler bozulur;
  ölçüldü: `UX_store_credit_referee_reward` filtresindeki metin bozulunca indeks **hiçbir
  satırla eşleşmiyor** — varlık görünür, koruma yok.

> **Collation:** veritabanı `Turkish_CI_AS` olmalıdır. Testler bunu ayrıca pinler
> (`CollationMetaPinTests`); Latin1 bir kurulumda `i/I` ve `ı/İ` ayrımına dayanan
> kimlik kuralları sessizce yanlış çalışır (bkz. CLAUDE.md bölüm 6c).

Şema kurulumu **ayrı ve ayrıcalıklı bir adımdır** — uygulama açılışta migrate **etmez** ve
üretimdeki DB kullanıcısının DDL yetkisi yoktur. Sıra ve yetki için
`ops/deployment-checklist.md` → "Veritabanı şeması".

Admin kullanıcısı seed'de **değildir**; uygulama açılışında `AdminSeeder` oluşturur
(`AdminSeed` config bölümü — şifre hash'i HMACSHA512 ile uygulama tarafında üretilir).
Müşteri şifreleri de uygulama üzerinden kayıtla oluşur.

## Şemayı yeniden üretmek

Entity ya da `DivisimaDbContext` değiştiğinde **önce migration üretilir**, sonra bu dosya
yenilenir:

```bash
dotnet ef migrations add <Ad> --project Divisima.Dal --startup-project Divisima.API
dotnet ef migrations script --idempotent \
  --project Divisima.Dal --startup-project Divisima.API --context DivisimaDbContext \
  -o database/mssql/01_schema.sql
```

Ardından dosyanın başındaki "ÜRETİLMİŞ DOSYA" başlık bloğu yeniden konur.

CI, model ile migration'ların ayrışmadığını **her push'ta** doğrular
(`dotnet ef migrations has-pending-model-changes`); migration üretmeden yapılan bir model
değişikliği build'i kırar.

## Neden elle bakımlı şema bırakıldı (D-ŞEMA, ölçüm)

Bu dosya eskiden entity sınıflarından üretilen, sonra **elle bakımı yapılan** bir script'ti.
Ölçüldüğünde:

| | beyan | dokümandaki komutla gerçekleşen |
|---|---|---|
| FK | 55 | **17** |
| indeks | 71 | **6** |

Satır 635'teki `FK_orders_payment_id` (`orders.payment_id` NVARCHAR, `payments.id` INT — o alan
Iyzico'nun PaymentId'sini tutar, bizim tablomuza FK değildir) patlıyor, `GO` olmadığı için
**batch'i düşürüyor** ve sonrasındaki hiçbir şey kurulmuyordu. `sqlcmd` yine de `EXIT 0`
dönüyordu. Ayrıca EF modeliyle **107 kolon farkı** (20'si uygulamanın yazdığı veriden **dar**,
biri tip uyumsuz) ve `sellers` tablosunun tamamen eksik olması vardı.

Üreteç (`generate_schema.py`) kaldırıldı: FK'ları modelden değil **adlandırma kuralından**
(`<x>_id → <x>s(id)`) çıkarıyordu — `payment_id` hatası buradan geliyordu — ve ilk commit'ten
beri hiç güncellenmemişken şema dosyası beş ayrı commit'te elle düzenlenmişti, yani zaten
mevcut şemayı yeniden üretemiyordu.

## Simülasyonlar (eski)

```bash
python3 database/db_simulation.py
python3 database/advanced_simulation.py
```

SQLite üzerinde iş akışlarını gerçek transaction'larla sürer: atomik stok düşümü, overselling
engeli, hediye kartı compare-and-swap, mağaza kredisi overdraft engeli, kupon doğrulama, iade
muhasebesi, KVKK rıza kaydı.

> **UYARI — bu simülasyonlar artık şemanın kanıtı DEĞİLDİR.** `sqlite_schema.sql` de aynı
> (kaldırılan) üreteçten çıkıyordu; MSSQL şeması EF'e taşındığı için ikisi **eşdeğer değil**.
> Dosya olduğu gibi bırakıldı ama artık **elle bakımlıdır** ve gerçek şemadan ayrışabilir.
> Bugün geçerli olan kanıt katmanı: `Divisima.IntegrationTests` (294 `Category=Sql` pini,
> gerçek SQL Server üzerinde).
