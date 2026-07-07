using Microsoft.AspNetCore.Mvc;

namespace StockManager.Server.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
