namespace Divisima.Core.Utilities.Loyalty
{
    // Açıklayıcı yorum: Sadakat seviyeleri - toplam harcamaya göre (yaşam boyu, teslim edilen siparişler).
    public enum LoyaltyTier
    {
        Bronze = 0,
        Silver = 1,
        Gold = 2,
        Platinum = 3
    }

    // Açıklayıcı yorum: Seviye eşikleri (TL) - saf/test edilebilir. Puan çarpanı seviyeyle artar (sadakat teşviki).
    public static class LoyaltyTierHelper
    {
        public static LoyaltyTier GetTier(decimal totalSpent)
        {
            if (totalSpent >= 25000m) return LoyaltyTier.Platinum;
            if (totalSpent >= 10000m) return LoyaltyTier.Gold;
            if (totalSpent >= 2500m)  return LoyaltyTier.Silver;
            return LoyaltyTier.Bronze;
        }

        // Açıklayıcı yorum: Seviyeye göre puan kazanım çarpanı (Gold %50 fazla puan, Platinum 2x).
        public static decimal PointMultiplier(LoyaltyTier tier) => tier switch
        {
            LoyaltyTier.Platinum => 2.0m,
            LoyaltyTier.Gold     => 1.5m,
            LoyaltyTier.Silver   => 1.2m,
            _ => 1.0m
        };

        // Bir sonraki seviyeye kalan tutar (0 = en üst seviye)
        public static decimal AmountToNextTier(decimal totalSpent) => GetTier(totalSpent) switch
        {
            LoyaltyTier.Bronze   => 2500m - totalSpent,
            LoyaltyTier.Silver   => 10000m - totalSpent,
            LoyaltyTier.Gold     => 25000m - totalSpent,
            _ => 0m
        };
    }
}
