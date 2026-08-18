using System.Net;
using AutoMapper;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Collection;
using Divisima.Entity.Dtos.Product;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Koleksiyon iş kuralları. Sezon + stil elçisi (curator) koleksiyonları,
    // ürün seçkisi CollectionItem üzerinden. Cafixo BrandManager kalıbı + curator/ürün ilişkisi.
    public class CollectionManager : ICollectionService
    {
        private readonly ICollectionDal _collectionDal;
        private readonly ICollectionItemDal _collectionItemDal;
        private readonly IProductDal _productDal;
        private readonly IMapper _mapper;

        public CollectionManager(ICollectionDal collectionDal, ICollectionItemDal collectionItemDal, IProductDal productDal, IMapper mapper)
        {
            _collectionDal = collectionDal;
            _collectionItemDal = collectionItemDal;
            _productDal = productDal;
            _mapper = mapper;
        }

        // Açıklayıcı yorum: Koleksiyon ekle. Elçi tipinde curator zorunlu; slug benzersiz; ürünler bağlanır.
        public async Task<(HttpStatusCode, Result)> Add(CollectionAddRequestDto dto)
        {
            var exists = await _collectionDal.GetAsync(c => c.slug == dto.slug && c.is_active);
            if (exists != null)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CollectionAlreadyExists));

            // Açıklayıcı yorum: İş kuralı - stil elçisi koleksiyonunda küratör adı zorunlu
            if (dto.collection_type == CollectionTypeEnum.Ambassador && string.IsNullOrWhiteSpace(dto.curator_name))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CollectionCuratorRequired));

            var collection = _mapper.Map<Collection>(dto);
            collection.collection_type = (byte)dto.collection_type;
            collection.is_active = true;
            collection.created_at = DateTime.Now;
            await _collectionDal.AddAsync(collection);

            // Açıklayıcı yorum: Ürünleri koleksiyona bağla (CollectionItem)
            await SyncItemsAsync(collection.id, dto.product_ids);

            return (HttpStatusCode.Created, new SuccessResult(Messages.CollectionAdded));
        }

        // Açıklayıcı yorum: Koleksiyon güncelle + ürün seçkisini yeniden yaz.
        public async Task<(HttpStatusCode, Result)> Update(CollectionUpdateRequestDto dto)
        {
            var collection = await _collectionDal.GetAsync(c => c.id == dto.id);
            if (collection == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.CollectionNotFound));

            if (dto.collection_type == CollectionTypeEnum.Ambassador && string.IsNullOrWhiteSpace(dto.curator_name))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CollectionCuratorRequired));

            _mapper.Map(dto, collection);
            collection.collection_type = (byte)dto.collection_type;
            collection.updated_at = DateTime.Now;
            await _collectionDal.UpdateAsync(collection);

            // Açıklayıcı yorum: Mevcut bağlantıları pasifle, gelenleri yeniden ekle
            if (dto.product_ids != null)
            {
                var current = await _collectionItemDal.GetListAsync(i => i.collection_id == collection.id);
                foreach (var old in current)
                {
                    old.is_active = false;
                    await _collectionItemDal.UpdateAsync(old);
                }
                await SyncItemsAsync(collection.id, dto.product_ids);
            }

            return (HttpStatusCode.OK, new SuccessResult(Messages.CollectionUpdated));
        }

        // Açıklayıcı yorum: Ürün id listesini CollectionItem olarak ekler.
        private async Task SyncItemsAsync(int collectionId, List<int> productIds)
        {
            if (productIds == null) return;
            var order = 0;
            // Açıklayıcı yorum: DEDUP - aynı ürün iki kez gönderilirse koleksiyonda tek görünsün (çift kalem engeli).
            foreach (var pid in productIds.Distinct())
            {
                await _collectionItemDal.AddAsync(new CollectionItem
                {
                    collection_id = collectionId,
                    product_id = pid,
                    display_order = order++,
                    is_active = true,
                    created_at = DateTime.Now
                });
            }
        }

        // Açıklayıcı yorum: Kalıcı sil.
        public async Task<(HttpStatusCode, Result)> Delete(int id)
        {
            var collection = await _collectionDal.GetAsync(c => c.id == id);
            if (collection == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.CollectionNotFound));

            // Açıklayıcı yorum: Soft-delete - kayıt silinmez, pasifleştirilir (sipariş/ilişki bütünlüğü korunur)
            collection.is_active = false;
            await _collectionDal.UpdateAsync(collection);
            return (HttpStatusCode.OK, new SuccessResult(Messages.CollectionDeleted));
        }

        // Açıklayıcı yorum: Aktif/pasif toggle.
        public async Task<(HttpStatusCode, Result)> ChangeStatus(int id)
        {
            var collection = await _collectionDal.GetIgnoringFiltersAsync(c => c.id == id);
            if (collection == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.CollectionNotFound));

            collection.is_active = !collection.is_active;
            collection.updated_at = DateTime.Now;
            await _collectionDal.UpdateAsync(collection);
            return (HttpStatusCode.OK, new SuccessResult(Messages.CollectionStatusChanged));
        }

        // Açıklayıcı yorum: Tüm aktif koleksiyonlar (ana sayfa + elçiler).
        public async Task<(HttpStatusCode, Result)> GetList()
        {
            var collections = await _collectionDal.GetListAsync(c => c.is_active);
            var data = _mapper.Map<List<CollectionListResponseDto>>(collections);
            return (HttpStatusCode.OK, new SuccessDataResult<List<CollectionListResponseDto>>(data, Messages.CollectionListed));
        }

        // Açıklayıcı yorum: Slug ile koleksiyon detayı + içindeki ürünler (frontend showCollection).
        public async Task<(HttpStatusCode, Result)> GetBySlug(string slug)
        {
            var collection = await _collectionDal.GetBySlugAsync(slug);
            if (collection == null)
                return (HttpStatusCode.NotFound, new ErrorDataResult<CollectionDetailResponseDto>(Messages.CollectionNotFound));

            var data = _mapper.Map<CollectionDetailResponseDto>(collection);

            // Açıklayıcı yorum: Koleksiyon ürünlerini ayrı DAL çağrılarıyla yükle (nav property yok)
            var items = await _collectionItemDal.GetListAsync(i => i.collection_id == collection.id && i.is_active);
            // N+1 ÖNLEME: tüm ürünleri TEK sorguda çek (her item için ayrı GetAsync yerine -> 50 ürün = 50 sorgu idi).
            // display_order sırası korunur: sıralı item listesi üzerinde dönüp dict'ten O(1) lookup.
            var ordered = items.OrderBy(i => i.display_order).ToList();
            var productIds = ordered.Select(i => i.product_id).Distinct().ToList();
            var productMap = (await _productDal.GetListAsync(p => productIds.Contains(p.id) && p.is_active))
                .ToDictionary(p => p.id);
            var products = new List<ProductListResponseDto>();
            foreach (var item in ordered)
            {
                if (productMap.TryGetValue(item.product_id, out var product))
                    products.Add(_mapper.Map<ProductListResponseDto>(product));
            }
            data.products = products;

            return (HttpStatusCode.OK, new SuccessDataResult<CollectionDetailResponseDto>(data, Messages.CollectionListed));
        }
    }
}
