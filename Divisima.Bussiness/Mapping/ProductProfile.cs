using AutoMapper;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Pricing;
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
                .ForMember(dest => dest.sizes, opt => opt.Ignore())
                // MANTIK-FIX-1 / K1: etkin fiyat BURADA doldurulur, ListeyiZenginlestirAsync'te
                // DEGIL. Gerekce olculdu: o yardimci PRIVATE ve yalniz ProductManager :439/:471'den
                // cagriliyor; CollectionManager / SearchManager / WishlistManager onu CAGIRMIYOR.
                // Profil ise ALTI uretim yolunun ALTISINDA da kosar.
                .ForMember(dest => dest.effective_price, opt => opt.MapFrom(src =>
                    PricingHelper.EffectivePrice(src.price, src.sale_price, src.sale_start, src.sale_end, DateTime.Now)));

            // Açıklayıcı yorum: Product -> Detail DTO (byte product_type -> enum string; stocks/review serviste)
            CreateMap<Product, ProductDetailResponseDto>()
                .ForMember(dest => dest.category_name, opt => opt.Ignore())
                .ForMember(dest => dest.sub_category_name, opt => opt.Ignore())
                .ForMember(dest => dest.product_type, opt => opt.MapFrom(src => ((ProductTypeEnum)src.product_type).ToString()))
                .ForMember(dest => dest.stocks, opt => opt.Ignore())
                .ForMember(dest => dest.review_average, opt => opt.Ignore())
                .ForMember(dest => dest.review_count, opt => opt.Ignore())
                .ForMember(dest => dest.effective_price, opt => opt.MapFrom(src =>
                    PricingHelper.EffectivePrice(src.price, src.sale_price, src.sale_start, src.sale_end, DateTime.Now)));

            CreateMap<ProductStock, ProductStockDto>().ReverseMap();
        }
    }
}
