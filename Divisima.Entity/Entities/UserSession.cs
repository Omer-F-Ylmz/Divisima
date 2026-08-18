using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Oturum kaydı (Cafixo UserSession kalıbı) - login'de oluşur.
    public class UserSession : IEntity
    {
        public int id { get; set; }
        public int customer_id { get; set; }
        public string refresh_token { get; set; }
        public string? device { get; set; }
        public string? ip_address { get; set; }
        public DateTime expires_at { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
    }
}
