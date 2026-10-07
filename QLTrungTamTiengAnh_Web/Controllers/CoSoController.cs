using Microsoft.AspNetCore.Mvc;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class CoSoController : Controller
    {
        // GET: /CoSo (F03 & F05)
        public IActionResult Index()
        {
            return View();
        }

        // POST: /CoSo/LuuCoSo (Thêm mới / Cập nhật cơ sở - F03)
        [HttpPost]
        public IActionResult LuuCoSo([FromBody] CoSoDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TenCoSo))
            {
                return Json(new { success = false, message = "Vui lòng nhập tên cơ sở!" });
            }

            if (string.IsNullOrWhiteSpace(model.DiaChi))
            {
                return Json(new { success = false, message = "Vui lòng nhập địa chỉ cơ sở!" });
            }

            return Json(new
            {
                success = true,
                message = "Lưu thông tin cơ sở thành công!",
                data = model
            });
        }

        // POST: /CoSo/DieuChuyenNhanSu (Đề xuất & duyệt chuyển cơ sở - F05)
        [HttpPost]
        public IActionResult DieuChuyenNhanSu([FromBody] DieuChuyenDto model)
        {
            if (model == null || model.NhanSuId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn nhân sự điều chuyển!" });
            }

            // Quy tắc ngoại lệ F05: Cơ sở cũ phải khác cơ sở mới
            if (model.CoSoHienTai == model.CoSoMoi)
            {
                return Json(new { success = false, message = "Cơ sở chuyển đến phải khác với cơ sở hiện tại!" });
            }

            return Json(new
            {
                success = true,
                message = $"Đã phê duyệt điều chuyển nhân sự {model.HoTen} sang cơ sở [{model.CoSoMoi}] thành công!",
                ngayDuyet = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
            });
        }
    }

    public class CoSoDto
    {
        public int CoSoId { get; set; }
        public string MaCoSo { get; set; }
        public string TenCoSo { get; set; }
        public string DiaChi { get; set; }
        public string SoDienThoai { get; set; }
        public string TrangThai { get; set; }
    }

    public class DieuChuyenDto
    {
        public int NhanSuId { get; set; }
        public string HoTen { get; set; }
        public string CoSoHienTai { get; set; }
        public string CoSoMoi { get; set; }
        public string LyDo { get; set; }
        public string NguoiDuyet { get; set; }
    }
}