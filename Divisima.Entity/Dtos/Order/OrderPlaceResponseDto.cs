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
    }
}
