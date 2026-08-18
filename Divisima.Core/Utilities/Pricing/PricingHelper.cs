using System;

namespace Divisima.Core.Utilities.Pricing
{
    // Açıklayıcı yorum: Etkin fiyat hesabı. Flash sale penceresi aktifse indirimli fiyat, değilse normal fiyat.
    // Tek doğruluk kaynağı - hem listeleme hem sipariş bu mantığı kullanmalı (fiyat tutarsızlığı önlenir).
    public static class PricingHelper
    {
        // Açıklayıcı yorum: Ürünün şu anki geçerli fiyatı
        public static decimal EffectivePrice(decimal price, decimal? salePrice, DateTime? saleStart, DateTime? saleEnd, DateTime now)
        {
            if (IsOnSale(salePrice, saleStart, saleEnd, now))
                return salePrice!.Value;
            return price;
        }

        // Açıklayıcı yorum: Flash sale şu an aktif mi
        public static bool IsOnSale(decimal? salePrice, DateTime? saleStart, DateTime? saleEnd, DateTime now)
        {
            if (!salePrice.HasValue || salePrice.Value <= 0) return false;
            if (saleStart.HasValue && now < saleStart.Value) return false;
            if (saleEnd.HasValue && now > saleEnd.Value) return false;
            return true;
        }

        // Aciklayici yorum: IADE BOLME - siparis kismen magaza kredisi kismen kartla odenmis olabilir.
        // Iade tutarini odeme kaynagina gore boler: online kisim (kart) + cuzdan kismi (store credit).
        // Boylece karta yalnizca online odenen kadar iade edilir (fazla-iade/Iyzico reddi onlenir).
        public static (decimal onlineRefund, decimal creditRefund) SplitRefund(
            decimal orderTotal, decimal storeCreditUsed, decimal refundAmount)
        {
            decimal onlineRatio = orderTotal > 0 ? (orderTotal - storeCreditUsed) / orderTotal : 1m;
            decimal onlineRefund = MoneyHelper.Round(refundAmount * onlineRatio);
            return (onlineRefund, MoneyHelper.Round(refundAmount - onlineRefund));
        }

    }
}
