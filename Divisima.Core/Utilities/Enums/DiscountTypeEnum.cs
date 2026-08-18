namespace Divisima.Core.Utilities.Enums
{
    // Açıklayıcı yorum: Kupon indirim tipi. Coupon.discount_type (byte) bu değerlerle yorumlanır.
    // Frontend karşılığı: Percentage=pct, Fixed=fixed, FreeShipping=ship.
    public enum DiscountTypeEnum
    {
        Percentage = 0,   // Yüzde indirim
        Fixed = 1,        // Sabit tutar indirim
        FreeShipping = 2  // Kargo bedava
    }
}
