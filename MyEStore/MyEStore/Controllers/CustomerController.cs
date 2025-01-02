using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyEStore.Entities;
using MyEStore.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.CodeAnalysis.Scripting;
using System.Net.Mail;
using System.Net;

namespace MyEStore.Controllers
{
    public class CustomerController : Controller
    {
        private readonly MyeStoreContext _ctx;
        public CustomerController(MyeStoreContext ctx)
        {
            _ctx = ctx;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginVM model, string? ReturnUrl)
        {
            ViewBag.ReturnUrl = ReturnUrl;

            // Tìm người dùng trong cơ sở dữ liệu
            var kh = _ctx.KhachHangs.SingleOrDefault(p => p.MaKh == model.UserName);
            if (kh == null || !BCrypt.Net.BCrypt.Verify(model.Password, kh.MatKhau))
            {
                ViewBag.ThongBao = "Sai thông tin đăng nhập";
                return View();
            }

            // Tạo các claims cho user
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, kh.HoTen),
        new Claim(ClaimTypes.Email, kh.Email),
        new Claim("UserId", kh.MaKh),
        new Claim(ClaimTypes.Role, "Administrator") // Tùy thuộc vào vai trò trong DB
    };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimPrincipal = new ClaimsPrincipal(claimsIdentity);

            // Đăng nhập bằng cookie authentication
            await HttpContext.SignInAsync(claimPrincipal);

            // Điều hướng đến ReturnUrl nếu có, nếu không chuyển đến trang khác
            if (!string.IsNullOrEmpty(ReturnUrl))
            {
                return Redirect(ReturnUrl);
            }

