using System.Collections.Generic;
using System.Linq;
using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Order;
using Divisima.Entity.Entities;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Concrete
{
    // ══ GF-6 / K1 (D1) - `request_id` REPLAY GUARD'I (TEK KAYNAK) ══════════════════════════
    //
    // GOVDE, `GuestCheckoutManager`in GF-1/K1 + GF-3/K12 turlerinde olculerek olusmus
    // yardimcilarindan TASINDI - YENIDEN YAZILMADI. Misafir yolunun gozlenebilir davranisi
    // (durum kodu + govde + hangi dalda hangi karar) DEGISMEDI; degisen tek sey sahiplik
    // yukleminin PARAMETRE haline gelmesi ve LOG METNININ ekseni anmasidir.
    //
    // ── NEDEN SAHIPLIK SORUSU SART (GF-1/K1'in olctugu sey) ────────────────────────────────
    // Yalniz "request_id daha once kullanilmis mi" sorulursa, BASKASININ anahtarini gonderen
    // istege o siparisin `order_number`i DONER. `orders.request_id` tekil indeksi GLOBALDIR
    // (kapsamlama migration olurdu - merkez REDDETTI), bu yuzden karar SAHIPLIKLE verilir.
    //
    // ── UYE YOLUNUN EKSENI NEDEN customer_id (e-posta DEGIL) ───────────────────────────────
    // Uye ucunda kimlik TOKEN'dan gelir ve `dto.customer_id`yi controller token'dan set eder
    // (CLAUDE.md 5: FluentValidation `customer_id > 0` kurali bu yuzden var). E-posta ekseni
    // uye yolunda FAZLADAN bir okuma olurdu ve ayni hesaba iki yazimla (buyuk/kucuk harf)
    // ulasilabilen tarihsel hatanin (6c) yuzeyini yeniden acardi.
    public class SiparisReplayGuardi : ISiparisReplayGuardi
    {
        private readonly IOrderDal _orderDal;
        private readonly IOrderItemDal _orderItemDal;
        private readonly ICustomerDal _customerDal;
        private readonly ILogger<SiparisReplayGuardi> _logger;

        public SiparisReplayGuardi(IOrderDal orderDal, IOrderItemDal orderItemDal,
            ICustomerDal customerDal, ILogger<SiparisReplayGuardi> logger)
        {
            _orderDal = orderDal;
            _orderItemDal = orderItemDal;
            _customerDal = customerDal;
            _logger = logger;
        }

        public async Task<(HttpStatusCode, Result)?> DegerlendirAsync(
            string? requestId, ReplaySahiplik sahiplik,
            IEnumerable<OrderItemRequestDto>? kalemler, string? kuponKodu)
        {
            if (string.IsNullOrWhiteSpace(requestId)) return null;

            var oncekiler = await _orderDal.GetListNoTrackingAsync(o => o.request_id == requestId);
            var onceki = oncekiler.FirstOrDefault();
            if (onceki == null) return null;

            if (await SahipMiAsync(onceki, sahiplik)
                && await AyniSiparisMiAsync(onceki.id, onceki.coupon_code, kalemler, kuponKodu))
                return (HttpStatusCode.OK, new SuccessDataResult<OrderPlaceResponseDto>(
                    new OrderPlaceResponseDto
                    {
                        id = onceki.id,
                        order_number = onceki.order_number,
                        replayed = true
                    },
                    Messages.OrderAlreadyPlaced));

            // ESLESMEZSE 400 - SIZINTISIZ: `OrderPlaceFailed` mesaji AYNEN kullanilir, yani ne
            // siparisin VARLIGI ne `order_number` sizar (GUVENLIK-2/#1 karariyla tutarli).
            _logger.LogWarning("SIPARIS REPLAY GUARD'I ({Eksen}): request_id BASKA bir siparise ya da "
                + "BASKA bir sepete ait (siparis {OrderId}) - istek reddedildi, hicbir kayit yazilmadi.",
                sahiplik.Eksen, onceki.id);
            return (HttpStatusCode.BadRequest, new ErrorResult(Messages.OrderPlaceFailed));
        }

        public async Task<bool> SiparisSahibiMiAsync(int siparisId, ReplaySahiplik sahiplik)
        {
            var siparisler = await _orderDal.GetListNoTrackingAsync(o => o.id == siparisId);
            var siparis = siparisler.FirstOrDefault();
            if (siparis == null) return false;
            return await SahipMiAsync(siparis, sahiplik);
        }

        // Sahiplik yuklemi - TEK YER. Iki eksen de "guvenli taraf" (false) ile biter.
        private async Task<bool> SahipMiAsync(Order siparis, ReplaySahiplik sahiplik)
        {
            if (sahiplik.Eksen == ReplaySahiplikEkseni.MusteriId)
                return sahiplik.MusteriId > 0 && siparis.customer_id == sahiplik.MusteriId;

            if (string.IsNullOrEmpty(sahiplik.Eposta)) return false;
            var sahipler = await _customerDal.GetListNoTrackingAsync(c => c.id == siparis.customer_id);
            var sahip = sahipler.FirstOrDefault();
            return sahip != null && string.Equals(sahip.email, sahiplik.Eposta, StringComparison.Ordinal);
        }

        // GF-3/K12: gelen istek, `request_id`in isaret ettigi siparisin AYNISI mi?
        // Kupon ve sepet TEK YERDE karsilastirilir.
        private async Task<bool> AyniSiparisMiAsync(int siparisId, string? kayitliKupon,
            IEnumerable<OrderItemRequestDto>? kalemler, string? kuponKodu)
        {
            // KUPON: iki taraf da kanonik. `KanonikKod` null ve "" icin AYNI degeri dondurur.
            if (!string.Equals(
                    Divisima.Core.Utilities.Text.KimlikDizgesi.KanonikKod(kayitliKupon),
                    Divisima.Core.Utilities.Text.KimlikDizgesi.KanonikKod(kuponKodu),
                    StringComparison.Ordinal))
                return false;

            // SEPET: COKLU KUME karsilastirmasi (sira onemsiz, tekrar onemli).
            // (2) IPTAL KALEMLERI DISLANIR (GF-3 merkez karari D7): dislama olcutu SIKILASTIRIR,
            //     gevsetmez - kismi iptalden SONRA gelen ve ORIJINAL sepeti tasiyan replay
            //     ESLESMEZ ve 400 alir. Davranis GF-3'te oldugu gibi KORUNDU.
            var siparisKalemleri = await _orderItemDal.GetListNoTrackingAsync(
                i => i.order_id == siparisId && !i.is_cancelled);

            return string.Equals(
                SepetAnahtari(siparisKalemleri.Select(i => (i.product_id, i.size, i.quantity))),
                SepetAnahtari((kalemler ?? Enumerable.Empty<OrderItemRequestDto>())
                    .Select(i => (i.product_id, i.size, i.quantity))),
                StringComparison.Ordinal);
        }

        // Sira BAGIMSIZ, tekrar DUYARLI anahtar. `size` bir KIMLIK dizgesidir (beden kodu) -
        // kulturlu casing YASAK (CLAUDE.md 6c): `ToUpperInvariant` ve `Ordinal` siralama.
        private static string SepetAnahtari(IEnumerable<(int urun, string beden, int adet)> kalemler) =>
            string.Join("|", kalemler
                .Select(k => k.urun + ":" + (k.beden ?? "").ToUpperInvariant() + ":" + k.adet)
                .OrderBy(s => s, StringComparer.Ordinal));
    }
}
