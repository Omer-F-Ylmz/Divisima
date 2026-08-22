using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Storage;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Product;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Ürün görsel yöneticisi. Yükleme sırasında tür + boyut doğrular, depolar, kaydeder.
    // Birincil görsel Product.image_url'e de yazılır (grid tek sorguda görsel görsün).
    public class ProductImageManager : IProductImageService
    {
        // Açıklayıcı yorum: İzinli tür + boyut (güvenlik - keyfi dosya/RCE engeli)
        private static readonly HashSet<string> AllowedTypes = new() { "image/jpeg", "image/png", "image/webp" };
        private const long MaxBytes = 5 * 1024 * 1024; // 5 MB

        private readonly IProductImageDal _imageDal;
        private readonly IProductDal _productDal;
        private readonly IImageStorage _storage;

        public ProductImageManager(IProductImageDal imageDal, IProductDal productDal, IImageStorage storage)
        {
            _imageDal = imageDal;
            _productDal = productDal;
            _storage = storage;
        }

        public async Task<(HttpStatusCode, Result)> Upload(int productId, byte[] content, string fileName, string contentType, bool isPrimary)
        {
            var product = await _productDal.GetAsync(p => p.id == productId);
            if (product == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.ProductNotFound));

            // Açıklayıcı yorum: Tür doğrulama (MIME + uzantı)
            if (!AllowedTypes.Contains(contentType?.ToLowerInvariant() ?? ""))   // MIME: makine dizgesi
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ImageTypeInvalid));
            if (content == null || content.Length == 0)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ImageEmpty));
            if (content.Length > MaxBytes)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ImageTooLarge));
            // GÜVENLİK: MAGIC-BYTE doğrulama - içerik GERÇEKTEN görsel mi (content-type client'tan gelir, sahte olabilir).
            // Sahte "image/png" content-type'lı bir HTML/script dosyasının yüklenmesini engeller (derinlemesine savunma).
            if (!HasValidImageSignature(content))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ImageTypeInvalid));

            // Açıklayıcı yorum: Depola (yerel/bulut) → URL
            var url = await _storage.SaveAsync(content, fileName, contentType);

            // Açıklayıcı yorum: İlk görselse otomatik birincil
            var existing = await _imageDal.GetListAsync(i => i.product_id == productId);
            var makePrimary = isPrimary || existing.Count == 0;

            if (makePrimary)
            {
                // Açıklayıcı yorum: Diğer birincilleri kaldır (tek birincil)
                foreach (var img in existing.Where(i => i.is_primary))
                {
                    img.is_primary = false;
                    await _imageDal.UpdateAsync(img);
                }
            }

            await _imageDal.AddAsync(new ProductImage
            {
                product_id = productId,
                image_url = url,
                sort_order = existing.Count,
                is_primary = makePrimary,
                created_at = DateTime.Now
            });

            // Açıklayıcı yorum: Birincilse Product.image_url'e yaz (grid tek sorguda görsün)
            if (makePrimary)
            {
                product.image_url = url;
                product.updated_at = DateTime.Now;
                await _productDal.UpdateAsync(product);
            }

            return (HttpStatusCode.OK, new SuccessDataResult<string>(url, Messages.ImageUploaded));
        }

        public async Task<(HttpStatusCode, Result)> GetByProduct(int productId)
        {
            var images = await _imageDal.GetListAsync(i => i.product_id == productId);
            var dtos = images.OrderBy(i => i.sort_order).Select(i => new ProductImageDto
            {
                id = i.id,
                product_id = i.product_id,
                image_url = i.image_url,
                sort_order = i.sort_order,
                is_primary = i.is_primary
            }).ToList();
            return (HttpStatusCode.OK, new SuccessDataResult<List<ProductImageDto>>(dtos));
        }

        public async Task<(HttpStatusCode, Result)> Delete(int imageId)
        {
            var image = await _imageDal.GetAsync(i => i.id == imageId);
            if (image == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.ImageNotFound));

            await _storage.DeleteAsync(image.image_url);
            await _imageDal.DeleteAsync(image);

            // Açıklayıcı yorum: Birincil silindiyse kalan ilk görseli birincil yap
            if (image.is_primary)
            {
                var remaining = (await _imageDal.GetListAsync(i => i.product_id == image.product_id)).OrderBy(i => i.sort_order).FirstOrDefault();
                var product = await _productDal.GetAsync(p => p.id == image.product_id);
                if (remaining != null)
                {
                    remaining.is_primary = true;
                    await _imageDal.UpdateAsync(remaining);
                    if (product != null) { product.image_url = remaining.image_url; await _productDal.UpdateAsync(product); }
                }
                else if (product != null)
                {
                    product.image_url = null;
                    await _productDal.UpdateAsync(product);
                }
            }
            return (HttpStatusCode.OK, new SuccessResult(Messages.ImageDeleted));
        }

        public async Task<(HttpStatusCode, Result)> SetPrimary(int imageId)
        {
            var image = await _imageDal.GetAsync(i => i.id == imageId);
            if (image == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.ImageNotFound));

            var all = await _imageDal.GetListAsync(i => i.product_id == image.product_id);
            foreach (var img in all)
            {
                img.is_primary = img.id == imageId;
                await _imageDal.UpdateAsync(img);
            }
            var product = await _productDal.GetAsync(p => p.id == image.product_id);
            if (product != null) { product.image_url = image.image_url; await _productDal.UpdateAsync(product); }

            return (HttpStatusCode.OK, new SuccessResult(Messages.ImagePrimarySet));
        }

        // Açıklayıcı yorum: MAGIC-BYTE (dosya imzası) doğrulama - baytlar gerçekten JPEG/PNG/WEBP mı?
        // JPEG: FF D8 FF | PNG: 89 50 4E 47 0D 0A 1A 0A | WEBP: "RIFF"....(0-3) + "WEBP"(8-11).
        // Content-type (client'tan) sahte olabilir; imza içeriğin gerçek türünü kanıtlar.
        private static bool HasValidImageSignature(byte[] c)
        {
            if (c.Length < 12) return false;
            // JPEG
            if (c[0] == 0xFF && c[1] == 0xD8 && c[2] == 0xFF) return true;
            // PNG
            if (c[0] == 0x89 && c[1] == 0x50 && c[2] == 0x4E && c[3] == 0x47 &&
                c[4] == 0x0D && c[5] == 0x0A && c[6] == 0x1A && c[7] == 0x0A) return true;
            // WEBP: "RIFF" + "WEBP"
            if (c[0] == 0x52 && c[1] == 0x49 && c[2] == 0x46 && c[3] == 0x46 &&
                c[8] == 0x57 && c[9] == 0x45 && c[10] == 0x42 && c[11] == 0x50) return true;
            return false;
        }
    }
}
