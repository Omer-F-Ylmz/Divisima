using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Pricing;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Cart;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Kalıcı sepet iş kuralları. Müşteri sepeti yoksa oluşturur; ekleme stok kontrollü.
    public class CartManager : ICartService
    {
        private readonly ICartDal _cartDal;
        private readonly ICartItemDal _cartItemDal;
        private readonly IProductDal _productDal;
        private readonly IStockService _stockService;

        private readonly IWishlistItemDal _wishlistItemDal;

        public CartManager(ICartDal cartDal, ICartItemDal cartItemDal, IProductDal productDal,
            IStockService stockService, IWishlistItemDal wishlistItemDal)
        {
            _cartDal = cartDal;
            _cartItemDal = cartItemDal;
            _productDal = productDal;
            _stockService = stockService;
            _wishlistItemDal = wishlistItemDal;
        }

        // Açıklayıcı yorum: Müşterinin aktif sepetini getir, yoksa oluştur
        private async Task<Cart> GetOrCreateCartAsync(int customerId)
        {
            var cart = await _cartDal.GetAsync(c => c.customer_id == customerId && c.is_active);
            if (cart == null)
            {
                cart = new Cart { customer_id = customerId, is_active = true, created_at = DateTime.Now };
                await _cartDal.AddAsync(cart);
            }
            return cart;
        }

        public async Task<(HttpStatusCode, Result)> AddItem(CartItemRequestDto dto)
        {
            // Açıklayıcı yorum: Adet sınırı - negatif/sıfır/aşırı engeli (CheckStock tek başına negatifi yakalamaz)
            if (dto.quantity < 1 || dto.quantity > 100)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CartInvalidQuantity));

            var product = await _productDal.GetAsync(p => p.id == dto.product_id && p.is_active);
            if (product == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.ProductNotFound));

            // Açıklayıcı yorum: Stok kontrolü (sepete eklenen adet mevcut mu)
            // BEDEN NORMALIZASYONU (H48): bastaki/sondaki bosluk ayri bir deger yaratir (" M" != "M") ->
            // stok satiri bulunamaz, musteri "stok yok" gorur; ayrica ayni beden icin MUKERRER sepet satiri olusabilir.
            // Trim guvenli (mevcut veriyi bozmaz); harf buyuk/kucuk normalizasyonu VERI GOCU ister -> Omer'in parcasi.
            dto.size = (dto.size ?? string.Empty).Trim();
            if (!await _stockService.CheckStock(dto.product_id, dto.size, dto.quantity))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.StockInsufficient));

            var cart = await GetOrCreateCartAsync(dto.customer_id);
            // Açıklayıcı yorum: Aynı ürün+beden varsa adet güncelle, yoksa ekle
            var existing = await _cartItemDal.GetAsync(i => i.cart_id == cart.id && i.product_id == dto.product_id && i.size == dto.size && i.is_active);
            if (existing != null)
            {
                existing.quantity = dto.quantity;
                existing.updated_at = DateTime.Now;
                await _cartItemDal.UpdateAsync(existing);
            }
            else
            {
                try
                {
                    await _cartItemDal.AddAsync(new CartItem
                    {
                        cart_id = cart.id,
                        product_id = dto.product_id,
                        size = dto.size,
                        quantity = dto.quantity,
                        is_active = true,
                        created_at = DateTime.Now
                    });
                }
                catch
                {
                    // Concurrency: eszamanlı AYNI urun+beden eklendi -> filtered-unique ihlali. Kazananin kalemini
                    // bul ve miktari guncelle (graceful - race loser hata almaz, CIFT kalem olusmaz).
                    var raced = await _cartItemDal.GetAsync(i => i.cart_id == cart.id && i.product_id == dto.product_id && i.size == dto.size && i.is_active);
                    if (raced != null)
                    {
                        raced.quantity = dto.quantity;
                        raced.updated_at = DateTime.Now;
                        await _cartItemDal.UpdateAsync(raced);
                    }
                }
            }
            return (HttpStatusCode.OK, new SuccessResult(Messages.CartItemAdded));
        }

        public async Task<(HttpStatusCode, Result)> RemoveItem(int customerId, int productId, string size)
        {
            var cart = await _cartDal.GetAsync(c => c.customer_id == customerId && c.is_active);
            if (cart == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.CartNotFound));
            var item = await _cartItemDal.GetAsync(i => i.cart_id == cart.id && i.product_id == productId && i.size == size && i.is_active);
            if (item != null) { item.is_active = false; await _cartItemDal.UpdateAsync(item); }
            return (HttpStatusCode.OK, new SuccessResult(Messages.CartItemRemoved));
        }

        public async Task<(HttpStatusCode, Result)> GetCart(int customerId)
        {
            var cart = await _cartDal.GetAsync(c => c.customer_id == customerId && c.is_active);
            var response = new CartResponseDto { cart_id = cart?.id ?? 0 };
            if (cart != null)
            {
                var items = await _cartItemDal.GetListAsync(i => i.cart_id == cart.id && i.is_active);
                // N+1 duzeltmesi: tum urunleri tek sorguda getir
                var cartIds = items.Select(i => i.product_id).Distinct().ToList();
                var cartProducts = (await _productDal.GetListAsync(p => cartIds.Contains(p.id) && p.is_active)).ToDictionary(p => p.id);
                foreach (var item in items)
                {
                    if (!cartProducts.TryGetValue(item.product_id, out var product)) continue;
                    // Açıklayıcı yorum: SEPET FİYAT TUTARLILIĞI - indirim (flash sale) aktifse indirimli fiyat göster.
                    // Aksi halde sepet önizlemesi tam fiyat, checkout indirimli (PlaceOrder EffectivePrice) -> tutarsızlık.
                    var effectivePrice = PricingHelper.EffectivePrice(product.price, product.sale_price,
                        product.sale_start, product.sale_end, DateTime.Now);
                    response.items.Add(new CartLineDto
                    {
                        product_id = product.id,
                        product_name = product.name,
                        size = item.size,
                        quantity = item.quantity,
                        unit_price = effectivePrice,
                        line_total = effectivePrice * item.quantity
                    });
                }
                response.subtotal = response.items.Sum(i => i.line_total);
            }
            return (HttpStatusCode.OK, new SuccessDataResult<CartResponseDto>(response, Messages.CartListed));
        }

        public async Task<(HttpStatusCode, Result)> ClearCart(int customerId)
        {
            var cart = await _cartDal.GetAsync(c => c.customer_id == customerId && c.is_active);
            if (cart != null)
            {
                var items = await _cartItemDal.GetListAsync(i => i.cart_id == cart.id && i.is_active);
                foreach (var item in items) { item.is_active = false; await _cartItemDal.UpdateAsync(item); }
            }
            return (HttpStatusCode.OK, new SuccessResult(Messages.CartCleared));
        }
        // Açıklayıcı yorum: Kaydet-sonra-al - sepet kaleminden çıkar, favorilere ekle (idempotent)
        public async Task<(HttpStatusCode, Result)> SaveForLater(int customerId, int productId, string size)
        {
            var cart = await _cartDal.GetAsync(c => c.customer_id == customerId && c.is_active);
            if (cart == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.CartNotFound));

            var item = await _cartItemDal.GetAsync(i => i.cart_id == cart.id && i.product_id == productId && i.size == size && i.is_active);
            if (item == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.CartItemNotFound));

            // Açıklayıcı yorum: Sepetten çıkar
            item.is_active = false;
            await _cartItemDal.UpdateAsync(item);

            // Açıklayıcı yorum: Favorilerde yoksa ekle (WishlistItem beden tutmaz - ürün bazlı)
            // DERLEME FIX (H44): WishlistItem entity'sinde is_active alanı YOK (id/customer_id/product_id/created_at) -> CS1061.
            // Tasarım HARD-DELETE: (customer,product) UNIQUE index var; soft-delete olsaydı silinen favori TEKRAR EKLENEMEZDI.
            var existing = await _wishlistItemDal.GetAsync(w => w.customer_id == customerId && w.product_id == productId);
            if (existing == null)
            {
                await _wishlistItemDal.AddAsync(new Divisima.Entity.Entities.WishlistItem
                {
                    customer_id = customerId,
                    product_id = productId,
                    created_at = DateTime.Now
                });
            }
            return (HttpStatusCode.OK, new SuccessResult(Messages.CartSavedForLater));
        }

        // Açıklayıcı yorum: Sonra-al listesinden sepete geri taşı (stok kontrollü)
        public async Task<(HttpStatusCode, Result)> MoveToCart(int customerId, int productId, string size)
        {
            var wishItem = await _wishlistItemDal.GetAsync(w => w.customer_id == customerId && w.product_id == productId);
            if (wishItem == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.WishlistItemNotFound));

            // Açıklayıcı yorum: Sepete ekle - mevcut AddItem stok kontrolünü yeniden kullan
            var addResult = await AddItem(new Divisima.Entity.Dtos.Cart.CartItemRequestDto
            {
                customer_id = customerId,
                product_id = productId,
                size = size,
                quantity = 1
            });
            if (addResult.Item1 != HttpStatusCode.OK && addResult.Item1 != HttpStatusCode.Created)
                return addResult; // stok yok vb.

            // Açıklayıcı yorum: Favorilerden çıkar (HARD DELETE - entity'de is_active yok; unique index tekrar eklemeyi
            // engellememesi için de kaydın tamamen silinmesi doğru davranış).
            await _wishlistItemDal.DeleteAsync(wishItem);
            return (HttpStatusCode.OK, new SuccessResult(Messages.CartMovedToCart));
        }

    }
}
