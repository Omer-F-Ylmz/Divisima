using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Address;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Adres defteri iş servisi.
    public interface IAddressService
    {
        Task<(HttpStatusCode, Result)> Upsert(AddressRequestDto dto);
        Task<(HttpStatusCode, Result)> Delete(int id, int customerId);
        Task<(HttpStatusCode, Result)> GetByCustomer(int customerId);
    }
}
