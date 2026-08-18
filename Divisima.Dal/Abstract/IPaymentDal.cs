using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Ödeme DAL. conversation_id ile eşleme.
    public interface IPaymentDal : IEntityRepository<Payment>
    {
        Task<Payment> GetByConversationIdAsync(string conversationId);
    }
}
