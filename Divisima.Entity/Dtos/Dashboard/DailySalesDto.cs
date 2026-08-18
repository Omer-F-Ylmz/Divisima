using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Dashboard
{
    // Açıklayıcı yorum: Günlük satış - grafik için (tarih + o günün cirosu + sipariş sayısı).
    public class DailySalesDto : IDto
    {
        public DateTime date { get; set; }
        public decimal revenue { get; set; }
        public int order_count { get; set; }
    }
}
