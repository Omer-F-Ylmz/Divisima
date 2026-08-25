using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Dtos;
using Divisima.Core.Utilities.Enums;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Denetim kaydı sorgulama (yalnızca admin).
    //
    // ═══ FAZ 0 / K6 - IS KATMANINDAN GECIYOR ═══════════════════════════════════════════
    // ONCE: bu controller IAuditLogDal'i DOGRUDAN enjekte ediyordu - 40 controller icinde
    // TEK ornek (olculdu). Iki somut bedeli vardi: (1) repository tipi PagedResult<T>
    // HTTP'ye siziyordu (DALGA B / B2 defekt sinifinin IKINCI ornegi: camelCase
    // {items,totalCount,...} vs deponun snake_case {items,total_count,...} konvansiyonu),
    // (2) ham entity AuditLog disari cikiyordu.
    // Hizalamanin kirilma riski SIFIR olculdu: uc bugun HIC CAGRILMIYOR (api-client.js
    // `auditLogs()` tanimli ama cagiran yok; admin.html'de denetim ekrani yok).
    // Davranis DEGISMEDI: sayfalama + opsiyonel table_name esitlik filtresi + created_at DESC.
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Denetim kayıtları (admin)")]
    public class AuditLogController : ControllerBase
    {
        private readonly IAuditLogService _auditLogService;
        public AuditLogController(IAuditLogService auditLogService) { _auditLogService = auditLogService; }

        // Açıklayıcı yorum: Sayfalı denetim kaydı listesi (en yeni önce)
        [HttpGet("list")]
        [RequireUserType(UserTypeEnum.Admin)]
        public async Task<IActionResult> List([FromQuery] PagingRequestDto paging, [FromQuery] string? tableName)
        {
            var r = await _auditLogService.GetPagedAsync(paging, tableName);
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
