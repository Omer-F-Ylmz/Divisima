using AutoMapper;
using Divisima.Entity.Dtos.Category;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Mapping.AutoMapper
{
    // Açıklayıcı yorum: Kategori AutoMapper profili. sub_categories serviste ayrı DAL ile doldurulur (ignore).
    public class CategoryProfile : Profile
    {
        public CategoryProfile()
        {
            CreateMap<CategoryAddRequestDto, Category>();
            CreateMap<CategoryUpdateRequestDto, Category>();
            CreateMap<Category, CategoryResponseDto>()
                .ForMember(dest => dest.sub_categories, opt => opt.Ignore());
            CreateMap<SubCategory, SubCategoryResponseDto>();
        }
    }
}
