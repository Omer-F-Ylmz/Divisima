using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Product
{
    // Açıklayıcı yorum: Ürün detay dönüşü (frontend openDetail). Bedenler + stok + puan özeti dahil.
    public class ProductDetailResponseDto : IDto
    {
        public int id { get; set; }
        public string name { get; set; }
        public string brand { get; set; }
        public int category_id { get; set; }
        public string category_name { get; set; }
        public int? sub_category_id { get; set; }
        public string sub_category_name { get; set; }
        public decimal price { get; set; }
        public decimal? old_price { get; set; }
        // DALGA B / B2: sale_price EKLENDI - admin panelinin urun DUZENLEME formu icin ZORUNLU.
        // Gerekce (olculdu): ProductManager.Update tam-varlik map yapar (_mapper.Map(dto, product)),
        // yani ProductUpdateRequestDto'da BULUNAN ama gonderilmeyen her alan NULL'a duser. sale_price
        // o DTO'da VAR; panel onu geri gonderemezse indirimli fiyat SESSIZCE SILINIR ve musteri tam
        // fiyat oder. Panelin dolduracagi tek kaynak bu uctu.
        // SIZINTI DEGERLENDIRILDI: uc [AllowAnonymous]. sale_start/sale_end depoda HICBIR kod yoluyla
        // yazilmiyor (tarandi: yalniz PricingHelper okuyor), dolayisiyla PricingHelper.IsOnSale
        // salePrice>0 oldugunda HER ZAMAN true doner - yani sale_price zaten musterinin ODEDIGI
        // fiyattir ve listeleme uclarindan gorunur. Ileride ZAMANLI indirim eklenirse bu alan
        // "gelecekteki kampanya fiyati" tasimaya baslar ve o gun burasi yeniden degerlendirilmelidir.
        public decimal? sale_price { get; set; }
        // MANTIK-FIX-1 / K1: MUSTERININ ODEYECEGI FIYAT (liste DTO'sundaki alanin ikizi).
        // Yukaridaki `sale_price` HAM kalir - admin formunun geri yazdigi alan odur ve
        // yorumundaki "zamanli indirim eklenirse burasi yeniden degerlendirilmelidir" uyarisi
        // TAM DA BU DALGADA karsilandi: istemci artik pencere farkindaligini KENDISI yorumlamak
        // yerine bu hesaplanmis alani okuyor. Iki tuketici (admin = HAM, vitrin = PENCERE
        // FARKINDA) boylece AYRI alanlardan beslenir; bir alana iki anlam yuklenmez.
        public decimal effective_price { get; set; }
        public string description { get; set; }
        public string color_hex { get; set; }
        public string product_type { get; set; }
        public List<ProductStockDto> stocks { get; set; }
        public double review_average { get; set; }
        public int review_count { get; set; }
        public decimal average_rating { get; set; }
        public bool is_active { get; set; }
    }
}
