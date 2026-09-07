using System.Security;
using System.Text;
using Divisima.DataAccess.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: SEO - dinamik sitemap.xml (aktif ürünler + kategoriler). Frontend host bunu /sitemap.xml'e
    // proxy'ler ya da build'de çeker. Arama motorları ürün sayfalarını keşfeder.
    [Route("api/[controller]")]
    [ApiController]
    public class SeoController : ControllerBase
    {
        private readonly IProductDal _productDal;
        private readonly ICategoryDal _categoryDal;
        private readonly IConfiguration _config;

        public SeoController(IProductDal productDal, ICategoryDal categoryDal, IConfiguration config)
        {
            _productDal = productDal;
            _categoryDal = categoryDal;
            _config = config;
        }

        // ══ LD-1 / LF-2 - SITE KOKU TEK KAYNAKTAN, ISTEMCIDEN DEGIL (AV-3 / SD-4) ══════════
        //
        // OLCULEN ONCEKI HAL: imza `Sitemap([FromQuery] string? baseUrl)` idi ve govde
        // `(baseUrl ?? "<sabit alan adi>")` yaziyordu. IKI AYRI KUSUR birdeydi:
        //
        //   (a) SABIT YEDEK YANLIS ALAN ADIYDI. Uc anonim istek - `/api/seo/sitemap` -
        //       depo sahibinin SAHIP OLMADIGI bir alan adinin URL'lerini uretiyordu. nginx
        //       proxy'si `?baseUrl=` ekiyle bunu MASKELIYORDU, yani kusur yalnizca uca
        //       DOGRUDAN gelen istekte gorunurdu (saglik probu, tarayici, kopyalanan bir
        //       baglanti). Maskeleyen ek de bu dalgada KALDIRILDI - iki yol artik AYNI.
        //
        //   (b) DAHA AGIRI: `baseUrl` ISTEMCI GIRDISIYDI ve dogrudan XML govdesine
        //       yaziliyordu. `[AllowAnonymous]` bir uctan kim gelirse gelsin sitemap'in
        //       ICERIGINI segebiliyordu; ustelik deger KACISLANMADIGI icin `<loc>` alanina
        //       ham `<`/`&` sokulabiliyordu (XML enjeksiyonu). Arama motoruna sunulan bir
        //       belgenin govdesi ISTEMCIYE birakilamaz.
        //
        // KARAR (merkez): sorgu parametresi TAMAMEN KALDIRILDI - opsiyonel birakmak "eski
        // cagrilar calismaya devam etsin" diye ayni kapiyi acik tutardi. Site koku TEK
        // KAYNAKTAN, `Storefront:BaseUrl` ayarindan okunur; bu ayar zaten uretimde ZORUNLU
        // (`appsettings.Production.example.json`) ve odeme donusu de ona baglidir.
        //
        // BOSSA GURULTULU DUSULUR (500), sessizce bir varsayilana kacilmaz: yanlis alan adli
        // bir sitemap, hic sitemap olmamasindan DAHA ZARARLIDIR - arama motoru onu indeksler
        // ve geri almak haftalar surer.
        [HttpGet("sitemap")]
        [AllowAnonymous]
        [Produces("application/xml")]
        public async Task<IActionResult> Sitemap()
        {
            var yapilandirilan = _config["Storefront:BaseUrl"];
            if (string.IsNullOrWhiteSpace(yapilandirilan))
                return Problem(
                    detail: "Storefront:BaseUrl tanımlı değil; sitemap üretilemez.",
                    statusCode: StatusCodes.Status500InternalServerError);

            // XML KACISLAMA - kaynak artik GUVENILIR (yapilandirma) olsa da govdeye giren her
            // deger kacislanir. Gerekce: "deger guvenilir" varsayimi, degerin NEREDEN geldigi
            // degistigi gun SESSIZCE cururu; kacislama ise hicbir sey maliyeti olmadan dogru
            // kalir. `&` tasiyan bir kok (ör. izleme parametreli) kacislanmadan XML'i BOZAR.
            var siteRoot = SecurityElement.Escape(yapilandirilan.TrimEnd('/'));
            var products = await _productDal.GetListAsync(p => p.is_active);
            var categories = await _categoryDal.GetListAsync(c => c.is_active);

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
            // Ana sayfa
            sb.AppendLine($"  <url><loc>{siteRoot}/</loc><changefreq>daily</changefreq><priority>1.0</priority></url>");
            // Kategoriler
            foreach (var c in categories)
                sb.AppendLine($"  <url><loc>{siteRoot}/#/kategori/{c.id}</loc><changefreq>weekly</changefreq><priority>0.8</priority></url>");
            // Ürünler
            foreach (var p in products)
                sb.AppendLine($"  <url><loc>{siteRoot}/#/urun/{p.id}</loc><changefreq>weekly</changefreq><priority>0.6</priority></url>");
            sb.AppendLine("</urlset>");

            return Content(sb.ToString(), "application/xml", Encoding.UTF8);
        }
    }
}
