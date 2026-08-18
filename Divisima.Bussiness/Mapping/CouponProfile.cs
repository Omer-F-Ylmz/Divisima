using AutoMapper;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Coupon;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Mapping.AutoMapper
{
    // Açıklayıcı yorum: Kupon AutoMapper profili. byte discount_type serviste set edilir (ignore).
    public class CouponProfile : Profile
    {
        public CouponProfile()
        {
            CreateMap<CouponAddRequestDto, Coupon>()
                .ForMember(dest => dest.discount_type, opt => opt.Ignore());
            CreateMap<CouponUpdateRequestDto, Coupon>()
                .ForMember(dest => dest.discount_type, opt => opt.Ignore());
            CreateMap<Coupon, CouponListResponseDto>()
                .ForMember(dest => dest.discount_type,
                    opt => opt.MapFrom(src => ((DiscountTypeEnum)src.discount_type).ToString()));
        }
    }
}
