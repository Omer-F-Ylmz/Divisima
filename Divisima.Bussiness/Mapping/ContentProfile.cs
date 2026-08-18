using AutoMapper;
using Divisima.Entity.Dtos.Content;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Mapping.AutoMapper
{
    // Açıklayıcı yorum: İçerik AutoMapper profili.
    public class ContentProfile : Profile
    {
        public ContentProfile()
        {
            CreateMap<Content, ContentResponseDto>();
        }
    }
}
