using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Coupon;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Kupon iş servisi. CRUD + ValidateCoupon (storefront).
    public interface ICouponService
    {
        Task<(HttpStatusCode, Result)> Add(CouponAddRequestDto dto);
        Task<(HttpStatusCode, Result)> Update(CouponUpdateRequestDto dto);
        Task<(HttpStatusCode, Result)> Delete(int id);
        Task<(HttpStatusCode, Result)> ChangeStatus(int id);
        Task<(HttpStatusCode, Result)> GetList();

        // Açıklayıcı yorum: Kupon doğrula ve indirimi hesapla (frontend validateCoupon + couponDiscount)
        Task<(HttpStatusCode, Result)> ValidateCoupon(CouponValidateRequestDto dto);
    }
}
