using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Caching;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Dtos;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Admin;
using Divisima.Core.Utilities.Enums;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Admin müşteri yönetimi. Sipariş sayısını kompoze eder (Cafixo: kompozisyon serviste).
    // Hassas alan (şifre/token) DTO'ya asla taşınmaz.
    public class AdminCustomerManager : IAdminCustomerService
    {
        private readonly ICustomerDal _customerDal;
        private readonly IOrderDal _orderDal;
        private readonly IUserSessionDal _userSessionDal;
        private readonly ICacheService _cache;

        public AdminCustomerManager(ICustomerDal customerDal, IOrderDal orderDal, IUserSessionDal userSessionDal,
            ICacheService cache)
        {
            _customerDal = customerDal;
            _orderDal = orderDal;
            _userSessionDal = userSessionDal;
            _cache = cache;
        }

        public async Task<(HttpStatusCode, Result)> ListCustomers(AdminCustomerFilterDto filter)
        {
            var page = filter.page < 1 ? 1 : filter.page;
            var size = filter.page_size is < 1 or > 100 ? 20 : filter.page_size;
            var search = (filter.search ?? "").Trim().ToLower();

            var all = await _customerDal.GetListAsync(c =>
                (filter.is_active == null || c.is_active == filter.is_active.Value) &&
                (search == "" || c.name.ToLower().Contains(search) || c.email.ToLower().Contains(search)));

            var total = all.Count;

            // Açıklayıcı yorum: Sipariş sayısını toplu çek (N+1 önle)
            var pageItems = all.OrderByDescending(c => c.created_at).Skip((page - 1) * size).Take(size).ToList();
            var ids = pageItems.Select(c => c.id).ToHashSet();
            var orders = await _orderDal.GetListAsync(o => ids.Contains(o.customer_id));
            var orderCounts = orders.GroupBy(o => o.customer_id).ToDictionary(g => g.Key, g => g.Count());

            var items = pageItems.Select(c => new AdminCustomerListDto
            {
                id = c.id,
                name = c.name,
                email = c.email,
                is_active = c.is_active,
                email_verified = c.email_verified,
                order_count = orderCounts.TryGetValue(c.id, out var cnt) ? cnt : 0,
                created_at = c.created_at
            }).ToList();

            var paged = new PagedResult<AdminCustomerListDto> { Items = items, TotalCount = total, Page = page, Size = size };
            return (HttpStatusCode.OK, new SuccessDataResult<PagedResult<AdminCustomerListDto>>(paged));
        }

        public async Task<(HttpStatusCode, Result)> SetActive(AdminCustomerStatusDto dto)
        {
            var customer = await _customerDal.GetAsync(c => c.id == dto.customer_id);
            if (customer == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.CustomerNotFound));

            customer.is_active = dto.is_active;
            customer.updated_at = DateTime.Now;
            await _customerDal.UpdateAsync(customer);

            // ASKIYA ALINDIYSA aktif oturumları iptal et (refresh akışı reddedilsin).
            // DUZELTILEN YANLIS GARANTI: buradaki eski yorum "banlanan kullanıcı mevcut token'ıyla
            // devam edemez" diyordu. DOGRU DEGILDI - user_sessions yalnız refresh tarafını kapatır;
            // ACCESS token JWT olduğu için süresi dolana kadar geçerli kalıyordu ve pasif müşteri
            // veri YAZABILIYORDU (testle kanıtlandı). Access token tarafındaki gerçek engel
            // TokenBlacklistMiddleware'deki hesap-durumu kontrolüdür; aşağıdaki cache düşürme
            // o kontrolün ANINDA devreye girmesini sağlar (TTL beklemeden).
            if (!dto.is_active)
                await _userSessionDal.InvalidateAllForCustomerAsync(customer.id);

            // Durum her iki yönde de değişmiş olabilir - cache'i düşür ki middleware taze okusun.
            _cache.Remove(CacheKeys.CustomerActive(customer.id));

            return (HttpStatusCode.OK, new SuccessResult(dto.is_active ? Messages.CustomerActivated : Messages.CustomerSuspended));
        }

        // Açıklayıcı yorum: Kullanıcı tipini değiştir (admin yap / müşteriye indir).
        public async Task<(HttpStatusCode, Result)> SetUserType(AdminSetUserTypeDto dto)
        {
            // Açıklayıcı yorum: Yalnızca tanımlı tipler (Admin=1 / Customer=2)
            if (dto.user_type != (byte)UserTypeEnum.Admin && dto.user_type != (byte)UserTypeEnum.Customer)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.InvalidUserType));

            var customer = await _customerDal.GetAsync(c => c.id == dto.customer_id);
            if (customer == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.CustomerNotFound));

            // Açıklayıcı yorum: SON ADMIN KORUMASI - bir admin'i müşteriye indirirken en az bir admin kalmalı (kilitlenmeyi önler)
            if (customer.user_type == (byte)UserTypeEnum.Admin && dto.user_type == (byte)UserTypeEnum.Customer)
            {
                // PERFORMANS (H51): COUNT(*) - tum admin kayitlarini cekmeye gerek yok.
                var adminCount = await _customerDal.CountAsync(c => c.user_type == (byte)UserTypeEnum.Admin);
                if (adminCount <= 1)
                    return (HttpStatusCode.Conflict, new ErrorResult(Messages.CannotDemoteLastAdmin));
            }

            customer.user_type = dto.user_type;
            customer.updated_at = DateTime.Now;
            await _customerDal.UpdateAsync(customer);
            return (HttpStatusCode.OK, new SuccessResult(Messages.UserTypeUpdated));
        }
    }
}
