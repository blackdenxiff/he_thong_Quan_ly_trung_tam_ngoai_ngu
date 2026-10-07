using Microsoft.AspNetCore.Mvc;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class LopHocController : Controller
    {
        // GET: /LopHoc
        public IActionResult Index()
        {
            return View();
        }

        // POST: /LopHoc/LuuLopHoc (Thêm mới / cập nhật lớp học qua AJAX)
        [HttpPost]
        public IActionResult LuuLopHoc([FromBody] LopHocDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TenLop))
            {
                return Json(new { success = false, message = "Vui lòng nhập tên mã lớp học!" });
            }

            if (model.SiSoToiDa <= 0 || model.SiSoToiDa > 40)
            {
                return Json(new { success = false, message = "Sĩ số tối đa của lớp phải từ 1 đến 40 học viên!" });
            }

            return Json(new
            {
                success = true,
                message = "Lưu thông tin lớp học thành công!",
                data = model
            });
        }

        // POST: /LopHoc/DoiTrangThai (Chuyển trạng thái đào tạo của lớp)
        [HttpPost]
        public IActionResult DoiTrangThai(int lopHocId, string trangThaiMoi)
        {
            if (lopHocId <= 0 || string.IsNullOrWhiteSpace(trangThaiMoi))
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });
            }

            return Json(new
            {
                success = true,
                message = $"Đã chuyển trạng thái lớp sang: {trangThaiMoi}!"
            });
        }
    }

    public class LopHocDto
    {
        public int LopHocId { get; set; }
        public string TenLop { get; set; }
        public int KhoaHocId { get; set; }
        public string TenKhoaHoc { get; set; }
        public string GiangVien { get; set; }
        public string PhongHoc { get; set; }
        public int SiSoToiDa { get; set; }
        public string NgayKhaiGiang { get; set; }
        public string LichHocMoTa { get; set; }
        public string TrangThai { get; set; }
    }
}