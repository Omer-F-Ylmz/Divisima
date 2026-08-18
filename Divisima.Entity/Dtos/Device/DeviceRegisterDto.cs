using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Device
{
    // Açıklayıcı yorum: Cihaz kaydı (push token). customer_id JWT'den set edilir.
    public class DeviceRegisterDto : IDto
    {
        public string device_token { get; set; }
        public byte platform { get; set; }        // 0=Web, 1=Android, 2=iOS
        public int customer_id { get; set; }       // JWT'den override
    }
}
