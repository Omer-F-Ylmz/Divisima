using System.Collections.Generic;
using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Audit
{
    // ═══ FAZ 0 / K6 - SAYFALAMA ZARFI TEK KONVANSIYONA HIZALANDI ═══════════════════════
    //
    // Bu, DALGA B / B2'de olculup duzeltilen defektin IKINCI ORNEGIDIR. Orada
    // `GetAllForAdmin`, repository katmaninin kendi tipini (Core.Utilities.Dtos.PagedResult<T>)
    // DOGRUDAN HTTP yanitina koyuyordu; tipin kendi yorumu "repository katmanindan doner,
    // servis DTO'ya cevirir" diyor - yani sizinti bilincli bir tasarim degil, ATLANMIS bir
    // donusum. Bedeli canlida olculmustu: PascalCase ozellikler camelCase'e serilesip zarf
    //   { items, totalCount, page, size, totalPages }
    // olarak cikiyor, oysa deponun diger sayfali uclari snake_case zarf donuyordu
    //   { items, total_count, page, size, total_pages }
    // ve admin paneli siparis listesini HEP BOS goruyordu.
    //
    // AuditLogController ayni sizintiyi tasiyordu (SuccessDataResult<PagedResult<AuditLog>>).
    // FARK: bu uc bugun HIC CAGRILMIYOR (api-client.js:570 `auditLogs()` tanimli ama cagiran
    // yok, admin.html'de denetim ekrani yok) - yani kirilacak istemci YOK ve hizalamak icin
    // EN UCUZ AN burasi. Yeni bir kalip uydurulmadi: ProductPagingListResponseDto /
    // AdminOrderPagingListResponseDto ile BIREBIR ayni alan seti.
    public class AuditLogPagingListResponseDto : IDto
    {
        public List<AuditLogListItemDto> items { get; set; } = new();
        public int total_count { get; set; }
        public int page { get; set; }
        public int size { get; set; }
        public int total_pages { get; set; }
    }
}
