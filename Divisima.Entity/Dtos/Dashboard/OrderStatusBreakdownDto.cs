using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Dashboard
{
    // Açıklayıcı yorum: Sipariş durumu dağılımı - her durumda kaç sipariş (pasta grafik).
    public class OrderStatusBreakdownDto : IDto
    {
        public byte status { get; set; }
        public string status_name { get; set; }
        public int count { get; set; }
    }
}
