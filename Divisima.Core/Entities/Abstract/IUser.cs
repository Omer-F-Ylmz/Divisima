namespace Divisima.Core.Entities.Abstract
{
    // Açıklayıcı yorum: Kimlik doğrulanabilir kullanıcılar için arayüz (Customer, AdminUser).
    public interface IUser
    {
        int id { get; set; }
        string email { get; set; }
        byte user_type { get; set; }   // Admin (1) / Customer (2) - JWT claim + yetkilendirme
        byte[] password_hash { get; set; }
        byte[] password_salt { get; set; }
    }
}
