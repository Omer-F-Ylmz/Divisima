using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Müşteri adres defteri (frontend checkout adres seçimi).
    public class Address : IEntity
    {
        public int id { get; set; }
        public int customer_id { get; set; }
        public string title { get; set; }          // "Ev", "İş"
        public string full_name { get; set; }
        public string? phone { get; set; }   // KVKK silmede NULL yazilir (anonimlestirme)
        public string city { get; set; }
        public string district { get; set; }
        public string full_address { get; set; }
        public string? zip_code { get; set; }
        public bool is_default { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
    }
}
