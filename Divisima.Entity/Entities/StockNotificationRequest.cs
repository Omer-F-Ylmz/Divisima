using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: "Stok gelince haber ver" talebi. Ürün+beden stoğu 0 iken müşteri e-posta bırakır;
    // stok geldiğinde bildirim gönderilir. Nav property yok (product_id skaler FK).
    public class StockNotificationRequest : IEntity
    {
        public int id { get; set; }
        public int product_id { get; set; }
        public string size { get; set; } // hangi beden (aksesuarda boş)
        public string email { get; set; } // bildirim gönderilecek e-posta
        public bool is_notified { get; set; } // bildirim gönderildi mi (tekrar gönderilmez)
        public DateTime created_at { get; set; }
        public DateTime? notified_at { get; set; } // ne zaman haber verildi

        // SPRINT 8 MADDE 10 - ABONELIKTEN CIKMA JETONU.
        // Abonelik ANONIM kurulabiliyor (uc AllowAnonymous, kayit e-posta ile). Dolayisiyla
        // "cikma" da kimlik dogrulamasi gerektiremez - aksi halde uye olmayan bir abone izni
        // GERI ALAMAZ. Kimlik yerine TAHMIN EDILEMEZ bir jeton kullanilir; e-postadaki baglanti
        // bu jetonu tasir. (E-posta + urun ile cikma kabul edilseydi herkes herkesi abonelikten
        // cikarabilir ve "bu e-posta abone mi" sorusuna yanit veren bir sizinti kanali olusurdu.)
        public string unsubscribe_token { get; set; }
    }
}
