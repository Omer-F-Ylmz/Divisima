namespace Divisima.Bussiness.Events
{
    // Açıklayıcı yorum: Sipariş oluşturuldu event verisi (Cafixo WebOrderPaidEvent kalıbı).
    public class OrderPlacedEvent
    {
        public int order_id { get; set; }
        public int customer_id { get; set; }
        public string order_number { get; set; }
        public decimal total { get; set; }
    }
}
