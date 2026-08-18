using Divisima.Core.DataAccess;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Dtos;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Denetim kaydı sorgulama (yalnızca admin).
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Denetim kayıtları (admin)")]
    public class AuditLogController : ControllerBase
    {
        private readonly IAuditLogDal _auditLogDal;
        public AuditLogController(IAuditLogDal auditLogDal) { _auditLogDal = auditLogDal; }

        // Açıklayıcı yorum: Sayfalı denetim kaydı listesi (en yeni önce)
        [HttpGet("list")]
        [RequireUserType(UserTypeEnum.Admin)]
        public async Task<IActionResult> List([FromQuery] PagingRequestDto paging, [FromQuery] string? tableName)
        {
            var result = await _auditLogDal.GetPagedAsync(
                paging,
                filter: string.IsNullOrEmpty(tableName) ? null : a => a.table_name == tableName,
                orderBy: a => a.created_at,
                descending: true);
            return Ok(new SuccessDataResult<PagedResult<AuditLog>>(result, "Denetim kayıtları listelendi."));
        }
    }
}
