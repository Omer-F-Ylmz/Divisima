namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Müşteri etkileşim kampanyaları (job'lardan çağrılır). Her metod gönderilen e-posta sayısını döner.
    public interface IEngagementService
    {
        Task<int> SendBirthdayOffers();   // bugün doğum günü olanlara
        Task<int> SendWinBackCampaigns(); // uzun süredir sipariş vermeyenlere
        Task<int> SendReviewInvites();    // teslimden N gün sonra yorum daveti
    }
}
