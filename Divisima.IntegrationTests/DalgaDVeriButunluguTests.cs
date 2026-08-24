using System;
using System.IO;
using System.Linq;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ DALGA D / D2 - YETIM STOK SATIRLARI ve REFERANS BUTUNLUGU ══════════════════════════
    //
    // OLCULEN ONCE-DURUM (dev veritabani):
    //   yetim product_stocks satiri     : 120  (40 ayri product_id, 3..182)
    //   yetimde reserved_quantity > 0   : 0
    //   bagli stock_reservations        : 0
    //   bagli stock_movements           : 0
    //   bagli order_items               : 0
    //   product_stocks -> products FK   : YOK
    //
    // Kaynak URETIM YOLU DEGIL: ProductManager.Delete SOFT-delete'tir (is_active=false) ve
    // urunu FIZIKSEL silen hicbir kod yolu yoktur. 120 satir, Dalga 3'un performans seed
    // temizliginin urun satirlarini DOGRUDAN silmesinden kaldi.
    //
    // KULLANICI KARARI: FK EKLENSIN. Gerekce: "bugun uretimde fiziksel silme yolu olmamasi
    // yarin da olmayacagi anlamina gelmiyor; pin kirildiginda hasar COKTAN olusmus olur."
    // Ayni tabloda ayni gece bir kez daha "kimse buraya dokunmaz" varsayiminin bedeli odendi
    // (filtresiz UNIQUE indeks -> urunun TUM bedenlerini kaybettiren guncelleme, Dalga B).
    //
    // IKISI BIRBIRININ ALTERNATIFI DEGIL (kullanici sarti 3): hem VERI BUTUNLUGU invarianti
    // ("yetim = 0") hem FK'nin GERCEKTEN ENGELLEDIGI ayri ayri pinlenir. Invariant tek basina
    // BIR SAYI olarak olculurse VAKUMA duser (taze bir veritabaninda FK olmasa da 0 cikar) -
    // bu yuzden invariant, onu URETEN YOLA baglandi: urun satirini dogrudan SQL ile silmek.
    // O yol acikken yetim URETILIR, kapaliyken URETILEMEZ. Gerekce ve olcum 2 numarali pinde.
    [Trait("Category", "Sql")]
    public class DalgaDVeriButunluguTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaDalgaDTest";
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");

        private static string ConnStr
        {
            get
            {
                var baseConn = string.IsNullOrWhiteSpace(ExplicitConn)
                    ? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True;"
                    : ExplicitConn;
                return new SqlConnectionStringBuilder(baseConn) { InitialCatalog = DbName }.ConnectionString;
            }
        }

        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            try
            {
                await using var pre = NewContext();
                await pre.Database.EnsureDeletedAsync();
                await pre.Database.EnsureCreatedAsync();
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak Dalga D testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await ctx.Database.EnsureDeletedAsync(); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        private static async Task<int> UrunOlusturAsync(DivisimaDbContext ctx)
        {
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8);
            var kat = new Category { name = "DalgaD " + damga, slug = "dalgad-" + damga, is_active = true, created_at = DateTime.Now };
            ctx.Set<Category>().Add(kat);
            await ctx.SaveChangesAsync();

            var urun = new Product
            {
                name = "DalgaD Urun " + damga,
                description = "Dalga D pini icin urun.",
                color_hex = "#334455",
                brand = "Divisima",
                price = 499.90m,
                category_id = kat.id,
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Product>().Add(urun);
            await ctx.SaveChangesAsync();
            return urun.id;
        }

        // ── 1) FK GERCEKTEN ENGELLIYOR (DAVRANIS PINI) ────────────────────────────────────
        [Fact]
        public async Task YETIM_STOK_SATIRI_EKLEMEK_VERITABANI_DUZEYINDE_REDDEDILIR()
        {
            if (Skipped()) return;
            await using var ctx = NewContext();

            // VAKUM KIRICI: once GECERLI bir satirin GERCEKTEN yazildigi dogrulanir. Tablo
            // tumden yazilamaz olsaydi asagidaki "reddedilir" asserti yanlis sebepten yesil
            // kalirdi.
            var urunId = await UrunOlusturAsync(ctx);
            ctx.Set<ProductStock>().Add(new ProductStock
            {
                product_id = urunId,
                size = "M",
                stock_quantity = 10,
                reserved_quantity = 0,
                is_active = true,
                created_at = DateTime.Now
            });
            await ctx.SaveChangesAsync();
            (await ctx.Set<ProductStock>().AsNoTracking().CountAsync(s => s.product_id == urunId))
                .Should().Be(1, "gecerli bir stok satiri YAZILABILMELI");

            // ASIL OLCUM: var olmayan bir urune stok satiri eklemek DB tarafindan reddedilmeli.
            // Bu, uygulama katmanindaki bir kontrol DEGIL - yapisal engel.
            await using var ctx2 = NewContext();
            ctx2.Set<ProductStock>().Add(new ProductStock
            {
                product_id = 999_999,
                size = "XL",
                stock_quantity = 5,
                reserved_quantity = 0,
                is_active = true,
                created_at = DateTime.Now
            });

            var hata = await Assert.ThrowsAsync<DbUpdateException>(() => ctx2.SaveChangesAsync());
            var sql = hata.GetBaseException() as SqlException;
            sql.Should().NotBeNull("red VERITABANINDAN gelmeli (uygulama kontrolu degil)");
            sql!.Number.Should().Be(547, "SQL Server FK ihlali kodu 547'dir");
            hata.GetBaseException().Message.Should().Contain("FK_product_stocks_product_id",
                "reddi yapan kisit ADIYLA gorunmeli - teshis kanali kapali kalmasin");

            await using var son = NewContext();
            (await son.Set<ProductStock>().AsNoTracking().AnyAsync(s => s.product_id == 999_999))
                .Should().BeFalse("reddedilen satir YAZILMAMIS olmali");
        }

        // ── 2) YETIM URETEN YOL KAPALI: URUNU FIZIKSEL SILMEK REDDEDILIR ─────────────────
        //
        // BU PIN ILK YAZIMDA ZAYIFTI - DALGA ICI DENETIMIN 5. KONTROLUNDE YAKALANDI (kendi
        // hatam, kayit): eski hali taze bir `EnsureCreated` veritabaninda yalnizca "yetim
        // sayisi 0" olcuyordu. O sayi FK KALDIRILSA BILE 0 kalirdi, cunku test hicbir yetim
        // URETMIYORDU - yani assert VAKUM YASAGINI ihlal ediyordu. Uretim mutasyonunda
        // birebir gorulduu: FK modelden dusuruldugunde diger iki pin KIRMIZI olurken bu
        // YESIL kaliyordu.
        //
        // Yeni hali, 120 yetimi GERCEKTEN URETEN YOLU olcuyor: Dalga 3'un performans seed
        // temizligi urun satirlarini DOGRUDAN SQL ile silmis, stok satirlarini birakmisti.
        // O yol artik VERITABANI tarafindan kapali - uygulama katmaninda degil.
        [Fact]
        public async Task URUNU_FIZIKSEL_SILMEK_REDDEDILIR_YETIM_URETEN_YOL_KAPALI()
        {
            if (Skipped()) return;
            await using var ctx = NewContext();

            var urunId = await UrunOlusturAsync(ctx);
            ctx.Set<ProductStock>().Add(new ProductStock
            {
                product_id = urunId,
                size = "S",
                stock_quantity = 3,
                reserved_quantity = 0,
                is_active = true,
                created_at = DateTime.Now
            });
            await ctx.SaveChangesAsync();

            // VAKUM KIRICI: stok satirinin GERCEKTEN yazildigi dogrulanir - yazilmamis bir
            // satir icin "silme reddedildi" asserti anlamsiz olurdu.
            (await ctx.Set<ProductStock>().AsNoTracking().CountAsync(s => s.product_id == urunId))
                .Should().Be(1, "on kosul: stok satiri yazilmis olmali");

            // ASIL OLCUM: urun satirini DOGRUDAN SQL ile silmek - Dalga 3'un yaptigi seyin
            // ta kendisi. Uygulama yolu SOFT-delete oldugu icin bu davranis ancak DB
            // duzeyinde engellenebilir.
            await using var ctx2 = NewContext();
            var hata = await Assert.ThrowsAsync<SqlException>(() =>
                ctx2.Database.ExecuteSqlRawAsync("DELETE FROM products WHERE id = {0}", urunId));

            hata.Number.Should().Be(547, "SQL Server FK ihlali kodu 547'dir");
            hata.Message.Should().Contain("FK_product_stocks_product_id",
                "engelleyen kisit ADIYLA gorunmeli");

            // CIFT-ANLAM KIRICI: silme reddedildi diye satirlarin BOZULMADIGI da olculur -
            // yarim silinmis bir durum (urun gitti, stok kaldi) tam olarak kacinilan seydir.
            await using var son = NewContext();
            (await son.Set<Product>().AsNoTracking().AnyAsync(p => p.id == urunId))
                .Should().BeTrue("reddedilen silme urun satirini BOZMAMALI");
            (await son.Set<ProductStock>().AsNoTracking().CountAsync(s => s.product_id == urunId))
                .Should().Be(1, "stok satiri YERINDE kalmali");

            var yetim = await son.Database
                .SqlQuery<int>($@"SELECT COUNT(*) AS Value FROM product_stocks ps
                                  WHERE NOT EXISTS (SELECT 1 FROM products p WHERE p.id = ps.product_id)")
                .SingleAsync();
            yetim.Should().Be(0, "yetim stok satiri OLUSMAMALI - olculen 120 satirin kok sebebi buydu");
        }

        // ── 3) SILME DAVRANISI: RESTRICT, CASCADE DEGIL ───────────────────────────────────
        [Fact]
        public async Task FK_SILME_DAVRANISI_RESTRICT_CASCADE_DEGIL()
        {
            if (Skipped()) return;
            await using var ctx = NewContext();

            var eylem = await ctx.Database
                .SqlQuery<string>($@"SELECT TOP 1 fk.delete_referential_action_desc AS Value
                                     FROM sys.foreign_keys fk
                                     WHERE fk.name = 'FK_product_stocks_product_id'
                                       AND fk.parent_object_id = OBJECT_ID('product_stocks')")
                .SingleOrDefaultAsync();

            eylem.Should().NotBeNull("FK_product_stocks_product_id KURULMUS olmali");
            eylem.Should().Be("NO_ACTION",
                "CASCADE REDDEDILDI: uretimde silme SOFT oldugu icin cascade normal isleyiste HIC "
              + "atesLENMEZ - yalnizca dogrudan-SQL ile fiziksel silme durumunda atesLENIR ve tam da "
              + "durdurulmasi gereken anda stok gecmisini SESSIZCE goturur. products'a isaret eden "
              + "mevcut iki FK de (product_reviews, order_items) NO_ACTION.");
        }

        // ── 4) IKI SEMA KAYNAGI AYNI KISIT ADINDA BULUSUYOR ───────────────────────────────
        // DALGA ICI DENETIM BULGUSU: `database/mssql/01_schema.sql` bu FK'yi ZATEN tanimliyordu
        // (satir 653) - eksik olan EF tarafiydi. Ilk yazimda EF'in urettigi VARSAYILAN ad
        // (`FK_product_stocks_products_product_id`) kullanilmisti; sema dosyasindan kurulmus bir
        // veritabaninda migration IKINCI, GEREKSIZ bir kisit yaratirdi. Ad hizalandi.
        [Fact]
        public void KISIT_ADI_DEPLOY_SEMA_DOSYASIYLA_ORTUSUR()
        {
            var kok = new DirectoryInfo(AppContext.BaseDirectory);
            while (kok != null && !File.Exists(Path.Combine(kok.FullName, "database", "mssql", "01_schema.sql")))
                kok = kok.Parent;
            kok.Should().NotBeNull("depo koku bulunmali - sessiz skip YOK");

            var sema = File.ReadAllText(Path.Combine(kok!.FullName, "database", "mssql", "01_schema.sql"));

            // VAKUM KIRICI: dosya gercekten sema dosyasi olmali.
            // NOT (D-SEMA-FIX): 01_schema.sql artik ELLE BAKIMLI degil, EF migration'larindan
            // URETILEN bir artefakt. Uretilen script tablolari KOSELI PARANTEZLE yazar
            // (`CREATE TABLE [product_stocks]`), eski elle yazilan dosya parantezsiz yaziyordu -
            // bu vakum kirici o bicime bagliydi ve dosya degisince kirildi. Assert'in OLCTUGU
            // sey degismedi, yalnizca aradigi bicim guncellendi.
            sema.Should().Contain("CREATE TABLE [product_stocks]", "01_schema.sql gercek sema tanimi olmali");

            sema.Should().Contain("ADD CONSTRAINT FK_product_stocks_product_id",
                "kisit adi deploy artefaktinda da GORUNMELI - D2 migration'inin ham SQL'i uretilen "
              + "script'e oldugu gibi gomulur; ad degisirse burasi da degisir");

            var migrationYolu = Path.Combine(kok.FullName, "Divisima.Dal", "Migrations");
            var migrationHam = Directory.GetFiles(migrationYolu, "*YetimStokReferansButunlugu.cs")
                                        .Where(f => !f.EndsWith(".Designer.cs", StringComparison.Ordinal))
                                        .Select(File.ReadAllText)
                                        .SingleOrDefault();
            migrationHam.Should().NotBeNull("FK migration'i bulunmali");

            // YORUM SATIRLARI AYIKLANIR - AYNI TUZAGA IKINCI KEZ DUSULDU (kayit):
            // Dalga B'de bir pin, kaldirilmis kalibi ALINTILAYAN kendi aciklama yorumuna
            // takilmisti ve ders CLAUDE.md'ye yazilmisti ("kaynak tarayan bir pin, kendi
            // belgeledigi kalibi da tarar"). Bu pin ilk yazimda AYNI SEKILDE kirildi:
            // migration'in yorumu, KULLANILMAYAN EF varsayilan adini gerekce olarak
            // alintiliyor. Tarama artik KOD uzerinde yapiliyor.
            var migration = string.Join("\n",
                migrationHam!.Split('\n').Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

            migration.Should().Contain("ADD CONSTRAINT FK_product_stocks_product_id",
                "kisit sema dosyasindakiyle AYNI adla kurulmali");
            migration.Should().NotContain("FK_product_stocks_products_product_id",
                "EF'in urettigi varsayilan ad KULLANILMAMALI - sema dosyasiyla ayrisirdi");

            // GUARD: kisit zaten varsa migration ikinci bir tane EKLEMEMELI.
            migration.Should().Contain("IF NOT EXISTS", "kisit varlik guard'i olmali (iki saglama yolu ayni kisitta bulusur)");
        }
    }
}
