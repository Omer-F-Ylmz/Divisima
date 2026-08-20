namespace Divisima.Core.Utilities.Caching
{
    // Açıklayıcı yorum: Cache anahtarları TEK yerden üretilir. Yazan ile silen (invalidate eden)
    // farklı katmanlarda olduğu için anahtarı elle string olarak yazmak sessiz hataya açıktır:
    // biri "cust-active:5" yazar, diğeri "customer-active:5" siler ve ban hiç etkili olmaz.
    public static class CacheKeys
    {
        // Müşteri hesabının aktif olup olmadığı. TokenBlacklistMiddleware her kimlikli istekte okur;
        // askıya alma (AdminCustomerManager.SetActive) ve hesap silme yolları düşürür.
        public static string CustomerActive(int customerId) => $"cust-active:{customerId}";
    }
}
