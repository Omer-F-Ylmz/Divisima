using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.ProductQuestion
{
    public class ProductQuestionAskDto : IDto
    {
        public int product_id { get; set; }
        public string question { get; set; }
    }
}
