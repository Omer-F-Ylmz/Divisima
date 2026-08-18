namespace Divisima.Core.Utilities.Dtos
{
    // Açıklayıcı yorum: Generic sayfalı sonuç (repository katmanından döner, servis DTO'ya çevirir).
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int Size { get; set; }
        public int TotalPages => Size > 0 ? (int)Math.Ceiling(TotalCount / (double)Size) : 0;
    }
}
