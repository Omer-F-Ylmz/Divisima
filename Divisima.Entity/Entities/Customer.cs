using Divisima.Core.Entities.Abstract;
using Divisima.Core.Utilities.Enums;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Müşteri (Cafixo Customer kalıbı). IEntity + IUser, şifre hash+salt byte[].
    public class Customer : IEntity, IUser
    {
        public int id { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public byte user_type { get; set; } = 2;   // Admin (1) / Customer (2) - varsayilan Customer
        public string? phone { get; set; }   // KVKK silmede NULL yazilir (anonimlestirme)
        public string? address { get; set; }
        public string? city { get; set; }
        public CustomerGenderEnum? gender { get; set; }
        public byte[] password_salt { get; set; }
        public byte[] password_hash { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public DateTime? last_login_at { get; set; }
        // Açıklayıcı yorum: Kaba kuvvet koruması - başarısız deneme sayısı + kilit bitiş zamanı
        // Açıklayıcı yorum: E-posta doğrulama - token + doğrulanma durumu
        public bool email_verified { get; set; }
        public string? email_verification_token { get; set; }
        public DateTime? email_verification_sent_at { get; set; }
        // Açıklayıcı yorum: Şifre sıfırlama - tek kullanımlık token + son geçerlilik
        public string? password_reset_token { get; set; }
        public DateTime? password_reset_expiry { get; set; }
        // Açıklayıcı yorum: İki faktörlü doğrulama (TOTP) - etkin mi + gizli anahtar (şifreli saklanmalı)
        public bool two_factor_enabled { get; set; }
        public string? two_factor_secret { get; set; }
        public string? two_factor_code { get; set; }          // login 2FA e-posta OTP (hash)
        public DateTime? two_factor_code_expiry { get; set; } // OTP son gecerlilik
        public int failed_login_attempts { get; set; }
        public DateTime? lockout_end { get; set; }
        public DateTime? birthdate { get; set; } // doğum günü indirimi için
        public bool notify_email { get; set; } = true; // e-posta bildirim tercihi
        public bool notify_sms { get; set; } = true; // SMS bildirim tercihi
        public bool notify_push { get; set; } = true; // push bildirim tercihi
        public int loyalty_points { get; set; } // sadakat puanı bakiyesi
        public decimal store_credit { get; set; } // mağaza kredisi bakiyesi
        public string? referral_code { get; set; } // bu müşterinin referans kodu
        public int? referred_by { get; set; } // hangi müşteri tarafından davet edildi (customer id)
        public DateTime? last_order_at { get; set; } // win-back için son sipariş zamanı
        public DateTime? last_winback_sent_at { get; set; } // son win-back e-postası (spam önleme)
        public DateTime? birthday_offer_sent_year { get; set; } // son doğum günü teklifi (yıl bazlı tekrar önleme)
    }
}
