using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Device;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Müşteri cihaz/push servisi. Cihaz kaydı + müşteriye push (tüm aktif cihazlarına).
    // Token çözümlemesi burada (Business) - Core push servisi yalnız tek token'a gönderir.
    public interface ICustomerDeviceService
    {
        Task<(HttpStatusCode, Result)> RegisterDevice(DeviceRegisterDto dto);
        Task<(HttpStatusCode, Result)> UnregisterDevice(string deviceToken, int customerId);
        // Açıklayıcı yorum: Müşterinin tüm aktif cihazlarına push (OrderManager çağırır). Best-effort.
        Task NotifyCustomerAsync(int customerId, string title, string body);
    }
}
