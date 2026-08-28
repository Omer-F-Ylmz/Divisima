using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Order
{
    // Açıklayıcı yorum: Sipariş detay dönüşü (kalemler + tutar dökümü).
    public class OrderDetailResponseDto : IDto
    {
        public int id { get; set; }
        public string order_number { get; set; }
        public string order_status { get; set; }
        public decimal subtotal { get; set; }
        public decimal discount_amount { get; set; }
        public decimal shipping_cost { get; set; }
        // MANTIK-FIX-1 / K2-A: `total` KREDIYI ICERIR - SEMANTIK DEGISMEDI.
        // Olculen zarar (R-M2) bir GORUNURLUK kusuruydu: checkout krediyi dusup 849,80
        // gosteriyor, sonuc/detay ekranlari bu `total` alanini basip 949,80 gosteriyordu -
        // AYNI SIPARIS icin ardisik IKI EKRAN FARKLI TOPLAM. Cozum tutari degistirmek DEGIL,
        // KIRILIMI BILDIRMEK: asagidaki alan eklendi.
        public decimal total { get; set; }
        // MANTIK-FIX-1 / K2-A: siparisin ne kadarinin MAGAZA KREDISIYLE odendigi.
        // NEDEN `total` NET'E CEVRILMEDI (D1 karari, olcume dayali): K2-B yolu
        // OrderCancellationMoneyTests.cs:283-284'teki "MUHASEBE KIMLIGI" pinini kirar VE
        // PaymentRefundTests.cs:20'yi YESIL BIRAKARAK uretimi tersine cevirir - tam-cuzdan
        // sipariste total_price 0 olur, PricingHelper.SplitRefund sifira-bolme yedegine duser
        // ve TUM IADE OLMAYAN BIR KARTA gider. Ayrica yedi uretim noktasi (IyzicoPaymentManager
        // :96 cift dusum, EfOrderDal :23 iade tavani, RefundManager :60, OrderManager :578/:773/
        // :966-1002/:303) etkilenir ve bunlarin ALTISI PINSIZDIR. Semantik degisikligi MF-2'ye
        // ait; orada once o invariantlar pinlenir.
        // FATURA NOTU (olculdu, MF-2 icin): InvoiceManager.cs:76 faturayi `order.total_price`
        // uzerinden uretir, yani BRUT matrah - bu MALI OLARAK DOGRUDUR (kredi bir ODEME
        // ARACIDIR, fiyat indirimi degil). MF-2 semantigi NET'e cevirirse :76 onu SESSIZCE
        // takip eder ve KDV EKSIK BEYAN EDILIR; o satir BRUT toplama acikca baglanmalidir.
        public decimal store_credit_used { get; set; }
        public string coupon_code { get; set; }
        public DateTime created_at { get; set; }
        public List<OrderItemResponseDto> items { get; set; }
    }
}
