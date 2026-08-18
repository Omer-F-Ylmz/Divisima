namespace Divisima.Core.Security.JWT
{
    // Açıklayıcı yorum: İptal edilen access token'ların (jti) listesi. Logout/şifre değişiminde token buraya eklenir.
    // Her istekte middleware jti'yi kontrol eder; kara listedeyse token reddedilir (kısa ömür + anında iptal).
    public interface ITokenBlacklist
    {
        Task RevokeAsync(string jti, DateTime expiresAt);
        Task<bool> IsRevokedAsync(string jti);
    }
}
