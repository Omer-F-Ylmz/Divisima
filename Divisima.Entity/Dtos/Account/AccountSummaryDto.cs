using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Account
{
    // Açıklayıcı yorum: Hesap özeti (profil + bakiye + tercihler) - hassas alan yok.
    public class AccountSummaryDto : IDto
    {
        public int id { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public string phone { get; set; }
        public DateTime? birthdate { get; set; }
        public bool email_verified { get; set; }
        public bool two_factor_enabled { get; set; }
        public int loyalty_points { get; set; }
        public decimal store_credit { get; set; }
        public string referral_code { get; set; }
        public bool notify_email { get; set; }
        public bool notify_sms { get; set; }
        public bool notify_push { get; set; }
    }
}
