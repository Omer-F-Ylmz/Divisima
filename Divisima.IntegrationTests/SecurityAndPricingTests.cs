using System;
using System.Collections.Generic;
using System.Linq;
using Divisima.Core.Security.Hashing;
using Divisima.Core.Security.Tokens;
using Divisima.Core.Utilities.Pricing;
using Divisima.Core.Utilities.Sanitization;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // Açıklayıcı yorum: Güvenlik-kritik saf fonksiyonların GERÇEK C# testleri (I/O yok, hızlı).
    // Kripto token üreteci, şifre hash'leme, XSS temizleme, SSRF koruması, indirim penceresi.
    public class SecureTokenGeneratorTests
    {
        [Fact]
        public void Generate_ProducesUniqueTokens()
        {
            // 1000 token üret - hepsi benzersiz olmalı (çakışma = zayıf entropi)
            var tokens = Enumerable.Range(0, 1000).Select(_ => SecureTokenGenerator.Generate()).ToList();
            tokens.Distinct().Count().Should().Be(1000);
        }

        [Fact]
        public void Generate_IsUrlSafe_NoPlusSlashEquals()
        {
            // URL/e-posta'da taşınır: +, /, = OLMAMALI
            for (int i = 0; i < 100; i++)
            {
                var token = SecureTokenGenerator.Generate();
                token.Should().NotContain("+");
                token.Should().NotContain("/");
                token.Should().NotContain("=");
            }
        }

        [Theory]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(64)]
        public void Generate_RespectsEntropyLength(int byteLength)
        {
            // base64: her 3 bayt -> 4 karakter. En az byteLength kadar entropi taşımalı.
            var token = SecureTokenGenerator.Generate(byteLength);
            token.Length.Should().BeGreaterOrEqualTo(byteLength);   // padding kırpıldığı için >=
        }
    }

    public class HashingHelperTests
    {
        [Fact]
        public void CreatePasswordHash_UsesRandomSalt_DifferentEachTime()
        {
            // Aynı şifre iki kez hash'lenince salt (ve dolayısıyla hash) FARKLI olmalı (rainbow-table savunması)
            HashingHelper.CreatePasswordHash("AyniSifre123", out var hash1, out var salt1);
            HashingHelper.CreatePasswordHash("AyniSifre123", out var hash2, out var salt2);
            salt1.Should().NotBeEquivalentTo(salt2);
            hash1.Should().NotBeEquivalentTo(hash2);
        }

        [Fact]
        public void VerifyPasswordHash_RoundTrip_Succeeds()
        {
            HashingHelper.CreatePasswordHash("DogruSifre!", out var hash, out var salt);
            HashingHelper.VerifyPasswordHash("DogruSifre!", hash, salt).Should().BeTrue();
        }

        [Fact]
        public void VerifyPasswordHash_WrongPassword_Fails()
        {
            HashingHelper.CreatePasswordHash("DogruSifre!", out var hash, out var salt);
            HashingHelper.VerifyPasswordHash("YanlisSifre!", hash, salt).Should().BeFalse();
        }
    }

    public class InputSanitizerExtraTests
    {
        [Theory]
        [InlineData("<script>alert('xss')</script>Merhaba")]
        [InlineData("<img src=x onerror=alert(1)>")]
        [InlineData("javascript:alert(document.cookie)")]
        public void Sanitize_RemovesDangerousContent(string malicious)
        {
            var clean = InputSanitizer.Sanitize(malicious);
            // Script etiketi ve tehlikeli şema temizlenmeli
            clean.ToLowerInvariant().Should().NotContain("<script");
            clean.ToLowerInvariant().Should().NotContain("javascript:");
        }

        [Fact]
        public void Sanitize_PreservesNormalText()
        {
            // Zararsız metin bozulmamalı
            InputSanitizer.Sanitize("Ayşe Yılmaz").Should().Contain("Ayşe");
        }
    }

    public class UrlValidatorExtraTests
    {
        [Theory]
        [InlineData("https://divisima.com/callback")]
        [InlineData("https://api.iyzico.com/payment")]
        public void IsSafePublicHttpsUrl_AllowsPublicHttps(string url)
        {
            UrlValidator.IsSafePublicHttpsUrl(url).Should().BeTrue();
        }

        [Theory]
        [InlineData("http://divisima.com")]              // https değil
        [InlineData("https://localhost/x")]              // yerel (SSRF)
        [InlineData("https://127.0.0.1/x")]              // loopback (SSRF)
        [InlineData("https://192.168.1.1/admin")]        // özel ağ (SSRF)
        [InlineData("https://169.254.169.254/latest")]   // cloud metadata (SSRF - kritik)
        [InlineData("ftp://divisima.com")]               // https değil
        [InlineData("not-a-url")]
        public void IsSafePublicHttpsUrl_BlocksUnsafeAndInternal(string url)
        {
            // SSRF savunması: sadece dış HTTPS; yerel/özel/metadata adresleri reddedilmeli
            UrlValidator.IsSafePublicHttpsUrl(url).Should().BeFalse();
        }
    }

    public class PricingHelperExtraTests
    {
        private static readonly DateTime Now = new DateTime(2026, 7, 20, 12, 0, 0);

        [Fact]
        public void EffectivePrice_NoSale_ReturnsFullPrice()
        {
            PricingHelper.EffectivePrice(1200m, null, null, null, Now).Should().Be(1200m);
        }

        [Fact]
        public void EffectivePrice_ActiveSaleWindow_ReturnsSalePrice()
        {
            var start = Now.AddDays(-1);
            var end = Now.AddDays(1);
            PricingHelper.EffectivePrice(1200m, 900m, start, end, Now).Should().Be(900m);
        }

        [Fact]
        public void EffectivePrice_ExpiredSale_ReturnsFullPrice()
        {
            // İndirim penceresi geçmiş -> tam fiyat (indirim sızması olmamalı)
            var start = Now.AddDays(-10);
            var end = Now.AddDays(-1);
            PricingHelper.EffectivePrice(1200m, 900m, start, end, Now).Should().Be(1200m);
        }

        [Fact]
        public void IsOnSale_ActiveWindow_True()
        {
            PricingHelper.IsOnSale(900m, Now.AddDays(-1), Now.AddDays(1), Now).Should().BeTrue();
        }

        [Fact]
        public void IsOnSale_NoSalePrice_False()
        {
            PricingHelper.IsOnSale(null, Now.AddDays(-1), Now.AddDays(1), Now).Should().BeFalse();
        }
    }
}
