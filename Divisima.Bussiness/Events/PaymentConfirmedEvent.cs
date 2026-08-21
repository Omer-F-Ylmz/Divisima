namespace Divisima.Bussiness.Events
{
    // SPRINT 8 MADDE 3 - ODEME ONAYLANDI OLAYI (outbox uzerinden islenir).
    //
    // NEDEN OUTBOX: bu olayin tetikledigi dort yan etki (fatura, sadakat, referans odulu, kupon
    // sayaci) onceden commit SONRASI "best-effort" kosuyordu. Patlarlarsa adiyla loglaniyor ve
    // siparis zaman cizelgesine not dusuluyordu - ama HIC YENIDEN DENENMIYORDU. Gecici bir
    // aksaklik (DB kesintisi, saglayici zaman asimi) o yan etkiyi KALICI OLARAK kaybettiriyordu:
    // fatura hic kesilmiyor, sadakat hic verilmiyor, referans odulu hic odenmiyor - ustelik
    // musteri "siparisin onaylandi" goruyor.
    //
    // Olay, odemenin durum gecisiyle AYNI TRANSACTION'da yazilir; yani "odeme Success oldu ama
    // olay kaybedildi" durumu OLUSAMAZ. Islem at-least-once'dir: mesaj birden fazla teslim
    // edilebilir, bu yuzden DORT ADIMIN DA IDEMPOTENT olmasi ZORUNLUDUR (Sprint 8'in on kosullari
    // madde 1 ve madde 2 tam bunun icindi).
    //
    // PAYLOAD NEDEN SIPARIS ID'SINDEN IBARET DEGIL: adimlar musteri id'si, tutar ve kupon kodunu
    // kullaniyor. Bunlari olayin ICINE koymak, isleyicinin siparisi yeniden okumasina gerek
    // birakmaz VE olayin uretildigi ANDAKI degerleri tasir - siparis sonradan degisse bile yan
    // etki dogru veriyle uygulanir (snapshot semantigi).
    public class PaymentConfirmedEvent
    {
        public int order_id { get; set; }
        public int customer_id { get; set; }
        public decimal total_price { get; set; }
        public string? coupon_code { get; set; }
    }
}
