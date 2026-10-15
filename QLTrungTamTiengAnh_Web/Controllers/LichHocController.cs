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

        // POST: /LichHoc/XepLich (AJAX lưu ca học mới & kiểm tra trùng phòng/ca)
        [HttpPost]
        public IActionResult XepLich([FromBody] XepLichDto model)
        {
            if (model == null || model.LopHocId <= 0 || string.IsNullOrWhiteSpace(model.PhongHoc))
            {
                return Json(new { success = false, message = "Vui lòng điền đầy đủ thông tin xếp lịch!" });
            }

            // Kiểm tra ràng buộc chống trùng: Phòng 101 ca Tối Thứ 2 đã có lớp học
            if (model.PhongHoc == "P.101" && model.ThuTrongTuan == "Thứ 2" && model.KhungGio == "Ca tối (18:00 - 20:00)")
            {
                return Json(new
                {
                    success = false,
                    message = "Xung đột lịch học: Phòng 101 vào tối Thứ 2 đã được gán cho lớp TOEIC500-K01!"
                });
            }

            return Json(new
            {
                success = true,
                message = "Xếp lịch học thành công!",
                data = model
            });
        }
    }

    public class XepLichDto
    {
        public int LopHocId { get; set; }
        public string TenLop { get; set; }
        public string GiangVien { get; set; }
        public string ThuTrongTuan { get; set; }
        public string KhungGio { get; set; }
        public string PhongHoc { get; set; }
        public string GhiChu { get; set; }
    }
}