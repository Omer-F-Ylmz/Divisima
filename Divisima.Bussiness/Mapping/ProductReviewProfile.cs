using AutoMapper;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.ProductReview;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Mapping.AutoMapper
{
    // Açıklayıcı yorum: Ürün yorumu AutoMapper profili. byte review_status -> enum string.
    public class ProductReviewProfile : Profile
    {
        public ProductReviewProfile()
        {
            CreateMap<ProductReview, ProductReviewListResponseDto>()
                .ForMember(dest => dest.review_status,
                    opt => opt.MapFrom(src => ((ReviewStatusEnum)src.review_status).ToString()));
        }
    }
}
