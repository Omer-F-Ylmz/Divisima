using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Admin
{
    // Açıklayıcı yorum: Admin müşteri filtresi (arama + aktiflik + sayfalama).
    public class AdminCustomerFilterDto : IDto
    {
        public string? search { get; set; }     // ad/e-posta araması
        public bool? is_active { get; set; }
        public int page { get; set; } = 1;
        public int page_size { get; set; } = 20;
    }
}
