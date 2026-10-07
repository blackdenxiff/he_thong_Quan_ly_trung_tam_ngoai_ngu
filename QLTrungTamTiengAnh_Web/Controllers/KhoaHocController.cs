using Microsoft.AspNetCore.Mvc;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class KhoaHocController : Controller
    {
        // GET: /KhoaHoc
        public IActionResult Index()
        {
            return View();
        }

        // POST: /KhoaHoc/LuuKhoaHoc (Thêm mới / Sửa khóa học qua AJAX)
        [HttpPost]
        public IActionResult LuuKhoaHoc([FromBody] KhoaHocDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TenKhoaHoc))
            {
                return Json(new { success = false, message = "Vui lòng nhập tên khóa học!" });
            }

            if (model.SoBuoiHoc <= 0 || model.SoBuoiHoc > 120)
            {
                return Json(new { success = false, message = "Số buổi học phải nằm trong khoảng từ 1 đến 120 buổi!" });
            }

            if (model.HocPhiNiemYet <= 0)
            {
                return Json(new { success = false, message = "Học phí niêm yết phải lớn hơn 0 đồng!" });
            }

            return Json(new
            {
                success = true,
                message = "Lưu thông tin khóa học thành công!",
                data = model
            });
        }

        // POST: /KhoaHoc/XoaKhoaHoc
        [HttpPost]
        public IActionResult XoaKhoaHoc(int khoaHocId)
        {
            if (khoaHocId <= 0)
            {
                return Json(new { success = false, message = "Mã khóa học không hợp lệ!" });
            }

            return Json(new
            {
                success = true,
                message = "Đã ngưng phát hành khóa học này thành công!"
            });
        }
    }

    public class KhoaHocDto
    {
        public int KhoaHocId { get; set; }
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public string CapDo { get; set; }
        public int SoBuoiHoc { get; set; }
        public decimal HocPhiNiemYet { get; set; }
        public string MoTa { get; set; }
        public string TrangThai { get; set; }
    }
}