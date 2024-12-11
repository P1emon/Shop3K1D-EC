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

        // Tạo slug cho URL thân thiện
        public IActionResult Detail(int id, string slug)
        {
            var hangHoa = _ctx.HangHoas.SingleOrDefault(p => p.MaHh == id);
            if (hangHoa == null)
            {
                return NotFound();
            }

            var loai = _ctx.Loais.SingleOrDefault(l => l.MaLoai == hangHoa.MaLoai);
            if (loai == null)
            {
                return NotFound();
            }

            // Tạo slug mong đợi từ Loai và HangHoa
            var expectedSlug = SlugHelper.GenerateSlug(loai.TenLoaiAlias + "/-" + hangHoa.TenAlias);

            // Kiểm tra slug trong URL và so sánh với expectedSlug
            if (slug != expectedSlug)
            {
                return RedirectToActionPermanent("Detail", "Products", new { id = hangHoa.MaHh, slug = expectedSlug });
            }

            // Truyền slug qua ViewData
            ViewData["ExpectedSlug"] = expectedSlug;

            // Truyền dữ liệu hình ảnh
            var imageUrl = "~/Hinh/HangHoa/" + hangHoa.Hinh;
            ViewData["ImageUrl"] = imageUrl;

            return View(hangHoa);
        }

        public IActionResult Index(int? cateid)
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

            // Chuẩn bị dữ liệu ViewModel
            var result = data.Select(hh => new HangHoaVM
            {
                MaHh = hh.MaHh,
                TenHh = hh.TenHh,
                DonGia = hh.DonGia ?? 0,
                Hinh = hh.Hinh
            });

            return View(result);
        }
    }
}
