using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Notification
{
    // SPRINT 8 MADDE 10 - "ABONELIKLERIM" LISTE SATIRI.
    //
    // Stok bildirimi ve fiyat dususu abonelikleri AYRI tablolarda tutuluyor ama kullanici icin
    // ikisi de "bana haber verilecek sey"dir; Hesabim ekraninda TEK listede gosterilirler.
    // Bu yuzden ortak bir satir bicimi var ve `type` hangi tur oldugunu soyler.
    //
    // NOT: `unsubscribe_token` bu DTO'da YOK ve BILEREK yok. Jeton e-postadaki baglantinin
    // kimligidir; giris yapmis kullanici zaten kimliğiyle silebiliyor (DELETE /{id}), dolayisiyla
    // jetonu ekrana tasimanin faydasi yok - yalniz sizma yuzeyi eklerdi.
    public class NotificationSubscriptionDto : IDto
    {
        public int id { get; set; }
        public string type { get; set; }             // "stock" | "price_drop"
        public int product_id { get; set; }
        public string product_name { get; set; }
        public string? size { get; set; }            // yalniz stok bildiriminde dolu
        public decimal? subscribed_price { get; set; } // yalniz fiyat dususunde dolu
        public bool is_notified { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? notified_at { get; set; }
    }
}
