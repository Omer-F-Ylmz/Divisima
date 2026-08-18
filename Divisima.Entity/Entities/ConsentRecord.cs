using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: KVKK/GDPR açık rıza kaydı. Rızanın ALINDIĞINI kanıtlamak için (metni göstermek yetmez).
    // Kayıt/checkout anında yazılır: hangi metin (tür + versiyon), ne zaman, hangi IP'den kabul edildi.
    public class ConsentRecord : IEntity
    {
        public int id { get; set; }
        public int? customer_id { get; set; }        // misafir onayı için null olabilir
        public string consent_type { get; set; }      // "terms", "privacy", "marketing", "distance_sales", "kvkk"
        public string document_version { get; set; }   // kabul edilen metin versiyonu (ör. "2025-01")
        public bool granted { get; set; }              // kabul(true) / ret(false) - pazarlama için ret de saklanir
        public string? ip_address { get; set; }        // rıza anındaki IP (kanıt)
        public string? user_agent { get; set; }        // tarayıcı (kanıt)
        public DateTime created_at { get; set; }
    }
}
