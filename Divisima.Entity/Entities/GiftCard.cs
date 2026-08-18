using Divisima.Core.Entities.Abstract;
namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Hediye kartı. Bozdurulunca bakiye mağaza kredisine aktarılır.
    public class GiftCard : IEntity
    {
        public int id { get; set; }
        public string code { get; set; } // benzersiz kod
        public decimal initial_amount { get; set; }
        public decimal balance { get; set; } // kalan (kısmi bozdurma destekli)
        public bool is_active { get; set; }
        public int? redeemed_by { get; set; } // bozduran müşteri (ilk)
        public DateTime created_at { get; set; }
        public DateTime? redeemed_at { get; set; }
    }
}
