using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Product
{
    // Açıklayıcı yorum: Ürün liste/grid dönüşü (frontend cardHTML). Toplam stok özet olarak döner.
    public class ProductListResponseDto : IDto
    {
        public int id { get; set; }
        public string name { get; set; }
        public string brand { get; set; }
        public int category_id { get; set; }
        public string category_name { get; set; }
        public decimal price { get; set; }
        public decimal? old_price { get; set; }
        // MANTIK-FIX-1 / K1: MUSTERININ ODEYECEGI FIYAT.
        // Neden AYRI alan (ve neden `price`in anlami DEGISTIRILMEDI): admin urun DUZENLEME
        // formu fiyati ayni ANONIM detay ucundan okuyup GERI YAZIYOR (admin.html:306 -> :338
        // -> saveProduct:376 -> ProductManager.Update tam-varlik map). `price` etkin fiyati
        // tasisaydi operator yalnizca adi degistirip kaydettiginde TABAN FIYAT KALICI OLARAK
        // ASAGI KAYARDI - Dalga B'de stok icin odenen bedelin fiyat esdegeri.
        // Neden `sale_price` DEGIL: o alan detay DTO'sunda ZATEN var ve admin formunun geri
        // yazdigi HAM degerdir; pencere farkindaligi eklemek ayni alana IKI ANLAM yuklerdi.
        // PENCERE (sale_start/sale_end) BURADA, SUNUCUDA degerlendirilir - istemci yorumlamaz.
        // Profildeki TEK CreateMap'ten doldugu icin ALTI uretici de kapsanir (ProductManager
        // :433/:466, CollectionManager :158, SearchManager :107/:128, WishlistManager :72);
        // ListeyiZenginlestirAsync'e konsaydi favoriler/arama/koleksiyon SESSIZCE bos donerdi.
        public decimal effective_price { get; set; }
        public string color_hex { get; set; }
        public int total_stock { get; set; }
        public string? image_url { get; set; }
        public List<string> sizes { get; set; } = new(); // frontend sizes[] - müsait bedenler
        public decimal average_rating { get; set; }
        public int review_count { get; set; }
        public bool is_active { get; set; }
    }
}
