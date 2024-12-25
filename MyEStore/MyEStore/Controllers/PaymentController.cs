using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyEStore.Entities;
using MyEStore.Helpers;
using MyEStore.Models;
using MyEStore.Servicess;
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
        private readonly IVnpayService _vnpayService;

        public PaymentController(PaypalClient paypalClient, MyeStoreContext ctx, IConfiguration configuration, IVnpayService vnpayService)
        {
            _paypalClient = paypalClient;
            _ctx = ctx;
            _configuration = configuration;
            _vnpayService = vnpayService;
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


        public async Task<IActionResult> PaypalCapture(string orderId, string ngayGiao, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _paypalClient.CaptureOrder(orderId);

                if (response.status == "COMPLETED")
                {
                    var reference = response.purchase_units[0].reference_id;
                    var transactionId = response.purchase_units[0].payments.captures[0].id;

                    var userId = User.FindFirstValue("UserId");
                    var userProfile = _ctx.KhachHangs.FirstOrDefault(u => u.MaKh == userId);
                    string customerAddress = userProfile?.DiaChi ?? "N/A";

                    DateTime? ngayGiaoDate = null;

                    if (DateTime.TryParse(ngayGiao, out DateTime parsedDate)) // Kiểm tra ngày giao hợp lệ
                    {
                        ngayGiaoDate = parsedDate;
                    }

                    var hoaDon = new HoaDon
                    {
                        MaKh = userId,
                        NgayDat = DateTime.Now,
                        HoTen = User.Identity.Name,
                        DiaChi = customerAddress,
                        CachThanhToan = "Paypal",
                        CachVanChuyen = "N/A",
                        MaTrangThai = 0,
                        NgayGiao = ngayGiaoDate,
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




        [HttpPost]
        public IActionResult VnpayOrder(string ngayGiao)
        {
            // Tính tổng tiền (VND)
            var tongTien = CartItems.Sum(p => p.ThanhTien);

            // Lấy userId và thông tin khách hàng từ cơ sở dữ liệu
            var userId = User.FindFirstValue("UserId");
            var userProfile = _ctx.KhachHangs.FirstOrDefault(u => u.MaKh == userId);

            // Lấy địa chỉ của khách hàng, nếu không có thì dùng "N/A"
            string customerAddress = userProfile?.DiaChi ?? "N/A";

            // Kiểm tra ngày giao hợp lệ
            DateTime? ngayGiaoDate = null;
            if (DateTime.TryParse(ngayGiao, out DateTime parsedDate))
            {
                ngayGiaoDate = parsedDate;
            }

            // Tạo model yêu cầu thanh toán
            var paymentRequest = new VnPaymentRequestModel
            {
                Amount = tongTien,
                OrderId = Guid.NewGuid().ToString().GetHashCode(), // Convert string to int
                CreatedDate = DateTime.Now
            };

            try
            {
                // Gọi dịch vụ để tạo URL thanh toán
                var paymentUrl = _vnpayService.CreatePaymentUrl(HttpContext, paymentRequest);

                // Lưu thông tin hóa đơn vào database
                var hoaDon = new HoaDon
                {
                    MaKh = userId ?? string.Empty, // Handle possible null reference
                    NgayDat = DateTime.Now,
                    HoTen = User.Identity?.Name ?? string.Empty, // Handle possible null reference
                    DiaChi = customerAddress, // Địa chỉ của khách hàng
                    CachThanhToan = "VNPay",
                    CachVanChuyen = "N/A",
                    MaTrangThai = 0,
                    NgayGiao = ngayGiaoDate, // Thêm ngày giao
                    GhiChu = "Đang chờ thanh toán VNPay"
                };
                _ctx.Add(hoaDon);
                _ctx.SaveChanges();

                // Lưu chi tiết hóa đơn (CartItems)
                foreach (var item in CartItems)
                {
                    var cthd = new ChiTietHd
                    {
                        MaHd = hoaDon.MaHd,
                        MaHh = item.MaHh,
                        DonGia = item.DonGia,
                        SoLuong = item.SoLuong,
                        GiamGia = 1 // Tùy chỉnh giảm giá nếu cần
                    };
                    _ctx.Add(cthd);
                }
                _ctx.SaveChanges(); // Lưu toàn bộ chi tiết hóa đơn vào database

                // Xóa giỏ hàng sau khi tạo đơn hàng
                HttpContext.Session.Set(CART_KEY, new List<CartItem>());

                return Redirect(paymentUrl); // Chuyển hướng đến VNPay
            }
            catch (Exception ex)
            {
                // Ghi log lỗi và trả về lỗi
                ViewBag.Message = "Lỗi tạo yêu cầu thanh toán: " + ex.Message;
                return View("MomoFail");
            }
        }



        public async Task<IActionResult> Success()
        {
            return View();
        }

        public IActionResult VnpayFail()
        {
            return View();
        }
        [HttpGet]
        [HttpPost]
        [Route("Payment/VnpayCallback")]
        public IActionResult VnpayCallback()
        {
            try
            {
                // Lấy phản hồi từ VNPay
                var response = _vnpayService.PaymentExcute(Request.Query);

                if (response.Success)
                {
                    // Kiểm tra hóa đơn đã tồn tại dựa trên TransactionId
                    var existingOrder = _ctx.HoaDons.FirstOrDefault(h => h.GhiChu.Contains($"TransactionId={response.TransactionId}"));

                    if (existingOrder != null)
                    {
                        // Hóa đơn đã được xử lý trước đó, chỉ hiển thị thông báo thành công
                        ViewBag.Message = "Hóa đơn đã được xử lý thành công!";
                        return View("Success");
                    }

                    // Tìm hóa đơn đang chờ thanh toán dựa trên OrderId
                    var pendingOrder = _ctx.HoaDons.FirstOrDefault(h => h.GhiChu.Contains($"Đang chờ thanh toán VNPay") && h.MaTrangThai == 0);

                    if (pendingOrder != null)
                    {
                        // Cập nhật trạng thái hóa đơn
                        pendingOrder.MaTrangThai = 1; // Hoàn tất
                        pendingOrder.GhiChu += $", TransactionId={response.TransactionId}";
                        _ctx.SaveChanges();

                        // Xóa giỏ hàng sau khi thanh toán thành công
                        HttpContext.Session.Set(CART_KEY, new List<CartItem>());

                        ViewBag.Message = "Thanh toán thành công!";
                        return View("Success");
                    }
                    else
                    {
                        // Nếu không tìm thấy hóa đơn đang chờ, tạo mới
                        var hoaDon = new HoaDon
                        {
                            MaKh = User.FindFirstValue("UserId"),
                            NgayDat = DateTime.Now,
                            HoTen = User.Identity?.Name ?? string.Empty,
                            DiaChi = "N/A",
                            CachThanhToan = response.PaymentMethod,
                            CachVanChuyen = "N/A",
                            MaTrangThai = 1, // Hoàn tất
                            GhiChu = $"TransactionId={response.TransactionId}, OrderId={response.OrderId}"
                        };
                        _ctx.Add(hoaDon);
                        _ctx.SaveChanges();

                        ViewBag.Message = "Thanh toán thành công!";
                        return View("Success");
                    }
                }
                else
                {
                    // Thanh toán thất bại
                    ViewBag.Message = $"Thanh toán thất bại: {response.Message}";
                    return View("VnpayFail");
                }
            }
            catch (Exception ex)
            {
                // Xử lý ngoại lệ
                ViewBag.Message = "Có lỗi xảy ra khi xử lý thanh toán: " + ex.Message;
                return View("MomoFail");
            }
        }
    }
}
