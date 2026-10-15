using Microsoft.AspNetCore.Mvc;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class HoaDonController : Controller
    {
        // GET: /HoaDon
        public IActionResult Index()
        {
            return View();
        }

        // POST: /HoaDon/LapHoaDon (Lập hóa đơn học phí mới)
        [HttpPost]
        public IActionResult LapHoaDon([FromBody] HoaDonRequestDto model)
        {
            if (model == null || model.HocVienId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn học viên hợp lệ!" });
            }

            if (model.GiamGia < 0 || model.GiamGia > model.TongTien)
            {
                return Json(new { success = false, message = "Số tiền giảm giá không hợp lệ (phải từ 0 đến tổng tiền)!" });
            }

            decimal tongPhaiTra = model.TongTien - model.GiamGia;

            return Json(new
            {
                success = true,
                message = "Phát hành hóa đơn học phí thành công!",
                data = new
                {
                    model.HocVienId,
                    model.DangKyId,
                    model.TongTien,
                    model.GiamGia,
                    tongPhaiTra,
                    model.GhiChu
                }
            });
        }

        // POST: /HoaDon/ThuTien (Ghi nhận đợt thanh toán vào bảng ThanhToan)
        [HttpPost]
        public IActionResult ThuTien([FromBody] ThanhToanRequestDto model)
        {
            if (model == null || model.HoaDonId <= 0)
            {
                return Json(new { success = false, message = "Mã hóa đơn không hợp lệ!" });
            }

            if (model.SoTien <= 0)
            {
                return Json(new { success = false, message = "Số tiền thanh toán phải lớn hơn 0 đồng!" });
            }

            return Json(new
            {
                success = true,
                message = $"Đã xác nhận thu {model.SoTien:N0} đ bằng hình thức {model.PhuongThuc}!",
                thoiGian = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
            });
        }
    }

    public class HoaDonRequestDto
    {
        public int HocVienId { get; set; }
        public int DangKyId { get; set; }
        public decimal TongTien { get; set; }
        public decimal GiamGia { get; set; }
        public string GhiChu { get; set; }
    }

    public class ThanhToanRequestDto
    {
        public int HoaDonId { get; set; }
        public decimal SoTien { get; set; }
        public string PhuongThuc { get; set; }
        public string GhiChu { get; set; }
    }
}