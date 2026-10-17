using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    [Authorize(Roles = "Admin,QuanLy,Quản lý,Nhân viên")]
    public class HocVienController : Controller
    {
        private readonly ApplicationDbContext _context;
        public HocVienController(ApplicationDbContext context) => _context = context;

        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> DanhSach(string? keyword, string? trinhDo, string? trangThai, int page = 1)
        {
            page = Math.Max(1, page);
            var query = _context.HocViens.AsNoTracking().Where(x => !x.IsDeleted);
            keyword = keyword?.Trim();
            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(x => x.HoTen.Contains(keyword) || (x.SoDienThoai ?? "").Contains(keyword) || (x.Email ?? "").Contains(keyword));
            if (!string.IsNullOrWhiteSpace(trinhDo)) query = query.Where(x => x.TrinhDoDauVao == trinhDo);
            if (!string.IsNullOrWhiteSpace(trangThai)) query = query.Where(x => x.TrangThai == trangThai);
            var total = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(total / 10d);
            page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
            var items = await query.OrderByDescending(x => x.HocVienID).Skip((page - 1) * 10).Take(10)
                .Select(x => new { id = x.HocVienID, ma = $"HV-{x.HocVienID:D3}", hoTen = x.HoTen, dienThoai = x.SoDienThoai, email = x.Email, trinhDo = x.TrinhDoDauVao, diem = x.DiemDauVao, nhanXet = x.NhanXetDauVao, trangThai = x.TrangThai, soDangKy = x.DangKys!.Count(d => !d.IsDeleted && d.TrangThai == "Đang học") }).ToListAsync();
            return Json(new { success = true, items, page, total, totalPages });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuHocVien([FromBody] HocVienDto? model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.HoTen)) return BadRequest(new { success = false, message = "Vui lòng nhập họ tên học viên." });
            if (model.DiemDauVao is < 0 or > 100) return BadRequest(new { success = false, message = "Điểm đầu vào phải từ 0 đến 100." });
            if (!string.IsNullOrWhiteSpace(model.Email) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(model.Email)) return BadRequest(new { success = false, message = "Email không hợp lệ." });
            var entity = model.HocVienId == 0 ? new HocVien() : await _context.HocViens.FirstOrDefaultAsync(x => x.HocVienID == model.HocVienId && !x.IsDeleted);
            if (entity == null) return NotFound(new { success = false, message = "Không tìm thấy học viên." });
            entity.HoTen = model.HoTen.Trim(); entity.SoDienThoai = model.SoDienThoai?.Trim(); entity.Email = model.Email?.Trim(); entity.GioiTinh = model.GioiTinh; entity.TrinhDoDauVao = string.IsNullOrWhiteSpace(model.TrinhDoDauVao) ? null : model.TrinhDoDauVao; entity.DiemDauVao = model.DiemDauVao; entity.NhanXetDauVao = model.NhanXet?.Trim(); entity.TrangThai = string.IsNullOrWhiteSpace(model.TrangThai) ? "Chờ xếp lớp" : model.TrangThai; entity.UpdatedAt = DateTime.Now;
            if (entity.HocVienID == 0) _context.HocViens.Add(entity);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã lưu hồ sơ học viên.", id = entity.HocVienID });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaHocVien(int hocVienId)
        {
            var entity = await _context.HocViens.FirstOrDefaultAsync(x => x.HocVienID == hocVienId && !x.IsDeleted);
            if (entity == null) return NotFound(new { success = false, message = "Không tìm thấy học viên." });
            if (await _context.DangKys.AnyAsync(x => x.HocVienID == hocVienId && !x.IsDeleted && x.TrangThai == "Đang học")) return Conflict(new { success = false, message = "Không thể xóa học viên đang có đăng ký hiệu lực." });
            entity.IsDeleted = true; entity.DeletedAt = DateTime.Now; entity.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã chuyển hồ sơ vào thùng rác." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DangKyLop([FromBody] DangKyLopDto? model)
        {
            if (model == null || model.HocVienId <= 0 || model.LopHocId <= 0) return BadRequest(new { success = false, message = "Dữ liệu đăng ký không hợp lệ." });
            var lop = await _context.LopHocs.Include(x => x.KhoaHoc).FirstOrDefaultAsync(x => x.LopHocID == model.LopHocId && !x.IsDeleted);
            if (lop == null || lop.TrangThai is "Đã kết thúc" or "Đã hủy") return BadRequest(new { success = false, message = "Lớp không còn nhận đăng ký." });
            if (await _context.DangKys.AnyAsync(x => x.HocVienID == model.HocVienId && x.LopHocID == model.LopHocId && !x.IsDeleted && x.TrangThai == "Đang học")) return Conflict(new { success = false, message = "Học viên đã đăng ký lớp này." });
            var count = await _context.DangKys.CountAsync(x => x.LopHocID == model.LopHocId && !x.IsDeleted && x.TrangThai == "Đang học");
            if (count >= lop.SiSoToiDa) return Conflict(new { success = false, message = "Lớp đã đủ sĩ số." });
            _context.DangKys.Add(new DangKy { HocVienID = model.HocVienId, LopHocID = model.LopHocId, HocPhiApDung = lop.KhoaHoc?.HocPhi ?? 0, GhiChu = model.GhiChu, NgayDangKy = DateTime.Now, TrangThai = "Đang học" });
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã đăng ký lớp thành công." });
        }
    }

    public class HocVienDto { public int HocVienId { get; set; } public string? HoTen { get; set; } public string? SoDienThoai { get; set; } public string? Email { get; set; } public string? GioiTinh { get; set; } public string? TrinhDoDauVao { get; set; } public decimal? DiemDauVao { get; set; } public string? NhanXet { get; set; } public string? TrangThai { get; set; } }
    public class DangKyLopDto { public int HocVienId { get; set; } public int LopHocId { get; set; } public bool CoCamKet { get; set; } public string? GhiChu { get; set; } }
}