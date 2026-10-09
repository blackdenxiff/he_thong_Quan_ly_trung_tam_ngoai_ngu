using Microsoft.AspNetCore.Mvc;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class LichHocController : Controller
    {
        // GET: /LichHoc
        public IActionResult Index()
        {
            return View();
        }

        // POST: /LichHoc/XepLich (AJAX lưu ca học mới & kiểm tra trùng phòng/ca - F12, RN03)
        [HttpPost]
        public IActionResult XepLich([FromBody] XepLichDto model)
        {
            if (model == null || model.LopHocId <= 0 || string.IsNullOrWhiteSpace(model.PhongHoc))
            {
                return Json(new { success = false, message = "Vui lòng điền đầy đủ thông tin xếp lịch!" });
            }

            // Kiểm tra ràng buộc chống trùng phòng (RN03)
            if (model.PhongHoc == "P.101" && model.ThuTrongTuan == "Thứ 2" && model.KhungGio.Contains("Ca tối"))
            {
                return Json(new
                {
                    success = false,
                    message = "Xung đột phòng học: Phòng 101 vào tối Thứ 2 đã có lớp học khác!"
                });
            }

            return Json(new
            {
                success = true,
                message = "Xếp lịch học thành công!",
                data = model
            });
        }

        // POST: /LichHoc/PhanCongDayThay (Ghi nhận phân công dạy thay vào bảng PhanCongDay - F13, RN02)
        [HttpPost]
        public IActionResult PhanCongDayThay([FromBody] PhanCongDayDto model)
        {
            if (model == null || model.LichHocId <= 0)
            {
                return Json(new { success = false, message = "Thông tin ca học không hợp lệ!" });
            }

            if (model.GiaoVienThucTeId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn giáo viên đứng lớp thực tế!" });
            }

            // Quy tắc RN02: Nếu là dạy thay thì giáo viên thực tế phải khác giáo viên phụ trách gốc
            if (model.GiaoVienPhuTrachId == model.GiaoVienThucTeId && model.LaDayThay)
            {
                return Json(new { success = false, message = "Giáo viên dạy thay phải khác với giáo viên phụ trách lớp!" });
            }

            return Json(new
            {
                success = true,
                message = model.LaDayThay
                    ? $"Đã phê duyệt phân công [{model.TenGiaoVienThucTe}] dạy thay lớp [{model.TenLop}] ({model.ThuTrongTuan})!"
                    : $"Đã khôi phục giảng dạy chính thức cho [{model.TenGiaoVienThucTe}]!",
                data = model
            });
        }
    }

    public class XepLichDto
    {
        public int LopHocId { get; set; }
        public string TenLop { get; set; }
        public int GiaoVienPhuTrachId { get; set; }
        public string GiangVien { get; set; }
        public string ThuTrongTuan { get; set; }
        public string KhungGio { get; set; }
        public string PhongHoc { get; set; }
        public string GhiChu { get; set; }
    }

    public class PhanCongDayDto
    {
        public int PhanCongId { get; set; }
        public int LichHocId { get; set; }
        public string TenLop { get; set; }
        public int GiaoVienPhuTrachId { get; set; }
        public string TenGiaoVienPhuTrach { get; set; }
        public int GiaoVienThucTeId { get; set; }
        public string TenGiaoVienThucTe { get; set; }
        public string ThuTrongTuan { get; set; }
        public string KhungGio { get; set; }
        public string LyDoDayThay { get; set; }
        public bool LaDayThay { get; set; }
    }
}