using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Comparison;
namespace Divisima.Bussiness.Abstract
{
    public interface IProductComparisonService
    {
        Task<(HttpStatusCode, Result)> Compare(CompareRequestDto dto);
    }
}
