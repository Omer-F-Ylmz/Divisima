using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Product;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Ürün iş servisi arayüzü. Tüm metotlar (HttpStatusCode, Result) tuple döner,
    // controller bunu StatusCode(x.Item1, x.Item2) ile client'a çevirir. Cafixo IProductService kalıbı.
    public interface IProductService
    {
        // Açıklayıcı yorum: Yeni ürün ekle (beden-stoklarıyla birlikte)
        Task<(HttpStatusCode, Result)> Add(ProductAddRequestDto dto);

        // Açıklayıcı yorum: Ürün güncelle
        Task<(HttpStatusCode, Result)> Update(ProductUpdateRequestDto dto);

        // Açıklayıcı yorum: Ürünü kalıcı sil (hard delete)
        Task<(HttpStatusCode, Result)> Delete(int id);

        // Açıklayıcı yorum: Aktif/pasif durumunu değiştir (soft delete - is_active toggle)
        Task<(HttpStatusCode, Result)> ChangeStatus(int id);

        // Açıklayıcı yorum: Tek ürünü detayıyla getir (bedenler + yorum özeti)
        Task<(HttpStatusCode, Result)> GetById(int id);

        // Açıklayıcı yorum: Tüm aktif ürünleri listele
        // DALGA-3-FIX (P3): admin listesi sayfali. Parametresiz cagri ESKISI GIBI derlenir
        // (varsayilanlar); yanit sozlesmesi storefront deseniyle ayni zarfa gecti.
        Task<(HttpStatusCode, Result)> GetList(int page = 1, int size = 100);

        // Açıklayıcı yorum: Filtre + sıralama + sayfalama ile ürün listesi (public storefront)
        Task<(HttpStatusCode, Result)> GetListSearchAndFilterWithPaging(ProductFilterRequestDto dto);
        Task<(HttpStatusCode, Result)> GetOnSale();
        Task<(HttpStatusCode, Result)> GetVariants(int productId);
        // Toplu urun ice-aktarma (CSV) - admin
        Task<(HttpStatusCode, Result)> ImportFromCsv(string csvContent);
    }
}
