using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Stok rezervasyonu. Sipariş verilince stok DÜŞMEZ, rezerve edilir (ödeme penceresi süresince).
    // Ödeme başarılı -> onaylanır (stok gerçekten düşer). Başarısız/süre dolar -> serbest bırakılır (stok geri).
    // Terk edilen sepetlerde hayalet stok kaybını önler (Hangfire job süresi geçenleri serbest bırakır).
    public class StockReservation : IEntity
    {
        public int id { get; set; }
        public int order_id { get; set; }
        public int product_id { get; set; }
        public string size { get; set; }
        public int quantity { get; set; }
        public byte status { get; set; }          // Aktif (0), Onaylandı (1), SerbestBırakıldı (2), SüresiDoldu (3)
        public DateTime expires_at { get; set; }  // bu süreden sonra otomatik serbest bırakılır
        public DateTime created_at { get; set; }
        public DateTime? closed_at { get; set; }
    }
}
