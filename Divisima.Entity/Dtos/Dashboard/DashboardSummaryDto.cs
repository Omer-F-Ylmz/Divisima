using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Dashboard
{
    // Açıklayıcı yorum: Genel özet - dashboard üst kartları (ciro, sipariş, ortalama sepet, müşteri).
    public class DashboardSummaryDto : IDto
    {
        public decimal total_revenue { get; set; }        // toplam ciro (tamamlanan siparişler)
        public int total_orders { get; set; }              // toplam sipariş
        public int pending_orders { get; set; }            // bekleyen sipariş
        public decimal average_order_value { get; set; }   // ortalama sepet tutarı
        public int total_customers { get; set; }           // toplam müşteri
        public int low_stock_count { get; set; }           // stoğu azalan ürün sayısı
    }
}
