using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Admin;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Admin müşteri yönetimi. Listeleme (arama/sayfalama) + askıya alma/aktifleştirme.
    public interface IAdminCustomerService
    {
        Task<(HttpStatusCode, Result)> ListCustomers(AdminCustomerFilterDto filter);
        Task<(HttpStatusCode, Result)> SetActive(AdminCustomerStatusDto dto);
        Task<(HttpStatusCode, Result)> SetUserType(AdminSetUserTypeDto dto);
    }
}
