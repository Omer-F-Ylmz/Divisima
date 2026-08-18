using Divisima.Core.Entities.Abstract;
namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Beden rehberi satırı - kategori + beden etiketi başına ölçüler (cm). Moda standartı.
    public class SizeGuideEntry : IEntity
    {
        public int id { get; set; }
        public int category_id { get; set; }
        public string size_label { get; set; } // XS, S, M, L, XL veya 36, 38...
        public decimal? bust_cm { get; set; }   // göğüs
        public decimal? waist_cm { get; set; }  // bel
        public decimal? hip_cm { get; set; }    // kalça
        public decimal? length_cm { get; set; } // boy/uzunluk
        public bool is_active { get; set; }
        public int sort_order { get; set; }
        public DateTime created_at { get; set; }
    }
}
