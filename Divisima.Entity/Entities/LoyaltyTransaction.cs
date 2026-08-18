using Divisima.Core.Entities.Abstract;
namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Sadakat puanı defteri (kazanım/harcama denetim izi). Bakiye Customer.loyalty_points.
    public class LoyaltyTransaction : IEntity
    {
        public int id { get; set; }
        public int customer_id { get; set; }
        public int points { get; set; } // her zaman pozitif; yön 'type' ile
        public byte type { get; set; } // LedgerEntryTypeEnum
        public string reason { get; set; }
        public int? order_id { get; set; }
        public DateTime created_at { get; set; }
    }
}
