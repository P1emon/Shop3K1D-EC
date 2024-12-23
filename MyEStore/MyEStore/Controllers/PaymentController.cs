using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyEStore.Entities;
using MyEStore.Models;
using Newtonsoft.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MyEStore.Controllers
{
	[Authorize]
	public class PaymentController : Controller
	{
		private readonly PaypalClient _paypalClient;
		private readonly MyeStoreContext _ctx;
        private readonly IConfiguration _configuration;

        public PaymentController(PaypalClient paypalClient, MyeStoreContext ctx, IConfiguration configuration)
        {
            _paypalClient = paypalClient;
            _ctx = ctx;
            _configuration = configuration;
        }

        [HttpPost]
        public IActionResult MomoPayment()
        {
            // Lấy thông tin từ cấu hình
            var endpoint = _configuration["MoMo:Endpoint"];
            var partnerCode = _configuration["MoMo:PartnerCode"];
            var accessKey = _configuration["MoMo:AccessKey"];
            var secretKey = _configuration["MoMo:SecretKey"];
            var returnUrl = _configuration["MoMo:ReturnUrl"];
            var notifyUrl = _configuration["MoMo:NotifyUrl"];

            // Tính tổng tiền (VND)
            var tongTien = CartItems.Sum(p => p.ThanhTien);

            // Tạo các tham số thanh toán
            var orderInfo = "Thanh toán đơn hàng tại MyEStore";
            var requestId = Guid.NewGuid().ToString();
            var orderId = "DH" + DateTime.Now.Ticks.ToString();
            var extraData = "";

            // Tạo chữ ký (signature)
            string rawHash = $"accessKey={accessKey}&amount={tongTien}&extraData={extraData}&ipnUrl={notifyUrl}&orderId={orderId}&orderInfo={orderInfo}&partnerCode={partnerCode}&redirectUrl={returnUrl}&requestId={requestId}&requestType=captureWallet";
            string signature = GenerateSignature(rawHash, secretKey);

            // Tạo body yêu cầu
            var body = new
            {
                partnerCode = partnerCode,
                accessKey = accessKey,
                requestId = requestId,
                amount = tongTien.ToString(),
                orderId = orderId,
                orderInfo = orderInfo,
                redirectUrl = returnUrl,
                ipnUrl = notifyUrl,
                extraData = extraData,
                requestType = "captureWallet",
                signature = signature
            };

            using (var client = new HttpClient())
            {
                var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                var response = client.PostAsync(endpoint, content).Result;

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = response.Content.ReadAsStringAsync().Result;
                    dynamic jsonResponse = JsonConvert.DeserializeObject(responseContent);
                    string payUrl = jsonResponse.payUrl;

                    return Redirect(payUrl); // Chuyển hướng đến MoMo để thanh toán
                }
                else
                {
                    return BadRequest("Không thể tạo yêu cầu thanh toán MoMo.");
                }
            }
        }
        public IActionResult MomoReturn()
        {
            var queryString = Request.Query;
            string resultCode = queryString["resultCode"];
            string orderId = queryString["orderId"];
            string message = queryString["message"];
            string transId = queryString["transId"];
            string amount = queryString["amount"];

            if (resultCode == "0")
            {
                try
                {
                    // Tạo hóa đơn mới
                    var hoaDon = new HoaDon
                    {
                        MaKh = User.FindFirstValue("UserId"),
                        NgayDat = DateTime.Now,
                        HoTen = User.Identity.Name,
                        DiaChi = "N/A",
                        CachThanhToan = "MoMo",
                        CachVanChuyen = "N/A",
                        MaTrangThai = 0,
                        GhiChu = $"transId={transId}, amount={amount}"
                    };
                    _ctx.Add(hoaDon);
                    _ctx.SaveChanges();

                    // Lưu chi tiết hóa đơn
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

                    // Xóa giỏ hàng
                    HttpContext.Session.Set(CART_KEY, new List<CartItem>());

                    // Gửi dữ liệu qua TempData
                    TempData["TransactionId"] = transId;
                    TempData["OrderId"] = orderId;

                    ViewBag.Message = "Thanh toán thành công!";
                    ViewBag.OrderId = orderId;
                    return View("Success");
                }
                catch (Exception e)
                {
                    ViewBag.Message = "Có lỗi khi lưu thông tin hóa đơn: " + e.Message;
                    return View("MomoFail");
                }
            }
            else
            {
                ViewBag.Message = "Thanh toán thất bại: " + message;
                ViewBag.OrderId = orderId;
                return View("MomoFail");
            }
        }


        private static string GenerateSignature(string data, string secretKey)
        {
            var encoding = Encoding.UTF8;
            using (var hmacsha256 = new HMACSHA256(encoding.GetBytes(secretKey)))
            {
                byte[] hashmessage = hmacsha256.ComputeHash(encoding.GetBytes(data));
                return BitConverter.ToString(hashmessage).Replace("-", "").ToLower();
            }
        }


        [HttpPost]
        public IActionResult MomoNotify()
        {
            using (var reader = new StreamReader(Request.Body))
            {
                var body = reader.ReadToEnd();
                dynamic jsonBody = JsonConvert.DeserializeObject(body);

                if (jsonBody.resultCode == "0")
                {
                    // Xử lý đơn hàng thành công
                    return Ok();
                }
                else
                {
                    // Xử lý thất bại
                    return BadRequest();
                }
            }
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
        [HttpPost]
        public async Task<IActionResult> PaypalOrder(CancellationToken cancellationToken)
        {
            // Tính tổng tiền bằng VND
            var tongTienVND = CartItems.Sum(p => p.ThanhTien);

            // Chuyển đổi VND sang USD
            const decimal conversionRate = 25400; // 1 USD = 25,000 VND
            var tongTienUSD = tongTienVND / (double)conversionRate;

            // Đảm bảo số tiền có 2 chữ số thập phân
            var tongTien = tongTienUSD.ToString("F2");

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

                if (response.status == "COMPLETED")
                {
                    var reference = response.purchase_units[0].reference_id;
                    var transactionId = response.purchase_units[0].payments.captures[0].id;

                    var hoaDon = new HoaDon
                    {
                        MaKh = User.FindFirstValue("UserId"),
                        NgayDat = DateTime.Now,
                        HoTen = User.Identity.Name,
                        DiaChi = "N/A",
                        CachThanhToan = "Paypal",
                        CachVanChuyen = "N/A",
                        MaTrangThai = 0,
                        GhiChu = $"reference_id={reference}, transactionId={transactionId}"
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

                    HttpContext.Session.Set(CART_KEY, new List<CartItem>());

                    // Gửi dữ liệu qua TempData
                    TempData["TransactionId"] = transactionId;
                    TempData["ReferenceId"] = reference;

                    return RedirectToAction("Success");
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

        public async Task<IActionResult> Success()
        {
            return View();
        }

    }
}
