using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class HomeController : Controller
    {
        [Authorize]
        public IActionResult Index()
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            return role switch
            {
                "Admin" or "QuanLy" or "Quản lý" => RedirectToAction("Index", "BaoCao"),
                "Giáo viên" or "GiaoVien" or "Giảng viên" => RedirectToAction("Index", "LichHoc"),
                "Nhân viên" => RedirectToAction("Index", "HocVien"),
                "Học viên" => RedirectToAction("Index", "LichHoc"),
                _ => Forbid()
            };
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
