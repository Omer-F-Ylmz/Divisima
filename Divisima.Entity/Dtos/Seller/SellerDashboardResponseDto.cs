using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Seller
{
    // Açıklayıcı yorum: Satıcı paneli özeti - "neyi nasıl satıyorum" tek bakışta. Tüm değerler
    // OTURUMDAKİ satıcıya aittir (izolasyon: SellerController CurrentSellerId'ye göre hesaplar).
    public class SellerDashboardResponseDto : IDto
    {
        public int total_products { get; set; }        // toplam ürün
        public int active_products { get; set; }        // aktif (yayında) ürün
        public int total_orders { get; set; }           // ürünlerini içeren farklı sipariş sayısı
        public int total_units_sold { get; set; }       // satılan toplam adet (iptaller hariç)
        public decimal gross_revenue { get; set; }      // brüt satış (iptaller hariç)
        public decimal commission_total { get; set; }   // platform komisyonu toplamı
        public decimal net_revenue { get; set; }        // net satıcı geliri (brüt - komisyon)
        public int pending_shipment_count { get; set; } // kargolanmayı bekleyen kalem sayısı
    }
}
