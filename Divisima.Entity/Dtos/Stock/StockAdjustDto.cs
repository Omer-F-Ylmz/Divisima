using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Stock
{
    // Açıklayıcı yorum: Admin stok düzeltme. new_quantity mutlak yeni değer; note zorunlu (denetim izi).
    public class StockAdjustDto : IDto
    {
        public int product_id { get; set; }
        public string size { get; set; }
        public int new_quantity { get; set; }   // yeni mutlak stok (fark otomatik hesaplanır)
        public string note { get; set; }          // "Yeni sevkiyat", "Sayım düzeltmesi" vb.
    }
}
