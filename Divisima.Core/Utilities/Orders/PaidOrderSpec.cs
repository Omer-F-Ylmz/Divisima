using Divisima.Core.Utilities.Enums;

namespace Divisima.Core.Utilities.Orders
{
    /// <summary>
    /// TEK DOĞRULUK KAYNAĞI (HUNT47): "Bu kalem GERÇEKTEN satıldı mı?"
    ///
    /// NEDEN VAR: Bir sipariş kalemi BİRBİRİNDEN BAĞIMSIZ İKİ yolla "yok" olabilir:
    ///   (1) Tüm sipariş iptal/ödenmemiş  -> orders.status = Cancelled veya Pending
    ///   (2) Sadece o kalem iptal edilmiş -> order_items.is_cancelled = true
    /// Bu iki filtreden YALNIZ BİRİNİ uygulayan her sorgu HATALIDIR. Bu hata dört ayrı
    /// yerde, dört ayrı turda bulundu:
    ///   H41 satıcı geliri (durum filtresi yoktu)      -> ödenmemiş/iptal sipariş gelire sızdı
    ///   H45 admin "en çok satan" (is_cancelled yoktu) -> iptal kalem rapora sızdı
    ///   H45b vitrin en-çok-satan/trend (hiçbiri yoktu)-> ödemesiz siparişle sıralama manipülasyonu
    ///   H46 öneri motoru (hiçbiri yoktu)              -> ödemesiz siparişle öneri manipülasyonu
    /// Kural artık BURADA yaşıyor; yeni bir tüketici eklenirken kopyalanacak mantık yok.
    ///
    /// KULLANIM:
    ///   var paidIds = (await _orderDal.GetListNoTrackingAsync(o => PaidOrderSpec.PaidStatuses.Contains(o.status)))
    ///                 .Select(o => o.id).ToHashSet();
    ///   var items = await _orderItemDal.GetListNoTrackingAsync(i => !i.is_cancelled);
    ///   var sold  = items.Where(i => paidIds.Contains(i.order_id));
    /// </summary>
    public static class PaidOrderSpec
    {
        // Açıklayıcı yorum: "Parası alınmış / alınacak" sayılan sipariş durumları.
        // Pending = henüz ödenmemiş (sepette bırakılmış gibi), Cancelled = iptal -> İKİSİ DE satış DEĞİLDİR.
        // EF Core, dizi üzerindeki Contains çağrısını SQL IN (...) olarak çevirir.
        public static readonly byte[] PaidStatuses =
        {
            (byte)OrderStatusEnum.Confirmed,
            (byte)OrderStatusEnum.Preparing,
            (byte)OrderStatusEnum.Shipped,
            (byte)OrderStatusEnum.Delivered
        };

        // KUPON LIMITI SAYIMI (H52): odenmis siparisler + hala TAZE bekleyen odemeler sayilir.
        // Bayat Pending sayilirsa saldirgan odemeden siparis acip kampanya kuponunu herkese kapatir (H50);
        // hic sayilmazsa es zamanli devam eden checkout'lar limiti asabilir. Sure BURADA - hem PlaceOrder
        // (enforcement) hem CouponManager.Validate (onizleme) ayni degeri kullanir, yoksa onizleme ile
        // gercek sonuc CELISIR (kodun kendi yorumunun uyardigi tutarsizlik).
        public const int PendingGraceMinutes = 30;

        // Açıklayıcı yorum: Sipariş durumu satış sayılır mı (bellek içi kontrol).
        public static bool IsPaidStatus(byte status) =>
            status == (byte)OrderStatusEnum.Confirmed ||
            status == (byte)OrderStatusEnum.Preparing ||
            status == (byte)OrderStatusEnum.Shipped ||
            status == (byte)OrderStatusEnum.Delivered;

        // Açıklayıcı yorum: Kalem gerçekten satıldı mı - İKİ bayrağı BİRLİKTE değerlendirir.
        // Rapor/gelir/sıralama/öneri hesaplayan her yer bunu kullanmalı.
        public static bool IsSoldItem(byte orderStatus, bool itemIsCancelled) =>
            !itemIsCancelled && IsPaidStatus(orderStatus);
    }
}
