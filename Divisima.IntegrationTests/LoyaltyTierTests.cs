using Divisima.Core.Utilities.Loyalty;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // Açıklayıcı yorum: Sadakat seviyesi eşik + çarpan testleri.
    public class LoyaltyTierTests
    {
        [Theory]
        [InlineData(0, LoyaltyTier.Bronze)]
        [InlineData(2499, LoyaltyTier.Bronze)]
        [InlineData(2500, LoyaltyTier.Silver)]
        [InlineData(9999, LoyaltyTier.Silver)]
        [InlineData(10000, LoyaltyTier.Gold)]
        [InlineData(24999, LoyaltyTier.Gold)]
        [InlineData(25000, LoyaltyTier.Platinum)]
        [InlineData(100000, LoyaltyTier.Platinum)]
        public void GetTier_ByThreshold(int spent, LoyaltyTier expected)
        {
            LoyaltyTierHelper.GetTier(spent).Should().Be(expected);
        }

        [Theory]
        [InlineData(LoyaltyTier.Bronze, 1.0)]
        [InlineData(LoyaltyTier.Silver, 1.2)]
        [InlineData(LoyaltyTier.Gold, 1.5)]
        [InlineData(LoyaltyTier.Platinum, 2.0)]
        public void PointMultiplier_IncreasesWithTier(LoyaltyTier tier, decimal expected)
        {
            LoyaltyTierHelper.PointMultiplier(tier).Should().Be(expected);
        }

        [Fact]
        public void AmountToNextTier_Bronze()
        {
            // 1000 harcamış bronz -> gümüşe 1500 kaldı
            LoyaltyTierHelper.AmountToNextTier(1000m).Should().Be(1500m);
        }

        [Fact]
        public void AmountToNextTier_Platinum_IsZero()
        {
            // En üst seviye -> 0
            LoyaltyTierHelper.AmountToNextTier(30000m).Should().Be(0m);
        }
    }
}
