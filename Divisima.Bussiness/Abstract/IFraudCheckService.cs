using System.Threading.Tasks;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Fraud/hız kontrolü. Kısa sürede çok ödeme denemesi = kart testi saldırısı şüphesi.
    public interface IFraudCheckService
    {
        // Açıklayıcı yorum: Bu müşteri şu an ödeme denemesi yapabilir mi? TEK ATOMİK çağrı - sayacı da artırır
        // (ayrı RecordAttempt YOK -> TOCTOU boşluğu yok). false = limit aşıldı, engelle.
        Task<bool> CanAttemptPaymentAsync(int customerId);
    }
}
