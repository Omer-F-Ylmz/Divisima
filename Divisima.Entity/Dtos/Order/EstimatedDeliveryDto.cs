using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Order
{
    public class EstimatedDeliveryDto : IDto
    {
        public int order_id { get; set; }
        public DateTime earliest { get; set; }
        public DateTime latest { get; set; }
    }
}
