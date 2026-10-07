using Microsoft.AspNetCore.Mvc;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class NhanSuController : Controller
    {
        // GET: /NhanSu
        public IActionResult Index()
        {
            return View();
        }

        // POST: /NhanSu/LuuNhanSu (Thêm mới / cập nhật hồ sơ nhân sự qua AJAX)
        [HttpPost]
        public IActionResult LuuNhanSu([FromBody] NhanSuDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.HoTen))
            {
                return Json(new { success = false, message = "Vui lòng nhập họ tên nhân sự!" });
            }

            if (string.IsNullOrWhiteSpace(model.SoDienThoai) || model.SoDienThoai.Length < 10)
            {
                return Json(new { success = false, message = "Số điện thoại phải từ 10 số trở lên!" });
            }

            return Json(new
            {
                success = true,
                message = "Lưu hồ sơ nhân sự thành công!",
                data = model
            });
        }
    }

    public class NhanSuDto
    {
        public int NhanVienId { get; set; }
        public string HoTen { get; set; }
        public string VaiTro { get; set; }
        public string Email { get; set; }
        public string SoDienThoai { get; set; }
        public string ChuyenMon { get; set; }
        public decimal LuongCoBan { get; set; }
        public string TrangThai { get; set; }
    }
}