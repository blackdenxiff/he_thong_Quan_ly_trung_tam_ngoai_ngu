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

        // POST: /LopHoc/LuuLopHoc (Thêm mới / Cập nhật lớp học theo đúng RN01)
        [HttpPost]
        public IActionResult LuuLopHoc([FromBody] LopHocDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TenLop))
            {
                return Json(new { success = false, message = "Vui lòng nhập tên lớp học!" });
            }

            if (model.KhoaHocId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn khóa học!" });
            }

            // Quy tắc RN01: Mỗi lớp phải thuộc một cơ sở cụ thể
            if (model.CoSoId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn cơ sở đào tạo tổ chức lớp!" });
            }

            if (model.SiSoToiDa <= 0)
            {
                return Json(new { success = false, message = "Sĩ số tối đa phải lớn hơn 0!" });
            }

            return Json(new
            {
                success = true,
                message = "Lưu thông tin lớp học thành công!",
                data = model
            });
        }

        // POST: /LopHoc/XoaLopHoc
        [HttpPost]
        public IActionResult XoaLopHoc(int id)
        {
            return Json(new
            {
                success = true,
                message = "Đã cập nhật trạng thái đóng lớp học!"
            });
        }
    }

    public class LopHocDto
    {
        public int LopHocId { get; set; }
        public string MaLop { get; set; }
        public string TenLop { get; set; }
        public int KhoaHocId { get; set; }
        public string TenKhoaHoc { get; set; }
        public int CoSoId { get; set; }          // Khóa ngoại CoSoID theo ERD
        public string TenCoSo { get; set; }       // Tên cơ sở hiển thị
        public int GiaoVienPhuTrachId { get; set; }
        public string TenGiaoVien { get; set; }
        public int SiSoHienTai { get; set; }
        public int SiSoToiDa { get; set; }
        public string NgayBatDau { get; set; }
        public string NgayKetThuc { get; set; }
        public string TrangThai { get; set; }     // Đang tuyển sinh, Đang học, Kết thúc
    }
}