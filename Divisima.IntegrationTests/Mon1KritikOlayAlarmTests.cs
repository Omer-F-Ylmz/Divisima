using System.Text.Json;
using System.Text.RegularExpressions;
using Divisima.API.Services;
using Divisima.Bussiness.Jobs;
using Divisima.Bussiness.Outbox;
using Divisima.Core.Utilities.Mail;
using Divisima.DataAccess.Concrete.EntityFramework;
using Divisima.Entity.Entities;
using FluentAssertions;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ MON-1 / D1 - KRITIK OLAY ALARMI: DAVRANIS PINLERI (gercek SQL) ═══════════════════════
    //
    // Is GERCEK `EfSecurityEventDal` + GERCEK `OutboxService` ile kosar; olcum outbox satirinin
    // KENDISIDIR (mail kanalinin girdisi). Imlec bu siniftaki testlerde bellekte tutulur - kalici
    // Hangfire uygulamasinin kendi pini ayri (`HANGFIRE_IMLECI_...`), gercek Hangfire deposuyla.
    //
    // IZOLASYON: siniftaki testler ayni DB'yi paylasir; her test IS'I ONCE BIR KEZ KOSTURUP
    // tabani kurar (onceki testlerin satirlari o tabanin altinda kalir) ve outbox'u KENDI
    // benzersiz alici adresiyle sayar.
    [Trait("Category", "Sql")]
    public class Mon1KritikOlayAlarmTests : SqlBackedTestBase
    {
        protected override string DatabaseName => "DivisimaMon1AlarmTest";

        private sealed class BellekImleci : IAlarmImleci
        {
            public int? Deger;
            public Task<int?> OkuAsync() => Task.FromResult(Deger);
            public Task YazAsync(int sonId) { Deger = sonId; return Task.CompletedTask; }
        }

        // Yerlesme payinin DISINDA kalan (okunabilir) zaman damgasi.
        private static DateTime Yerlesmis => DateTime.Now - KritikOlayAlarmJob.YerlesmePayi - TimeSpan.FromMinutes(1);

        private async Task<int> KosAsync(IAlarmImleci imlec, string? alici)
        {
            await using var ctx = NewContext();
            var ayar = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { [KritikOlayAlarmJob.AliciAnahtari] = alici })
                .Build();
            var job = new KritikOlayAlarmJob(new EfSecurityEventDal(ctx), new OutboxService(new EfOutboxMessageDal(ctx)),
                imlec, ayar, NullLogger<KritikOlayAlarmJob>.Instance);
            return await job.RunAsync();
        }

        private async Task<int> OlayYazAsync(string tip, string severity, DateTime? zaman = null,
            string? ip = null, string? detay = null)
        {
            await using var ctx = NewContext();
            var olay = new SecurityEvent
            {
                event_type = tip,
                severity = severity,
                ip_address = ip,
                detail = detay,
                created_at = zaman ?? Yerlesmis,
            };
            ctx.Set<SecurityEvent>().Add(olay);
            await ctx.SaveChangesAsync();
            return olay.id;
        }

        private async Task<List<MailMessageDto>> AlarmMailleriAsync(string alici)
        {
            await using var ctx = NewContext();
            var satirlar = await ctx.Set<OutboxMessage>().AsNoTracking()
                .Where(m => m.event_type == "EmailNotification").ToListAsync();
            return satirlar.Select(m => JsonSerializer.Deserialize<MailMessageDto>(m.payload)!)
                .Where(m => m.To == alici).ToList();
        }

        private static string YeniAlici() => $"mon1-{Guid.NewGuid():N}@divisima.test";

        [Fact]
        public async Task YENI_CRITICAL_TEK_ALARM_MAILI_YAZAR_ve_GOVDE_AYRINTI_TASIMAZ()
        {
            if (Skipped()) return;
            var alici = YeniAlici();
            var imlec = new BellekImleci();
            (await KosAsync(imlec, alici)).Should().Be(0, "ilk kosum yalniz taban kurar");

            var kilit = await OlayYazAsync("AccountLocked", "Critical", ip: "203.0.113.77", detay: "SIZMAMALI-detay-mon1");
            var imza = await OlayYazAsync("PaymentSignatureInvalid", "Warning");
            await OlayYazAsync("LoginFailed", "Warning"); // izlenmeyen tip

            (await KosAsync(imlec, alici)).Should().Be(1, "yeni bir Critical olay TEK alarm maili uretmeli");

            var mailler = await AlarmMailleriAsync(alici);
            mailler.Should().HaveCount(1);
            var mail = mailler[0];
            mail.Subject.Should().StartWith("Divisima ALARM", "kabul olcutu: konu 'Divisima ALARM'");
            mail.Body.Should().Contain($"AccountLocked | 1 | {kilit}");
            mail.Body.Should().Contain($"PaymentSignatureInvalid | 1 | {imza}",
                "ayni turdaki izlenen Warning olay da ozete girer");
            mail.Body.Should().NotContain("LoginFailed", "izlenen bes tip DISINDAKI olay ozete girmez");
            mail.Body.Should().NotContain("203.0.113.77", "IP govdeye GIRMEZ");
            mail.Body.Should().NotContain("SIZMAMALI", "detail govdeye GIRMEZ");

            imlec.Deger.Should().Be(imza, "imlec bu turda islenen en buyuk izlenen id'ye ilerler");
        }

        [Fact]
        public async Task IKINCI_TUR_YENI_OLAY_YOKSA_MAIL_YAZMAZ_IDEMPOTENT()
        {
            if (Skipped()) return;
            var alici = YeniAlici();
            var imlec = new BellekImleci();
            await KosAsync(imlec, alici);
            await OlayYazAsync("RefreshTokenReuse", "Critical");

            (await KosAsync(imlec, alici)).Should().Be(1, "vakum kirici: ilk tur gercekten alarm uretmeli");
            (await KosAsync(imlec, alici)).Should().Be(0, "ayni olay IKINCI kez bildirilmez");
            (await AlarmMailleriAsync(alici)).Should().HaveCount(1);
        }

        [Fact]
        public async Task YALNIZ_WARNING_MAIL_URETMEZ_ama_IMLEC_ILERLER()
        {
            if (Skipped()) return;
            var alici = YeniAlici();
            var imlec = new BellekImleci();
            await KosAsync(imlec, alici);
            var taban = imlec.Deger;
            var uyari = await OlayYazAsync("ProductImportRejected", "Warning");

            (await KosAsync(imlec, alici)).Should().Be(0, "tarif: tetik YENI CRITICAL");
            (await AlarmMailleriAsync(alici)).Should().BeEmpty();
            imlec.Deger.Should().Be(uyari, "Warning islendi sayilir; sonraki Critical ozetinde tekrar sayilmaz");
            imlec.Deger.Should().NotBe(taban);
        }

        [Fact]
        public async Task ALICI_BOSSA_MAIL_YOK_IMLEC_ILERLEMEZ_ve_VERILINCE_BEKLEYEN_BILDIRILIR()
        {
            if (Skipped()) return;
            var alici = YeniAlici();
            var imlec = new BellekImleci();
            await KosAsync(imlec, alici);
            var taban = imlec.Deger;
            var olay = await OlayYazAsync("PaymentAfterTerminal", "Critical");

            (await KosAsync(imlec, "")).Should().Be(0);
            imlec.Deger.Should().Be(taban, "alici yokken imlec ILERLERSE olay kalici olarak kaybolur");

            (await KosAsync(imlec, alici)).Should().Be(1, "alici verilince bekleyen Critical bildirilir");
            (await AlarmMailleriAsync(alici)).Single().Body.Should().Contain($"PaymentAfterTerminal | 1 | {olay}");
        }

        [Fact]
        public async Task YERLESME_PAYINDAN_TAZE_OLAY_BU_TURDA_OKUNMAZ_SONRAKI_TURDA_OKUNUR()
        {
            if (Skipped()) return;
            var alici = YeniAlici();
            var imlec = new BellekImleci();
            await KosAsync(imlec, alici);
            var taban = imlec.Deger;
            var olay = await OlayYazAsync("AccountLocked", "Critical", zaman: DateTime.Now);

            (await KosAsync(imlec, alici)).Should().Be(0, "commit'i henuz yerlesmemis olabilecek satir okunmaz");
            imlec.Deger.Should().Be(taban, "imlec taze satirin id'sini GECMEMELI - gecerse o satir kacar");

            await using (var ctx = NewContext())
                await ctx.Set<SecurityEvent>().Where(e => e.id == olay)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.created_at, Yerlesmis));

            (await KosAsync(imlec, alici)).Should().Be(1, "pay dolunca ayni olay bildirilir");
        }

        [Fact]
        public async Task ILK_KOSUM_TABAN_KURAR_GECMIS_CRITICAL_BILDIRILMEZ()
        {
            if (Skipped()) return;
            var alici = YeniAlici();
            var gecmis = await OlayYazAsync("AccountLocked", "Critical");
            var imlec = new BellekImleci();

            (await KosAsync(imlec, alici)).Should().Be(0);
            (await AlarmMailleriAsync(alici)).Should().BeEmpty();
            imlec.Deger.Should().NotBeNull("ilk kosum imleci YAZMALI - yazmazsa her tur 'ilk kosum' olur ve hic alarm gitmez");
            imlec.Deger!.Value.Should().BeGreaterThanOrEqualTo(gecmis);
        }

        // Kalici imlec: GERCEK Hangfire SQL deposu, IKI ayri depo ornegi ("yeniden baslatma").
        [Fact]
        public async Task HANGFIRE_IMLECI_YENI_DEPO_ORNEGINDE_KORUNUR()
        {
            if (Skipped()) return;
            var ilk = new HangfireAlarmImleci(new SqlServerStorage(ConnStr));
            (await ilk.OkuAsync()).Should().BeNull("hic yazilmamis imlec null olmali - ilk kosum dali buna bagli");

            await ilk.YazAsync(4242);

            var yeniden = new HangfireAlarmImleci(new SqlServerStorage(ConnStr));
            (await yeniden.OkuAsync()).Should().Be(4242, "deger depoda kalici olmali, surec bellegi degil");
        }

        // KAYNAK-SOZLESME: kayit Program.cs'te. Davranis kaniti canli sunucuda (muhur 60).
        [Fact]
        public void PROGRAM_ALARM_ISINI_5_DAKIKADA_BIR_ve_IMLECI_ARKA_PLAN_DALINDA_KAYDEDER()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "docker-compose.yml"))) d = d.Parent;
            d.Should().NotBeNull("depo koku bulunmali - sessiz skip YOK");
            var program = string.Join("\n", File.ReadAllText(Path.Combine(d!.FullName, "Divisima.API", "Program.cs"))
                .Split('\n').Select(s => { var i = s.IndexOf("//", StringComparison.Ordinal); return i < 0 ? s : s.Substring(0, i); }));

            var kayit = program.Split('\n').SingleOrDefault(s => s.Contains("RecurringJob.AddOrUpdate<Divisima.Bussiness.Jobs.KritikOlayAlarmJob>", StringComparison.Ordinal));
            kayit.Should().NotBeNull("alarm isi recurring olarak kayitli olmali - kayitsiz is HIC kosmaz");
            kayit!.Should().Contain("\"*/5 * * * *\"", "tarif: 5 dakikalik tur");

            var dal = Regex.Match(program, @"if \(arkaPlanIsleri\)\s*\{(.*?)\n\}", RegexOptions.Singleline);
            dal.Success.Should().BeTrue("vakum kirici: ilk arka plan isleri dali bulunmali");
            dal.Groups[1].Value.Should().Contain("AddSingleton<Divisima.Bussiness.Jobs.IAlarmImleci, Divisima.API.Services.HangfireAlarmImleci>",
                "imlec Hangfire deposuna bagli; depo yalniz bu dalda kayitli");
        }
    }
}
