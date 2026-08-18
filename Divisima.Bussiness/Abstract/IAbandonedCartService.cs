namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Terk edilmiş sepet hatırlatması. Atıl (uzun süre dokunulmamış) dolu sepetlere e-posta.
    public interface IAbandonedCartService
    {
        // Açıklayıcı yorum: Atıl sepetleri tarar, hatırlatma e-postası gönderir. Gönderilen sepet sayısını döner.
        Task<int> SendReminders();
    }
}
