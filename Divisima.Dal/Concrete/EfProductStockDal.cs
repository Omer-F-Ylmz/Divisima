using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Beden-stok DAL implementasyonu. Generic CRUD base yeterli.
    public class EfProductStockDal : EfEntityRepositoryBase<ProductStock, DivisimaDbContext>, IProductStockDal
    {
        public EfProductStockDal(DivisimaDbContext context) : base(context)
        {
        }

        // Aciklayici yorum: ATOMIK rezervasyon (CAS) - musait miktar yetiyorsa reserved artar, yetmiyorsa
        // hicbir satir etkilenmez. WHERE kosulu ve UPDATE ayni ifadede oldugu icin "kontrol et sonra yaz"
        // araligi YOKTUR; iki eszamanli cagri asla birbirinin uzerine yazamaz ve overselling olusmaz.
        public async Task<int> TryReserveAsync(int productId, string size, int quantity)
        {
            return await Context.Set<ProductStock>()
                .Where(s => s.product_id == productId && s.size == size && s.is_active
                            && s.stock_quantity - s.reserved_quantity >= quantity)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.reserved_quantity, s => s.reserved_quantity + quantity));
        }

        // Aciklayici yorum: ATOMIK onay - stock_quantity ve reserved_quantity tek UPDATE'te (row_version cakismasi olmaz).
        // reserved 0 altina inmez (CASE). ReserveStock retry'i ile ayni satirda guvenle calisir.
        public async Task<int> ConfirmStockAsync(int productId, string size, int quantity)
        {
            return await Context.Set<ProductStock>()
                .Where(s => s.product_id == productId && s.size == size && s.is_active)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.stock_quantity, s => s.stock_quantity - quantity)
                    .SetProperty(s => s.reserved_quantity, s => s.reserved_quantity >= quantity ? s.reserved_quantity - quantity : 0));
        }

        // Aciklayici yorum: ATOMIK dogrudan stok dusumu - YALNIZCA available (stock - reserved) >= qty ise (CAS).
        // Odeme onayli ama rezervasyon expire senaryosu icin: reserved'a dokunmadan stogu dus.
        public async Task<int> TryDirectDeductAsync(int productId, string size, int quantity)
        {
            return await Context.Set<ProductStock>()
                .Where(s => s.product_id == productId && s.size == size && s.is_active
                            && s.stock_quantity - s.reserved_quantity >= quantity)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.stock_quantity, s => s.stock_quantity - quantity));
        }

        // Aciklayici yorum: ATOMIK rezerve serbest (0 altina inmez).
        public async Task<int> ReleaseReservedAsync(int productId, string size, int quantity)
        {
            return await Context.Set<ProductStock>()
                .Where(s => s.product_id == productId && s.size == size && s.is_active)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.reserved_quantity, s => s.reserved_quantity >= quantity ? s.reserved_quantity - quantity : 0));
        }

        // Aciklayici yorum: ATOMIK fiziksel stok artisi (iade - tek UPDATE, concurrency exception yok).
        public async Task<int> IncrementStockQuantityAsync(int productId, string size, int quantity)
        {
            return await Context.Set<ProductStock>()
                .Where(s => s.product_id == productId && s.size == size && s.is_active)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.stock_quantity, s => s.stock_quantity + quantity));
        }
    }
}
