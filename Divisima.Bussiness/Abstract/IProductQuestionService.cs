using System.Net;
using Divisima.Core.Utilities.Results;
namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Ürün soru-cevap iş servisi.
    public interface IProductQuestionService
    {
        Task<(HttpStatusCode, Result)> Ask(int customerId, int productId, string question);
        Task<(HttpStatusCode, Result)> Answer(int questionId, int adminId, string answer);
        Task<(HttpStatusCode, Result)> GetAnsweredByProduct(int productId);
        Task<(HttpStatusCode, Result)> GetPending();
    }
}
