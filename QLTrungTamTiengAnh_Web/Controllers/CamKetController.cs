using Microsoft.AspNetCore.Mvc;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class CamKetController : Controller
    {
        // GET: /CamKet
        public IActionResult Index()
        {
            return View();
        }

        // POST: /CamKet/LapCamKet (F18: Giáo viên đề xuất cam kết mới)
        [HttpPost]
        public IActionResult LapCamKet([FromBody] CamKetDto model)
        {
            if (model == null || model.DangKyId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn đợt ghi danh hợp lệ!" });
            }

            if (model.DiemYeuCau <= model.DiemDauVao)
            {
                return Json(new { success = false, message = "Điểm cam kết đầu ra phải cao hơn điểm kiểm tra đầu vào!" });
            }

            return Json(new
            {
                success = true,
                message = $"Đã lập đề xuất cam kết mục tiêu [{model.DiemYeuCau}đ] cho học viên {model.HoTenHocVien}! Đang chờ duyệt.",
                data = model
            });
        }

        // POST: /CamKet/DuyetCamKet (F18: Quản lý phê duyệt / Từ chối)
        [HttpPost]
        public IActionResult DuyetCamKet(int camKetId, bool dongY)
        {
            string trangThai = dongY ? "Đã duyệt" : "Từ chối";
            return Json(new
            {
                success = true,
                message = $"Đã {trangThai.ToLower()} thỏa thuận cam kết đầu ra!",
                trangThai = trangThai
            });
        }

        // POST: /CamKet/XetKetQuaCuoiKhoa (F21 & F22: Nhập điểm cuối khóa & xét học lại 0đ)
        [HttpPost]
        public IActionResult XetKetQuaCuoiKhoa([FromBody] KetQuaCuoiKhoaDto model)
        {
            if (model == null || model.CamKetId <= 0)
            {
                return Json(new { success = false, message = "Thông tin hồ sơ không hợp lệ!" });
            }

            if (model.DiemCuoiKhoa < 0 || model.DiemCuoiKhoa > 100)
            {
                return Json(new { success = false, message = "Điểm thi cuối khóa phải nằm trong thang điểm 0 - 100!" });
            }

            bool datChuan = model.DiemCuoiKhoa >= model.DiemCamKet;
            bool duocHocLai = !datChuan && model.TyLeChuyenCan >= 80; // Quy định học lại: không đạt nhưng chuyên cần >= 80%

            return Json(new
            {
                success = true,
                datChuan = datChuan,
                duocHocLai = duocHocLai,
                message = datChuan
                    ? "Học viên ĐẠT chuẩn đầu ra đã cam kết!"
                    : (duocHocLai
                        ? "Học viên KHÔNG ĐẠT chuẩn, đủ điều kiện cấp quyền HỌC LẠI MIỄN PHÍ (Học phí 0đ)!"
                        : "Học viên KHÔNG ĐẠT chuẩn và KHÔNG đủ điều kiện học lại do chuyên cần < 80%!")
            });
        }
    }

    public class CamKetDto
    {
        public int CamKetId { get; set; }
        public int DangKyId { get; set; }
        public string HoTenHocVien { get; set; }
        public string TenKhoaHoc { get; set; }
        public double DiemDauVao { get; set; }
        public double DiemYeuCau { get; set; }
        public string NguoiDeXuat { get; set; }
        public string TrangThai { get; set; }
    }

    public class KetQuaCuoiKhoaDto
    {
        public int CamKetId { get; set; }
        public double DiemCuoiKhoa { get; set; }
        public double DiemCamKet { get; set; }
        public double TyLeChuyenCan { get; set; }
        public string NhanXet { get; set; }
    }
}