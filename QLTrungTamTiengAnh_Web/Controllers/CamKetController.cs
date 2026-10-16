using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;
using System.Linq;
using System.Threading.Tasks;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class CamKetController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CamKetController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Helper method: Kiếm tra quyền truy cập CamKet của người dùng hiện tại
        private bool CanAccessCamKet(int camKetId, out string errorMessage)
        {
            errorMessage = "";
            var userRole = HttpContext.Session.GetString("UserRole");

            // Admin và Quản lý có quyền truy cập toàn bộ
            if (userRole == "Admin" || userRole == "Quản lý")
                return true;

            var camKet = _context.CamKets
                .Include(c => c.HocVien)
                .Include(c => c.DangKy)
                .ThenInclude(d => d.LopHoc)
                .FirstOrDefault(c => c.CamKetID == camKetId);

            if (camKet == null)
            {
                errorMessage = "Cam kết không tồn tại";
                return false;
            }

            // Học viên chỉ xem cam kết của chính mình
            if (userRole == "Học viên")
            {
                if (HttpContext.Session.TryGetValue("HocVienID", out var hocVienIdBytes))
                {
                    int hocVienId = System.BitConverter.ToInt32(hocVienIdBytes);
                    if (camKet.HocVienID == hocVienId)
                        return true;
                }
                errorMessage = "Bạn không có quyền xem cam kết này";
                return false;
            }

            // Giáo viên chỉ xem cam kết của lớp mình phụ trách
            if (userRole == "Giáo viên")
            {
                if (HttpContext.Session.TryGetValue("NhanSuID", out var nhanSuIdBytes))
                {
                    int nhanSuId = System.BitConverter.ToInt32(nhanSuIdBytes);

                    // Kiểm tra: Giáo viên này có phụ trách lớp mà cam kết này thuộc không?
                    if (camKet.DangKy != null && camKet.DangKy.LopHoc != null)
                    {
                        if (camKet.DangKy.LopHoc.GiaoVienPhuTrachID == nhanSuId)
                            return true;
                    }
                }
                errorMessage = "Bạn không có quyền xem cam kết này";
                return false;
            }

            // Nhân viên không có quyền
            if (userRole == "Nhân viên")
            {
                errorMessage = "Nhân viên không có quyền xem cam kết";
                return false;
            }

            errorMessage = "Không xác định quyền truy cập";
            return false;
        }

        // GET: /CamKet/Index - Danh sách cam kết
        public async Task<IActionResult> Index(int? page, int? hocVienId, int? khoaHocId, string trangThai, string keyword)
        {
            var userRole = HttpContext.Session.GetString("UserRole");

            // Kiểm tra quyền: Học viên chỉ xem của chính mình
            if (userRole == "Học viên")
            {
                if (HttpContext.Session.TryGetValue("HocVienID", out var hocVienIdBytes))
                {
                    hocVienId = System.BitConverter.ToInt32(hocVienIdBytes);
                }
                else
                {
                    return Unauthorized();
                }
            }
            else if (userRole == "Nhân viên")
            {
                // Nhân viên không được truy cập
                return Unauthorized();
            }

            var query = _context.CamKets
                .Include(c => c.HocVien)
                .Include(c => c.KhoaHoc)
                .Include(c => c.DangKy)
                .ThenInclude(d => d.LopHoc)
                .AsQueryable();

            // Lọc theo giáo viên
            if (userRole == "Giáo viên")
            {
                if (HttpContext.Session.TryGetValue("NhanSuID", out var nhanSuIdBytes))
                {
                    int nhanSuId = System.BitConverter.ToInt32(nhanSuIdBytes);
                    query = query.Where(c => c.DangKy.LopHoc.GiaoVienPhuTrachID == nhanSuId);
                }
            }

            // Lọc theo học viên
            if (hocVienId.HasValue)
                query = query.Where(c => c.HocVienID == hocVienId.Value);

            // Lọc theo khóa học
            if (khoaHocId.HasValue)
                query = query.Where(c => c.KhoaHocID == khoaHocId.Value);

            // Lọc theo trạng thái
            if (!string.IsNullOrEmpty(trangThai))
                query = query.Where(c => c.TrangThai == trangThai);

            // Tìm kiếm theo tên học viên
            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(c => c.HocVien.HoTen.Contains(keyword));

            // Phân trang (10 bản ghi mỗi trang)
            int pageNumber = page ?? 1;
            int pageSize = 10;
            var totalCount = await query.CountAsync();
            var camKets = await query
                .OrderByDescending(c => c.NgayLap)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = (int)System.Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.FilterHocVienId = hocVienId;
            ViewBag.FilterKhoaHocId = khoaHocId;
            ViewBag.FilterTrangThai = trangThai;
            ViewBag.FilterKeyword = keyword;

            return View(camKets);
        }

        // GET: /CamKet/ChiTiet/{id} - Xem chi tiết cam kết
        public async Task<IActionResult> ChiTiet(int id)
        {
            if (!CanAccessCamKet(id, out string errorMessage))
                return Unauthorized();

            var camKet = await _context.CamKets
                .Include(c => c.HocVien)
                .Include(c => c.KhoaHoc)
                .Include(c => c.DangKy)
                .ThenInclude(d => d.LopHoc)
                .ThenInclude(l => l.GiaoVienPhuTrach)
                .Include(c => c.GiaoVienDeXuat)
                .Include(c => c.NguoiDuyet)
                .FirstOrDefaultAsync(c => c.CamKetID == id);

            if (camKet == null)
                return NotFound();

            return View(camKet);
        }

        // POST: /CamKet/DeXuatDiem - Giáo viên đề xuất điểm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeXuatDiem(int camKetId, decimal diemCuoiKhoa, string ghiChu)
        {
            var userRole = HttpContext.Session.GetString("UserRole");

            // Chỉ giáo viên được đề xuất
            if (userRole != "Giáo viên")
                return Unauthorized();

            var camKet = await _context.CamKets
                .Include(c => c.DangKy)
                .ThenInclude(d => d.LopHoc)
                .FirstOrDefaultAsync(c => c.CamKetID == camKetId);

            if (camKet == null)
                return NotFound();

            // Kiểm tra quyền: Giáo viên này có phụ trách lớp này không?
            if (HttpContext.Session.TryGetValue("NhanSuID", out var nhanSuIdBytes))
            {
                int nhanSuId = System.BitConverter.ToInt32(nhanSuIdBytes);
                if (camKet.DangKy?.LopHoc?.GiaoVienPhuTrachID != nhanSuId)
                    return Unauthorized();
            }
            else
            {
                return Unauthorized();
            }

            // Kiểm tra trạng thái: chỉ được đề xuất khi trạng thái là "Chờ duyệt"
            if (camKet.TrangThai != "Chờ duyệt")
                return BadRequest(new { message = "Cam kết phải ở trạng thái 'Chờ duyệt' để đề xuất điểm" });

            // Kiểm tra điểm hợp lệ
            if (diemCuoiKhoa < 0 || diemCuoiKhoa > 100)
                return BadRequest(new { message = "Điểm phải nằm từ 0 đến 100" });

            // Cập nhật cam kết
            camKet.DiemCuoiKhoa = diemCuoiKhoa;
            camKet.GiaoVienDeXuatID = System.BitConverter.ToInt32(nhanSuIdBytes);
            camKet.GhiChu = ghiChu;

            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đề xuất điểm thành công" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi khi lưu: " + ex.Message });
            }
        }

        // POST: /CamKet/Duyet - Duyệt hoặc từ chối cam kết
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duyet(int camKetId, bool approved, string ghiChuTuChoi)
        {
            var userRole = HttpContext.Session.GetString("UserRole");

            // Chỉ Admin/Quản lý được duyệt
            if (userRole != "Admin" && userRole != "Quản lý")
                return Unauthorized();

            var camKet = await _context.CamKets
                .Include(c => c.DangKy)
                .FirstOrDefaultAsync(c => c.CamKetID == camKetId);

            if (camKet == null)
                return NotFound();

            // Kiểm tra trạng thái
            if (camKet.TrangThai != "Chờ duyệt")
                return BadRequest(new { message = "Cam kết phải ở trạng thái 'Chờ duyệt' để duyệt" });

            // Xác định NguoiDungID từ session (nếu có)
            int? nguoiDungId = null;
            if (HttpContext.Session.TryGetValue("NguoiDungID", out var nguoiDungIdBytes))
            {
                nguoiDungId = System.BitConverter.ToInt32(nguoiDungIdBytes);
            }

            if (approved)
            {
                // Duyệt
                camKet.TrangThai = "Đã duyệt";
                camKet.NguoiDuyetID = nguoiDungId;
                camKet.NgayDuyet = DateTime.Now;
                camKet.GhiChu = null;

                // Cập nhật DangKy: Lưu điểm và kết quả nếu có
                if (camKet.DangKy != null && camKet.DiemCuoiKhoa.HasValue)
                {
                    camKet.DangKy.DiemCuoiKhoa = camKet.DiemCuoiKhoa;

                    // Tính KetQua dựa trên điểm (quy tắc: >= 50 = Đỗ, < 50 = Rớt)
                    // LƯU Ý: Nếu thiếu quy tắc chính thức, chỉ cập nhật điểm
                    if (camKet.DiemCuoiKhoa >= 50)
                        camKet.DangKy.KetQua = "Đỗ";
                    else if (camKet.DiemCuoiKhoa < 50)
                        camKet.DangKy.KetQua = "Rớt";
                }
            }
            else
            {
                // Từ chối
                camKet.TrangThai = "Từ chối";
                camKet.NguoiDuyetID = nguoiDungId;
                camKet.NgayDuyet = DateTime.Now;
                camKet.GhiChu = ghiChuTuChoi;
            }

            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = approved ? "Duyệt thành công" : "Từ chối thành công" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi khi lưu: " + ex.Message });
            }
        }
    }
}
