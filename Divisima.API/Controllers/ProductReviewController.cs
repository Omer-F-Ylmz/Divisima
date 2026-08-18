using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Security.Identity;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.ProductReview;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Ürün yorumu controller'ı. Müşteri ekler, admin onaylar, herkes onaylıları görür.
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Ürün yorumları")]
    public class ProductReviewController : ControllerBase
    {
        private readonly IProductReviewService _productReviewService;
        private readonly ICurrentUserService _currentUser;

        public ProductReviewController(IProductReviewService productReviewService, ICurrentUserService currentUser)
        {
            _productReviewService = productReviewService;
            _currentUser = currentUser;
        }

        [HttpPost("add")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Yorum ekle", Description = "Ürüne yorum ekler (onay bekler). Müşteri yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.Created)]
        public async Task<IActionResult> Add([FromBody] ProductReviewAddRequestDto dto)
        {
            // Açıklayıcı yorum: Yorum sahibi token'dan (başkası adına yorum yazılamaz)
            dto.customer_id = _currentUser.GetRequiredUserId();
            var result = await _productReviewService.Add(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpPost("vote-helpful/{reviewId:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Yorumu faydalı bul", Description = "Yoruma 'faydalı' oyu (müşteri başına tek).")]
        public async Task<IActionResult> VoteHelpful(int reviewId)
        {
            var result = await _productReviewService.VoteHelpful(reviewId, _currentUser.GetRequiredUserId());
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpPatch("approve/{id:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Yorum onayla", Description = "Yorumu onaylar. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Approve(int id)
        {
            var result = await _productReviewService.Approve(id);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpPatch("reject/{id:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Yorum reddet", Description = "Yorumu reddeder. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Reject(int id)
        {
            var result = await _productReviewService.Reject(id);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpGet("product/{productId:int:min(1)}")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Ürün yorumları", Description = "Ürünün onaylı yorumlarını listeler.")]
        [ProducesResponseType(typeof(SuccessDataResult<List<ProductReviewResponseDto>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetByProduct(int productId)
        {
            var result = await _productReviewService.GetByProduct(productId);
            return StatusCode((int)result.Item1, result.Item2);
        }
    }
}