            return RedirectToAction("Index", "Cart");
        }



        [Authorize]
        public IActionResult PurchaseOrder()
        {
            return View();
        }

        [Authorize(Roles ="Accountant")]
        public IActionResult Statistics()
        {
            return View();
        }

        [HttpGet("/Forbidden")]
        public IActionResult Forbidden()
        {
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return Redirect("/");
        }

        // GET: Register
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check if username or email already exists
            if (_ctx.KhachHangs.Any(kh => kh.MaKh == model.UserName || kh.Email == model.Email))
            {
                ViewBag.ThongBao = "Username or Email already exists.";
                return View(model);
            }

            // Hash the password before saving it
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);

            // Create new customer
            var newCustomer = new KhachHang
            {
                MaKh = model.UserName,
                MatKhau = hashedPassword,  // Store the hashed password
                HoTen = model.FullName,
                Email = model.Email,
                // Thêm các trường khác nếu cần
            };

            _ctx.KhachHangs.Add(newCustomer);
            _ctx.SaveChanges();

            TempData["ThongBao"] = "Account successfully created! Please log in.";
            return RedirectToAction("Login");
        }
        // GET: Profile
        [HttpGet]
        public IActionResult Profile()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login");
            }

            var customer = _ctx.KhachHangs.SingleOrDefault(kh => kh.MaKh == userId);

            if (customer == null)
            {
                return NotFound("Không tìm thấy thông tin người dùng.");
            }

            var model = new ProfileVM
            {
                FullName = customer.HoTen,
                Email = customer.Email
            };

            return View(model);
        }

        // POST: Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Profile(ProfileVM model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.";
                return View(model);
            }

            var userId = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login");
            }

            var customer = _ctx.KhachHangs.SingleOrDefault(kh => kh.MaKh == userId);

            if (customer == null)
            {
                TempData["Error"] = "Không tìm thấy thông tin người dùng.";
                return View(model);
            }

            if (_ctx.KhachHangs.Any(kh => kh.Email == model.Email && kh.MaKh != userId))
            {
                TempData["Error"] = "Email đã được sử dụng bởi người dùng khác.";
                return View(model);
            }

            customer.HoTen = model.FullName;
            customer.Email = model.Email;

            _ctx.SaveChanges();

            TempData["Success"] = "Thông tin của bạn đã được cập nhật thành công.";
            return RedirectToAction("Profile");
        }


        // POST: Change Password
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(string Password, string NewPassword, string ConfirmPassword)
        {
            if (string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(NewPassword) || string.IsNullOrEmpty(ConfirmPassword))
            {
                TempData["Error"] = "Vui lòng điền đủ thông tin.";
                return RedirectToAction("Profile");
            }

            var userId = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;

            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var customer = _ctx.KhachHangs.SingleOrDefault(kh => kh.MaKh == userId);

            if (customer == null)
            {
                return NotFound("Customer not found.");
            }

            // Validate current password using Bcrypt
            if (!BCrypt.Net.BCrypt.Verify(Password, customer.MatKhau))
            {
                TempData["Error"] = "Mật khẩu hiện tại không chính xác.";
                return RedirectToAction("Profile");
            }

            // Validate new password and confirmation
            if (NewPassword != ConfirmPassword)
            {
                TempData["Error"] = "Mật khẩu mới và mật khẩu xác nhận không khớp.";
                return RedirectToAction("Profile");
            }

            // Hash new password using Bcrypt
            customer.MatKhau = BCrypt.Net.BCrypt.HashPassword(NewPassword);
            _ctx.SaveChanges();

            TempData["Success"] = "Mật khẩu của bạn đã được thay đổi thành công.";
            return RedirectToAction("Profile");
        }


        // Hiển thị lịch sử giao dịch của khách hàng
        public IActionResult TransactionHistory()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;

            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var orders = _ctx.HoaDons
                .Where(hd => hd.MaKh == userId)
                .Include(hd => hd.ChiTietHds) // Đảm bảo bạn đang nạp ChiTietHds cùng với HoaDons
                .OrderByDescending(hd => hd.NgayDat)
                .ToList();

            return View(orders);
        }


        // Hiển thị chi tiết hóa đơn và các sản phẩm đã mua
        [Authorize]
        public IActionResult OrderDetails(int id)
        {
            var order = _ctx.HoaDons
                .Where(hd => hd.MaHd == id)
                .Select(hd => new
                {
                    hd.MaHd,
                    hd.NgayDat,
                    hd.HoTen,
                    hd.DiaChi,
                    hd.CachThanhToan,
                    hd.PhiVanChuyen,
                    Products = hd.ChiTietHds.Select(ct => new
                    {
                        ct.MaHh,
                        ct.SoLuong,
                        ct.DonGia,
                        ProductName = ct.MaHhNavigation.TenHh,
                        Hinh = ct.MaHhNavigation.Hinh // Lấy thông tin hình ảnh từ bảng HangHoa
                    }).ToList()
                }).FirstOrDefault();

            if (order == null)
            {
                return NotFound("Order not found.");
            }

            return View(order);
        }

        #region ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            var khachHang = await _ctx.KhachHangs.SingleOrDefaultAsync(kh => kh.Email == email);
            if (khachHang == null)
            {
                ViewBag.ErrorMessage = "Email không tồn tại.";
                return View();
            }

            var newPassword = GenerateRandomPassword();
            khachHang.MatKhau = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _ctx.SaveChangesAsync();

            string message = $@"
        <p>Xin chào: {khachHang.HoTen},</p>
        <p>Mật khẩu mới của bạn là: <strong>{newPassword}</strong></p>
        <p>Vui lòng đăng nhập lại và đổi mật khẩu mới để đảm bảo an toàn.</p>";

            await SendEmail(khachHang.Email, "Mật khẩu mới của bạn", message);

            ViewBag.SuccessMessage = "Mật khẩu mới đã được gửi đến email của bạn.";
            return View();
        }

        private string GenerateRandomPassword(int length = 8)
        {
            const string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            var random = new Random();
            return new string(Enumerable.Repeat(validChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private async Task SendEmail(string toEmail, string subject, string message)
        {
            var client = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential("truongminhduc4002@gmail.com", "hocekpuhklqvkniu"),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress("truongminhduc4002@gmail.com"),
                Subject = subject,
                Body = message,
                IsBodyHtml = true
            };
            mailMessage.To.Add(toEmail);

            try
            {
                await client.SendMailAsync(mailMessage);
            }
            catch (SmtpException ex)
            {
                // Xử lý lỗi gửi email
                Console.WriteLine($"SMTP Exception: {ex.Message}");
                throw;
            }
        }

        #endregion

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateAddress(string hoTen, string soDienThoai, string diaChi)
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;

            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var customer = _ctx.KhachHangs.SingleOrDefault(kh => kh.MaKh == userId);

            if (customer == null)
            {
                return NotFound("Customer not found.");
            }

            try
            {
                // Cập nhật thông tin
                customer.HoTen = hoTen;
                customer.DienThoai = soDienThoai;
                customer.DiaChi = diaChi;

                _ctx.SaveChanges();

                TempData["Success"] = "Cập nhật thành công thông tin nhận hàng.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Đã xảy ra lỗi khi cập nhật thông tin: " + ex.Message;
            }

            return RedirectToAction("Address");
        }

        [HttpGet]
        public IActionResult Address()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;

            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var customer = _ctx.KhachHangs.SingleOrDefault(kh => kh.MaKh == userId);

            if (customer == null)
            {
                return NotFound("Customer not found.");
            }

            // Truyền thông tin khách hàng vào View
            return View(customer);
        }
        // Cập nhật phương thức trong Controller - OrderDetails
       public IActionResult Thongbao()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;

            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            // Lấy ngày hôm nay (bỏ phần thời gian)
            var today = DateTime.Today;

            var orders = _ctx.HoaDons
                .Where(hd => hd.MaKh == userId && hd.NgayDat.Date >= today)
                .Include(hd => hd.ChiTietHds)
                .OrderByDescending(hd => hd.NgayDat)
                .Select(hd => new
                {
                    hd.MaHd,
                    hd.NgayDat,
                    hd.NgayGiao,
                    DaysToDelivery = hd.NgayGiao.HasValue ? (hd.NgayGiao.Value - DateTime.Now).Days : (int?)null
                })
                .ToList();

            var model = orders.Select(o => new Thongbao
            {
                MaHd = o.MaHd,
                NgayDat = o.NgayDat,
                NgayGiao = o.NgayGiao,
                DaysToDelivery = o.DaysToDelivery
            }).ToList();

            // Lấy số lượng đơn hàng sắp giao trong vòng 7 ngày tới
            var upcomingDeliveriesCount = orders.Count(o => o.DaysToDelivery.HasValue && o.DaysToDelivery.Value <= 99);
            // Truyền số lượng thông báo vào View
            ViewData["UpcomingDeliveriesCount"] = upcomingDeliveriesCount;

            return View(model);
        }

    }
}
