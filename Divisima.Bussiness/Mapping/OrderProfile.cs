using AutoMapper;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Order;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Mapping.AutoMapper
{
    // Açıklayıcı yorum: Sipariş AutoMapper profili. byte status -> enum string, total_price -> total.
    // items serviste ayrı DAL çağrısıyla doldurulur (nav property yok).
    public class OrderProfile : Profile
    {
        public OrderProfile()
        {
            CreateMap<Order, OrderListResponseDto>()
                .ForMember(dest => dest.order_status,
                    opt => opt.MapFrom(src => ((OrderStatusEnum)src.status).ToString()))
                .ForMember(dest => dest.total, opt => opt.MapFrom(src => src.total_price));

            CreateMap<Order, OrderDetailResponseDto>()
                .ForMember(dest => dest.order_status,
                    opt => opt.MapFrom(src => ((OrderStatusEnum)src.status).ToString()))
                .ForMember(dest => dest.total, opt => opt.MapFrom(src => src.total_price))
                .ForMember(dest => dest.items, opt => opt.Ignore());  // serviste kompozisyonla doldurulur
        }
    }
}
