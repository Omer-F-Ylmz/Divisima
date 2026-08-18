using System.Collections.Generic;
using Divisima.Core.Utilities.Dtos;
using Divisima.Entity.Dtos.Order;
namespace Divisima.Entity.Dtos.Guest
{
    // Açıklayıcı yorum: Misafir (hesapsız) sipariş - iletişim + teslimat + kalemler tek istekte.
    public class GuestCheckoutDto : IDto
    {
        public string guest_name { get; set; }
        public string guest_email { get; set; }
        public string guest_phone { get; set; }
        public string city { get; set; }
        public string district { get; set; }
        public string full_address { get; set; }
        public string? zip_code { get; set; }
        public string coupon_code { get; set; }
        public string? request_id { get; set; }
        public List<OrderItemRequestDto> items { get; set; }
    }
}
