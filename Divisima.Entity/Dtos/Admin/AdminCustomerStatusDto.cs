using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Admin
{
    // Açıklayıcı yorum: Müşteri aktiflik değiştirme (askıya al / aktifleştir).
    public class AdminCustomerStatusDto : IDto
    {
        public int customer_id { get; set; }
        public bool is_active { get; set; }
    }
}
