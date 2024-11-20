using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyEStore.Entities;
using MyEStore.Models;
using System.Security.Claims;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MyEStore.Controllers
{
	[Authorize]
	public class PaymentController : Controller
	{
		private readonly PaypalClient _paypalClient;
		private readonly MyeStoreContext _ctx;

		public PaymentController(PaypalClient paypalClient, MyeStoreContext ctx)
		{
			_paypalClient = paypalClient;
			_ctx = ctx;
		}

		public IActionResult Index()
		{
			ViewBag.PaypalClientId = _paypalClient.ClientId;
			return View(CartItems);
		}

		const string CART_KEY = "MY_CART";
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

		[HttpPost]
		public async Task<IActionResult> PaypalOrder(CancellationToken cancellationToken)
		{
			// Tạo đơn hàng (thông tin lấy từ Session???)
			var tongTien = CartItems.Sum(p => p.ThanhTien).ToString();
			var donViTienTe = "USD";
			// OrderId - mã tham chiếu (duy nhất)
			var orderIdref = "DH" + DateTime.Now.Ticks.ToString();

			try
			{
				// a. Create paypal order
				var response = await _paypalClient.CreateOrder(tongTien, donViTienTe, orderIdref);

				return Ok(response);
			}
			catch (Exception e)
			{
				var error = new
				{
					e.GetBaseException().Message
				};

				return BadRequest(error);
			}
		}

		public async Task<IActionResult> PaypalCapture(string orderId, CancellationToken cancellationToken)
		{
			try
			{
				var response = await _paypalClient.CaptureOrder(orderId);

				//nhớ kiểm tra status complete
				if (response.status == "COMPLETED")
				{
					var reference = response.purchase_units[0].reference_id;//mã đơn hàng mình tạo ở trên

					// Put your logic to save the transaction here
					// You can use the "reference" variable as a transaction key
					// 1. Tạo và Lưu đơn hàng vô database
					// TransactionId của Seller: response.payments.captures[0].id
					var hoaDon = new HoaDon
					{
						MaKh = User.FindFirstValue("UserId"),
						NgayDat = DateTime.Now,
						HoTen = User.Identity.Name,
						DiaChi = "N/A",//tự update
						CachThanhToan = "Paypal",
						CachVanChuyen = "N/A",
						MaTrangThai = 0, //Mới đặt hàng
						GhiChu = $"reference_id={reference}, transactionId={response.purchase_units[0].payments.captures[0].id}"
					};
					_ctx.Add(hoaDon);
					_ctx.SaveChanges();
					foreach (var item in CartItems)
					{
						var cthd = new ChiTietHd
						{
							MaHd = hoaDon.MaHd,
							MaHh = item.MaHh,
							DonGia = item.DonGia,
							SoLuong = item.SoLuong,
							GiamGia = 1
						};
						_ctx.Add(cthd);
					}
					_ctx.SaveChanges();
					//2. Xóa session giỏ hàng
					HttpContext.Session.Set(CART_KEY, new List<CartItem>());

					return Ok(response);
				}
				else
				{
					return BadRequest(new { Message = "Có lỗi thanh toán" });
				}
			}
			catch (Exception e)
			{
				var error = new
				{
					e.GetBaseException().Message
				};

				return BadRequest(error);
			}
		}

		public IActionResult Success()
		{
			return View();
		}
	}
}
