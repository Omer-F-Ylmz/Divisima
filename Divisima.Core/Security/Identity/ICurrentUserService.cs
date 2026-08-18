namespace Divisima.Core.Security.Identity
{
    // Açıklayıcı yorum: Oturum açmış kullanıcının kimliğini JWT token'ından okur.
    // KRİTİK: Müşteriye ait işlemlerde customer_id ASLA istemci girdisinden alınmaz - buradan alınır.
    // Böylece bir müşteri başka müşterinin verisine erişemez (IDOR engeli).
    public interface ICurrentUserService
    {
        int? UserId { get; }        // token'daki kullanıcı id (null = anonim)
        int? UserType { get; }      // Admin (1) / Customer (2)
        bool IsAuthenticated { get; }
        bool IsAdmin { get; }
        // Açıklayıcı yorum: Müşteri id'si zorunlu olan yerlerde çağrılır; yoksa yetkisiz.
        int GetRequiredUserId();
    }
}
