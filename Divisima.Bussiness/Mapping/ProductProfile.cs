using AutoMapper;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Product;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Mapping.AutoMapper
{
    // Açıklayıcı yorum: Ürün AutoMapper profili. byte<->enum cast; nav-bağımlı alanlar (category_name,
    // total_stock, stocks) serviste ayrı DAL çağrısıyla doldurulur (nav property yok).
    public class ProductProfile : Profile
    {
        public ProductProfile()
        {
            // Açıklayıcı yorum: Add/Update DTO -> Product; enum product_type -> byte
            CreateMap<ProductAddRequestDto, Product>()
                .ForMember(dest => dest.product_type, opt => opt.MapFrom(src => (byte)src.product_type));
            CreateMap<ProductUpdateRequestDto, Product>()
                .ForMember(dest => dest.product_type, opt => opt.MapFrom(src => (byte)src.product_type));

            // Açıklayıcı yorum: Product -> List DTO (category_name/total_stock/sizes serviste doldurulur; image_url convention ile eşleşir)
            CreateMap<Product, ProductListResponseDto>()
                .ForMember(dest => dest.category_name, opt => opt.Ignore())
                .ForMember(dest => dest.total_stock, opt => opt.Ignore())
                .ForMember(dest => dest.sizes, opt => opt.Ignore());

            // Açıklayıcı yorum: Product -> Detail DTO (byte product_type -> enum string; stocks/review serviste)
            CreateMap<Product, ProductDetailResponseDto>()
                .ForMember(dest => dest.category_name, opt => opt.Ignore())
                .ForMember(dest => dest.sub_category_name, opt => opt.Ignore())
                .ForMember(dest => dest.product_type, opt => opt.MapFrom(src => ((ProductTypeEnum)src.product_type).ToString()))
                .ForMember(dest => dest.stocks, opt => opt.Ignore())
                .ForMember(dest => dest.review_average, opt => opt.Ignore())
                .ForMember(dest => dest.review_count, opt => opt.Ignore());

            CreateMap<ProductStock, ProductStockDto>().ReverseMap();
        }
    }
}
