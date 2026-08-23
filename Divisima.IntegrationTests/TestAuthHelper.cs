using System.Net.Http.Headers;
using System.Net.Http.Json;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Divisima.IntegrationTests
{
    // Açıklayıcı yorum: GENEL test auth yardımcısı - tek kullanımlık değil; yetki isteyen her test
    // dalgası bunu kullanır. Yetkili bir müşteri HttpClient'ı üretir.
    //
    // TASARIM: token GERÇEK uçlardan alınır (register -> verify-email -> login). Token üretiminin
    // iç detaylarına (JwtHelper, imzalama anahtarı, claim şeması) bağımlılık YOK; uygulama token
    // üretimini değiştirirse bu yardımcı kendiliğinden doğru kalır.
    //
    // TEK sapma: doğrulama token'ı e-posta yerine DB'den okunuyor. Sebebi e-posta TESLİMİ'nin dış
    // bağımlılık olması (CI'da SMTP yok). Token'ın kendisi yine UYGULAMANIN ürettiği değerdir -
    // uydurulmuş/kopyalanmış bir değer değil - ve doğrulama GERÇEK /api/auth/verify-email ucundan
    // yapılır. Yani akışın hiçbir adımı atlanmıyor, yalnız "postayı okuma" kısmı kısa devre.
    public static class TestAuthHelper
    {
        // Şifre politikası (CustomerRegisterRequestValidator): >=8 karakter, büyük+küçük harf, rakam.
        // PUBLIC: hesap silme sonrası "artık giriş yapılamıyor" gibi testler aynı şifreyi
        // tekrar kullanmak zorunda; sabiti kopyalamak yerine buradan okunur.
        public const string TestPassword = "TestPass123";

        public sealed record AuthenticatedCustomer(int CustomerId, string Email, string Token, HttpClient Client);

        public static async Task<AuthenticatedCustomer> CreateCustomerClientAsync(WebApplicationFactory<Program> factory)
        {
            var anon = factory.CreateClient();

            // Açıklayıcı yorum: BENZERSİZ alanlar - var olan satırlara güvenilmez, her çağrı kendi
            // kullanıcısını yaratır (testler paralel koşabilir, birbirini ezmemeli).
            var unique = Guid.NewGuid().ToString("N");
            var email = $"test-{unique}@divisima.test";

            // 1) KAYIT (gerçek uç)
            var register = await anon.PostAsJsonAsync("/api/auth/register", new
            {
                name = "Test Musteri",
                email,
                phone = "5550000000",                 // validator: ^[0-9+\s()-]{7,20}$
                password = TestPassword,
                accepted_terms = true,
                accepted_privacy = true,
                accepted_marketing = false
            });
            await EnsureAsync(register, "register");

            // 2) E-POSTA DOĞRULAMA - token'ı DB'den al (e-posta teslimi dış bağımlılık), doğrulamayı
            //    GERÇEK uçtan yap. Kayıt e-postayı küçük harfe çeviriyor (AuthManager), o yüzden
            //    aramada da küçük harf kullanılıyor.
            string verificationToken;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DivisimaDbContext>();
                var lowered = email.ToLowerInvariant();
                var customer = await db.Set<Customer>().AsNoTracking()
                    .FirstOrDefaultAsync(c => c.email == lowered)
                    ?? throw new InvalidOperationException($"Kayit sonrasi musteri bulunamadi: {lowered}");

                verificationToken = customer.email_verification_token
                    ?? throw new InvalidOperationException("email_verification_token bos - kayit akisi degismis olabilir.");
            }

            var verify = await anon.GetAsync($"/api/auth/verify-email?token={Uri.EscapeDataString(verificationToken)}");
            await EnsureAsync(verify, "verify-email");

            // 3) GİRİŞ (gerçek uç) -> gerçek JWT
            var login = await anon.PostAsJsonAsync("/api/auth/login", new { email, password = TestPassword });
            await EnsureAsync(login, "login");

            var envelope = await login.Content.ReadFromJsonAsync<LoginEnvelope>()
                ?? throw new InvalidOperationException("Login yaniti cozulemedi.");
            var data = envelope.data
                ?? throw new InvalidOperationException("Login yanitinda 'data' yok.");
            if (string.IsNullOrWhiteSpace(data.token))
                throw new InvalidOperationException("Login yanitinda token bos.");

            var authed = factory.CreateClient();
            authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.token);
            return new AuthenticatedCustomer(data.customer_id, email, data.token!, authed);
        }

        // Açıklayıcı yorum: Hata durumunda gövdeyi de mesaja koy - CI'da adım kırmızı düşerse
        // sebebi assert mesajından görülebilsin.
        private static async Task EnsureAsync(HttpResponseMessage response, string step)
        {
            if (response.IsSuccessStatusCode) return;
            // MASKELEME URETIM NOKTASINDA (CLAUDE.md bolum 1): bu yardimci register/verify/LOGIN
            // adimlarini kosuyor; basarisiz login yaniti JWT tasiyabilir ve bu mesaj DOGRUDAN CI
            // ciktisina duser. Elle kirpmaya guvenilmez - jeton benzeri her dizge burada kirpilir.
            var body = Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(
                await response.Content.ReadAsStringAsync());
            throw new InvalidOperationException(
                $"TestAuthHelper: '{step}' adimi basarisiz. HTTP {(int)response.StatusCode} {response.StatusCode}. Govde: {body}");
        }

        private sealed class LoginEnvelope { public LoginData? data { get; set; } }
        private sealed class LoginData
        {
            public int customer_id { get; set; }
            public string? token { get; set; }
        }
    }
}
