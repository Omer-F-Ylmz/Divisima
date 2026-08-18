using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Seller;
using Microsoft.AspNetCore.Mvc;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Satıcı paneli - YALNIZCA Seller tipi erişebilir. Tüm işlemler oturumdaki satıcının
    // id'sine (CurrentSellerId, JWT'den) göre izole edilir; client'tan gelen seller_id ASLA kullanılmaz (IDOR engeli).
    [Route("api/seller")]
    [ApiController]
    [RequireUserType(UserTypeEnum.Seller)]
    public class SellerController : SecureControllerBase
    {
        private readonly ISellerService _sellerService;

        public SellerController(ISellerService sellerService)
        {
            _sellerService = sellerService;
        }

        // Açıklayıcı yorum: Panel özeti - satıcının ürün/satış/gelir durumu tek bakışta.
        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(SuccessDataResult<SellerDashboardResponseDto>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Dashboard()
        {
            var result = await _sellerService.GetDashboardAsync(CurrentSellerId);
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Satıcının ürünleri + ürün başına satış performansı.
        [HttpGet("products")]
        [ProducesResponseType(typeof(SuccessDataResult<List<SellerProductResponseDto>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> MyProducts()
        {
            var result = await _sellerService.GetMyProductsAsync(CurrentSellerId);
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Satıcının satış kalemleri (kendi ürünlerini içeren sipariş kalemleri).
        [HttpGet("sales")]
        [ProducesResponseType(typeof(SuccessDataResult<List<SellerSaleItemResponseDto>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> MySales()
        {
            var result = await _sellerService.GetMySalesAsync(CurrentSellerId);
            return StatusCode((int)result.Item1, result.Item2);
        }
    }
}
