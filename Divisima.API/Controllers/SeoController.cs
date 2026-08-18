using System.Text;
using Microsoft.AspNetCore.Authorization;
using Divisima.DataAccess.Abstract;
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

        public SeoController(IProductDal productDal, ICategoryDal categoryDal)
        {
            _productDal = productDal;
            _categoryDal = categoryDal;
        }

        [HttpGet("sitemap")]
        [AllowAnonymous]
        [Produces("application/xml")]
        public async Task<IActionResult> Sitemap([FromQuery] string? baseUrl)
        {
            // Açıklayıcı yorum: baseUrl = frontend kök (ör. https://divisima.com)
            var siteRoot = (baseUrl ?? "https://divisima.com").TrimEnd('/');
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
