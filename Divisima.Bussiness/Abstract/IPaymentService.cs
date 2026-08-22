using System.Net;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Payment;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Ödeme iş servisi. authenticatedCustomerId = JWT'den gelen gerçek kullanıcı
    // (sahiplik kontrolü: kullanıcı yalnızca KENDİ siparişini ödeyebilir).
    public interface IPaymentService
    {
        Task<(HttpStatusCode, Result)> Initialize(PaymentInitRequestDto dto, int authenticatedCustomerId);
        // KANAL PARAMETRESI - VARSAYILAN Strict, FAIL-CLOSED. Yeni bir cagiran dusunmeden
        // yazarsa TUM savunmalari acik olan davranisi alir; gevseme her zaman ACIKCA secilir.
        //
        // Onceki imza `bool imzaZorunlu = true` idi. DEGISTIRILDI cunku OLCULDU: Sprint 8
        // madde 9'dan sonra HER IKI uretim cagri yeri de `false` veriyordu, yani bayrak artik
        // KANALI ayirt etmiyordu. Webhook yolunda token yasi sinirini da gevsetmek gerekince
        // (SUPHELI #15) ikinci bir bool eklemek gecersiz bilesimlere kapi acardi.
        // Gerekcenin tamami PaymentNotificationChannel enum'unun basinda.
        Task<(HttpStatusCode, Result)> HandleCallback(PaymentCallbackRequestDto dto,
            PaymentNotificationChannel kanal = PaymentNotificationChannel.Strict);

        // E2: YALNIZ callback yonlendirmesi icin - token'in ait oldugu siparis id'si (yoksa 0).
        // AYRI ve SALT-OKUR bir metot olarak eklendi: HandleCallback'in imzasi ve donusu
        // DEGISMEDI, dolayisiyla webhook yolu ve S2S dogrulama davranisi AYNEN korunuyor.
        Task<int> GetOrderIdByTokenAsync(string token);
    }
}
