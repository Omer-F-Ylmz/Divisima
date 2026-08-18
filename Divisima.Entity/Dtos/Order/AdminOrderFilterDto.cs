using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Order
{
    // Açıklayıcı yorum: Admin sipariş filtresi (durum + tarih aralığı + sayfalama).
    public class AdminOrderFilterDto : IDto
    {
        public byte? status { get; set; }
        public DateTime? start_date { get; set; }
        public DateTime? end_date { get; set; }
        public int page { get; set; } = 1;
        public int page_size { get; set; } = 20;
    }
}
