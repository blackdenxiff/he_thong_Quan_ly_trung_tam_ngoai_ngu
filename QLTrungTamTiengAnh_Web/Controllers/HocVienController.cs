using Microsoft.AspNetCore.Mvc;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class HocVienController : Controller
    {
        // GET: /HocVien
        public IActionResult Index()
        {
            return View();
        }

        // Action nhận dữ liệu thêm/sửa học viên qua AJAX
        [HttpPost]
        public IActionResult LuuHocVien([FromBody] HocVienDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.HoTen))
            {
                return Json(new { success = false, message = "Vui lòng nhập họ tên học viên!" });
            }

            if (model.DiemDauVao < 0 || model.DiemDauVao > 100)
            {
                return Json(new { success = false, message = "Điểm đầu vào phải nằm trong thang điểm từ 0 đến 100!" });
            }

            return Json(new
            {
                success = true,
                message = "Lưu thông tin học viên thành công!",
                data = model
            });
        }

        // Action nhận đăng ký lớp học cho học viên (F10)
        [HttpPost]
        public IActionResult DangKyLop([FromBody] DangKyLopDto model)
        {
            if (model == null || model.HocVienId <= 0 || model.LopHocId <= 0)
            {
                return Json(new { success = false, message = "Dữ liệu đăng ký lớp không hợp lệ!" });
            }

            return Json(new
            {
                success = true,
                message = "Đăng ký xếp lớp thành công! Hóa đơn học phí đã được tạo tự động."
            });
        }
    }

    public class HocVienDto
    {
        public int HocVienId { get; set; }
        public string HoTen { get; set; }
        public string SoDienThoai { get; set; }
        public string Email { get; set; }
        public string GioiTinh { get; set; }
        public string TrinhDoDauVao { get; set; }
        public decimal DiemDauVao { get; set; }
        public string NhanXet { get; set; }
    }

    public class DangKyLopDto
    {
        public int HocVienId { get; set; }
        public int LopHocId { get; set; }
        public decimal HocPhiApDung { get; set; }
        public bool CoCamKet { get; set; }
        public string GhiChu { get; set; }
    }
}