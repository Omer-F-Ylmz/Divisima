using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Order
{
    // Açıklayıcı yorum: Sipariş oluşturma yanıtı (üye + misafir). ÖNCEDEN uç yalnızca sayısal
    // `id` dönüyordu ve istemci müşteriye gösterecek gerçek sipariş numarasını ELDE EDEMİYORDU:
    // üye yolunda order_number için İKİNCİ bir /api/order/get çağrısı yapılıyordu, misafir yolunda
    // ise o uç Customer'a kilitli olduğu için (anonim 401) numara HİÇ alınamıyor ve ekranda
    // veritabanı kimliği "Referans: 224" olarak gösteriliyordu.
    //
    // `id` KALDI ve KALMALI: /api/payment/initialize ve /api/order/get sayısal kimlikle çalışır,
    // sonuç sayfası da URL'de onu taşır. order_number bunun YERİNE geçmez, YANINA gelir.
    public class OrderPlaceResponseDto : IDto
    {
        public int id { get; set; }
        public string order_number { get; set; }

        // ══ GF-1 / K1 (DV1) - "BU ISTEK SIPARISI OLUSTURDU MU" ═════════════════════════════
        //
        // OLCULEN ZARAR: `PlaceOrder`in IKI replay dali da `Success=TRUE` donuyordu
        // (`OrderManager` :122-132 request_id dedup, :478-485 unique-index yaris kaybi) ve
        // `GuestCheckoutManager`in telafi kosulu `!siparisSonuc.Success` idi - yani replay
        // dalinda telafi ATESLEMIYOR, akisin ONCEDEN yazdigi misafir musterisi + adresi
        // YETIM KALIYORDU. Cagiranin ihtiyaci "basarili mi" DEGIL, "BU CAGRI YENI BIR SIPARIS
        // YAZDI MI" - iki soru bugune kadar tek bayrakta cakisikti.
        //
        // `false` = bu cagri siparisi OLUSTURDU · `true` = var olan siparis DONDURULDU.
        // Yeni alan EKLENDI, mevcut iki alan DEGISMEDI: istemci sozlesmesi geriye donuk uyumlu
        // (vitrin `id` + `order_number` okuyor, fazladan alan yok sayilir).
        public bool replayed { get; set; }
    }
}
