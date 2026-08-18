using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Beden-stok DAL arayüzü. Ortak CRUD IEntityRepository'den gelir.
    public interface IProductStockDal : IEntityRepository<ProductStock>
    {
        // Aciklayici yorum: ATOMIK rezervasyon onayi - fiziksel stok VE rezerve tek UPDATE'te duser (concurrency exception yok).
        Task<int> ConfirmStockAsync(int productId, string size, int quantity);

        // Aciklayici yorum: Odeme basarili AMA rezervasyon expire olduysa (expiry job serbest birakmis) - stok MEVCUTSA
        // (available >= qty) DOGRUDAN dus (reserved'a dokunma; o zaten geri verildi). Musteri odedi, stok gerekli.
        // 0 doner = stok kalmamis (baskasi almis) -> caller manuel mudahale (iade/tedarik) icin isaretler.
        Task<int> TryDirectDeductAsync(int productId, string size, int quantity);
        // Aciklayici yorum: ATOMIK rezerve serbest birakma (0 altina inmez).
        Task<int> ReleaseReservedAsync(int productId, string size, int quantity);
        // Aciklayici yorum: ATOMIK fiziksel stok artisi (iade/iptal - concurrency exception yok).
        Task<int> IncrementStockQuantityAsync(int productId, string size, int quantity);
    }
}
