using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Order
{
    // DALGA B / B2 - SAYFALAMA ZARFI TEK KONVANSIYONA HIZALANDI.
    //
    // OLCULEN ONCE-DURUM: GetAllForAdmin, repository katmaninin kendi tipini
    // (Core.Utilities.Dtos.PagedResult<T>) DOGRUDAN HTTP yanitina koyuyordu. O tipin
    // kendi yorumu "repository katmanindan doner, servis DTO'ya cevirir" diyor - yani
    // sizinti bilincli bir tasarim degil, atlanmis bir donusum.
    // Bedeli: PascalCase ozellikler camelCase'e serilesiyor ve zarf
    //   { items, totalCount, page, size, totalPages }
    // olarak cikiyordu; oysa deponun DIGER sayfali uclari (product/filter ve admin urun
    // listesi, ProductPagingListResponseDto) snake_case zarf donuyor:
    //   { items, total_count, page, size, total_pages }
    // AYNI API'de IKI KONVANSIYON. Admin paneli snake_case bekleyip "Items"/"TotalCount"
    // okudugu icin sipariş listesi CANLIDA HEP BOS geliyordu: dashboard "52 siparis"
    // derken Siparisler sekmesi "Siparis yok" diyordu (olculdu).
    //
    // Bu DTO, ProductPagingListResponseDto kalibinin birebir esidir - yeni bir kalip
    // uydurulmadi, var olan konvansiyona hizalandi.
    public class AdminOrderPagingListResponseDto : IDto
    {
        public List<AdminOrderListItemDto> items { get; set; } = new();
        public int total_count { get; set; }
        public int page { get; set; }
        public int size { get; set; }
        public int total_pages { get; set; }
    }
}
