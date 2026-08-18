using System.Net;
using AutoMapper;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Content;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: İçerik iş kuralları (legal sayfalar). Basit getir/güncelle.
    public class ContentManager : IContentService
    {
        private readonly IContentDal _contentDal;
        private readonly IMapper _mapper;

        public ContentManager(IContentDal contentDal, IMapper mapper)
        {
            _contentDal = contentDal;
            _mapper = mapper;
        }

        // Açıklayıcı yorum: Slug ile içerik (frontend showLegal)
        public async Task<(HttpStatusCode, Result)> GetBySlug(string slug)
        {
            var content = await _contentDal.GetBySlugAsync(slug);
            if (content == null)
                return (HttpStatusCode.NotFound, new ErrorDataResult<ContentResponseDto>(Messages.ContentNotFound));

            var data = _mapper.Map<ContentResponseDto>(content);
            return (HttpStatusCode.OK, new SuccessDataResult<ContentResponseDto>(data, Messages.ContentListed));
        }

        // Açıklayıcı yorum: Tüm aktif içerikler (footer linkleri)
        public async Task<(HttpStatusCode, Result)> GetList()
        {
            var contents = await _contentDal.GetListAsync(c => c.is_active);
            var data = _mapper.Map<List<ContentResponseDto>>(contents);
            return (HttpStatusCode.OK, new SuccessDataResult<List<ContentResponseDto>>(data, Messages.ContentListed));
        }

        // Açıklayıcı yorum: İçerik güncelle (admin)
        public async Task<(HttpStatusCode, Result)> Update(ContentUpdateRequestDto dto)
        {
            var content = await _contentDal.GetAsync(c => c.id == dto.id);
            if (content == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.ContentNotFound));

            content.title_tr = dto.title_tr;
            content.title_en = dto.title_en;
            content.body_tr = dto.body_tr;
            content.body_en = dto.body_en;
            content.updated_at = DateTime.Now;
            await _contentDal.UpdateAsync(content);

            return (HttpStatusCode.OK, new SuccessResult(Messages.ContentUpdated));
        }
    }
}
