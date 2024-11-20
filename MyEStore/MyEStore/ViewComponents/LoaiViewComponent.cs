using Microsoft.AspNetCore.Mvc;
using MyEStore.Entities;

namespace MyEStore.ViewComponents
{
    public class LoaiViewComponent : ViewComponent
    {
        private readonly MyeStoreContext _ctx;

        public LoaiViewComponent(MyeStoreContext ctx)
        {
            _ctx = ctx;
        }

        public IViewComponentResult Invoke()
        {
            return View(_ctx.Loais.ToList());
        }
    }
}
