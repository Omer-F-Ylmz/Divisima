namespace Divisima.Core.Utilities.Dtos
{
    // Açıklayıcı yorum: Sayfalama isteği tabanı (tüm filtreli listeler bunu miras alır/içerir).
    public class PagingRequestDto : IDto
    {
        private int _page = 1;
        private int _size = 20;

        public int page { get => _page; set => _page = value < 1 ? 1 : value; }
        // Açıklayıcı yorum: Sayfa boyutu 1-100 arası sınırlanır (aşırı yük koruması)
        public int size { get => _size; set => _size = value < 1 ? 20 : (value > 100 ? 100 : value); }
    }
}
