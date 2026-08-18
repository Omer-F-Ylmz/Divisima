using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Fatura uçları. Müşteri kendi faturalarını görür; admin sipariş için fatura üretir.
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Fatura")]
    public class InvoiceController : SecureControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        public InvoiceController(IInvoiceService invoiceService) { _invoiceService = invoiceService; }

        [HttpPost("generate/{orderId}")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Sipariş için fatura oluştur (admin)")]
        public async Task<IActionResult> Generate(int orderId)
        {
            var r = await _invoiceService.GenerateForOrder(orderId);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpGet("my")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Faturalarım")]
        public async Task<IActionResult> MyInvoices()
        {
            var r = await _invoiceService.GetMyInvoices(CurrentCustomerId);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpGet("order/{orderId}")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Siparişin faturası")]
        public async Task<IActionResult> ByOrder(int orderId)
        {
            var r = await _invoiceService.GetByOrder(orderId, CurrentCustomerId);
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
