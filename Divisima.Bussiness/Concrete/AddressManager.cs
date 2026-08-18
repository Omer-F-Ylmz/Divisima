using System.Net;
using AutoMapper;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.Core.Utilities.Sanitization;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Address;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Adres defteri iş kuralları. Varsayılan adres tekilliği korunur.
    public class AddressManager : IAddressService
    {
        private readonly IAddressDal _addressDal;
        private readonly IMapper _mapper;

        public AddressManager(IAddressDal addressDal, IMapper mapper)
        {
            _addressDal = addressDal;
            _mapper = mapper;
        }

        public async Task<(HttpStatusCode, Result)> Upsert(AddressRequestDto dto)
        {
            // Açıklayıcı yorum: Varsayılan seçildiyse müşterinin diğer adreslerini varsayılanlıktan çıkar
            if (dto.is_default)
            {
                var others = await _addressDal.GetListAsync(a => a.customer_id == dto.customer_id && a.is_default && a.is_active);
                foreach (var o in others) { o.is_default = false; await _addressDal.UpdateAsync(o); }
            }

            if (dto.id.HasValue && dto.id.Value > 0)
            {
                var addr = await _addressDal.GetAsync(a => a.id == dto.id.Value);
                if (addr == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.AddressNotFound));
                // BROKEN ACCESS CONTROL / IDOR DÜZELTMESİ: güncellenecek adres İSTEK SAHİBİNE ait olmalı. Aksi halde kullanıcı
                // dto.id'ye BAŞKASININ adres id'sini vererek o adresi ezebilir veya (mapper customer_id'yi map ederse) KENDİNE
                // geçirebilirdi. Delete'te sahiplik kontrolü vardı ama Upsert'in UPDATE yolunda YOKTU (controller yorumu aksini iddia ediyordu).
                if (addr.customer_id != dto.customer_id)
                    return (HttpStatusCode.Forbidden, new ErrorResult(Messages.AccessDenied));
                _mapper.Map(dto, addr);
                addr.updated_at = DateTime.Now;
                await _addressDal.UpdateAsync(addr);
                return (HttpStatusCode.OK, new SuccessResult(Messages.AddressUpdated));
            }

            var entity = _mapper.Map<Address>(dto);
            // Açıklayıcı yorum: Serbest metin alanlarını sanitize et (stored XSS savunması)
            entity.full_address = InputSanitizer.Sanitize(entity.full_address);
            entity.title = InputSanitizer.Sanitize(entity.title);
            entity.full_name = InputSanitizer.Sanitize(entity.full_name);
            entity.is_active = true;
            entity.created_at = DateTime.Now;
            await _addressDal.AddAsync(entity);
            return (HttpStatusCode.Created, new SuccessResult(Messages.AddressAdded));
        }

        public async Task<(HttpStatusCode, Result)> Delete(int id, int customerId)
        {
            var addr = await _addressDal.GetAsync(a => a.id == id);
            if (addr == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.AddressNotFound));
            // Açıklayıcı yorum: Sahiplik doğrulaması - müşteri yalnızca kendi adresini silebilir (IDOR engeli)
            if (addr.customer_id != customerId)
                return (HttpStatusCode.Forbidden, new ErrorResult(Messages.AccessDenied));
            addr.is_active = false;   // soft delete
            await _addressDal.UpdateAsync(addr);

            // Açıklayıcı yorum: Silinen adres VARSAYILANSA, kalan bir adresi varsayılan yap (müşteri varsayılansız kalmasın).
            if (addr.is_default)
            {
                var remaining = await _addressDal.GetListAsync(a => a.customer_id == customerId && a.is_active && a.id != id);
                var newDefault = remaining.OrderByDescending(a => a.id).FirstOrDefault();
                if (newDefault != null)
                {
                    newDefault.is_default = true;
                    await _addressDal.UpdateAsync(newDefault);
                }
            }
            return (HttpStatusCode.OK, new SuccessResult(Messages.AddressDeleted));
        }

        public async Task<(HttpStatusCode, Result)> GetByCustomer(int customerId)
        {
            var list = await _addressDal.GetListNoTrackingAsync(a => a.customer_id == customerId && a.is_active);
            var data = _mapper.Map<List<AddressResponseDto>>(list);
            return (HttpStatusCode.OK, new SuccessDataResult<List<AddressResponseDto>>(data, Messages.AddressListed));
        }
    }
}
