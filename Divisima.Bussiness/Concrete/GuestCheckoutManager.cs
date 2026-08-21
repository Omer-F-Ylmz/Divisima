using System;
using System.Net;
using System.Security.Cryptography;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Hashing;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.Core.Utilities.Sanitization;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Guest;
using Divisima.Entity.Dtos.Order;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Misafir checkout iş kuralları. Misafir müşteri+adres oluşturur, sonra normal PlaceOrder'a devreder.
    // PlaceOrder kendi transaction'ını yönetir; misafir kayıtları öncesinde ayrı yazılır (nested transaction yok).
    public class GuestCheckoutManager : IGuestCheckoutService
    {
        private readonly ICustomerDal _customerDal;
        private readonly IAddressDal _addressDal;
        private readonly IOrderService _orderService;

        public GuestCheckoutManager(ICustomerDal customerDal, IAddressDal addressDal, IOrderService orderService)
        {
            _customerDal = customerDal;
            _addressDal = addressDal;
            _orderService = orderService;
        }

        public async Task<(HttpStatusCode, Result)> PlaceGuestOrder(GuestCheckoutDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.guest_email) || !dto.guest_email.Contains("@"))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.InvalidEmail));
            if (string.IsNullOrWhiteSpace(dto.guest_name))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ProfileNameRequired));
            if (dto.items == null || dto.items.Count == 0)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.OrderEmptyCart));

            var email = dto.guest_email.Trim().ToLowerInvariant();

            // Açıklayıcı yorum: E-posta zaten kayıtlıysa misafir checkout'a izin verme - giriş yapsın (hesap ele geçirme önleme)
            var existing = await _customerDal.GetAsync(c => c.email == email);
            if (existing != null)
                return (HttpStatusCode.Conflict, new ErrorResult(Messages.GuestEmailExists));

            // Açıklayıcı yorum: Misafir müşteri oluştur - rastgele güçlü şifre (müşteri bilmez; sonradan şifre-sıfırlama ile talep edebilir)
            var randomPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            HashingHelper.CreatePasswordHash(randomPassword, out var hash, out var salt);
            var guest = new Customer
            {
                name = InputSanitizer.Sanitize(dto.guest_name.Trim()),  // stored XSS savunması
                user_type = 2,   // misafir de Customer
                email = email,
                phone = dto.guest_phone ?? "",
                password_hash = hash,
                password_salt = salt,
                is_active = true,
                email_verified = false,
                created_at = DateTime.Now,
                notify_email = true,
                notify_sms = false,
                notify_push = false
            };
            await _customerDal.AddAsync(guest);

            // Açıklayıcı yorum: Teslimat adresi oluştur
            var address = new Address
            {
                customer_id = guest.id,
                title = "Teslimat",
                full_name = InputSanitizer.Sanitize(dto.guest_name.Trim()),  // stored XSS savunması
                phone = dto.guest_phone ?? "",
                city = dto.city ?? "",
                district = dto.district ?? "",
                full_address = dto.full_address ?? "",
                zip_code = dto.zip_code,
                is_default = true,
                is_active = true,
                created_at = DateTime.Now
            };
            await _addressDal.AddAsync(address);

            // Açıklayıcı yorum: Normal sipariş akışına devret (stok/kupon/transaction hepsi PlaceOrder'da)
            var orderDto = new OrderCreateRequestDto
            {
                customer_id = guest.id,
                address_id = address.id,
                coupon_code = dto.coupon_code,
                request_id = dto.request_id,
                items = dto.items
            };
            return await _orderService.PlaceOrder(orderDto);
        }
    }
}
