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

        // E4a: ADMIN stok detayi - beden basina fiziksel/rezerve/satilabilir.
        // Sinif duzeyindeki [RequireUserType(UserTypeEnum.Admin)] burayi da kapsar: rezerve bilgisi
        // ("kac adet acik siparislerce tutuluyor") anonim yuzeye SIZMAZ. Bu yuzden mevcut
        // ProductStockDto'ya alan eklemek yerine AYRI admin ucu acildi.
        [HttpGet("{productId:int:min(1)}")]
        [SwaggerOperation(Summary = "Ürün stok detayı (admin)", Description = "Beden bazında fiziksel stok, rezerve ve satılabilir miktar.")]
        public async Task<IActionResult> Detail(int productId)
        {
            var r = await _stockService.GetStockDetail(productId);
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
