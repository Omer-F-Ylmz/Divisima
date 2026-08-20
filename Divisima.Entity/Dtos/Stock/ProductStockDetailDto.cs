using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Stock
{
    // Aciklayici yorum: ADMIN stok detayi (beden bazli). Operator paneli stok duzeltmesi yapmadan
    // once mevcut durumu gormek zorunda: yalniz stock_quantity yaniltir cunku rezerve edilmis
    // adet fiziksel stokta durur ama SATILAMAZ (available = stock_quantity - reserved_quantity).
    //
    // NEDEN AYRI DTO: ProductStockDto (urun detayinda kullanilan) ANONIM uclarda donuyor;
    // reserved_quantity oraya eklenirse "kac kisi sepetinde tutuyor" bilgisi herkese acilirdi.
    // Bu DTO yalniz admin ucundan doner.
    public class ProductStockDetailDto : IDto
    {
        public string size { get; set; }
        public int stock_quantity { get; set; }     // fiziksel stok
        public int reserved_quantity { get; set; }  // acik siparislerce rezerve edilmis
        public int available { get; set; }          // satilabilir = stock_quantity - reserved_quantity
    }
}
