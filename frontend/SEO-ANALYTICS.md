# SEO & Analytics Kurulumu

## SEO
- **JSON-LD yapısal veri** — index.html'e eklendi (Organization + WebSite + SearchAction). Google zengin sonuç için okur.
- **robots.txt** — `frontend/robots.txt`; admin ve /api gizlenir, sitemap gösterilir.
- **Dinamik sitemap** — backend `GET /api/seo/sitemap?baseUrl=https://divisima.com` aktif ürün + kategorileri XML döner. Frontend host'ta `/sitemap.xml` bu uca proxy'lenir (nginx: `location = /sitemap.xml { proxy_pass http://api/api/seo/sitemap?baseUrl=https://divisima.com; }`).

### SSR / Prerender (öneri — SPA SEO sınırı)
SPA olduğu için ürün sayfaları JS ile render olur; bazı botlar bunu göremez. Seçenekler:
- **Prerender.io** veya **Rendertron** — bot isteklerinde sunucu tarafı render (en kolay).
- **Next.js/Nuxt'e taşıma** — tam SSR (büyük iş, en iyi SEO).
- Şimdilik JSON-LD + meta + sitemap çoğu keşif için yeterli; ürün trafiği kritikse prerender ekle.

## Analytics
index.html'e GA4 + Meta Pixel hook'u eklendi. Etkinleştirmek için `<head>`'e ID'lerini ekle:
```html
<script>
  window.DIVISIMA_GA_ID = "G-XXXXXXXXXX";   // Google Analytics 4
  window.DIVISIMA_PIXEL_ID = "1234567890";  // Meta Pixel
</script>
```
ID boşsa analytics devre dışı (gizlilik dostu varsayılan).

### Olay takibi
`window.divisimaTrack(name, params)` — GA4 + Pixel'e aynı anda olay yollar. Frontend'in mevcut
`_track()` fonksiyonu buna bağlanabilir. Eşlenen olaylar: `add_to_cart`→AddToCart, `purchase`→Purchase,
`view_item`→ViewContent, `begin_checkout`→InitiateCheckout.
