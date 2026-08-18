using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Admin dashboard/rapor uçları. Tümü Admin yetkisi ister (satış/istatistik gizli veri).
    [Route("api/[controller]")]
    [ApiController]
    [RequireUserType(UserTypeEnum.Admin)]
    [SwaggerTag("Admin dashboard ve raporlar (yalnız admin)")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        public DashboardController(IDashboardService dashboardService) { _dashboardService = dashboardService; }

        [HttpGet("summary")]
        [SwaggerOperation(Summary = "Genel özet", Description = "Ciro, sipariş, ortalama sepet, müşteri, stok uyarısı.")]
        public async Task<IActionResult> Summary()
        {
            var r = await _dashboardService.GetSummary();
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpGet("daily-sales")]
        [SwaggerOperation(Summary = "Günlük satış grafiği", Description = "Tarih aralığında günlük ciro + sipariş sayısı.")]
        public async Task<IActionResult> DailySales([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            // Açıklayıcı yorum: Varsayılan son 30 gün
            var endDate = end ?? DateTime.Now;
            var startDate = start ?? endDate.AddDays(-30);
            var r = await _dashboardService.GetDailySales(startDate, endDate);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpGet("top-products")]
        [SwaggerOperation(Summary = "En çok satan ürünler", Description = "Satılan adede göre ilk N ürün.")]
        public async Task<IActionResult> TopProducts([FromQuery] int top = 10)
        {
            var r = await _dashboardService.GetTopProducts(top);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpGet("order-status")]
        [SwaggerOperation(Summary = "Sipariş durumu dağılımı", Description = "Her durumda kaç sipariş (pasta grafik).")]
        public async Task<IActionResult> OrderStatus()
        {
            var r = await _dashboardService.GetOrderStatusBreakdown();
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpGet("low-stock")]
        [SwaggerOperation(Summary = "Stok uyarıları", Description = "Eşik altındaki ürün/beden listesi.")]
        public async Task<IActionResult> LowStock([FromQuery] int threshold = 5)
        {
            var r = await _dashboardService.GetLowStock(threshold);
            return StatusCode((int)r.Item1, r.Item2);
        }

        // Kategori bazli satis raporu (admin)
        [HttpGet("sales-by-category")]
        public async Task<IActionResult> SalesByCategory([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            var s0 = start ?? DateTime.Now.AddDays(-30);
            var e0 = end ?? DateTime.Now;
            var result = await _dashboardService.GetSalesByCategory(s0, e0);
            return StatusCode((int)result.Item1, result.Item2);
        }

    }
}
