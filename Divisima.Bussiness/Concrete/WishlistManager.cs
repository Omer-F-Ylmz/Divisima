using System.Net;
using Divisima.Entity.Dtos.Cart;
using AutoMapper;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Product;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Favori listesi iş kuralları. Toggle: varsa siler, yoksa ekler (frontend kalp ikonu).
    public class WishlistManager : IWishlistService
    {
        private readonly IWishlistItemDal _wishlistDal;
        private readonly IProductDal _productDal;
        private readonly IMapper _mapper;
        private readonly ICartService _cartService;

        public WishlistManager(IWishlistItemDal wishlistDal, IProductDal productDal, IMapper mapper, ICartService cartService)
        {
            _wishlistDal = wishlistDal;
            _productDal = productDal;
            _mapper = mapper;
            _cartService = cartService;
        }

        public async Task<(HttpStatusCode, Result)> Toggle(int customerId, int productId)
        {
            var existing = await _wishlistDal.GetAsync(w => w.customer_id == customerId && w.product_id == productId);
            if (existing != null)
            {
                await _wishlistDal.DeleteAsync(existing);
                return (HttpStatusCode.OK, new SuccessResult(Messages.WishlistRemoved));
            }
            await _wishlistDal.AddAsync(new WishlistItem { customer_id = customerId, product_id = productId, created_at = DateTime.Now });
            return (HttpStatusCode.OK, new SuccessResult(Messages.WishlistAdded));
        }


        // Açıklayıcı yorum: WISHLIST -> SEPET. İstek listesindeki ürünü sepete taşır (stok kontrolü CartManager'da),
        // başarılıysa istek listesinden çıkarır. Beden seçimi gerektiğinden size parametresi alınır.
        public async Task<(HttpStatusCode, Result)> MoveToCart(int customerId, int productId, string size, int quantity)
        {
            var wish = await _wishlistDal.GetAsync(w => w.customer_id == customerId && w.product_id == productId);
            if (wish == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.WishlistItemNotFound));

            // Sepete ekle (stok/miktar doğrulaması CartManager.AddItem'da)
            var addResult = await _cartService.AddItem(new CartItemRequestDto
            {
                customer_id = customerId, product_id = productId, size = size,
                quantity = quantity < 1 ? 1 : quantity
            });
            if (addResult.Item1 != HttpStatusCode.OK && addResult.Item1 != HttpStatusCode.Created)
                return addResult;  // stok yok / geçersiz -> sepete eklenmedi, wishlist'te kalır

            // Sepete girdiyse istek listesinden çıkar
            await _wishlistDal.DeleteAsync(wish);
            return (HttpStatusCode.OK, new SuccessResult(Messages.WishlistMovedToCart));
        }

        public async Task<(HttpStatusCode, Result)> GetByCustomer(int customerId)
        {
            var items = await _wishlistDal.GetListNoTrackingAsync(w => w.customer_id == customerId);
            // N+1 duzeltmesi: tum urunleri tek sorguda getir
            var wishIds = items.Select(w => w.product_id).Distinct().ToList();
            var wishProducts = await _productDal.GetListAsync(x => wishIds.Contains(x.id) && x.is_active);
            var products = wishProducts.Select(p => _mapper.Map<ProductListResponseDto>(p)).ToList();
            return (HttpStatusCode.OK, new SuccessDataResult<List<ProductListResponseDto>>(products, Messages.WishlistListed));
        }
    }
}
