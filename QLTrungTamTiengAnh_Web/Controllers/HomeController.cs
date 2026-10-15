using Microsoft.AspNetCore.Mvc;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        // Action trả về dữ liệu biểu đồ qua AJAX (đáp ứng tiêu chí xử lý AJAX đề cương)
        [HttpGet]
        public IActionResult GetChartData()
        {
            var revenueData = new
            {
                months = new[] { "Tháng 5", "Tháng 6", "Tháng 7", "Tháng 8", "Tháng 9", "Tháng 10" },
                revenues = new[] { 45000000, 52000000, 61000000, 58000000, 74000000, 85000000 }
            };

            var studentLevelData = new
            {
                labels = new[] { "A1 (Mất gốc)", "A2 (Sơ cấp)", "B1 (TOEIC 500+)", "B2 (IELTS 6.5+)" },
                counts = new[] { 25, 40, 30, 15 }
            };

            return Json(new { success = true, revenue = revenueData, levels = studentLevelData });
        }
    }
}