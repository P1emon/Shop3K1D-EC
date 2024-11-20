using Microsoft.AspNetCore.Mvc;
using MyEStore.Entities;
using MyEStore.Models;

namespace MyEStore.Controllers
{
	public class CartController : Controller
	{
		const string CART_KEY = "MY_CART";
		private readonly MyeStoreContext _ctx;

		public List<CartItem> CartItems
		{
			get
			{
				var carts = HttpContext.Session.Get<List<CartItem>>(CART_KEY);
				if (carts == null)
				{
					carts = new List<CartItem>();
				}
				return carts;
			}
		}

		public CartController(MyeStoreContext ctx)
		{
			_ctx = ctx;
		}

		public IActionResult Index()
		{
			return View(CartItems);
		}

        [HttpGet]
        public IActionResult GetCartCount()
        {
            var cartCount = CartItems.Sum(item => item.SoLuong);
            return Json(cartCount);
        }
        public IActionResult AddToCart(int id, int qty = 1)
		{
			var cart = CartItems;
			var cartItem = cart.SingleOrDefault(p => p.MaHh == id);
			if (cartItem != null)
			{
				cartItem.SoLuong += qty;
			}
			else
			{
				var hangHoa = _ctx.HangHoas.SingleOrDefault(h => h.MaHh == id);
				if (hangHoa == null)
				{
					//không có trong database
					TempData["ThongBao"] = $"Không tìm thấy hàng hóa có mã {id}";
					return RedirectToAction("Index", "Products");
				}
				cartItem = new CartItem
				{
					MaHh = id,
					SoLuong = qty,
					TenHh = hangHoa.TenHh,
					Hinh = hangHoa.Hinh,
					DonGia = hangHoa.DonGia ?? 0
				};
				cart.Add(cartItem);
			}
			HttpContext.Session.Set(CART_KEY, cart);
			return RedirectToAction("Index");
		}

		public IActionResult RemoveCartItem(int id)
		{
            var cart = CartItems;
            var cartItem = cart.SingleOrDefault(p => p.MaHh == id);
            if (cartItem != null)
            {
				cart.Remove(cartItem);
				HttpContext.Session.Set(CART_KEY, cart);
            }
			return RedirectToAction("Index");
        }

    }
}
