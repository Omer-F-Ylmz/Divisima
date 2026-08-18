using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Auth
{
    // Açıklayıcı yorum: Yeni şifre belirleme (token + yeni şifre).
    public class ResetPasswordRequestDto : IDto
    {
        public string token { get; set; }
        public string new_password { get; set; }
    }
}
