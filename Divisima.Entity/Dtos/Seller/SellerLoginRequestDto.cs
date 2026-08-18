using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Seller
{
    // Açıklayıcı yorum: Satıcı giriş isteği (müşteri girişinden ayrı endpoint).
    public class SellerLoginRequestDto : IDto
    {
        public string email { get; set; }
        public string password { get; set; }
    }
}
