namespace Divisima.Core.Utilities.Dtos
{
    // Açıklayıcı yorum: Sayfalama meta bilgisi (senin {X}PagingListResponseDto kalıbının PagingInformation'ı).
    public class PagingInfo
    {
        public int Page { get; set; }
        public int Size { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
