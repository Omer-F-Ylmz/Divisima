using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.Core.Utilities.Validation;
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
        // GF-6 / K7 (D7): istek govdesi tavani - sinir `GirdiSinirlari.CsvDosyaEnBuyukBayt`
        // ile AYNI degerdir. Oznitelik SABIT ister (derleme zamani), bu yuzden ifade
        // `5 * 1024 * 1024` olarak yazildi; ikisinin ayrisMAdigi `GuvenlikFix6SozlesmeTests`
        // ile PINLI. Bu kapi govdeyi HIC OKUMADAN reddeder; yukaridaki `file.Length`
        // kontrolu ise multipart icindeki TEK dosyanin boyutunu sorar (ikisi FARKLI sey).
        [RequestSizeLimit(5 * 1024 * 1024)]
        [SwaggerOperation(Summary = "Toplu ürün içe-aktar (CSV)", Description = "CSV dosyasından çok sayıda ürünü tek seferde ekler. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessDataResult<object>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Import(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return StatusCode((int)HttpStatusCode.BadRequest, new ErrorResult(Messages.ImportEmpty));

            // ══ GF-6 / K7 (D7) - DOSYA TURU VE BOYUT KAPISI ═══════════════════════════════
            //
            // OLCULEN ONCE-DURUM (AV-3 / T2-2): uc, gelen HER dosyayi metin gibi okuyordu -
            // ne uzanti ne content-type soruluyordu. Boyut da yalnizca sunucunun genel
            // multipart tavanina bagliydi.
            //
            // IKI OLCUT BIRLIKTE, "VEYA" DEGIL "VE": tarayicilar `.csv` icin content-type'i
            // isletim sistemine gore FARKLI gonderir (`text/csv`, `application/vnd.ms-excel`,
            // hatta `application/octet-stream`), yani content-type TEK BASINA hem yanlis
            // pozitif hem yanlis negatif verir. Bu yuzden KARAR UZANTIYA baglandi ve
            // content-type yalnizca ACIKCA CELISIYORSA reddeder (`image/`, `application/pdf`
            // gibi). Uzanti karsilastirmasi ORDINAL/INVARIANT - `.CSV` bir KIMLIK dizgesidir
            // ve Turkce `I` katlanmasi burada YANLIS sonuc verirdi (CLAUDE.md 6c).
            if (file.Length > GirdiSinirlari.CsvDosyaEnBuyukBayt)
                return StatusCode((int)HttpStatusCode.BadRequest, new ErrorResult(Messages.ImportFileTooLarge));

            var uzanti = System.IO.Path.GetExtension(file.FileName ?? string.Empty);
            if (!string.Equals(uzanti, ".csv", StringComparison.OrdinalIgnoreCase))
                return StatusCode((int)HttpStatusCode.BadRequest, new ErrorResult(Messages.ImportFileTypeInvalid));

            var tur = (file.ContentType ?? string.Empty).Trim();
            if (tur.Length > 0
                && !tur.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                && !tur.StartsWith("application/vnd.ms-excel", StringComparison.OrdinalIgnoreCase)
                && !tur.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase))
                return StatusCode((int)HttpStatusCode.BadRequest, new ErrorResult(Messages.ImportFileTypeInvalid));

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

        // Açıklayıcı yorum: Aktif ürünler (admin liste) - DALGA-3-FIX (P3) ile SAYFALI.
        // Parametreler OPSIYONEL: parametresiz cagri varsayilan sayfayi doner (page=1, size=100).
        // Yanit storefront yolundaki zarfin AYNISI (items + total_count + page + size + total_pages)
        // - boylece kirpilma sessiz kalamaz. Gerekcenin tamami ProductManager.GetList uzerinde.
        [HttpGet("getlist")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Ürün listesi (sayfalı)", Description = "Aktif ürünleri sayfalı listeler (varsayılan 100/sayfa, üst sınır 200). Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessDataResult<ProductPagingListResponseDto>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int size = 100)
        {
            var result = await _productService.GetList(page, size);
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
