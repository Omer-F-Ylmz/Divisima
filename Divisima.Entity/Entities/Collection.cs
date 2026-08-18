using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Koleksiyon (frontend COLLECTIONS: sezon + stil elçisi). byte collection_type ile.
    public class Collection : IEntity
    {
        public int id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }
        public byte collection_type { get; set; } // Sezon - Season (0), Stil Elçisi - Ambassador (1)
        public string? curator_name { get; set; } // stil elçisi adı (sezonda null)
        public string? subtitle { get; set; }
        public string? gradient { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
    }
}
