using System.Collections.Generic;
using Divisima.Core.Utilities.Dtos;
using Divisima.Entity.Dtos.Order;
namespace Divisima.Entity.Dtos.Guest
{
    // Açıklayıcı yorum: Misafir (hesapsız) sipariş - iletişim + teslimat + kalemler tek istekte.
    public class GuestCheckoutDto : IDto
    {
        public string guest_name { get; set; }
        public string guest_email { get; set; }
        public string guest_phone { get; set; }
        public string city { get; set; }
        public string district { get; set; }
        public string full_address { get; set; }
        public string? zip_code { get; set; }
        public string coupon_code { get; set; }
        public string? request_id { get; set; }

        // ══ A3 HIBRIT - MISAFIR ODEME YONTEMI ══════════════════════════════════════════════
        //
        // OLCULEN ONCE-DURUM: bu alan YOKTU. PlaceOrder varsayilani aliyor (payment_method = 0
        // = Online) ve misafir siparisi ONLINE olarak olusuyordu. Ama /api/payment/initialize
        // ucu [RequireUserType(Customer)] ve musteriyi TOKEN'dan okuyor - misafirin token'i YOK.
        // Sonuc: misafir siparisi OLUSTURULABILIYOR ama ASLA ODENEMIYORDU; sonsuza kadar
        // Pending kaliyor (B13'teki terk edilmis siparis yiginina duser).
        //
        // KULLANICI KARARI (secenek iii - HIBRIT): misafire YALNIZ KAPIDA ODEME. Boylece
        // dogrulanmamis hesaba OTURUM VERME kapisi hic acilmaz (bu projenin defalarca bedelini
        // odedigi sinir) ve SSS'deki vaat DOGRU hale gelir. Kartla odeme uye girisine bagli
        // kalir; (i) secenegine giden yol da kapanmaz.
        //
        // 1 = Kapida odeme (COD). Baska bir deger gelirse uc REDDEDER - sessizce COD'a
        // DUSURULMEZ: musteriye sormadan odeme yontemini degistirmek olurdu.
        public byte payment_method { get; set; }

        public List<OrderItemRequestDto> items { get; set; }
    }
}
