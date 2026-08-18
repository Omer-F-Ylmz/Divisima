using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.ProductQuestion
{
    public class ProductQuestionAnswerDto : IDto
    {
        public int question_id { get; set; }
        public string answer { get; set; }
    }
}
