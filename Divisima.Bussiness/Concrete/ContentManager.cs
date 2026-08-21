using System.Net;
using AutoMapper;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.Core.Utilities.Sanitization;
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

            // E3 - IKI KATMAN SANITIZASYONUN YAZMA KATMANI.
            // Bu uc [RequireUserType(Admin)] ile korumali, ama "yetkili kullanici guvenilir icerik yazar"
            // varsayimi stored XSS icin YETERSIZ: admin hesabi ele gecirilebilir, icerik bir baska
            // sistemden yapistirilabilir, ya da yonetici farkinda olmadan zararli isaretleme kopyalayabilir.
            // Govde storefront'ta innerHTML ile ciziliyor - yani kayittaki her sey CALISABILIR.
            // Depoya YALNIZ temizlenmis icerik girer; okuma tarafinda DOMPurify ikinci kalkandir.
            //
            // Sanitize HTML'i ENCODE ETMEZ, yalniz tehlikeli kisimlari (script, iframe/object/embed/
            // form/link/meta/style/base/svg, on*= olay yakalayicilari, javascript:) SOKER. CMS govdesi
            // mesru HTML (h3/p/strong...) icerdigi icin dogru davranis budur; HtmlEncode icerigi bozardi.
            content.title_tr = InputSanitizer.Sanitize(dto.title_tr);
            content.title_en = InputSanitizer.Sanitize(dto.title_en);
            content.body_tr = InputSanitizer.Sanitize(dto.body_tr);
            content.body_en = InputSanitizer.Sanitize(dto.body_en);
            content.updated_at = DateTime.Now;
            await _contentDal.UpdateAsync(content);

            return (HttpStatusCode.OK, new SuccessResult(Messages.ContentUpdated));
        }
    }
}
