using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Auth
{
    // Açıklayıcı yorum: Şifre sıfırlama talebi (e-posta).
    public class ForgotPasswordRequestDto : IDto { public string email { get; set; } }
}
