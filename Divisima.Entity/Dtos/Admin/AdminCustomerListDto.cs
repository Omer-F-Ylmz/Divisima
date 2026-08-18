using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Admin
{
    // Açıklayıcı yorum: Admin müşteri listesi. Hassas alan (şifre/token) ASLA yok. Yönetim için gereken minimum.
    public class AdminCustomerListDto : IDto
    {
        public int id { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public bool is_active { get; set; }
        public bool email_verified { get; set; }
        public int order_count { get; set; }
        public DateTime created_at { get; set; }
    }
}
