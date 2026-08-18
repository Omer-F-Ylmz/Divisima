using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Auth
{
    public class VerifyTwoFactorDto : IDto
    {
        public string email { get; set; }
        public string code { get; set; }
    }
}
