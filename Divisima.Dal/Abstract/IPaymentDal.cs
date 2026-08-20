using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Ödeme DAL. conversation_id ile eşleme.
    public interface IPaymentDal : IEntityRepository<Payment>
    {
        Task<Payment> GetByConversationIdAsync(string conversationId);

        // ATOMIK DURUM GECISI (WHERE payment_status=from). Tek kazanan birakir: eszamanli
        // callback'lerden yalnizca biri 1 doner, digerleri 0 alir ve yan etki UYGULAMAZ.
        Task<int> TryTransitionStatusAsync(int paymentId, byte fromStatus, byte toStatus);
    }
}
