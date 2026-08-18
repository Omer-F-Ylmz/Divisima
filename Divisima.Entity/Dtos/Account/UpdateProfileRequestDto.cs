using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Account
{
    // Açıklayıcı yorum: Profil güncelleme (ad/telefon/doğum günü). E-posta değişimi ayrı akış (yeniden doğrulama gerekir).
    public class UpdateProfileRequestDto : IDto
    {
        public string name { get; set; }
        public string phone { get; set; }
        public DateTime? birthdate { get; set; }
    }
}
