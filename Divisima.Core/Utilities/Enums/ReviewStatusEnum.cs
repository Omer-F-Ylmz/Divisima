namespace Divisima.Core.Utilities.Enums
{
    // Açıklayıcı yorum: Ürün yorumu onay durumu (Cafixo ProductReview onay akışı kalıbı).
    public enum ReviewStatusEnum
    {
        Pending = 0,      // Onay bekliyor
        Approved = 1,     // Onaylı (storefront'ta görünür)
        Rejected = 2      // Reddedildi
    }
}
