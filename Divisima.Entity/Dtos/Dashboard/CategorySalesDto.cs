using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Dashboard
{
    // Açıklayıcı yorum: Kategori bazlı satış raporu - hangi kategori ne kadar ciro/adet yaptı (admin analiz).
    public class CategorySalesDto : IDto
    {
        public int category_id { get; set; }
        public string category_name { get; set; }
        public decimal revenue { get; set; }
        public int units_sold { get; set; }
    }
}
