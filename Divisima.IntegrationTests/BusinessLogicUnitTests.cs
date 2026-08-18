using System;
using Divisima.Core.Utilities.Pricing;
using Divisima.Core.Utilities.Sanitization;
using Divisima.Core.Utilities.Shipping;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // Açıklayıcı yorum: SAF iş-mantığı unit testleri - I/O yok, DB yok, auth yok, container yok.
    // Hızlı ve deterministik; deyim mantığının doğruluğunu Python simülasyonu değil GERÇEK C# ile doğrular.
    public class MoneyHelperTests
    {
        [Theory]
        // Açıklayıcı yorum: Ticari yuvarlama away-from-zero olmalı (banker's rounding DEĞİL).
        // 2.345 -> away-from-zero 2.35 (banker's 2.34 verirdi; bu test regresyonu yakalar).
        // String girdi -> kesin decimal (double->decimal dönüşüm kaymasını önler).
        [InlineData("2.345", "2.35")]
        [InlineData("2.344", "2.34")]
        [InlineData("2.346", "2.35")]
        [InlineData("1.005", "1.01")]
        [InlineData("10.00", "10.00")]
        [InlineData("0", "0")]
        public void Round_UsesAwayFromZero(string input, string expected)
        {
            var inp = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
            var exp = decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);
            MoneyHelper.Round(inp).Should().Be(exp);
        }

        [Theory]
        [InlineData("100", "10", "10.00")]      // %10 = 10.00
        [InlineData("99.99", "15", "15.00")]    // 99.99*0.15 = 14.9985 -> 15.00
        [InlineData("250", "20", "50.00")]      // %20 = 50.00
        [InlineData("33.33", "33", "11.00")]    // 33.33*0.33 = 10.9989 -> 11.00
        public void Percentage_RoundsCorrectly(string baseAmount, string percent, string expected)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            MoneyHelper.Percentage(decimal.Parse(baseAmount, ic), decimal.Parse(percent, ic))
                .Should().Be(decimal.Parse(expected, ic));
        }

        [Fact]
        public void Add_SumsAndRounds()
        {
            MoneyHelper.Add(1.11m, 2.22m, 3.33m).Should().Be(6.66m);
            MoneyHelper.Add(0.1m, 0.2m).Should().Be(0.30m); // klasik float sorunu decimal'de yok
        }
    }

    public class PricingHelperTests
    {
        private static readonly DateTime Now = new(2024, 6, 15, 12, 0, 0);

        [Fact]
        public void EffectivePrice_NoSalePrice_ReturnsRegular()
        {
            PricingHelper.EffectivePrice(500m, null, null, null, Now).Should().Be(500m);
        }

        [Fact]
        public void EffectivePrice_ZeroSalePrice_ReturnsRegular()
        {
            PricingHelper.EffectivePrice(500m, 0m, null, null, Now).Should().Be(500m);
        }

        [Fact]
        public void EffectivePrice_ActiveSaleWindow_ReturnsSalePrice()
        {
            // Açıklayıcı yorum: now (15 Haz) pencere içinde (10-20 Haz) -> indirimli
            PricingHelper.EffectivePrice(500m, 350m, new DateTime(2024, 6, 10), new DateTime(2024, 6, 20), Now)
                .Should().Be(350m);
        }

        [Fact]
        public void EffectivePrice_BeforeSaleStart_ReturnsRegular()
        {
            PricingHelper.EffectivePrice(500m, 350m, new DateTime(2024, 6, 20), new DateTime(2024, 6, 25), Now)
                .Should().Be(500m);
        }

        [Fact]
        public void EffectivePrice_AfterSaleEnd_ReturnsRegular()
        {
            PricingHelper.EffectivePrice(500m, 350m, new DateTime(2024, 6, 1), new DateTime(2024, 6, 10), Now)
                .Should().Be(500m);
        }

        [Fact]
        public void IsOnSale_NoWindowButValidSalePrice_ReturnsTrue()
        {
            // Açıklayıcı yorum: start/end yoksa süresiz indirim geçerli
            PricingHelper.IsOnSale(350m, null, null, Now).Should().BeTrue();
        }
    }

    public class DeliveryEstimatorTests
    {
        [Theory]
        // Açıklayıcı yorum: 2024-01-01 Pazartesi. İş günü eklerken Cmt/Paz atlanır.
        [InlineData("2024-01-01", 1, "2024-01-02")] // Pzt +1 = Salı
        [InlineData("2024-01-01", 3, "2024-01-04")] // Pzt +3 = Perşembe
        [InlineData("2024-01-01", 5, "2024-01-08")] // Pzt +5 = ertesi Pzt (hafta sonu atlandı)
        [InlineData("2024-01-05", 1, "2024-01-08")] // Cuma +1 = Pzt (Cmt/Paz atlandı)
        [InlineData("2024-01-06", 1, "2024-01-08")] // Cmt +1 = Pzt
        public void AddBusinessDays_SkipsWeekends(string start, int days, string expected)
        {
            var result = DeliveryEstimator.AddBusinessDays(DateTime.Parse(start), days);
            result.Date.Should().Be(DateTime.Parse(expected).Date);
        }

        [Fact]
        public void Estimate_ReturnsThreeToFiveBusinessDayWindow()
        {
            var monday = new DateTime(2024, 1, 1);
            var (earliest, latest) = DeliveryEstimator.Estimate(monday);
            earliest.Date.Should().Be(new DateTime(2024, 1, 4).Date);  // +3 iş günü
            latest.Date.Should().Be(new DateTime(2024, 1, 8).Date);    // +5 iş günü
            latest.Should().BeAfter(earliest);
        }
    }

    public class UrlValidatorTests
    {
        [Theory]
        [InlineData("https://divisima.com", true)]
        [InlineData("https://api.iyzipay.com/callback", true)]
        [InlineData("http://divisima.com", false)]             // https değil
        [InlineData("https://localhost", false)]               // localhost
        [InlineData("https://127.0.0.1", false)]               // loopback
        [InlineData("https://169.254.169.254/latest/meta-data", false)] // cloud metadata (SSRF)
        [InlineData("https://192.168.1.10", false)]            // özel ağ
        [InlineData("https://10.0.0.5", false)]                // özel ağ
        [InlineData("https://172.16.5.5", false)]              // özel ağ
        [InlineData("ftp://divisima.com", false)]              // şema geçersiz
        [InlineData("", false)]
        [InlineData("saçma metin", false)]
        public void IsSafePublicHttpsUrl_BlocksUnsafeTargets(string url, bool expected)
        {
            UrlValidator.IsSafePublicHttpsUrl(url).Should().Be(expected);
        }
    }

    public class InputSanitizerTests
    {
        [Fact]
        public void Sanitize_RemovesScriptTag()
        {
            var result = InputSanitizer.Sanitize("<script>alert('xss')</script>Merhaba");
            result.Should().NotContain("<script");
            result.Should().Contain("Merhaba");
        }

        [Fact]
        public void Sanitize_RemovesDangerousTags()
        {
            InputSanitizer.Sanitize("<iframe src=evil></iframe>").Should().NotContain("<iframe");
            InputSanitizer.Sanitize("<object data=x></object>").Should().NotContain("<object");
        }

        [Fact]
        public void Sanitize_RemovesEventHandlers()
        {
            var result = InputSanitizer.Sanitize("<a onclick=\"steal()\">link</a>");
            result.Should().NotContain("onclick");
            result.Should().Contain("link");
        }

        [Fact]
        public void Sanitize_RemovesJavascriptProtocol()
        {
            InputSanitizer.Sanitize("javascript:alert(1)").Should().NotContain("javascript:");
        }

        [Fact]
        public void Sanitize_PreservesCleanText()
        {
            var clean = "Ürün çok güzel, tavsiye ederim! 5 yıldız.";
            InputSanitizer.Sanitize(clean).Should().Be(clean);
        }

        [Fact]
        public void HtmlEncode_EscapesAngleBrackets()
        {
            InputSanitizer.HtmlEncode("<b>x</b>").Should().Be("&lt;b&gt;x&lt;/b&gt;");
        }
    }
}
