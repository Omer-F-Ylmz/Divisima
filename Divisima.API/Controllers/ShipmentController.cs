using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Shipping;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Kargo uçları. Admin kargo oluşturur; müşteri kendi siparişini takip eder.
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Kargo takip")]
    public class ShipmentController : SecureControllerBase
    {
        private readonly IShipmentService _shipmentService;
        public ShipmentController(IShipmentService shipmentService) { _shipmentService = shipmentService; }

        [HttpPost("create")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Kargo oluştur - takip no (admin)")]
        public async Task<IActionResult> Create([FromBody] ShipmentCreateDto dto)
        {
            var r = await _shipmentService.CreateShipment(dto);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpGet("track/{orderId}")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Siparişimi takip et", Description = "Kargo firmasından güncel durumu çeker.")]
        public async Task<IActionResult> Track(int orderId)
        {
            var r = await _shipmentService.TrackByOrder(orderId, CurrentCustomerId);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpGet("order/{orderId}")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Siparişin kargosu (admin)")]
        public async Task<IActionResult> ByOrder(int orderId)
        {
            var r = await _shipmentService.GetByOrderForAdmin(orderId);
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
