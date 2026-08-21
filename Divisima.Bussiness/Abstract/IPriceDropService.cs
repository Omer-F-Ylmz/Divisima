using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Notification;
using Divisima.Entity.Dtos.PriceDrop;
namespace Divisima.Bussiness.Abstract
{
    public interface IPriceDropService
    {
        Task<(HttpStatusCode, Result)> Subscribe(PriceDropSubscribeDto dto);
        Task NotifyPriceDrop(int productId, decimal newPrice);

        // SPRINT 8 MADDE 10 - ABONELIK YONETIMI.
        // Onceden backend'de YALNIZ "subscribe" vardi (tum controller'lar tarandi): kullanici
        // kurdugu bildirimi ne GOREBILIYOR ne KAPATABILIYORDU. Uc kalem birlikte geldi.

        // Giris yapmis musterinin abonelikleri (kendi e-postasiyla eslesenler).
        Task<(HttpStatusCode, Result)> GetMine(string email);

        // Giris yapmis musteri kendi aboneligini siler. `email` sahiplik kontrolu icin -
        // yalniz id ile silmek IDOR olurdu (baskasinin aboneligini silme).
        Task<(HttpStatusCode, Result)> RemoveMine(int id, string email);

        // E-postadaki baglantidan cikma - ANONIM, jetonla. Kimlik dogrulamasi YOK cunku abone
        // uye olmayabilir. Jeton tahmin edilemez oldugu icin sahiplik kanitidir.
        Task<(HttpStatusCode, Result)> UnsubscribeByToken(string token);
    }
}
