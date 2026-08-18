using FluentValidation;
using Divisima.Entity.Dtos.Device;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Cihaz kaydı validasyonu.
    public class DeviceRegisterValidator : AbstractValidator<DeviceRegisterDto>
    {
        public DeviceRegisterValidator()
        {
            RuleFor(x => x.device_token).NotEmpty().WithMessage("Cihaz token gerekli.").MaximumLength(500);
            RuleFor(x => x.platform).LessThanOrEqualTo((byte)2).WithMessage("Geçersiz platform.");
        }
    }
}
