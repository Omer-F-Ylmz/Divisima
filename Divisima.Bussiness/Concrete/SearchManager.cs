using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using AutoMapper;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Dtos;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Product;
using Divisima.Entity.Dtos.Search;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Ürün arama iş kuralları. Metin + fiyat + kategori + stok filtreleri, alaka sıralaması, sayfalama.
    // Metin sorgusu varsa RELEVANCE RANKING (in-memory skorlama) uygulanır; fiyat sıralaması istenirse DB-tarafı sayfalama.
    public class SearchManager : ISearchService
    {
        private readonly IProductDal _productDal;
        private readonly IProductStockDal _productStockDal;
        private readonly IMapper _mapper;

        public SearchManager(IProductDal productDal, IProductStockDal productStockDal, IMapper mapper)
        {
            _productDal = productDal;
            _productStockDal = productStockDal;
            _mapper = mapper;
        }

        // GUVENLIK-FIX (G3): `query` UZUNLUGU giriste dogrulanir (ProductSearchRequestValidator, 200 krkt).
        // Burada SESSIZ bir kirpma YAPILMAZ - kirpma, kullanicinin yazdigi terimden baska bir sey
        // aratmak olurdu. Sinir kalkarsa 4000+ karakterlik terim SQL 8152 ile 500 dondurur (olculdu).
        public async Task<(HttpStatusCode, Result)> SearchProducts(ProductSearchRequestDto dto)
        {
            var q = dto.query?.Trim().ToLower() ?? "";
            bool hasQuery = !string.IsNullOrEmpty(q);
            bool priceSort = dto.sort_by is "price_asc" or "price_desc";
            bool stockFilter = dto.in_stock_only == true;

            // Açıklayıcı yorum: Metinsiz olmayan filtreler (aktif + kategori + fiyat) - her iki yolda da geçerli
            System.Linq.Expressions.Expression<Func<Product, bool>> baseFilter = p =>
                p.is_active
                && (string.IsNullOrEmpty(q) || p.name.ToLower().Contains(q) || p.brand.ToLower().Contains(q))
                && (!dto.category_id.HasValue || p.category_id == dto.category_id.Value)
                && (!dto.sub_category_id.HasValue || p.sub_category_id == dto.sub_category_id.Value)
                && (!dto.min_price.HasValue || p.price >= dto.min_price.Value)
                && (!dto.max_price.HasValue || p.price <= dto.max_price.Value);

            // Açıklayıcı yorum: RELEVANCE veya STOK filtresi gerekiyorsa in-memory yol (skorla + sırala + sayfala).
            // Sadece fiyat sıralaması + stok filtresi yoksa DB-tarafı sayfalama (performanslı).
            if ((hasQuery && !priceSort) || stockFilter)
            {
                var matches = await _productDal.GetListNoTrackingAsync(baseFilter);

                // Açıklayıcı yorum: Stok filtresi - müsait (stock - reserved) > 0 olan ürünler
                if (stockFilter && matches.Count > 0)
                {
                    var matchIds = matches.Select(p => p.id).ToList();
                    var stocks = await _productStockDal.GetListNoTrackingAsync(s => matchIds.Contains(s.product_id) && s.is_active);
                    var inStockProductIds = stocks
                        .Where(s => (s.stock_quantity - s.reserved_quantity) > 0)
                        .Select(s => s.product_id)
                        .Distinct()
                        .ToHashSet();
                    matches = matches.Where(p => inStockProductIds.Contains(p.id)).ToList();
                }

                // Açıklayıcı yorum: Sıralama - metin sorgusu varsa alaka puanı, yoksa istenen sıra
                IEnumerable<Product> ordered;
                if (hasQuery && !priceSort)
                {
                    var tokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    ordered = matches
                        .Select(p => new { p, score = RelevanceScore(p, q, tokens) })
                        .OrderByDescending(x => x.score)
                        .ThenByDescending(x => x.p.created_at) // eşitlikte en yeni
                        .Select(x => x.p);
                }
                else
                {
                    ordered = dto.sort_by switch
                    {
                        "price_asc" => matches.OrderBy(p => p.price),
                        "price_desc" => matches.OrderByDescending(p => p.price),
                        _ => matches.OrderByDescending(p => p.created_at)
                    };
                }

                var orderedList = ordered.ToList();
                var total = orderedList.Count;

                // PAGINATION SINIR (ProductManager ile tutarlı): page>=1, size 1..100. Clamp'siz size çok büyük -> tüm
                // eşleşenler yanıta serialize edilir (DoS - bellek+bant); page<=0 -> negatif Skip. Kullanıcı girdisi clamp'lenir.
                var page = dto.page < 1 ? 1 : dto.page;
                var size = dto.size < 1 ? 20 : (dto.size > 100 ? 100 : dto.size);

                // Açıklayıcı yorum: In-memory sayfalama
                var pageItems = orderedList
                    .Skip((page - 1) * size)
                    .Take(size)
                    .ToList();

                var respRel = new PagedResult<ProductListResponseDto>
                {
                    Items = _mapper.Map<List<ProductListResponseDto>>(pageItems),
                    TotalCount = total,
                    Page = page,
                    Size = size
                };
                return (HttpStatusCode.OK, new SuccessDataResult<PagedResult<ProductListResponseDto>>(respRel, Messages.SearchCompleted));
            }

            // Açıklayıcı yorum: DB-tarafı sayfalama yolu (fiyat sıralaması / sorgusuz liste)
            System.Linq.Expressions.Expression<Func<Product, object>> orderBy = dto.sort_by switch
            {
                "price_asc" => p => p.price,
                "price_desc" => p => p.price,
                _ => p => p.created_at
            };
            bool descending = dto.sort_by is "price_desc" or "newest" or null;

            var paged = await _productDal.GetPagedAsync(dto, baseFilter, orderBy, descending);

            var response = new PagedResult<ProductListResponseDto>
            {
                Items = _mapper.Map<List<ProductListResponseDto>>(paged.Items),
                TotalCount = paged.TotalCount,
                Page = paged.Page,
                Size = paged.Size
            };
            return (HttpStatusCode.OK, new SuccessDataResult<PagedResult<ProductListResponseDto>>(response, Messages.SearchCompleted));
        }

        // Açıklayıcı yorum: Alaka puanı - tam eşleşme > baştan başlar > içerir; ad markadan ağırlıklı; çok-kelime token bonusu.
        private static int RelevanceScore(Product p, string query, string[] tokens)
        {
            var name = (p.name ?? "").ToLower();
            var brand = (p.brand ?? "").ToLower();
            int score = 0;

            // Tam ifade eşleşmeleri
            if (name == query) score += 100;
            else if (name.StartsWith(query)) score += 50;
            else if (name.Contains(query)) score += 30;

            if (brand == query) score += 40;
            else if (brand.Contains(query)) score += 15;

            // Token bazlı (çok kelimeli sorgu)
            int tokenHitsInName = 0;
            foreach (var t in tokens)
            {
                if (name.Contains(t)) { score += 10; tokenHitsInName++; }
                if (brand.Contains(t)) score += 5;
            }
            // Tüm kelimeler adda geçiyorsa bonus
            if (tokens.Length > 1 && tokenHitsInName == tokens.Length) score += 20;

            return score;
        }
    }
}
