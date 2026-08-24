using System.Net;
using Divisima.Core.Utilities.Results;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Admin dashboard/rapor servisi. Tümü admin yetkisi ister (controller'da RequireUserType.Admin).
    public interface IDashboardService
    {
        Task<(HttpStatusCode, Result)> GetSummary();
        Task<(HttpStatusCode, Result)> GetDailySales(DateTime startDate, DateTime endDate);
        Task<(HttpStatusCode, Result)> GetTopProducts(int top);
        Task<(HttpStatusCode, Result)> GetOrderStatusBreakdown();
        Task<(HttpStatusCode, Result)> GetLowStock(int threshold);
        Task<(HttpStatusCode, Result)> GetSalesByCategory(DateTime startDate, DateTime endDate);

        // DALGA C / C4: basarisiz arka plan isleri (outbox status=Failed). Operatorun bu bilgiyi
        // gorebilecegi TEK yuzey - gerekce FailedJobDto'nun basinda.
        Task<(HttpStatusCode, Result)> GetFailedJobs(int take);
    }
}
