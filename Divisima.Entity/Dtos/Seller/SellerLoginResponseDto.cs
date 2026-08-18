using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Seller
{
    // Açıklayıcı yorum: Satıcı giriş sonucu - JWT + mağaza bilgisi + hesap durumu.
    public class SellerLoginResponseDto : IDto
    {
        public int seller_id { get; set; }
        public string business_name { get; set; }
        public string email { get; set; }
        public byte status { get; set; }          // Pending/Approved/Suspended
        public string token { get; set; }
        public DateTime expiration { get; set; }
        public string refresh_token { get; set; }
    }
}
