using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.Core.Utilities.Moderation;
using Divisima.Core.Utilities.Sanitization;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Ürün soru-cevap yönetimi. Müşteri sorar (XSS temiz), admin yanıtlar, herkes yanıtlıları görür.
    public class ProductQuestionManager : IProductQuestionService
    {
        private readonly IProductQuestionDal _questionDal;
        private readonly IProductDal _productDal;

        public ProductQuestionManager(IProductQuestionDal questionDal, IProductDal productDal)
        {
            _questionDal = questionDal;
            _productDal = productDal;
        }

        public async Task<(HttpStatusCode, Result)> Ask(int customerId, int productId, string question)
        {
            if (string.IsNullOrWhiteSpace(question) || question.Trim().Length < 5)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.QuestionTooShort));

            // Ürün var mı
            var product = await _productDal.GetAsync(p => p.id == productId && p.is_active);
            if (product == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.ProductNotFound));

            // Küfür/uygunsuz içerik reddi + XSS temizleme
            if (ProfanityFilter.ContainsProfanity(question))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.QuestionProfanity));

            await _questionDal.AddAsync(new ProductQuestion
            {
                product_id = productId,
                customer_id = customerId,
                question = InputSanitizer.Sanitize(question.Trim()),
                is_answered = false,
                is_active = true,
                created_at = DateTime.Now
            });
            return (HttpStatusCode.Created, new SuccessResult(Messages.QuestionAsked));
        }

        public async Task<(HttpStatusCode, Result)> Answer(int questionId, int adminId, string answer)
        {
            if (string.IsNullOrWhiteSpace(answer))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.AnswerEmpty));

            var q = await _questionDal.GetAsync(x => x.id == questionId && x.is_active);
            if (q == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.QuestionNotFound));

            q.answer = InputSanitizer.Sanitize(answer.Trim());
            q.answered_by = adminId;
            q.is_answered = true;
            q.answered_at = DateTime.Now;
            await _questionDal.UpdateAsync(q);
            return (HttpStatusCode.OK, new SuccessResult(Messages.QuestionAnswered));
        }

        // Public: yalnızca yanıtlanmış sorular görünür
        public async Task<(HttpStatusCode, Result)> GetAnsweredByProduct(int productId)
        {
            var list = await _questionDal.GetListNoTrackingAsync(q =>
                q.product_id == productId && q.is_answered && q.is_active);
            return (HttpStatusCode.OK, new SuccessDataResult<List<ProductQuestion>>(list));
        }

        // Admin: yanıt bekleyen sorular
        public async Task<(HttpStatusCode, Result)> GetPending()
        {
            var list = await _questionDal.GetListNoTrackingAsync(q => !q.is_answered && q.is_active);
            return (HttpStatusCode.OK, new SuccessDataResult<List<ProductQuestion>>(list));
        }
    }
}
