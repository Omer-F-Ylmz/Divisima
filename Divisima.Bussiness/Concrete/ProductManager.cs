using System.Globalization;
using System.Net;
using AutoMapper;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Caching;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Pricing;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Product;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Ürün iş kuralları. Akış her metotta aynı: (validation FluentValidation ile aspect'te) ->
    // iş kuralı kontrolü -> _mapper.Map -> _productDal.XAsync -> (HttpStatusCode, SuccessResult/ErrorResult).
    // Cafixo ProductManager/BrandManager kalıbına birebir.
    public class ProductManager : IProductService
    {
        private readonly IProductDal _productDal;
        private readonly IProductStockDal _productStockDal;
        private readonly IProductReviewDal _productReviewDal;
        private readonly IPriceDropService _priceDropService;
        private readonly IMapper _mapper;

        private readonly ICacheService _cache;

        // BAYAT VITRIN FIX (H47): urun degisince vitrin listelerinin cache'i TEMIZLENMELI.
        // Onceden hicbir yerde temizlenmiyordu -> 10 dk boyunca ESKI fiyat gosteriliyor, pasiflenen/silinen
        // urun listede kaliyor, YENI urun "yeni gelenler"de gorunmuyordu. (Odeme sunucu tarafinda hesaplandigi
        // icin para kaybi yok ama musteri 199 gorup 249 oduyordu = guven sorunu.)
        private void InvalidateStorefrontCache() => _cache.RemoveByPrefix("merch:");

        // SPRINT 8 MADDE 5: liste yolu artik category_name dolduruyor - kategori adlarina erisim gerekli.
        private readonly ICategoryDal _categoryDal;

        public ProductManager(IProductDal productDal, IProductStockDal productStockDal, IProductReviewDal productReviewDal, IMapper mapper,
            IPriceDropService priceDropService, ICacheService cache, ICategoryDal categoryDal)
        {
            _categoryDal = categoryDal;
            _productDal = productDal;
            _productStockDal = productStockDal;
            _productReviewDal = productReviewDal;
            _priceDropService = priceDropService;
            _cache = cache;
            _mapper = mapper;
        }

        // Açıklayıcı yorum: Yeni ürün ekle. Aynı isim+marka varsa reddedilir, sonra beden stokları eklenir.
        public async Task<(HttpStatusCode, Result)> Add(ProductAddRequestDto dto)
        {
            // Açıklayıcı yorum: FİYAT VALİDASYONU - fiyat pozitif; indirimli fiyat varsa 0 < sale_price < price.
            // (Aksi halde sahte indirim / sale_price >= price ile "indirim" adı altında normal/yüksek fiyat gösterilirdi.)
            if (dto.price <= 0)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ProductInvalidPrice));
            if (dto.sale_price.HasValue && (dto.sale_price.Value <= 0 || dto.sale_price.Value >= dto.price))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ProductInvalidSalePrice));

            // Açıklayıcı yorum: İş kuralı - aynı isimde ürün zaten var mı
            var exists = await _productDal.GetAsync(p => p.name == dto.name && p.brand == dto.brand && p.is_active);
            if (exists != null)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ProductAlreadyExists));

            // Açıklayıcı yorum: DTO -> entity
            var product = _mapper.Map<Product>(dto);
            product.is_active = true;
            product.created_at = DateTime.Now;

            await _productDal.AddAsync(product);

            // Açıklayıcı yorum: Beden-stok satırlarını ekle (frontend sizeStockOf karşılığı)
            if (dto.stocks != null && dto.stocks.Count > 0)
            {
                foreach (var s in dto.stocks)
                {
                    await _productStockDal.AddAsync(new ProductStock
                    {
                        product_id = product.id,
                        size = s.size,
                        stock_quantity = s.stock_quantity,
                        is_active = true,
                        created_at = DateTime.Now
                    });
                }
            }

            InvalidateStorefrontCache();   // H47: yeni urun "yeni gelenler"de hemen gorunsun
            return (HttpStatusCode.Created, new SuccessResult(Messages.ProductAdded));
        }

        // Aciklayici yorum: TOPLU URUN ICE-AKTARMA (CSV). Admin yuzlerce urunu tek tek eklemek zorunda kalmasin.
        // Beklenen baslik: name,brand,category_id,price,sale_price,description,color_hex,product_type,size,stock_quantity
        // Ayni (name+brand) satirlari tek urunde gruplanir (bedenler stok olarak eklenir). Mevcut urunler atlanir.
        public async Task<(HttpStatusCode, Result)> ImportFromCsv(string csvContent)
        {
            if (string.IsNullOrWhiteSpace(csvContent))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ImportEmpty));
            var lines = csvContent.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ImportEmpty));

            var errors = new List<string>();
            var grouped = new Dictionary<string, (Product head, List<(string size, int qty)> stocks)>();
            var ci = CultureInfo.InvariantCulture;

            for (int i = 1; i < lines.Length; i++)
            {
                var cols = ParseCsvLine(lines[i]);
                if (cols.Length < 10) { errors.Add($"Satir {i + 1}: eksik kolon"); continue; }
                var name = cols[0].Trim();
                var brand = cols[1].Trim();
                if (string.IsNullOrWhiteSpace(name)) { errors.Add($"Satir {i + 1}: ad bos"); continue; }
                if (!int.TryParse(cols[2].Trim(), out var categoryId)) { errors.Add($"Satir {i + 1}: gecersiz category_id"); continue; }
                if (!decimal.TryParse(cols[3].Trim(), NumberStyles.Any, ci, out var price) || price <= 0) { errors.Add($"Satir {i + 1}: gecersiz fiyat"); continue; }
                decimal? salePrice = null;
                if (!string.IsNullOrWhiteSpace(cols[4]) && decimal.TryParse(cols[4].Trim(), NumberStyles.Any, ci, out var sp)) salePrice = sp;
                // sahte indirim engeli: indirimli fiyat 0 < sale < price olmali
                if (salePrice.HasValue && (salePrice.Value <= 0 || salePrice.Value >= price)) { errors.Add($"Satir {i + 1}: gecersiz indirimli fiyat"); continue; }
                // KALITE SUPURMESI B4: donus degeri YOK SAYILIYORDU - bozuk bir deger SESSIZCE 0
                // ("giyim") oluyordu. Ayni dongudeki diger DOKUZ kolonun hepsi dogrulanip hata
                // listesine yaziliyor; tek istisna buydu. Bos birakilmasi mesru (varsayilan 0),
                // ama DOLU ve BOZUK bir deger artik sessizce yutulmaz.
                byte productType = 0;
                if (!string.IsNullOrWhiteSpace(cols[7]) && !byte.TryParse(cols[7].Trim(), out productType))
                { errors.Add($"Satir {i + 1}: gecersiz product_type"); continue; }
                var size = cols[8].Trim();
                if (!int.TryParse(cols[9].Trim(), out var qty) || qty < 0) { errors.Add($"Satir {i + 1}: gecersiz stok"); continue; }

                var key = name + "|" + brand;
                if (!grouped.ContainsKey(key))
                    grouped[key] = (new Product
                    {
                        name = name,
                        brand = brand,
                        category_id = categoryId,
                        price = price,
                        sale_price = salePrice,
                        description = cols[5].Trim(),
                        color_hex = cols[6].Trim(),
                        product_type = productType
                    }, new List<(string, int)>());
                if (!string.IsNullOrWhiteSpace(size))
                    grouped[key].stocks.Add((size, qty));
            }

            int imported = 0, skipped = 0;
            foreach (var kv in grouped)
            {
                var h = kv.Value.head;
                var exists = await _productDal.GetAsync(p => p.name == h.name && p.brand == h.brand && p.is_active);
                if (exists != null) { skipped++; continue; }
                h.is_active = true;
                h.created_at = DateTime.Now;
                await _productDal.AddAsync(h);
                foreach (var (size, qty) in kv.Value.stocks)
                    await _productStockDal.AddAsync(new ProductStock
                    {
                        product_id = h.id,
                        size = size,
                        stock_quantity = qty,
                        is_active = true,
                        created_at = DateTime.Now
                    });
                imported++;
            }

            var summary = $"{imported} urun eklendi, {skipped} atlandi (mevcut)" + (errors.Count > 0 ? $", {errors.Count} hatali satir" : "");
            InvalidateStorefrontCache();   // H47: vitrin listeleri bayat kalmasin
            return (HttpStatusCode.OK, new SuccessDataResult<object>(new { imported, skipped, errors }, summary));
        }

        // Aciklayici yorum: Basit CSV satir ayiristirici - tirnakli alan icindeki virgulu korur.
        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new System.Text.StringBuilder();
            bool inQuotes = false;
            foreach (var ch in line)
            {
                if (ch == '"') inQuotes = !inQuotes;
                else if (ch == ',' && !inQuotes) { result.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(ch);
            }
            result.Add(sb.ToString());
            return result.ToArray();
        }

        // Açıklayıcı yorum: Ürün güncelle. Kayıt yoksa NotFound, varsa alanlar güncellenir + stoklar senkronlanır.
        public async Task<(HttpStatusCode, Result)> Update(ProductUpdateRequestDto dto)
        {
            var product = await _productDal.GetAsync(p => p.id == dto.id);
            if (product == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.ProductNotFound));

            // FİYAT VALİDASYONU (Add ile AYNI - Update'te de zorunlu): fiyat pozitif; indirimli fiyat varsa 0 < sale_price < price.
            // Aksi halde mapper dto.price/sale_price'ı doğrudan yazdığından negatif fiyat veya sale_price>=price (SAHTE indirim) geçerdi.
            if (dto.price <= 0)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ProductInvalidPrice));
            if (dto.sale_price.HasValue && (dto.sale_price.Value <= 0 || dto.sale_price.Value >= dto.price))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ProductInvalidSalePrice));

            // Açıklayıcı yorum: Fiyat düşüş bildirimi için eski fiyatı yakala (mapper'dan ÖNCE)
            var oldPrice = product.price;

            // Açıklayıcı yorum: Alanları güncelle (mapper mevcut entity üzerine)
            _mapper.Map(dto, product);
            product.updated_at = DateTime.Now;
            await _productDal.UpdateAsync(product);

            // Açıklayıcı yorum: Fiyat düştüyse "fiyat düşünce haber ver" abonelerini bilgilendir
            if (product.price < oldPrice)
                await _priceDropService.NotifyPriceDrop(product.id, product.price);

            // Açıklayıcı yorum: Beden stoklarını güncelle - mevcutları pasifle, gelenleri yeniden yaz
            if (dto.stocks != null)
            {
                var current = await _productStockDal.GetListAsync(s => s.product_id == product.id);
                foreach (var old in current)
                {
                    old.is_active = false;
                    await _productStockDal.UpdateAsync(old);
                }
                foreach (var s in dto.stocks)
                {
                    await _productStockDal.AddAsync(new ProductStock
                    {
                        product_id = product.id,
                        size = s.size,
                        stock_quantity = s.stock_quantity,
                        is_active = true,
                        created_at = DateTime.Now
                    });
                }
            }

            InvalidateStorefrontCache();   // H47: vitrin listeleri bayat kalmasin
            return (HttpStatusCode.OK, new SuccessResult(Messages.ProductUpdated));
        }

        // Açıklayıcı yorum: Kalıcı sil (hard delete). Kayıt yoksa NotFound.
        public async Task<(HttpStatusCode, Result)> Delete(int id)
        {
            var product = await _productDal.GetAsync(p => p.id == id);
            if (product == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.ProductNotFound));

            // Açıklayıcı yorum: Soft-delete - kayıt silinmez, pasifleştirilir (sipariş/ilişki bütünlüğü korunur)
            product.is_active = false;
            await _productDal.UpdateAsync(product);
            InvalidateStorefrontCache();   // H47: vitrin listeleri bayat kalmasin
            return (HttpStatusCode.OK, new SuccessResult(Messages.ProductDeleted));
        }

        // Açıklayıcı yorum: Aktif/pasif toggle (soft delete). Storefront sadece is_active=true görür.
        public async Task<(HttpStatusCode, Result)> ChangeStatus(int id)
        {
            var product = await _productDal.GetIgnoringFiltersAsync(p => p.id == id);
            if (product == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.ProductNotFound));

            product.is_active = !product.is_active;
            product.updated_at = DateTime.Now;
            await _productDal.UpdateAsync(product);

            InvalidateStorefrontCache();   // H47: vitrin listeleri bayat kalmasin
            return (HttpStatusCode.OK, new SuccessResult(Messages.ProductStatusChanged));
        }

        // Açıklayıcı yorum: Tek ürünü detayıyla getir (bedenler + yorum ortalaması).
        public async Task<(HttpStatusCode, Result)> GetById(int id)
        {
            var product = await _productDal.GetAsync(p => p.id == id && p.is_active);
            if (product == null)
                return (HttpStatusCode.NotFound, new ErrorDataResult<ProductDetailResponseDto>(Messages.ProductNotFound));

            var data = _mapper.Map<ProductDetailResponseDto>(product);

            // Açıklayıcı yorum: Bedenleri ayrı yükle (nav property yok - kompozisyon serviste)
            var stocks = await _productStockDal.GetListAsync(s => s.product_id == id && s.is_active);
            data.stocks = stocks.Select(s => new ProductStockDto { size = s.size, stock_quantity = s.stock_quantity }).ToList();

            // Açıklayıcı yorum: Onaylı yorumlardan puan özeti (frontend reviewsOf)
            var reviews = await _productReviewDal.GetApprovedByProductAsync(id);
            if (reviews.Count > 0)
            {
                data.review_count = reviews.Count;
                data.review_average = Math.Round(reviews.Average(r => r.rating), 1);
            }

            return (HttpStatusCode.OK, new SuccessDataResult<ProductDetailResponseDto>(data, Messages.ProductListed));
        }

        // Açıklayıcı yorum: Tüm aktif ürünler (admin liste).
        public async Task<(HttpStatusCode, Result)> GetList()
        {
            var products = await _productDal.GetListAsync(p => p.is_active);
            var data = _mapper.Map<List<ProductListResponseDto>>(products);

            // SPRINT 8 MADDE 5: bedenler ARTIK ortak yardimcidan - kategori adi ve toplam stok da
            // burada doluyor. Onceden yalniz "sizes" doldurulurdu ve storefront yolu HICBIRINI
            // doldurmuyordu; iki yol ayrisiyordu. Tek yardimci, bir daha ayrisamazlar.
            await ListeyiZenginlestirAsync(data);

            return (HttpStatusCode.OK, new SuccessDataResult<List<ProductListResponseDto>>(data, Messages.ProductListed));
        }

        // Açıklayıcı yorum: Filtre + sıralama + sayfalama (storefront). Filtreleme DataAccess'te (PredicateBuilder).
        public async Task<(HttpStatusCode, Result)> GetListSearchAndFilterWithPaging(ProductFilterRequestDto dto)
        {
            // Açıklayıcı yorum: PAGINATION SINIR - page>=1, size 1..100. Aksi halde: page<=0 -> Skip negatif (patlar),
            // size=0 -> sıfıra bölme (total_pages), size çok büyük -> tüm tablo (DoS). Kullanıcı girdisi clamp'lenir.
            dto.page = dto.page < 1 ? 1 : dto.page;
            dto.size = dto.size < 1 ? 20 : (dto.size > 100 ? 100 : dto.size);

            var (items, totalCount) = await _productDal.GetListWithFilterAsync(
                dto.category_id, dto.sub_category_id, dto.sizes, dto.colors,
                dto.min_price, dto.max_price, dto.on_sale, dto.in_stock,
                dto.sort, dto.page, dto.size);

            var list = _mapper.Map<List<ProductListResponseDto>>(items);

            // SPRINT 8 MADDE 5: category_name + total_stock + sizes ARTIK BURADA doluyor.
            // Bu satir olmadan vitrindeki her urun "kategorisiz + 0 stok + bedensiz" geliyordu
            // ve istemci urun basina ayri detay cagrisi yapmak zorunda kaliyordu.
            await ListeyiZenginlestirAsync(list);

            // Açıklayıcı yorum: Sayfalama meta bilgisiyle sar ({X}PagingListResponseDto kalıbı)
            var paged = new ProductPagingListResponseDto
            {
                items = list,
                total_count = totalCount,
                page = dto.page,
                size = dto.size,
                total_pages = (int)Math.Ceiling(totalCount / (double)dto.size)
            };

            return (HttpStatusCode.OK, new SuccessDataResult<ProductPagingListResponseDto>(paged, Messages.ProductListed));
        }
        // Açıklayıcı yorum: Şu an flash sale'de olan ürünler (aktif indirim penceresi)
        public async Task<(HttpStatusCode, Result)> GetOnSale()
        {
            var now = DateTime.Now;
            var all = await _productDal.GetListNoTrackingAsync(p => p.is_active && p.sale_price != null);
            var onSale = all.Where(p => PricingHelper.IsOnSale(p.sale_price, p.sale_start, p.sale_end, now)).ToList();
            return (HttpStatusCode.OK, new SuccessDataResult<System.Collections.Generic.List<Divisima.Entity.Entities.Product>>(onSale));
        }

        // Açıklayıcı yorum: Ürün varyantları - aynı variant_group_id'ye sahip diğer aktif ürünler (renk varyantları)
        public async Task<(HttpStatusCode, Result)> GetVariants(int productId)
        {
            var product = await _productDal.GetAsync(p => p.id == productId && p.is_active);
            if (product == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.ProductNotFound));
            if (string.IsNullOrEmpty(product.variant_group_id))
                return (HttpStatusCode.OK, new SuccessDataResult<System.Collections.Generic.List<Divisima.Entity.Entities.Product>>(new System.Collections.Generic.List<Divisima.Entity.Entities.Product>()));

            var variants = await _productDal.GetListNoTrackingAsync(p => p.is_active && p.variant_group_id == product.variant_group_id);
            return (HttpStatusCode.OK, new SuccessDataResult<System.Collections.Generic.List<Divisima.Entity.Entities.Product>>(variants.ToList()));
        }


        // ── SPRINT 8 MADDE 5 - LISTE DTO ZENGINLESTIRME (TEK YERDE) ────────────────
        //
        // OLCULEN BOSLUK (E1'de bulundu, E1 pini ile sabitlenmisti): storefront'un kullandigi
        // "GetListSearchAndFilterWithPaging" yolu `category_name`, `total_stock` ve `sizes`
        // alanlarini HIC DOLDURMUYORDU (ProductProfile ucunu de Ignore ediyor). Ham veriyle
        // vitrindeki HER urun "kategorisiz + 0 stok + bedensiz" gorunuyor, yani bastan sona
        // "Tukendi" yaziyordu. E1 bunu ISTEMCIDE telafi etti: urun basina AYRI detay cagrisi
        // (6 eszamanli, sayfa boyutu 24) - yani bir vitrin sayfasi 25 istek demekti.
        //
        // Admin "GetList" yolu bedenleri zaten dolduruyordu ama kategori/stok orada da yoktu.
        // Iki yol AYNI yardimciya baglandi: bir daha ayrisamazlar.
        //
        // N+1 YOK: kategoriler ve stoklar TEK sorguda cekilip sozlukten eslestirilir.
        private async Task ListeyiZenginlestirAsync(List<ProductListResponseDto> data)
        {
            if (data == null || data.Count == 0) return;

            // 1) KATEGORI ADI - yalniz listedeki kategoriler cekilir.
            var katIds = data.Select(d => d.category_id).Distinct().ToList();
            var katAdlari = (await _categoryDal.GetListNoTrackingAsync(c => katIds.Contains(c.id)))
                .ToDictionary(c => c.id, c => c.name);

            // 2) BEDENLER + TOPLAM STOK - tek sorgu, urune gore gruplanir.
            // "available" (stock_quantity - reserved_quantity) kullanilir: bir bedenin tamami
            // baskalarinin sepetinde rezerveyse o beden SATILABILIR DEGILDIR ve vitrinde
            // "var" gibi gorunmemeli. (CLAUDE.md: stok assertleri available uzerinden yapilir.)
            var urunIds = data.Select(d => d.id).Distinct().ToList();
            var stoklar = await _productStockDal.GetListNoTrackingAsync(
                st => st.is_active && urunIds.Contains(st.product_id));

            var stokSozluk = stoklar
                .GroupBy(st => st.product_id)
                .ToDictionary(
                    g => g.Key,
                    g => (
                        Toplam: g.Sum(st => Math.Max(0, st.stock_quantity - st.reserved_quantity)),
                        Bedenler: g.Where(st => st.stock_quantity - st.reserved_quantity > 0)
                                   .Select(st => st.size)
                                   .Distinct()
                                   .ToList()
                    ));

            foreach (var item in data)
            {
                if (katAdlari.TryGetValue(item.category_id, out var ad)) item.category_name = ad;
                if (stokSozluk.TryGetValue(item.id, out var s))
                {
                    item.total_stock = s.Toplam;
                    item.sizes = s.Bedenler;
                }
                else
                {
                    item.total_stock = 0;
                    item.sizes = new List<string>();
                }
            }
        }
    }
}
