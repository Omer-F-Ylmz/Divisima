using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Divisima.DataAccess.Concrete.Context;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Divisima.IntegrationTests
{
    // === D-SEMA-FIX: SEMANIN TEK DOGRULUK KAYNAGI EF MIGRATIONS ==========================
    //
    // OLCULEN ONCE-DURUM (D-SEMA turu, dort saglama yolu ayni sunucuda kuruldu):
    //
    //                     A(dokumandaki komut)  A2(dosyanin niyeti)  B(EF model)  C(EF migration)
    //   tablo                    44                   44                 45            45
    //   FK                       17                   54                  9             9
    //   indeks (PK haric)         6                   71                 75            75
    //
    //   * B ile C BIT BIREBIR AYNI (dort kategoride de fark satiri 0) - EF'in iki yolu
    //     birbiriyle ve dev veritabaniyla TAM MUTABIK. Ayrisan taraf TEK: sema dosyasi.
    //   * A'daki 17 sayisi bir yazim hatasi degil: satir 635'teki FK_orders_payment_id tip
    //     uyumsuzlugundan patliyor, dosyada GO OLMADIGI icin BATCH DUSUYOR ve sonrasindaki
    //     37 FK + 65 indeks HIC olusmuyordu. sqlcmd yine de EXIT 0 donuyordu.
    //   * Ayrica -f 65001 verilmediginde UX_store_credit_referee_reward filtresindeki Turkce
    //     metin bozuluyor, indeks HICBIR SATIRLA eslesmiyordu: varlik gorunur, koruma YOK.
    //
    // KULLANICI KARARI: tek dogruluk kaynagi EF migrations; database/mssql/01_schema.sql
    // URETILEN bir artefakt olur, elle bakim biter, generate_schema.py kaldirilir.
    //
    // BU DOSYA NEYI TUTAR: karari KORUYAN dort sozlesme. Ucu DAVRANIS (gercek SQL Server'da
    // script kosuluyor, gercek katalog okunuyor), biri artefakt sozlesmesi.
    [Trait("Category", "Sql")]
    public class SemaTekKaynakTests : IAsyncLifetime
    {
        // TEST BASINA AYRI VERITABANI. CLAUDE.md'nin "sinif basina ayri DB" kurali burada
        // BIR ADIM DAHA ILERI tasindi ve gerekcesi OLCULDU: dort test de semayi SIFIRDAN
        // kuruyor, yani ayni ada baglanan dort Init/Dispose birbirinin veritabanini
        // dusuruyordu ("Cannot open database ... login failed" ve "Database already exists"
        // birebir goruldu). Guid ekiyle catisma YAPISAL olarak imkansiz.
        private readonly string _dbName = "DivisimaSemaPin_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");

        private static string BaseConn => string.IsNullOrWhiteSpace(ExplicitConn)
            ? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True;"
            : ExplicitConn;

        private string ConnStr =>
            new SqlConnectionStringBuilder(BaseConn) { InitialCatalog = _dbName }.ConnectionString;

        private static string MasterConn =>
            new SqlConnectionStringBuilder(BaseConn) { InitialCatalog = "master" }.ConnectionString;

        private bool _sqlAvailable;

        private static readonly Lazy<string> KokDizin = new(() =>
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "docker-compose.yml")))
                d = d.Parent;
            if (d == null)
                throw new InvalidOperationException(
                    "Depo koku bulunamadi (docker-compose.yml iceren ust dizin yok). " +
                    "SESSIZ SKIP YOK - bu pinler artefakti okuyamadan yesil kalamaz.");
            return d.FullName;
        });

        private static string Oku(params string[] parcalar) =>
            File.ReadAllText(Path.Combine(new[] { KokDizin.Value }.Concat(parcalar).ToArray()));

        // Uygulama tarafinda BOS bir veritabani acilir; testler semayi SCRIPT'TEN kurar
        // (EnsureCreated DEGIL - olculen sey tam olarak SCRIPT'in kendisidir).
        public async Task InitializeAsync()
        {
            try
            {
                await using var master = new SqlConnection(MasterConn);
                await master.OpenAsync();
                await using var cmd = master.CreateCommand();
                // Collation ACIKCA verilir: kimlik kurallari Turkish_CI_AS varsayar
                // (CLAUDE.md 6c) ve sunucu varsayilani Latin1 olabilir.
                cmd.CommandText = $"CREATE DATABASE [{_dbName}] COLLATE Turkish_CI_AS;";
                await cmd.ExecuteNonQueryAsync();
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak sema pin ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (!_sqlAvailable) return;
            try
            {
                // Havuzdaki acik baglantilar DROP'u engeller; once bosaltilir.
                SqlConnection.ClearAllPools();
                await using var master = new SqlConnection(MasterConn);
                await master.OpenAsync();
                await using var cmd = master.CreateCommand();
                cmd.CommandText =
                    $"ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_dbName}];";
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        // sqlcmd'nin GO batch ayiricisi T-SQL DEGILDIR; ADO.NET tek batch calistirir. Script
        // GO ile ayrilmis oldugu icin (D-SEMA'nin kapattigi tuzagin ta kendisi) burada da
        // ayni sekilde bolunur.
        private static IEnumerable<string> GoIleBol(string script) =>
            Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                 .Where(p => !string.IsNullOrWhiteSpace(p));

        private async Task<int> ScriptKosAsync(string script)
        {
            var calisan = 0;
            await using var conn = new SqlConnection(ConnStr);
            await conn.OpenAsync();
            foreach (var batch in GoIleBol(script))
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = batch;
                cmd.CommandTimeout = 180;
                await cmd.ExecuteNonQueryAsync();   // hata FIRLAR - sessiz gecistirme YOK
                calisan++;
            }
            return calisan;
        }

        private async Task<(int tablo, int fk, int indeks)> SayAsync()
        {
            await using var conn = new SqlConnection(ConnStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0),
       (SELECT COUNT(*) FROM sys.foreign_keys),
       (SELECT COUNT(*) FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
        WHERE i.type > 0 AND i.is_primary_key = 0);";
            await using var r = await cmd.ExecuteReaderAsync();
            await r.ReadAsync();
            return (r.GetInt32(0), r.GetInt32(1), r.GetInt32(2));
        }

        // ── 1) URETILEN ARTEFAKT GERCEKTEN KURUYOR ve IDEMPOTENT ────────────────────────
        [Fact]
        public async Task URETILEN_SCRIPT_KURAR_ve_IKINCI_KOSUMDA_HATA_VERMEZ()
        {
            if (Skipped()) return;
            var script = Oku("database", "mssql", "01_schema.sql");

            var batchSayisi = await ScriptKosAsync(script);
            batchSayisi.Should().BeGreaterThan(50,
                "script GO ile bolunmus olmali - tek batch'e sikismis bir script, ilk hatada "
              + "GERI KALANI SESSIZCE atlar (olculen once-durum: 55 FK beyani -> 17 FK)");

            var ilk = await SayAsync();
            // VAKUM KIRICI: script GERCEKTEN bir sey kurmus olmali. Bos bir script de
            // "iki kez kosunca hata vermez" testini gecerdi.
            ilk.tablo.Should().BeGreaterThan(40, "script semayi GERCEKTEN kurmali");
            ilk.fk.Should().Be(53, "karar verilen FK kumesi");
            ilk.indeks.Should().BeGreaterThan(60);

            // ASIL OLCUM: ayni script IKINCI kez - istisna FIRLARSA test kirmizi olur.
            await ScriptKosAsync(script);
            var ikinci = await SayAsync();
            ikinci.Should().Be(ilk, "idempotent script ikinci kosumda nesne SAYISINI degistirmemeli");
        }

        // ── 2) SEED SEMAYA UYUYOR (bu da olculen bir kirikti) ───────────────────────────
        [Fact]
        public async Task SEED_URETILEN_SEMAYA_UYAR_ve_SESSIZ_YARIM_KALMAZ()
        {
            if (Skipped()) return;
            await ScriptKosAsync(Oku("database", "mssql", "01_schema.sql"));

            // 02_seed.sql sonunda sayilari DOGRULAYAN bir blok tasir; tutmuyorsa RAISERROR
            // eder ve asagidaki cagri ISTISNA firlatir. Eski hali kosulsuz "Seed tamamlandi"
            // basiyordu ve OLCULDU ki urun/stok/kupon INSERT'leri NOT NULL ihlaliyle
            // dusuyordu (products.average_rating, products.review_count, coupons.per_user_limit).
            await ScriptKosAsync(Oku("database", "mssql", "02_seed.sql"));

            await using var conn = new SqlConnection(ConnStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT (SELECT COUNT(*) FROM categories), (SELECT COUNT(*) FROM products),
                                       (SELECT COUNT(*) FROM product_stocks), (SELECT COUNT(*) FROM coupons);";
            await using var r = await cmd.ExecuteReaderAsync();
            await r.ReadAsync();

            // CIFT-ANLAM KIRICI: yalniz "hata firlamadi" yetmez - satirlarin GERCEKTEN
            // yazildigi olculur. Eski hal kategorileri yaziyor, gerisini yazmiyor ve
            // yine de basarili gorunuyordu.
            r.GetInt32(0).Should().Be(3, "kategoriler");
            r.GetInt32(1).Should().Be(3, "urunler - eski halde 0 KALIYORDU");
            r.GetInt32(2).Should().Be(5, "stoklar - urunler yazilmadigi icin FK'ya takiliyordu");
            r.GetInt32(3).Should().Be(2, "kuponlar - per_user_limit NOT NULL ihlali");
        }

        // ── 3) FK KUMESI KARARLA BIREBIR ORTUSUR ───────────────────────────────────────
        [Fact]
        public async Task FK_KUMESI_KARARLA_ORTUSUR_IKI_DISLAMA_UYGULANMIS_HEPSI_RESTRICT()
        {
            if (Skipped()) return;
            await ScriptKosAsync(Oku("database", "mssql", "01_schema.sql"));

            await using var conn = new SqlConnection(ConnStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT fk.name, OBJECT_NAME(fk.parent_object_id), c.name,
       OBJECT_NAME(fk.referenced_object_id), rc.name, fk.delete_referential_action_desc
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c  ON c.object_id  = fkc.parent_object_id     AND c.column_id  = fkc.parent_column_id
JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id;";

            var iliskiler = new List<string>();
            var cascade = new List<string>();
            var adSapmasi = new List<string>();
            await using (var r = await cmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    var ad = r.GetString(0);
                    var cocuk = r.GetString(1);
                    var kolon = r.GetString(2);
                    iliskiler.Add($"{cocuk}.{kolon} -> {r.GetString(3)}.{r.GetString(4)}");
                    if (r.GetString(5) != "NO_ACTION") cascade.Add(ad);
                    if (ad != $"FK_{cocuk}_{kolon}") adSapmasi.Add(ad);
                }
            }

            iliskiler.Should().BeEquivalentTo(BeklenenIliskiler,
                "FK kumesi D-SEMA olcumunun VERDIGI karardir: 54 gecerli adaydan 9'u zaten EF'te "
              + "vardi, 28'i gercek dev verisinde ihlalsiz olculdu, 16'sinin yazma yolu okundu, "
              + "2'si DISLANDI. Kume degisiyorsa bu bilincli bir karar olmali.");

            // CIFT-ANLAM KIRICI (1): iki DISLAMA gercekten uygulanmis olmali. Yalniz sayiya
            // bakan bir assert, yanlis iki FK'nin eklenip dogru ikisinin cikarilmasini goremezdi.
            iliskiler.Should().NotContain(x => x.StartsWith("orders.payment_id", StringComparison.Ordinal),
                "orders.payment_id Iyzico'nun PaymentId'sidir (string), bizim payments tablomuza FK DEGIL");
            iliskiler.Should().NotContain(x => x.StartsWith("consent_records.", StringComparison.Ordinal),
                "KVKK riza kaydi hesap silindikten sonra da kanit olarak saklanabilmeli - KULLANICI KARARI");

            // CIFT-ANLAM KIRICI (2): silme davranisi ve ad bicimi ayri ayri tutulur.
            cascade.Should().BeEmpty("hicbir FK CASCADE olmamali - uretimde silme SOFT'tur");
            adSapmasi.Should().BeEmpty("her FK adi FK_<tablo>_<kolon> KISA bicimini izlemeli");
        }

        // ── 3b) FK'nin ACTIGI TEK DAVRANIS KAPISI KAPATILDI ────────────────────────────
        // 44 yeni FK'dan yalnizca BIRI mevcut bir uca davranis degisikligi getiriyordu:
        // SizeGuideManager.Upsert `dto.category_id`'yi DOGRULAMADAN yaziyordu, yani var
        // olmayan bir kategori SESSIZCE yetim satir uretiyordu. FK eklendigi an ayni girdi
        // HTTP 500 olurdu - kendi degisikligimiz operatore anlasilmaz bir hata dondururdu.
        // Guard eklendi (ayni katmandaki ProductAttributeManager idiyomu: 404 + adiyla mesaj).
        [Fact]
        public async Task OLMAYAN_KATEGORIYE_BEDEN_REHBERI_404_DONER_500_DEGIL()
        {
            if (Skipped()) return;
            await ScriptKosAsync(Oku("database", "mssql", "01_schema.sql"));

            await using var ctx = new DivisimaDbContext(
                new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);
            var mgr = new Divisima.Bussiness.Concrete.SizeGuideManager(
                new Divisima.DataAccess.Concrete.EntityFramework.EfSizeGuideEntryDal(ctx),
                new Divisima.DataAccess.Concrete.EntityFramework.EfCategoryDal(ctx));

            var yok = await mgr.Upsert(new Divisima.Entity.Dtos.SizeGuide.SizeGuideEntryDto
            {
                category_id = 999_999,
                size_label = "M",
                sort_order = 1
            });
            yok.Item1.Should().Be(System.Net.HttpStatusCode.NotFound,
                "var olmayan kategori 404 ile reddedilmeli - FK ihlalinden dogan 500 DEGIL");
            yok.Item2.Success.Should().BeFalse();

            // VAKUM KIRICI: guard'in HER SEYI reddetmedigi gosterilir. Bu assert olmadan
            // "Upsert'i tumden bozan" bir uygulama da yukaridaki pini gecerdi.
            var kategori = new Divisima.Entity.Entities.Category
            {
                name = "Rehber Kategori",
                slug = "rehber-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Divisima.Entity.Entities.Category>().Add(kategori);
            await ctx.SaveChangesAsync();

            var var_ = await mgr.Upsert(new Divisima.Entity.Dtos.SizeGuide.SizeGuideEntryDto
            {
                category_id = kategori.id,
                size_label = "M",
                sort_order = 1
            });
            var_.Item2.Success.Should().BeTrue($"gecerli kategori KABUL edilmeli: {var_.Item2.Message}");
        }

        // ── 4) ARTEFAKT SOZLESMESI: dosya URETILMIS, elle bakim BITTI ──────────────────
        [Fact]
        public void SEMA_DOSYASI_URETILMIS_ARTEFAKT_ve_MIGRATIONLARLA_SENKRON()
        {
            var sema = Oku("database", "mssql", "01_schema.sql");

            sema.Should().Contain("URETILMIS DOSYA - ELLE DUZENLEMEYIN",
                "dosya bir artefakt oldugunu KENDISI soylemeli");
            sema.Should().Contain("migrations script --idempotent",
                "yeniden uretim komutu dosyanin icinde yazili olmali");
            sema.Should().Contain("-f 65001",
                "kod sayfasi bayragi olmadan Turkce filtre metni bozulur - bedeli olculdu");
            sema.Should().Contain("-b ",
                "-b olmadan yarim kalan bir kurulum EXIT 0 doner");

            File.Exists(Path.Combine(KokDizin.Value, "database", "generate_schema.py"))
                .Should().BeFalse("olu ureteci kaldirildi (FK'lari modelden degil ADLANDIRMA "
                                + "KURALINDAN cikariyordu; payment_id hatasinin kaynagi buydu)");

            // SENKRON: her migration'in kimligi script'te GECMELI. Biri migration ekleyip
            // script'i yenilemezse dagitim artefakti BAYATLAR ve bu pin kirilir.
            var migrationDizini = Path.Combine(KokDizin.Value, "Divisima.Dal", "Migrations");
            var migrationIdleri = Directory.GetFiles(migrationDizini, "*.cs")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n != null && !n.EndsWith(".Designer", StringComparison.Ordinal)
                                      && n != "DivisimaDbContextModelSnapshot")
                .Select(n => n!)
                .ToList();

            // VAKUM KIRICI: tarama gercekten migration bulmus olmali.
            migrationIdleri.Should().HaveCountGreaterThan(5, "depoda migration'lar var");

            var eksik = migrationIdleri.Where(id => !sema.Contains($"N'{id}'", StringComparison.Ordinal)).ToList();
            eksik.Should().BeEmpty(
                "uretilen script TUM migration'lari icermeli - eksikse dosya BAYAT demektir "
              + "(yenile: dotnet ef migrations script --idempotent ... -o database/mssql/01_schema.sql)");
        }

        // D-SEMA olcumunun verdigi karar. Liste CANLI KATALOGDAN uretildi (elle yazilmadi).
        private static readonly string[] BeklenenIliskiler =
        {
            "addresses.customer_id -> customers.id",
            "cart_items.cart_id -> carts.id",
            "cart_items.product_id -> products.id",
            "carts.customer_id -> customers.id",
            "collection_items.collection_id -> collections.id",
            "collection_items.product_id -> products.id",
            "coupon_usages.coupon_id -> coupons.id",
            "coupon_usages.customer_id -> customers.id",
            "coupon_usages.order_id -> orders.id",
            "customer_devices.customer_id -> customers.id",
            "invoices.customer_id -> customers.id",
            "invoices.order_id -> orders.id",
            "loyalty_transactions.customer_id -> customers.id",
            "loyalty_transactions.order_id -> orders.id",
            "order_items.order_id -> orders.id",
            "order_items.product_id -> products.id",
            "order_snapshot_items.order_snapshot_id -> order_snapshots.id",
            "order_snapshot_items.product_id -> products.id",
            "order_snapshots.customer_id -> customers.id",
            "order_snapshots.order_id -> orders.id",
            "order_status_histories.order_id -> orders.id",
            "orders.address_id -> addresses.id",
            "orders.customer_id -> customers.id",
            "payments.order_id -> orders.id",
            "price_drop_subscriptions.product_id -> products.id",
            "product_attributes.product_id -> products.id",
            "product_images.product_id -> products.id",
            "product_questions.customer_id -> customers.id",
            "product_questions.product_id -> products.id",
            "product_reviews.customer_id -> customers.id",
            "product_reviews.product_id -> products.id",
            "product_stocks.product_id -> products.id",
            "products.category_id -> categories.id",
            "products.sub_category_id -> sub_categories.id",
            "recently_viewed_products.customer_id -> customers.id",
            "recently_viewed_products.product_id -> products.id",
            "return_requests.customer_id -> customers.id",
            "return_requests.order_id -> orders.id",
            "return_requests.product_id -> products.id",
            "review_helpful_votes.customer_id -> customers.id",
            "security_events.customer_id -> customers.id",
            "shipments.order_id -> orders.id",
            "size_guide_entries.category_id -> categories.id",
            "stock_movements.product_id -> products.id",
            "stock_notification_requests.product_id -> products.id",
            "stock_reservations.order_id -> orders.id",
            "stock_reservations.product_id -> products.id",
            "store_credit_transactions.customer_id -> customers.id",
            "store_credit_transactions.order_id -> orders.id",
            "sub_categories.category_id -> categories.id",
            "user_sessions.customer_id -> customers.id",
            "wishlist_items.customer_id -> customers.id",
            "wishlist_items.product_id -> products.id",
        };
    }
}
