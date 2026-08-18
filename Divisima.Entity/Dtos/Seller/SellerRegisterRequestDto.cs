using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Seller
{
    // Açıklayıcı yorum: Satıcı kayıt isteği. Kayıt sonrası status=Pending (admin onayı bekler).
    public class SellerRegisterRequestDto : IDto
    {
        public string business_name { get; set; }
        public string email { get; set; }
        public string password { get; set; }
        public string phone { get; set; }
        public string? tax_number { get; set; }
    }
}
