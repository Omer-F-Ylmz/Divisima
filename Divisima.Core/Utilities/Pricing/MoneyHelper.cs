namespace Divisima.Core.Utilities.Pricing
{
    // Açıklayıcı yorum: Para yuvarlama tek noktadan. Ticari standart: MidpointRounding.AwayFromZero
    // (Math.Round varsayılanı banker's rounding'dir - 2.005 -> 2.00 gibi kuruş sapması yapabilir).
    // Tüm para hesaplarında (indirim, vergi, ortalama sepet) tutarlı 2 hane için bunu kullan.
    public static class MoneyHelper
    {
        public const string DefaultCurrency = "TRY";

        // Açıklayıcı yorum: Para tutarını 2 haneye yuvarla (kuruş) - away-from-zero
        public static decimal Round(decimal amount) =>
            Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        // Açıklayıcı yorum: İki tutarı topla + yuvarla (birikimli kuruş hatası önleme)
        public static decimal Add(params decimal[] amounts) =>
            Round(amounts.Sum());

        // Açıklayıcı yorum: Yüzde indirim hesabı (yuvarlanmış)
        public static decimal Percentage(decimal baseAmount, decimal percent) =>
            Round(baseAmount * percent / 100m);
    }
}
