using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Stock;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Stok yönetimi (admin). Sevkiyat girişi / sayım düzeltmesi.
    [Route("api/[controller]")]
    [ApiController]
    [RequireUserType(UserTypeEnum.Admin)]
    [SwaggerTag("Stok yönetimi (admin)")]
    public class StockController : ControllerBase
    {
        private readonly IStockService _stockService;
        public StockController(IStockService stockService) { _stockService = stockService; }

        [HttpPost("adjust")]
        [SwaggerOperation(Summary = "Stok düzelt (yeni sevkiyat / sayım)")]
        public async Task<IActionResult> Adjust([FromBody] StockAdjustDto dto)
        {
            var r = await _stockService.AdjustStock(dto.product_id, dto.size, dto.new_quantity, dto.note ?? "");
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
