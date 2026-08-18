using Divisima.Core.Utilities.Pricing;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // Açıklayıcı yorum: İADE BÖLME testleri - kart/cüzdan payına doğru bölünme (fazla-iade regresyonu).
    public class PaymentRefundSplitTests
    {
        [Fact]
        public void SplitRefund_FullOnline_AllToCard()
        {
            // Cüzdan kullanılmadı -> tümü karta
            var (online, credit) = PricingHelper.SplitRefund(1000m, 0m, 1000m);
            online.Should().Be(1000m);
            credit.Should().Be(0m);
        }

        [Fact]
        public void SplitRefund_FullWallet_AllToCredit()
        {
            // Tamamen cüzdanla ödendi -> karta 0, hepsi krediye
            var (online, credit) = PricingHelper.SplitRefund(1000m, 1000m, 1000m);
            online.Should().Be(0m);
            credit.Should().Be(1000m);
        }

        [Theory]
        [InlineData(1000, 300, 1000, 700, 300)]   // 300 cüzdan / 700 kart -> tam iade
        [InlineData(1000, 300, 500, 350, 150)]    // kısmi iade orantılı
        [InlineData(1000, 500, 200, 100, 100)]    // yarı yarıya
        [InlineData(2000, 800, 1000, 600, 400)]   // 40% cüzdan
        public void SplitRefund_ProportionalSplit(int total, int credit_used, int refund, int expOnline, int expCredit)
        {
            var (online, credit) = PricingHelper.SplitRefund(total, credit_used, refund);
            online.Should().Be(expOnline);
            credit.Should().Be(expCredit);
        }

        [Fact]
        public void SplitRefund_SumEqualsRefund_NoOverRefund()
        {
            // KRİTİK: online + kredi == iade tutarı (asla fazla-iade olmamalı)
            var (online, credit) = PricingHelper.SplitRefund(1337m, 442m, 899m);
            (online + credit).Should().Be(899m);
        }

        [Fact]
        public void SplitRefund_ZeroTotal_DoesNotDivideByZero()
        {
            var (online, credit) = PricingHelper.SplitRefund(0m, 0m, 100m);
            (online + credit).Should().Be(100m);   // güvenli fallback (tümü online)
        }
    }
}
