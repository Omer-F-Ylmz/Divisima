using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Integrations.Iyzico;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.Payment;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Divisima.IntegrationTests
{
    // SPRINT 8 MADDE 8 - ODEME INIT HATASINDA AYIRT EDILEBILIR MESAJ
    //
    // OLCULEN VAKA (E2b, gercek Iyzico sandbox): saglayici "@divisima.test" adresini
    // "email hatali format ile gonderilmistir" ile REDDEDIYOR; AYNI musteri example.com
    // adresiyle 200 aliyor. Bizim kayit validatorumuz ise FluentValidation'in permisif
    // ".EmailAddress()" kuralini kullaniyor ve ".test" gibi RFC 2606 ayrilmis ust alan
    // adlarini KABUL EDIYOR. Sonuc: bizim kabul ettigimiz bir e-posta ile uye olan musteri
    // HIC kart odemesi yapamiyor - ve ekranda yalnizca "Odeme baslatilamadi." goruyor.
    //
    // NE YAPILDI: sebep KENDIMIZ tespit ediliyor (teslim edilemez ust alan adi), saglayicinin
    // ham hata metni ne musteriye yansitiliyor ne de METIN ESLESTIRMESI yapiliyor - yabanci bir
    // API'nin dizgesine bagimli olmak kirilgan olurdu. Diger tum init hatalari eski genel
    // mesajda kaliyor; yanlis teshis vermiyoruz.
    //
    // NE YAPILMADI (SUPHELI - karar kullanicinin): KAYIT VALIDATORU sikilastirilmadi. ".test"
    // gibi adresleri kayitta reddetmek ayri bir urun karari; gecerli ama alisilmadik adresleri
    // kapida cevirmek gercek musteri kaybettirebilir.
    [Trait("Category", "Sql")]
    public class PaymentInitMessageTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaPaymentInitMsgTest";
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

        // Init'i HER ZAMAN reddeden istemci - gercek Iyzico'nun "email hatali format" reddini
        // taklit eder. Digerleri cagrilmaz; cagrilirsa GURULTULU duser.
        private sealed class ReddedenIyzicoClient : IIyzicoClient
        {
            public Task<IyzicoCheckoutInitResult> InitializeCheckoutFormAsync(IyzicoCheckoutInitRequest request)
                => Task.FromResult(new IyzicoCheckoutInitResult
                {
                    Success = false,
                    ErrorMessage = "email hatali format ile gonderilmistir"
                });

            public bool VerifyCallbackSignature(string token, string signature)
                => throw new NotSupportedException("Bu testte kullanilmaz.");
            public Task<IyzicoPaymentResult> RetrievePaymentResultAsync(string token)
                => throw new NotSupportedException("Bu testte kullanilmaz.");
            public Task<IyzicoRefundResult> RefundAsync(string paymentTransactionId, decimal amount)
                => throw new NotSupportedException("Bu testte kullanilmaz.");
        }

        private sealed class InitFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.UseSetting("Iyzico:UseRealSdk", "false");
                builder.UseSetting("Iyzico:CallbackUrl", "https://api.divisima.test/api/payment/callback");
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                    // IIyzicoClient Program.cs'te ServiceCollection'a kayitli - buradaki kayit SONRA
                    // geldigi icin kazanir (Autofac modulundeki servisler icin ayni sey GECERLI DEGIL).
                    services.AddScoped<IIyzicoClient, ReddedenIyzicoClient>();
                });
            }
        }

        private InitFactory? _factory;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            try
            {
                await using (var pre = NewContext())
                {
                    await TestDbKurulum.SilAsync(pre.Database);
                    await TestDbKurulum.OlusturAsync(pre.Database);
                }
                _factory = new InitFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak odeme init mesaj testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (_factory != null) await _factory.DisposeAsync();
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await TestDbKurulum.SilAsync(ctx.Database); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        // Verilen e-postali musteri + odenmeyi bekleyen Pending siparis uretir.
        private static async Task<(int CustomerId, int OrderId)> MusteriVeSiparisAsync(string eposta)
        {
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            await using var ctx = NewContext();

            var m = new Customer
            {
                name = "Init Musteri " + damga,
                email = eposta,
                phone = "5550000000",
                password_hash = new byte[] { 1 },
                password_salt = new byte[] { 2 },
                user_type = (byte)UserTypeEnum.Customer,
                is_active = true,
                email_verified = true,
                created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(m);
            await ctx.SaveChangesAsync();

            var o = new Order
            {
                customer_id = m.id,
                order_number = "DVS" + DateTime.Now.ToString("yyyyMMdd") + "-" + damga,
                status = (byte)OrderStatusEnum.Pending,
                subtotal = 100m,
                discount_amount = 0m,
                shipping_cost = 0m,
                total_price = 100m,
                currency = "TRY",
                payment_type = 0,
                created_at = DateTime.Now
            };
            ctx.Set<Order>().Add(o);
            await ctx.SaveChangesAsync();
            return (m.id, o.id);
        }

        private async Task<(HttpStatusCode, string)> InitDeneAsync(int customerId, int orderId)
        {
            using var scope = _factory!.Services.CreateScope();
            var r = await scope.ServiceProvider.GetRequiredService<IPaymentService>()
                .Initialize(new PaymentInitRequestDto { order_id = orderId }, customerId);
            return (r.Item1, r.Item2.Message ?? "");
        }

        // ── 1) TESLIM EDILEMEZ ADRES: AYIRT EDILEBILIR MESAJ ─────────────────────────
        [Fact]
        [Trait("Category", "Sql")]
        public async Task InitHatasi_TESLIM_EDILEMEZ_EPOSTADA_AYIRT_EDILEBILIR_MESAJ_Doner()
        {
            if (Skipped()) return;

            var (musteri, siparis) = await MusteriVeSiparisAsync($"init-{Guid.NewGuid():N}@divisima.test");
            var (kod, mesaj) = await InitDeneAsync(musteri, siparis);

            kod.Should().Be(HttpStatusCode.BadRequest);
            mesaj.Should().Contain("e-posta",
                "musteri NE OLDUGUNU gormeli - E2b'de yalnizca 'Odeme baslatilamadi.' goruluyordu");
            mesaj.Should().NotBe("Ödeme başlatılamadı.", "genel mesaj bu dalda YETERSIZ");
            mesaj.Should().NotContain("hatali format",
                "saglayicinin HAM hata metni musteriye YANSITILMAMALI");
        }

        // ── 2) CIFT-ANLAM KIRICI: BASKA SEBEPLERDE GENEL MESAJ KALIR ─────────────────
        //
        // Bu olmadan 1. pin, "her init hatasinda e-posta mesaji doner" durumunda da yesil
        // kalirdi - yani YANLIS TESHIS veren bir uygulama da testi gecerdi.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task InitHatasi_GECERLI_EPOSTADA_GENEL_MESAJ_KALIR()
        {
            if (Skipped()) return;

            var (musteri, siparis) = await MusteriVeSiparisAsync($"init-{Guid.NewGuid():N}@example.com");
            var (kod, mesaj) = await InitDeneAsync(musteri, siparis);

            kod.Should().Be(HttpStatusCode.BadRequest);
            mesaj.Should().Be("Ödeme başlatılamadı.",
                "sebep e-posta DEGILSE eski genel mesaj korunmali - uydurma teshis verilmez");
        }
    }
}
