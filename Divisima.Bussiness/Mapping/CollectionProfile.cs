using AutoMapper;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Collection;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Mapping.AutoMapper
{
    // Açıklayıcı yorum: Koleksiyon AutoMapper profili. byte collection_type -> enum string;
    // collection_type Add/Update serviste set edilir; products/product_count serviste doldurulur.
    public class CollectionProfile : Profile
    {
        public CollectionProfile()
        {
            CreateMap<CollectionAddRequestDto, Collection>()
                .ForMember(dest => dest.collection_type, opt => opt.Ignore());
            CreateMap<CollectionUpdateRequestDto, Collection>()
                .ForMember(dest => dest.collection_type, opt => opt.Ignore());

            CreateMap<Collection, CollectionListResponseDto>()
                .ForMember(dest => dest.collection_type,
                    opt => opt.MapFrom(src => ((CollectionTypeEnum)src.collection_type).ToString()))
                .ForMember(dest => dest.product_count, opt => opt.Ignore());

            CreateMap<Collection, CollectionDetailResponseDto>()
                .ForMember(dest => dest.collection_type,
                    opt => opt.MapFrom(src => ((CollectionTypeEnum)src.collection_type).ToString()))
                .ForMember(dest => dest.products, opt => opt.Ignore());
        }
    }
}
