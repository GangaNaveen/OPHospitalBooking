using HospitalOPBooking.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace HospitalOPBooking.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            var role = HttpContext.Session.GetString("UserRole");

            if (role == "Patient")
                return RedirectToAction("Dashboard", "Booking");

            if (role == "Hospital")
                return RedirectToAction("Dashboard", "Hospital");

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
