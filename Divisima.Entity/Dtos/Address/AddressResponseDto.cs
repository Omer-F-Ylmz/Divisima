using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Address
{
    public class AddressResponseDto : IDto
    {
        public int id { get; set; }
        public string title { get; set; }
        public string full_name { get; set; }
        public string? phone { get; set; }   // anonimlestirilmis musteride NULL olabilir
        public string city { get; set; }
        public string district { get; set; }
        public string full_address { get; set; }
        public string? zip_code { get; set; }
        public bool is_default { get; set; }
    }
}
