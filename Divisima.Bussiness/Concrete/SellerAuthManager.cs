using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Hashing;
using Divisima.Core.Security.JWT;
using Divisima.Core.Security.Tokens;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Seller;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Satıcı kimlik yönetimi (Cafixo AuthManager kalıbıyla aynı güvenlik: HMAC-SHA512 hash+salt,
    // timing-safe login, brute-force kilidi). Müşteri auth'undan TAMAMEN ayrı - kendi tablosu, kendi endpoint'i.
    public class SellerAuthManager : ISellerAuthService
    {
        private const int MaxFailedAttempts = 5;
        private const int LockoutMinutes = 15;

        private readonly ISellerDal _sellerDal;
        private readonly ITokenHelper _tokenHelper;

        public SellerAuthManager(ISellerDal sellerDal, ITokenHelper tokenHelper)
        {
            _sellerDal = sellerDal;
            _tokenHelper = tokenHelper;
        }

        // Açıklayıcı yorum: Satıcı kaydı. E-posta benzersiz olmalı. Kayıt -> status=Pending (admin onayı bekler).
        public async Task<(HttpStatusCode, Result)> Register(SellerRegisterRequestDto dto)
        {
            var existing = await _sellerDal.GetByEmailAsync(dto.email);
            if (existing != null)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.EmailAlreadyExists));

            HashingHelper.CreatePasswordHash(dto.password, out byte[] passwordHash, out byte[] passwordSalt);

            var seller = new Seller
            {
                business_name = dto.business_name,
                email = (dto.email ?? "").Trim().ToLowerInvariant(),   // B1: KIMLIK dizgesi - kultursuz (bkz. EfCustomerDal)
                phone = dto.phone,
                tax_number = dto.tax_number,
                user_type = (byte)UserTypeEnum.Seller,
                status = (byte)SellerStatusEnum.Pending,   // admin onaylayana kadar satış yapamaz
                commission_rate = 10m,
                password_hash = passwordHash,
                password_salt = passwordSalt,
                is_active = true,
                created_at = DateTime.Now
            };

            await _sellerDal.AddAsync(seller);
            return (HttpStatusCode.Created, new SuccessResult(Messages.SellerRegisterSuccess));
        }

        // Açıklayıcı yorum: Satıcı girişi. Timing-safe (bulunamasa da dummy hash doğrula), brute-force kilidi,
        // askıya-alınmış/pasif kontrolü. Başarılıysa JWT (user_type=Seller) üretir.
        public async Task<(HttpStatusCode, Result)> Login(SellerLoginRequestDto dto)
        {
            var seller = await _sellerDal.GetByEmailAsync(dto.email);
            if (seller == null)
            {
                // Timing-safe: kullanıcı-var/yok sürelerini eşitle (enumeration engeli)
                HashingHelper.CreatePasswordHash("dummy_timing_equalizer", out var dh, out var ds);
                HashingHelper.VerifyPasswordHash(dto.password ?? "x", dh, ds);
                return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.LoginFailed));
            }

            if (seller.lockout_end.HasValue && seller.lockout_end.Value > DateTime.Now)
                return (HttpStatusCode.Forbidden, new ErrorResult(Messages.AccountLocked));

            if (!HashingHelper.VerifyPasswordHash(dto.password, seller.password_hash, seller.password_salt))
            {
                var attempts = await _sellerDal.IncrementFailedLoginAsync(seller.id);
                if (attempts >= MaxFailedAttempts)
                    await _sellerDal.LockAccountAsync(seller.id, DateTime.Now.AddMinutes(LockoutMinutes));
                return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.LoginFailed));
            }

            if (!seller.is_active)
                return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.AccountInactive));

            if (seller.status == (byte)SellerStatusEnum.Suspended)
                return (HttpStatusCode.Forbidden, new ErrorResult(Messages.SellerSuspended));

            // Başarılı giriş: login durumunu sıfırla + JWT üret
            await _sellerDal.ResetLoginStateAsync(seller.id);
            var accessToken = _tokenHelper.CreateToken(seller);

            var response = new SellerLoginResponseDto
            {
                seller_id = seller.id,
                business_name = seller.business_name,
                email = seller.email,
                status = seller.status,
                token = accessToken.Token,
                expiration = accessToken.Expiration,
                refresh_token = accessToken.RefreshToken
            };
            return (HttpStatusCode.OK, new SuccessDataResult<SellerLoginResponseDto>(response, Messages.LoginSuccess));
        }
    }
}
