using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Integrations.Notifications;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Device;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Müşteri cihaz yöneticisi. Cihaz token'larını yönetir ve push dispatch eder.
    // Katman: token DB'den çekilir (Dal), tek tek Core push servisine (FCM) iletilir. Geçersiz token pasifleştirilir.
    public class CustomerDeviceManager : ICustomerDeviceService
    {
        private readonly ICustomerDeviceDal _deviceDal;
        private readonly IPushNotificationService _pushService;

        public CustomerDeviceManager(ICustomerDeviceDal deviceDal, IPushNotificationService pushService)
        {
            _deviceDal = deviceDal;
            _pushService = pushService;
        }

        public async Task<(HttpStatusCode, Result)> RegisterDevice(DeviceRegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.device_token))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.DeviceTokenRequired));

            // Açıklayıcı yorum: Upsert - aynı token varsa güncelle (müşteri/aktiflik), yoksa ekle
            var existing = await _deviceDal.GetAsync(d => d.device_token == dto.device_token);
            // CAPRAZ-HESAP DEVRALMA FIX (H46): token BASKA musteriye aitse eski kayit SESSIZCE devralinmaz.
            // Onceki davranis: existing.customer_id = yeni musteri -> token'i ele geciren biri kurbanin
            // cihazina KENDI bildirimlerini gonderebilir, kurban da kendi siparis bildirimlerini ALAMAZDI.
            // Yeni davranis: eski baglanti PASIFLESTIRILIR (bildirim gitmez) + yeni musteri icin AYRI kayit acilir.
            // Ortak cihaz (A cikis yapti, B giris yapti) senaryosu bozulmaz; gecmis korunur ve denetlenebilir.
            if (existing != null && existing.customer_id != dto.customer_id)
            {
                existing.is_active = false;
                existing.last_used_at = DateTime.Now;
                await _deviceDal.UpdateAsync(existing);
                existing = null;   // asagida YENI kayit acilacak
            }
            if (existing != null)
            {
                existing.customer_id = dto.customer_id;
                existing.platform = dto.platform;
                existing.is_active = true;
                existing.last_used_at = DateTime.Now;
                await _deviceDal.UpdateAsync(existing);
                return (HttpStatusCode.OK, new SuccessResult(Messages.DeviceRegistered));
            }

            await _deviceDal.AddAsync(new CustomerDevice
            {
                customer_id = dto.customer_id,
                device_token = dto.device_token,
                platform = dto.platform,
                is_active = true,
                created_at = DateTime.Now,
                last_used_at = DateTime.Now
            });
            return (HttpStatusCode.OK, new SuccessResult(Messages.DeviceRegistered));
        }

        public async Task<(HttpStatusCode, Result)> UnregisterDevice(string deviceToken, int customerId)
        {
            var device = await _deviceDal.GetAsync(d => d.device_token == deviceToken && d.customer_id == customerId);
            if (device == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.DeviceNotFound));
            device.is_active = false;
            await _deviceDal.UpdateAsync(device);
            return (HttpStatusCode.OK, new SuccessResult(Messages.DeviceUnregistered));
        }

        public async Task NotifyCustomerAsync(int customerId, string title, string body)
        {
            // Açıklayıcı yorum: Müşterinin tüm aktif cihazlarına gönder; başarısız token'ı pasifleştir
            var devices = await _deviceDal.GetListAsync(d => d.customer_id == customerId && d.is_active);
            foreach (var device in devices)
            {
                var ok = await _pushService.SendAsync(device.device_token, title, body);
                if (!ok)
                {
                    // Açıklayıcı yorum: Geçersiz/expired token - bir daha denenmesin
                    device.is_active = false;
                    await _deviceDal.UpdateAsync(device);
                }
            }
        }
    }
}
