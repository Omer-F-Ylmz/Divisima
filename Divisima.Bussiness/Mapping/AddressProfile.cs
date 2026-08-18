using AutoMapper;
using Divisima.Entity.Dtos.Address;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Mapping.AutoMapper
{
    public class AddressProfile : Profile
    {
        public AddressProfile()
        {
            CreateMap<AddressRequestDto, Address>();
            CreateMap<Address, AddressResponseDto>();
        }
    }
}
