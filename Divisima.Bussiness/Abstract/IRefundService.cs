using System.Threading.Tasks;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: MERKEZİ para-iadesi soyutlaması (DRY). İade/iptal/COD hepsi buradan geçer -> ödeme-kaynağına
    // göre bölme mantığı TEK yerde (önceden ReturnManager + OrderManager'da tekrarlanıyordu = duplikasyon-drift riski).
    public interface IRefundService
    {
        // order'ı refundAmount kadar ödeme kaynağına göre iade eder: kartla ödenen -> Iyzico, cüzdanla ödenen -> store credit.
        // Kart yoksa (COD/nakit) TÜM tutar store credit'e. Ambient transaction'a katılır (kendi tx açmaz).
        Task<RefundOutcome> RefundToSourceAsync(Order order, decimal refundAmount, string reason);
    }

    // Açıklayıcı yorum: İade sonucu - success false ise (Iyzico başarısız) caller rollback etmeli.
    public class RefundOutcome
    {
        public bool Success { get; set; }
        public string RefundId { get; set; }
        public decimal OnlineRefunded { get; set; }
        public decimal CreditRefunded { get; set; }
        public static RefundOutcome Fail() => new RefundOutcome { Success = false };
    }
}
