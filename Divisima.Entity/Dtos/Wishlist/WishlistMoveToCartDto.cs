using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Wishlist
{
    public class WishlistMoveToCartDto : IDto
    {
        public int product_id { get; set; }
        public string size { get; set; }
        public int quantity { get; set; }
    }
}
