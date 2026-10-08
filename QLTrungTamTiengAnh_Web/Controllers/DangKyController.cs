using Microsoft.AspNetCore.Mvc;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class DangKyController : Controller
    {
        // GET: /DangKy
        public IActionResult Index()
        {
            return View();
        }

        // POST: /DangKy/LuuDangKy (Tiếp nhận đăng ký lớp học mới - F10)
        [HttpPost]
        public IActionResult LuuDangKy([FromBody] DangKyDto model)
        {
            if (model == null || model.HocVienId <= 0 || model.LopHocId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn học viên và lớp học hợp lệ!" });
            }

            // Quy tắc ngoại lệ F10: Kiểm tra lớp đầy
            if (model.SiSoHienTai >= model.SiSoToiDa)
            {
                return Json(new { success = false, message = "Lớp học đã đạt sĩ số tối đa, không thể tiếp nhận thêm học viên!" });
            }

            return Json(new
            {
                success = true,
                message = $"Đăng ký thành công học viên [{model.HoTenHocVien}] vào lớp [{model.TenLop}]!",
                data = model
            });
        }

        // POST: /DangKy/LuuDiemDauVao (Ghi nhận điểm kiểm tra đầu vào - F07)
        [HttpPost]
        public IActionResult LuuDiemDauVao([FromBody] DiemDauVaoDto model)
        {
            if (model == null || model.HocVienId <= 0)
            {
                return Json(new { success = false, message = "Thông tin học viên không hợp lệ!" });
            }

            // Quy tắc ngoại lệ F07: Điểm phải từ 0 - 100
            if (model.DiemDauVao < 0 || model.DiemDauVao > 100)
            {
                return Json(new { success = false, message = "Điểm kiểm tra đầu vào phải nằm trong thang điểm từ 0 đến 100!" });
            }

            return Json(new
            {
                success = true,
                message = $"Đã cập nhật điểm đầu vào ({model.DiemDauVao}/100) và xếp trình độ [{model.TrinhDoDeXuat}] thành công!"
            });
        }
    }

    public class DangKyDto
    {
        public int DangKyId { get; set; }
        public int HocVienId { get; set; }
        public string HoTenHocVien { get; set; }
        public int LopHocId { get; set; }
        public string TenLop { get; set; }
        public string TenKhoaHoc { get; set; }
        public decimal HocPhiPhaiThu { get; set; }
        public string NgayDangKy { get; set; }
        public int SiSoHienTai { get; set; }
        public int SiSoToiDa { get; set; }
        public string TrangThai { get; set; }
    }

    public class DiemDauVaoDto
    {
        public int HocVienId { get; set; }
        public double DiemDauVao { get; set; }
        public string TrinhDoDeXuat { get; set; }
        public string NhanXet { get; set; }
    }
}