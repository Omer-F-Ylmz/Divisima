using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Satıcı (marketplace vendor). Cafixo Customer kalıbıyla aynı: IEntity + IUser,
    // şifre hash+salt byte[], hesap kilidi. user_type = Seller (3). JWT'de NameIdentifier=id + user_type=3
    // taşınır; SellerController tüm sorguları bu id'ye göre izole eder (bir satıcı başkasının verisini görmez).
    public class Seller : IEntity, IUser
    {
        public int id { get; set; }
        public string business_name { get; set; }   // mağaza/işletme adı (müşteriye görünür)
        public string email { get; set; }           // giriş + iletişim
        public byte user_type { get; set; } = 3;     // Seller (3) - JWT claim + yetkilendirme
        public byte[] password_salt { get; set; }
        public byte[] password_hash { get; set; }

        public string phone { get; set; }
        public string? tax_number { get; set; }      // vergi no (fatura/ödeme için)
        public byte status { get; set; } = 0;        // SellerStatusEnum: Pending(0)/Approved(1)/Suspended(2)
        public decimal commission_rate { get; set; } = 10m; // platform komisyonu (%). Satıcı geliri = tutar*(1-rate/100)

        public bool is_active { get; set; } = true;  // yumuşak silme / pasifleştirme
        public int failed_login_attempts { get; set; }
        public DateTime? lockout_end { get; set; }   // brute-force kilidi (Customer ile aynı mantık)

        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
    }
}
