using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Seller
{
    // Açıklayıcı yorum: Satıcı ürün listesi kalemi - ürün + o ürünün satış performansı.
    public class SellerProductResponseDto : IDto
    {
        public int id { get; set; }
        public string name { get; set; }
        public decimal price { get; set; }
        public bool is_active { get; set; }
        public int units_sold { get; set; }     // bu üründen satılan adet
        public decimal revenue { get; set; }     // bu üründen brüt gelir
    }
}
