using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ META-PIN: TESTLERIN KOSTUGU VERITABANI COLLATION'I ═════════════════════════════════
    //
    // Bu sinif bir URUN davranisini degil, OLCUM ORTAMININ KENDISINI pinler.
    //
    // NEDEN GEREKLI (kalite supurmesi Dalga 1'de olculdu):
    // Turkce collation'da 'i' ile 'I' AYNI HARF DEGILDIR. Olculdu (Turkish_CI_AS):
    //     'irem' = 'IREM'  -> FARKLI      (cift'ler I <-> ı  ve  İ <-> i)
    // Latin1 collation'da ise ayni ifade ESIT doner. Yani KIMLIK/casing hatalari
    // (B1: ayni e-postayla iki hesap, B2: kupon kodu) Latin1 bir veritabaninda HIC GORUNMEZ.
    //
    // CI SQL Server container'i varsayilan olarak SQL_Latin1_General_CP1_CI_AS ile acilir;
    // yerel gelistirme sunucusu ve hedef uretim Turkish_CI_AS. Bu ayrisma OLCULMEMISTI ve
    // sessizdi: B1 sinifi bir regresyonun pini CI'da YALANCI YESIL verirdi.
    // Iki workflow'a MSSQL_COLLATION=Turkish_CI_AS eklendi; BU TEST o ayarin gercekten
    // yururlukte oldugunu dogrular. Ayar bir gun silinir ya da iki workflow ayrisirsa
    // burada GURULTULU kirilir - sessiz kalamaz.
    //
    // KAPSAM SINIRI (durust kayit): test veritabanlari sunucu collation'ini DEVRALARAK
    // olusturuluyor (EnsureCreated), yani bu assert etkin olarak SUNUCU collation'ini olcer.
    // Kolon duzeyinde COLLATE geçersiz kilmalari ayri bir konudur ve burada olculmez.
    [Trait("Category", "Sql")]
    public class CollationMetaPinTests : SqlBackedTestBase
    {
        protected override string DatabaseName => "DivisimaCollationMetaTest";

        private const string BeklenenCollation = "Turkish_CI_AS";

        [Fact]
        public async Task TestVeritabani_TURKISH_CI_AS_Collation_ile_Kosar()
        {
            if (Skipped()) return;
            await using var ctx = NewContext();

            var collation = (await ctx.Database
                .SqlQuery<string>($"SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS nvarchar(128)) AS Value")
                .ToListAsync()).Single();

            collation.Should().Be(BeklenenCollation,
                "testler URETIMLE AYNI collation'da kosmali. Latin1 bir veritabaninda 'i' ile 'I' " +
                "esit sayilir ve B1/B2 sinifi kimlik hatalari GORUNMEZ olur. Bu kirmizi, " +
                "workflow'daki MSSQL_COLLATION ayarinin kaybolduguna ya da iki workflow'un " +
                "ayristigina isaret eder.");
        }

        // CIFT-ANLAM KIRICI: yukaridaki assert yalnizca bir ETIKET okuyor. Etiketin gercekten
        // Turkce KARSILASTIRMA anlamina geldigi ayrica dogrulanir - collation adi dogru ama
        // davranis farkli olsaydi (or. kolon duzeyi override), ilk test yine yesil kalirdi.
        [Fact]
        public async Task TurkceKarsilastirma_GERCEKTEN_YURURLUKTE_i_ile_I_AYNI_DEGIL()
        {
            if (Skipped()) return;
            await using var ctx = NewContext();

            var esitMi = (await ctx.Database
                .SqlQuery<int>($"SELECT CASE WHEN N'irem' = N'IREM' THEN 1 ELSE 0 END AS Value")
                .ToListAsync()).Single();
            esitMi.Should().Be(0,
                "Turkcede 'i' ile 'I' cift DEGIL - esit cikiyorsa Latin1 semantigi yururlukte demektir");

            // DIKKAT: SQL metnine C# ENTERPOLASYONU KOYULMAZ. EF'in SqlQuery'si enterpolasyon
            // deliklerini PARAMETREYE cevirir; dizge LITERALININ icine konulan bir delik
            // bozuk SQL uretir (bu pin yazilirken birebir yasandi: karsilastirma her zaman
            // FARKLI donuyordu). Turkce harf NCHAR ile yazilir.
            var turkceCiftEsitMi = (await ctx.Database
                .SqlQuery<int>($"SELECT CASE WHEN N'IREM' = NCHAR(305) + N'rem' THEN 1 ELSE 0 END AS Value")
                .ToListAsync()).Single();
            turkceCiftEsitMi.Should().Be(1,
                "Turkcede gercek cift I <-> ı'dir; buyuk/kucuk duyarsizlik BU cift uzerinden calismali " +
                "(vakum kirici: her karsilastirmanin FARKLI dondugu bir kurulumda ilk assert de gecerdi)");
        }
    }
}
