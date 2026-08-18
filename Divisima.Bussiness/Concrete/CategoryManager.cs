using System.Net;
using AutoMapper;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Category;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Kategori iş kuralları. Cafixo RestaurantProductCategoryManager kalıbı.
    public class CategoryManager : ICategoryService
    {
        private readonly ICategoryDal _categoryDal;
        private readonly ISubCategoryDal _subCategoryDal;
        private readonly IMapper _mapper;

        public CategoryManager(ICategoryDal categoryDal, ISubCategoryDal subCategoryDal, IMapper mapper)
        {
            _categoryDal = categoryDal;
            _subCategoryDal = subCategoryDal;
            _mapper = mapper;
        }

        // Açıklayıcı yorum: Kategori ekle. Aynı slug varsa reddet.
        public async Task<(HttpStatusCode, Result)> Add(CategoryAddRequestDto dto)
        {
            var exists = await _categoryDal.GetAsync(c => c.slug == dto.slug && c.is_active);
            if (exists != null)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CategoryAlreadyExists));

            var category = _mapper.Map<Category>(dto);
            category.is_active = true;
            category.created_at = DateTime.Now;
            await _categoryDal.AddAsync(category);

            return (HttpStatusCode.Created, new SuccessResult(Messages.CategoryAdded));
        }

        // Açıklayıcı yorum: Kategori güncelle.
        public async Task<(HttpStatusCode, Result)> Update(CategoryUpdateRequestDto dto)
        {
            var category = await _categoryDal.GetAsync(c => c.id == dto.id);
            if (category == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.CategoryNotFound));

            _mapper.Map(dto, category);
            category.updated_at = DateTime.Now;
            await _categoryDal.UpdateAsync(category);

            return (HttpStatusCode.OK, new SuccessResult(Messages.CategoryUpdated));
        }

        // Açıklayıcı yorum: Kalıcı sil.
        public async Task<(HttpStatusCode, Result)> Delete(int id)
        {
            var category = await _categoryDal.GetAsync(c => c.id == id);
            if (category == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.CategoryNotFound));

            // Açıklayıcı yorum: Soft-delete - kayıt silinmez, pasifleştirilir (sipariş/ilişki bütünlüğü korunur)
            category.is_active = false;
            await _categoryDal.UpdateAsync(category);
            return (HttpStatusCode.OK, new SuccessResult(Messages.CategoryDeleted));
        }

        // Açıklayıcı yorum: Aktif/pasif toggle.
        public async Task<(HttpStatusCode, Result)> ChangeStatus(int id)
        {
            var category = await _categoryDal.GetIgnoringFiltersAsync(c => c.id == id);
            if (category == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.CategoryNotFound));

            category.is_active = !category.is_active;
            category.updated_at = DateTime.Now;
            await _categoryDal.UpdateAsync(category);
            return (HttpStatusCode.OK, new SuccessResult(Messages.CategoryStatusChanged));
        }

        // Açıklayıcı yorum: Tek kategori (alt kategorileriyle).
        public async Task<(HttpStatusCode, Result)> GetById(int id)
        {
            var category = await _categoryDal.GetAsync(c => c.id == id && c.is_active);
            if (category == null)
                return (HttpStatusCode.NotFound, new ErrorDataResult<CategoryResponseDto>(Messages.CategoryNotFound));

            var data = _mapper.Map<CategoryResponseDto>(category);
            // Açıklayıcı yorum: Alt kategorileri ayrı yükle (nav property yok)
            var subs = await _subCategoryDal.GetListAsync(s => s.category_id == id && s.is_active);
            data.sub_categories = _mapper.Map<List<SubCategoryResponseDto>>(subs);
            return (HttpStatusCode.OK, new SuccessDataResult<CategoryResponseDto>(data, Messages.CategoryListed));
        }

        // Açıklayıcı yorum: Tüm kategoriler + alt kategorileri (frontend menü/filtre).
        public async Task<(HttpStatusCode, Result)> GetList()
        {
            var categories = await _categoryDal.GetListAsync(c => c.is_active);
            var data = _mapper.Map<List<CategoryResponseDto>>(categories.OrderBy(c => c.display_order).ToList());
            // Açıklayıcı yorum: Her kategori için alt kategorileri ayrı yükle (nav property yok)
            var allSubs = await _subCategoryDal.GetListAsync(s => s.is_active);
            foreach (var cat in data)
                cat.sub_categories = _mapper.Map<List<SubCategoryResponseDto>>(allSubs.Where(s => s.category_id == cat.id).ToList());
            return (HttpStatusCode.OK, new SuccessDataResult<List<CategoryResponseDto>>(data, Messages.CategoryListed));
        }
    }
}
