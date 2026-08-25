using System.Linq.Expressions;
using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Dtos;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Audit;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // ═══ FAZ 0 / K6 ═══════════════════════════════════════════════════════════════════
    // DAVRANIS AYNEN TASINDI (controller'dan): sayfalama + opsiyonel table_name esitlik
    // filtresi + created_at DESC. Degisen TEK sey, disari cikan SEKIL: repository tipi
    // PagedResult<T> ve ham entity yerine snake_case zarf + DTO.
    public class AuditLogManager : IAuditLogService
    {
        private readonly IAuditLogDal _auditLogDal;

        public AuditLogManager(IAuditLogDal auditLogDal)
        {
            _auditLogDal = auditLogDal;
        }

        public async Task<(HttpStatusCode, Result)> GetPagedAsync(PagingRequestDto paging, string? tableName)
        {
            // Filtre YALNIZ deger verilmisse kurulur - null gecmek "filtresiz" demektir.
            // (Controller'daki davranisin ta kendisi; ayrica CS8604 uyarisinin kaynagi olan
            //  `null` literal gecisi burada tipli bir degiskene alindi.)
            Expression<Func<AuditLog, bool>>? filter = null;
            if (!string.IsNullOrWhiteSpace(tableName))
                filter = a => a.table_name == tableName;

            var sayfa = await _auditLogDal.GetPagedAsync(
                paging,
                filter: filter!,
                orderBy: a => a.created_at,
                descending: true);

            var zarf = new AuditLogPagingListResponseDto
            {
                items = sayfa.Items.Select(a => new AuditLogListItemDto
                {
                    id = a.id,
                    table_name = a.table_name,
                    entity_id = a.entity_id,
                    action = a.action,
                    changes = a.changes,
                    user_id = a.user_id,
                    created_at = a.created_at
                }).ToList(),
                total_count = sayfa.TotalCount,
                page = sayfa.Page,
                size = sayfa.Size,
                total_pages = sayfa.TotalPages
            };

            return (HttpStatusCode.OK, new SuccessDataResult<AuditLogPagingListResponseDto>(zarf, "Denetim kayıtları listelendi."));
        }
    }
}
