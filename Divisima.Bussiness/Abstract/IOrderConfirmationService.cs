using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: "Sipariş Confirmed olunca ne olur" sorusunun TEK tanımı.
    // Onaylama dört ayrı yoldan yapılıyor (COD, tam mağaza kredisi, havale onayı,
    // online ödeme callback) ve fatura üretimi bunların HİÇBİRİNDE çağrılmıyordu.
    public interface IOrderConfirmationService
    {
        Task ApplyConfirmedSideEffectsAsync(int orderId);
    }
}
