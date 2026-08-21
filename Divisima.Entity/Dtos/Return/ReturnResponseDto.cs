using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Return
{
    // Açıklayıcı yorum: İade görüntüleme.
    public class ReturnResponseDto : IDto
    {
        public int id { get; set; }
        public int order_id { get; set; }
        public int product_id { get; set; }

        // SPRINT 8 MADDE 5: URUN ADI (ve siparis numarasi) DTO'ya EKLENDI.
        // Onceden yalniz product_id donuyordu; istemci adi KATALOGDAN cozmek zorunda kaliyordu
        // (E3'te olculdu). Bu yalniz fazladan is degil, YANLIS da olabiliyordu: pasiflenmis ya
        // da katalogdan cikmis bir urunun iadesi "Urun #12" olarak gorunuyordu. Iade kaydi
        // GECMISE ait bir belgedir - adi kaydin KENDISI tasimali.
        public string? product_name { get; set; }
        public string? order_number { get; set; }

        public string size { get; set; }
        public int quantity { get; set; }
        public byte reason { get; set; }
        public byte return_type { get; set; }
        public byte status { get; set; }
        public string status_name { get; set; }
        public decimal refund_amount { get; set; }
        public string? admin_note { get; set; }
        public DateTime created_at { get; set; }
    }
}
