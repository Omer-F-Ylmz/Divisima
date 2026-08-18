using Divisima.Core.Entities.Abstract;
namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Mağaza kredisi defteri. Bakiye Customer.store_credit.
    public class StoreCreditTransaction : IEntity
    {
        public int id { get; set; }
        public int customer_id { get; set; }
        public decimal amount { get; set; } // her zaman pozitif; yön 'type' ile
        public byte type { get; set; } // LedgerEntryTypeEnum (Earn=kredi ekle, Redeem=kullan)
        public string reason { get; set; }
        public int? order_id { get; set; }
        public DateTime created_at { get; set; }
    }
}
