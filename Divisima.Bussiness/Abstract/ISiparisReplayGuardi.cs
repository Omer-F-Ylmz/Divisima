using System.Collections.Generic;
using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Order;

namespace Divisima.Bussiness.Abstract
{
    // ══ GF-6 / K1 (D1) - request_id REPLAY GUARD'I: TEK KAYNAK ═════════════════════════════
    //
    // OLCULEN ONCE-DURUM (AV-3 / T1-B1, LAUNCH BLOKER): kural IKI YERDE, IKI FARKLI GUCTE
    // yaziliydi. Misafir yolu (`GuestCheckoutManager.ReplayGuardiAsync`) SAHIPLIK SORUYORDU;
    // uye yolu (`OrderManager.PlaceOrder`) SORMUYORDU - `o.request_id == dto.request_id`
    // eslesen HER siparisin `id` ve `order_number` alanlarini, isteyen KIM OLURSA OLSUN
    // 200 ile geri veriyordu. `orders.request_id` tekil indeksi GLOBAL oldugu icin baskasinin
    // anahtarini gonderen bir uye, o siparisin numarasini OGRENIYORDU.
    //
    // NEDEN ORTAK SERVIS, NEDEN IKINCI KOPYA DEGIL: "ayni kuralin ikinci kopyasi" bu depoda
    // YEDI kez bedeli odenmis bir hatadir (CLAUDE.md B6 · aile sayaci). Misafir yolunun
    // kazandigi kapiyi uye yoluna ELLE KOPYALAMAK, iki kuralin zamanla AYRISMASINI garanti
    // ederdi - nitekim bugunku durum tam olarak odur.
    //
    // SAHIPLIK EKSENI PARAMETREDIR, KURAL DEGIL: misafir yolunda kimlik SAKLANAN E-POSTADIR
    // (anonim uc, token yok), uye yolunda TOKEN'DAN GELEN customer_id'dir. Algoritma (kupon
    // kanoniklestirmesi + coklu-kume sepet karsilastirmasi + sizintisiz 400) IKISINDE DE AYNI;
    // degisen tek sey "bu siparis SENIN MI" sorusunun sorulma bicimidir.
    public enum ReplaySahiplikEkseni : byte
    {
        // Misafir yolu: hesap kimligi SAKLANAN e-postadir. ORDINAL karsilastirma - kanonik
        // kutu DEGIL (CLAUDE.md 6c): `kurban+a@x` ile `kurban@x` AYRI hesaplardir.
        Eposta = 0,

        // Uye yolu: kimlik token'dan gelen `customer_id`dir. Sayisal esitlik - casing sorusu YOK.
        MusteriId = 1
    }

    // Sahiplik ekseni + degeri. `readonly struct` - guard'in ic durumu yok, cagiran her
    // istekte kendi eksenini verir.
    public readonly struct ReplaySahiplik
    {
        public ReplaySahiplikEkseni Eksen { get; }
        public string? Eposta { get; }
        public int MusteriId { get; }

        private ReplaySahiplik(ReplaySahiplikEkseni eksen, string? eposta, int musteriId)
        {
            Eksen = eksen;
            Eposta = eposta;
            MusteriId = musteriId;
        }

        public static ReplaySahiplik EpostaIle(string eposta) =>
            new ReplaySahiplik(ReplaySahiplikEkseni.Eposta, eposta, 0);

        public static ReplaySahiplik MusteriIdIle(int musteriId) =>
            new ReplaySahiplik(ReplaySahiplikEkseni.MusteriId, null, musteriId);
    }

    public interface ISiparisReplayGuardi
    {
        // `requestId` bos ise `null` doner (guard ATESLEMEZ - idempotency istege baglidir).
        // Eslesen siparis YOKSA `null` doner (cagiran normal akisa devam eder).
        // Eslesen siparis VARSA:
        //   sahiplik TUTAR ve sepet+kupon AYNI  -> (200, replayed:true + id + order_number)
        //   diger her durum                     -> (400, Messages.OrderPlaceFailed) SIZINTISIZ
        Task<(HttpStatusCode, Result)?> DegerlendirAsync(
            string? requestId, ReplaySahiplik sahiplik,
            IEnumerable<OrderItemRequestDto>? kalemler, string? kuponKodu);

        // Yaris dalinda (tekil indeks ihlalinden SONRA) sahiplik yuklemini TEK BASINA sorar.
        // Siparis ya da musteri bulunamazsa GUVENLI TARAF secilir (false).
        Task<bool> SiparisSahibiMiAsync(int siparisId, ReplaySahiplik sahiplik);
    }
}
