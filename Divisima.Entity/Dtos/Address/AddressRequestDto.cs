using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Address
{
    // Açıklayıcı yorum: Adres ekle/güncelle isteği.
    public class AddressRequestDto : IDto
    {
        public int? id { get; set; }
        public int customer_id { get; set; }
        public string title { get; set; }
        public string full_name { get; set; }
        public string phone { get; set; }
        public string city { get; set; }
        public string district { get; set; }
        public string full_address { get; set; }
        public string? zip_code { get; set; }
        public bool is_default { get; set; }
    }
}
