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

        // Açıklayıcı yorum: "Sipariş Cancelled olunca ne olur" - faturanın da iptal edilmesi.
        // İptal iki ayrı yoldan olabiliyor (admin durum değişimi, son kalemin CancelItem ile iptali)
        // ve fatura iptali bunların HİÇBİRİNDE çağrılmıyordu.
        Task ApplyCancelledSideEffectsAsync(int orderId);
    }
}
