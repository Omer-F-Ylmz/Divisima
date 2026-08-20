using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Sipariş DAL. Müşteriye göre getirme (kalemler serviste ayrı yüklenir).
    public interface IOrderDal : IEntityRepository<Order>
    {
        // KUMULATIF IADE REZERVASYONU (CAS): refunded_amount'i YALNIZCA beklenen degerdeyken artirir.
        // 1 donerse tutar bu cagriya tahsis edilmistir; 0 donerse eszamanli baska bir iade araya
        // girmistir (cagiran taze deger ile yeniden dener). Toplam total_price'i asamaz.
        Task<int> TryAddRefundedAmountAsync(int orderId, decimal amount, decimal expectedCurrent);

        // Basarisiz saglayici iadesinden sonra rezervasyonu geri birak (tahsisi serbest birakir).
        Task<int> ReleaseRefundedAmountAsync(int orderId, decimal amount);
    }
}
