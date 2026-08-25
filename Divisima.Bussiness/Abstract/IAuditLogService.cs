using System.Net;
using Divisima.Core.Utilities.Dtos;
using Divisima.Core.Utilities.Results;

namespace Divisima.Bussiness.Abstract
{
    // ═══ FAZ 0 / K6 - DENETIM KAYDI IS SERVISI ═════════════════════════════════════════
    //
    // NEDEN VAR: AuditLogController, 40 controller icinde is katmanini ATLAYIP dogrudan
    // IAuditLogDal enjekte eden TEK ornekti (olculdu). Bunun iki somut bedeli vardi:
    //  (1) Repository tipi PagedResult<T> HTTP'ye siziyordu (B2 defekt sinifinin ikinci
    //      ornegi - ayrinti AuditLogPagingListResponseDto'nun basinda),
    //  (2) Ham entity disari cikiyordu.
    // Uc bugun hicbir istemci tarafindan cagrilmadigi icin hizalamanin kirilma riski SIFIR.
    public interface IAuditLogService
    {
        // Sayfali denetim kaydi listesi. tableName verilirse table_name ESITLIK filtresi
        // uygulanir; siralama her zaman created_at DESC (en yeni once) - bu davranis
        // controller'dan TASINDI, degistirilmedi.
        Task<(HttpStatusCode, Result)> GetPagedAsync(PagingRequestDto paging, string? tableName);
    }
}
