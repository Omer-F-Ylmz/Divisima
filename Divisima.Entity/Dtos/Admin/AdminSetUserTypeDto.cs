using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Admin
{
    // Açıklayıcı yorum: Kullanıcı tipi değiştirme (admin yap / müşteriye indir). user_type: Admin(1) / Customer(2).
    public class AdminSetUserTypeDto : IDto
    {
        public int customer_id { get; set; }
        public byte user_type { get; set; }
    }
}
