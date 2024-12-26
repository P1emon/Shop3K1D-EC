using Microsoft.AspNetCore.Mvc;
using MyEStore.Entities;
using System.Linq;
using MyEStore.Helpers;
using MyEStore.Models;

namespace MyEStore.Controllers
{
    public class ProductsController : Controller
    {
        private readonly MyeStoreContext _ctx;

        public ProductsController(MyeStoreContext ctx)
        {
            _ctx = ctx;
        }

        [HttpGet("san-pham/{categorySlug}/{productSlug}")]
        public IActionResult Detail(string categorySlug, string productSlug)
        {
            // Lấy sản phẩm từ slug
            var hangHoa = _ctx.HangHoas.SingleOrDefault(p => p.TenAlias == productSlug);
            if (hangHoa == null)
            {
                return NotFound();
            }

            // Kiểm tra slug của danh mục để đảm bảo URL đúng
            var loai = _ctx.Loais.SingleOrDefault(l => l.MaLoai == hangHoa.MaLoai);
            if (loai == null)
            {
                return NotFound();
            }

            var expectedCategorySlug = SlugHelper.GenerateSlug(loai.TenLoaiAlias);

            // Nếu categorySlug không khớp, chuyển hướng đến URL chính xác
            if (categorySlug != expectedCategorySlug)
            {
                return RedirectToActionPermanent("Detail", new { categorySlug = expectedCategorySlug, productSlug });
            }

            // Truyền slug hình ảnh và các thông tin cần thiết vào ViewData
            ViewData["ImageUrl"] = "~/Hinh/HangHoa/" + hangHoa.Hinh;

            return View(hangHoa);
        }

        public IActionResult Index(int? cateid, int page = 1, int pageSize = 10)
        {
            var data = _ctx.HangHoas.AsQueryable();

            if (cateid.HasValue)
            {
                // Lọc theo MaLoai
                data = data.Where(hh => hh.MaLoai == cateid.Value);

                // Lấy tên loại tương ứng
                var tenLoai = _ctx.Loais
                    .Where(l => l.MaLoai == cateid.Value)
                    .Select(l => l.TenLoai)
                    .FirstOrDefault();

                // Gán tên loại vào ViewData["Title"]
                ViewData["Title"] = tenLoai ?? "Danh sách hàng hóa";
            }
            else
            {
                ViewData["Title"] = "Danh sách hàng hóa";
            }

            // Tổng số sản phẩm
            int totalItems = data.Count();

            // Lấy dữ liệu phân trang
            var result = data
                .OrderBy(hh => hh.MaHh) // Sắp xếp theo mã sản phẩm (hoặc thuộc tính khác)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(hh => new HangHoaVM
                {
                    MaHh = hh.MaHh,
                    TenHh = hh.TenHh,
                    TenAlias = hh.TenAlias,
                    DonGia = hh.DonGia ?? 0,
                    Hinh = hh.Hinh
                }).ToList();

            // Truyền thêm dữ liệu phân trang
            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewData["CateId"] = cateid; // Truyền cateid để giữ nguyên danh mục

            return View(result);
        }

        public IActionResult Search(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return RedirectToAction("Index");
            }

            var results = _ctx.HangHoas
                .Where(hh => hh.TenHh.Contains(query) || hh.TenAlias.Contains(query))
                .Select(hh => new HangHoaVM
                {
                    MaHh = hh.MaHh,
                    TenHh = hh.TenHh,
                    TenAlias = hh.TenAlias,
                    DonGia = hh.DonGia ?? 0,
                    Hinh = hh.Hinh
                }).ToList();

            ViewData["Title"] = $"Kết quả tìm kiếm cho '{query}'";
            return View("Index", results);
        }

    }
}
