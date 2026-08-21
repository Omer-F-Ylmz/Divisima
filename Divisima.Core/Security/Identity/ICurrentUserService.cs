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

        // SPRINT 8 MADDE 10: bildirim abonelikleri E-POSTA ile anahtarlaniyor (uc anonim de
        // kullanilabildigi icin customer_id yok). "Aboneliklerim" ve "kendi aboneligimi sil"
        // uclari bu yuzden token'daki e-postaya ihtiyac duyuyor. Istemci girdisinden ALINMAZ -
        // JWT claim'inden okunur (IDOR engeli, GetRequiredUserId ile ayni gerekce).
        string GetRequiredEmail();
    }
}
