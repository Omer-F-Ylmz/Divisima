using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Payment;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Ödeme iş servisi. authenticatedCustomerId = JWT'den gelen gerçek kullanıcı
    // (sahiplik kontrolü: kullanıcı yalnızca KENDİ siparişini ödeyebilir).
    public interface IPaymentService
    {
        Task<(HttpStatusCode, Result)> Initialize(PaymentInitRequestDto dto, int authenticatedCustomerId);
        // E2b: imzaZorunlu VARSAYILAN TRUE - fail-closed. Yeni bir cagiran dusunmeden yazarsa
        // GUVENLI davranisi alir. Gevsek yol TEK bir cagri yerinde ACIKCA secilir:
        // PaymentController.Callback. Gerekce orada, olcumle birlikte yaziyor.
        Task<(HttpStatusCode, Result)> HandleCallback(PaymentCallbackRequestDto dto, bool imzaZorunlu = true);

        // E2: YALNIZ callback yonlendirmesi icin - token'in ait oldugu siparis id'si (yoksa 0).
        // AYRI ve SALT-OKUR bir metot olarak eklendi: HandleCallback'in imzasi ve donusu
        // DEGISMEDI, dolayisiyla webhook yolu ve S2S dogrulama davranisi AYNEN korunuyor.
        Task<int> GetOrderIdByTokenAsync(string token);
    }
}
