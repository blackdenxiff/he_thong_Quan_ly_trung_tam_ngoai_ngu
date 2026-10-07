using Microsoft.AspNetCore.Mvc;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class BangLuongController : Controller
    {
        // GET: /BangLuong
        public IActionResult Index()
        {
            return View();
        }

        // POST: /BangLuong/LuuBangLuong (Tạo/Cập nhật phiếu lương qua AJAX)
        [HttpPost]
        public IActionResult LuuBangLuong([FromBody] BangLuongDto model)
        {
            if (model == null || model.NhanVienId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn nhân sự/giảng viên hợp lệ!" });
            }

            if (model.SoGioDay < 0 || model.DonGiaGio < 0)
            {
                return Json(new { success = false, message = "Số giờ dạy và đơn giá không được mang giá trị âm!" });
            }

            decimal tongLuongDay = model.SoGioDay * model.DonGiaGio;
            decimal thucLinh = model.LuongCoBan + tongLuongDay + model.PhuCap - model.KhauTru;

            return Json(new
            {
                success = true,
                message = "Lập bảng tính lương thành công!",
                data = new
                {
                    model.NhanVienId,
                    model.HoTen,
                    thangNam = model.ThangNam,
                    thucLinh = thucLinh
                }
            });
        }

        // POST: /BangLuong/ChiTraLuong (Xác nhận giải ngân lương)
        [HttpPost]
        public IActionResult ChiTraLuong(int bangLuongId)
        {
            if (bangLuongId <= 0)
            {
                return Json(new { success = false, message = "Mã bảng lương không hợp lệ!" });
            }

            return Json(new
            {
                success = true,
                message = "Đã xác nhận giải ngân lương thành công!",
                ngayChi = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
            });
        }
    }

    public class BangLuongDto
    {
        public int BangLuongId { get; set; }
        public int NhanVienId { get; set; }
        public string HoTen { get; set; }
        public string ThangNam { get; set; }
        public decimal LuongCoBan { get; set; }
        public decimal SoGioDay { get; set; }
        public decimal DonGiaGio { get; set; }
        public decimal PhuCap { get; set; }
        public decimal KhauTru { get; set; }
        public decimal ThucLinh { get; set; }
        public string GhiChu { get; set; }
    }
}