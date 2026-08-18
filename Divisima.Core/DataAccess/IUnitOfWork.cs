namespace Divisima.Core.DataAccess
{
    // Açıklayıcı yorum: Birim-of-iş (transaction) soyutlaması. Çok adımlı işlemleri (PlaceOrder gibi)
    // tek atomik transaction'da toplar - hata olursa hepsi geri alınır (all-or-nothing).
    public interface IUnitOfWork
    {
        Task BeginTransactionAsync();
        Task CommitAsync();
        Task RollbackAsync();
        // Aciklayici yorum: RETRY-GUVENLI transaction. EnableRetryOnFailure ile uyumlu tek yol -
        // execution strategy tum begin->is->commit'i tek retriable delege olarak sarar.
        // operation basariyla donerse commit, exception firlarsa rollback. Yeni transaction'lar bunu kullanmali.
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation);
    }
}
