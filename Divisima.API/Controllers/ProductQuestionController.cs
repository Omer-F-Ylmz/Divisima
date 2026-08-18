using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Security.Identity;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.ProductQuestion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Ürün soru-cevap. Müşteri sorar (auth), admin yanıtlar, herkes yanıtlıları görür.
    [ApiController]
    [Route("api/product-question")]
    public class ProductQuestionController : SecureControllerBase
    {
        private readonly IProductQuestionService _service;
        public ProductQuestionController(IProductQuestionService service) { _service = service; }

        // Public: bir ürünün yanıtlanmış soruları
        [HttpGet("product/{productId}")]
        [AllowAnonymous]
        public async Task<IActionResult> ByProduct(int productId)
        {
            var r = await _service.GetAnsweredByProduct(productId);
            return StatusCode((int)r.Item1, r.Item2);
        }

        // Müşteri soru sorar (customer_id token'dan - IDOR yok)
        [HttpPost("ask")]
        [RequireUserType(UserTypeEnum.Customer)]
        public async Task<IActionResult> Ask([FromBody] ProductQuestionAskDto dto)
        {
            var r = await _service.Ask(CurrentUserId, dto.product_id, dto.question);
            return StatusCode((int)r.Item1, r.Item2);
        }

        // Admin: yanıt bekleyenler
        [HttpGet("pending")]
        [RequireUserType(UserTypeEnum.Admin)]
        public async Task<IActionResult> Pending()
        {
            var r = await _service.GetPending();
            return StatusCode((int)r.Item1, r.Item2);
        }

        // Admin yanıtlar (answered_by token'dan)
        [HttpPost("answer")]
        [RequireUserType(UserTypeEnum.Admin)]
        public async Task<IActionResult> Answer([FromBody] ProductQuestionAnswerDto dto)
        {
            var r = await _service.Answer(dto.question_id, CurrentUserId, dto.answer);
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
