using Microsoft.AspNetCore.Mvc;
using MyEStore.Entities;
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

        public IActionResult Detail(int id)
        {
            var hangHoa = _ctx.HangHoas.SingleOrDefault(p => p.MaHh == id);
            if (hangHoa == null)
            {
                return NotFound();
            }
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
