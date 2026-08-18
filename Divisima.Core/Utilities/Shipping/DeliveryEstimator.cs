using System;

namespace Divisima.Core.Utilities.Shipping
{
    // Açıklayıcı yorum: Tahmini teslim tarihi hesabı. Hafta sonlarını atlayarak iş günü ekler.
    // Üretimde kargo firması API'si ile değiştirilebilir; şimdilik sabit pencere (min-max iş günü).
    public static class DeliveryEstimator
    {
        private const int MinBusinessDays = 3;
        private const int MaxBusinessDays = 5;

        // Açıklayıcı yorum: Sipariş tarihinden itibaren tahmini teslim penceresi (en erken - en geç)
        public static (DateTime earliest, DateTime latest) Estimate(DateTime orderDate)
        {
            return (AddBusinessDays(orderDate, MinBusinessDays), AddBusinessDays(orderDate, MaxBusinessDays));
        }

        // Açıklayıcı yorum: Hafta sonlarını (Cmt/Paz) atlayarak iş günü ekle
        public static DateTime AddBusinessDays(DateTime start, int businessDays)
        {
            var date = start;
            int added = 0;
            while (added < businessDays)
            {
                date = date.AddDays(1);
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                    added++;
            }
            return date;
        }
    }
}
