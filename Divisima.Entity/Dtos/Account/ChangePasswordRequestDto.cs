using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Account
{
    public class ChangePasswordRequestDto : IDto
    {
        public string current_password { get; set; }
        public string new_password { get; set; }
    }
}
