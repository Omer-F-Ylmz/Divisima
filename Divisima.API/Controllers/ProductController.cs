using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Product;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Ürün controller'ı. İnce (thin) - iş yok, sadece servisi çağırıp
    // (HttpStatusCode, Result) tuple'ını StatusCode ile döndürür. Cafixo ProductController kalıbı.
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Ürün yönetimi ve storefront listeleme")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }

        // Açıklayıcı yorum: Yeni ürün ekle (sadece admin)
        [HttpPost("add")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Ürün ekle", Description = "Yeni ürünü beden-stoklarıyla birlikte ekler. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.Created)]
        [ProducesResponseType(typeof(ErrorResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Add([FromBody] ProductAddRequestDto dto)
        {
            var result = await _productService.Add(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: TOPLU ÜRÜN İÇE-AKTARMA (CSV yükleme, sadece admin).
        // CSV başlık: name,brand,category_id,price,sale_price,description,color_hex,product_type,size,stock_quantity
        [HttpPost("import")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Toplu ürün içe-aktar (CSV)", Description = "CSV dosyasından çok sayıda ürünü tek seferde ekler. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessDataResult<object>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Import(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return StatusCode((int)HttpStatusCode.BadRequest, new ErrorResult(Messages.ImportEmpty));
            string content;
            using (var reader = new StreamReader(file.OpenReadStream()))
                content = await reader.ReadToEndAsync();
            var result = await _productService.ImportFromCsv(content);
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Ürün güncelle (sadece admin)
        [HttpPut("update")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Ürün güncelle", Description = "Mevcut ürünü ve beden-stoklarını günceller. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorResult), (int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Update([FromBody] ProductUpdateRequestDto dto)
        {
            var result = await _productService.Update(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Ürünü kalıcı sil (sadece admin)
        [HttpDelete("delete/{id:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Ürün sil", Description = "Ürünü kalıcı olarak siler (hard delete). Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorResult), (int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _productService.Delete(id);
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Aktif/pasif durum değiştir (sadece admin, soft delete)
        [HttpPatch("status/{id:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Ürün durumu değiştir", Description = "Ürünü aktif/pasif yapar (soft delete). Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> ChangeStatus(int id)
        {
            var result = await _productService.ChangeStatus(id);
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Ürün detayı (herkese açık - storefront)
        [HttpGet("get/{id:int:min(1)}")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Ürün detayı", Description = "Ürünü bedenleri, stokları ve yorum özetiyle getirir.")]
        [ProducesResponseType(typeof(SuccessDataResult<ProductDetailResponseDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorDataResult<ProductDetailResponseDto>), (int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _productService.GetById(id);
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Tüm ürünler (admin liste)
        [HttpGet("getlist")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Ürün listesi", Description = "Tüm aktif ürünleri listeler. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessDataResult<List<ProductListResponseDto>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetList()
        {
            var result = await _productService.GetList();
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Filtre + sıralama + sayfalama (herkese açık - storefront ürün grid)
        [HttpPost("filter")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Ürün filtrele", Description = "Kategori, beden, renk, fiyat aralığı, indirim ve stok filtreleriyle sayfalı ürün listesi döner.")]
        [ProducesResponseType(typeof(SuccessDataResult<object>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Filter([FromBody] ProductFilterRequestDto dto)
        {
            var result = await _productService.GetListSearchAndFilterWithPaging(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }
        [HttpGet("on-sale")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Flash sale ürünleri", Description = "Şu an aktif indirimde olan ürünler.")]
        public async Task<IActionResult> OnSale()
        {
            var result = await _productService.GetOnSale();
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpGet("{productId:int:min(1)}/variants")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Ürün varyantları", Description = "Aynı gruptaki renk varyantları.")]
        public async Task<IActionResult> Variants(int productId)
        {
            var result = await _productService.GetVariants(productId);
            return StatusCode((int)result.Item1, result.Item2);
        }

    }
}
