using Divisima.Core.Utilities.Dtos;
using Divisima.Core.Utilities.Enums;

namespace Divisima.Entity.Dtos.Auth
{
    // Açıklayıcı yorum: Müşteri kayıt isteği. Cafixo Customer tek 'name' alanı.
    public class CustomerRegisterRequestDto : IDto
    {
        public string name { get; set; }
        public string email { get; set; }
        public string phone { get; set; }
        public string password { get; set; }
        public CustomerGenderEnum? gender { get; set; }
        public string? referral_code { get; set; } // opsiyonel davet kodu
        // Aciklayici yorum: KVKK acik riza - kayit aninda kabul edilen sozlesmeler (kanit icin saklanir)
        public bool accepted_terms { get; set; }        // mesafeli satis + uyelik sozlesmesi
        public bool accepted_privacy { get; set; }       // gizlilik/KVKK aydinlatma
        public bool accepted_marketing { get; set; }     // ticari elektronik ileti (opsiyonel; ret de saklanir)
        public string? consent_version { get; set; }      // kabul edilen metin versiyonu
    }
}
