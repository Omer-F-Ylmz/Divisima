using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Loyalty
{
    // Açıklayıcı yorum: Sadakat seviyesi yanıtı (rozet + ilerleme).
    public class LoyaltyTierDto : IDto
    {
        public string tier { get; set; }
        public decimal total_spent { get; set; }
        public decimal point_multiplier { get; set; }
        public decimal amount_to_next_tier { get; set; }
    }
}
