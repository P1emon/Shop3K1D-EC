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

            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var customer = _ctx.KhachHangs.SingleOrDefault(kh => kh.MaKh == userId);

            if (customer == null)
            {
                return NotFound("Customer not found.");
            }

            var model = new ProfileVM
            {
                UserName = customer.MaKh,
                FullName = customer.HoTen,
                Email = customer.Email,
                Phone = customer.DienThoai,
                Password = customer.MatKhau
            };

            return View(model);
        }

        // POST: Update Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Profile(ProfileVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
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

            // Update customer information
            customer.HoTen = model.FullName;
            customer.Email = model.Email;
            customer.DienThoai = model.Phone;
            customer.MatKhau = model.Password;

            _ctx.SaveChanges();

            TempData["Success"] = "Your profile has been updated successfully.";
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

    }
}
