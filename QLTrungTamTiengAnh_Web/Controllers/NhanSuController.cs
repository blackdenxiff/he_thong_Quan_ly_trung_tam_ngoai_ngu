using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;
using System.Text.RegularExpressions;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    [Authorize(Roles = "Admin,QuanLy,Quản lý")]
    public class NhanSuController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NhanSuController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.CoSos = await _context.CoSos.AsNoTracking()
                .Where(x => x.TrangThai)
                .OrderBy(x => x.TenCoSo)
                .Select(x => new CoSoOptionDto { Id = x.CoSoID, Name = x.TenCoSo })
                .ToListAsync();
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> DanhSach(string? keyword = null, string? vaiTro = null,
            string? trangThai = null, int page = 1, int pageSize = 10)
        {
            page = Math.Max(page, 1);
            pageSize = 10;
            keyword = (keyword ?? string.Empty).Trim();
            vaiTro = (vaiTro ?? string.Empty).Trim();
            trangThai = NormalizeStatus(trangThai);
            var today = DateTime.Today;

            var query = _context.NhanSus.AsNoTracking()
                .Include(x => x.CoSo)
                .Where(x => !x.IsDeleted);
            if (keyword.Length > 100)
                return BadRequest(new { success = false, message = "Từ khóa không được vượt quá 100 ký tự." });
            if (keyword.Length > 0)
            {
                query = query.Where(x => x.HoTen.Contains(keyword)
                    || (x.Email != null && x.Email.Contains(keyword))
                    || (x.SoDienThoai != null && x.SoDienThoai.Contains(keyword))
                    || x.NhanSuID.ToString().Contains(keyword));
            }
            if (vaiTro.Length > 0)
                query = query.Where(x => x.LoaiNhanSu == vaiTro);
            if (trangThai.Length > 0)
                query = query.Where(x => x.TrangThai == trangThai);

            var total = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
            var items = await query.OrderByDescending(x => x.NhanSuID)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new
                {
                    id = x.NhanSuID,
                    maNhanSu = $"NS-{x.NhanSuID:D3}",
                    hoTen = x.HoTen,
                    vaiTro = x.LoaiNhanSu,
                    chuyenMon = x.TrinhDo ?? string.Empty,
                    soDienThoai = x.SoDienThoai ?? string.Empty,
                    email = x.Email ?? string.Empty,
                    luongCoBan = x.LuongCoBan,
                    trangThai = x.TrangThai,
                    coSoId = x.CoSoID,
                    coSo = x.CoSo != null ? x.CoSo.TenCoSo : string.Empty,
                    soLop = x.LopHocs!.Count(l => !l.IsDeleted && l.NgayKetThuc >= today && l.TrangThai != "Đã kết thúc")
                }).ToListAsync();

            var stats = new
            {
                total = await _context.NhanSus.CountAsync(),
                teaching = await _context.NhanSus.CountAsync(x => x.LoaiNhanSu == "Giảng viên"
                    && x.LopHocs!.Any(l => !l.IsDeleted && l.NgayKetThuc >= today && l.TrangThai != "Đã kết thúc")),
                active = await _context.NhanSus.CountAsync(x => x.TrangThai == "Đang làm"),
                inactive = await _context.NhanSus.CountAsync(x => x.TrangThai != "Đang làm")
            };
            return Json(new { success = true, items, page, pageSize, total, totalPages, stats });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuNhanSu([FromBody] NhanSuDto? model)
        {
            if (model == null || model.NhanVienId < 0)
                return BadRequest(new { success = false, message = "Thông tin nhân sự không hợp lệ." });
            var name = model.HoTen?.Trim() ?? string.Empty;
            var role = model.VaiTro?.Trim() ?? string.Empty;
            var phone = model.SoDienThoai?.Trim() ?? string.Empty;
            var email = model.Email?.Trim() ?? string.Empty;
            var skill = model.ChuyenMon?.Trim() ?? string.Empty;
            var status = NormalizeStatus(model.TrangThai);
            if (status.Length == 0)
                return BadRequest(new { success = false, message = "Trạng thái nhân sự không hợp lệ." });
            if (name.Length == 0 || name.Length > 100 || role.Length == 0 || role.Length > 20)
                return BadRequest(new { success = false, message = "Họ tên và vai trò là bắt buộc, không vượt quá độ dài cho phép." });
            if (phone.Length > 0 && !Regex.IsMatch(phone, "^[0-9+ .-]{10,20}$"))
                return BadRequest(new { success = false, message = "Số điện thoại không hợp lệ." });
            if (email.Length > 100 || (email.Length > 0 && !Regex.IsMatch(email, "^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$")))
                return BadRequest(new { success = false, message = "Email không hợp lệ." });
            if (model.LuongCoBan < 0)
                return BadRequest(new { success = false, message = "Lương cơ bản không được âm." });
            if (model.CoSoId <= 0 || !await _context.CoSos.AnyAsync(x => x.CoSoID == model.CoSoId && x.TrangThai))
                return BadRequest(new { success = false, message = "Cơ sở không tồn tại hoặc đang tạm ngưng." });

            var entity = model.NhanVienId == 0
                ? new NhanSu { NgayVaoLam = DateTime.Today }
                : await _context.NhanSus.FirstOrDefaultAsync(x => x.NhanSuID == model.NhanVienId && !x.IsDeleted);
            if (entity == null)
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ nhân sự." });

            if (entity.NhanSuID > 0 && status != "Đang làm"
                && await HasFutureTeachingAsync(entity.NhanSuID))
                return Conflict(new { success = false, message = "Không thể ngừng công tác khi nhân sự còn phân công hoặc lớp học sắp diễn ra." });

            entity.HoTen = name;
            entity.LoaiNhanSu = role;
            entity.TrinhDo = string.IsNullOrWhiteSpace(skill) ? null : skill;
            entity.SoDienThoai = string.IsNullOrWhiteSpace(phone) ? null : phone;
            entity.Email = string.IsNullOrWhiteSpace(email) ? null : email;
            entity.LuongCoBan = model.LuongCoBan;
            entity.TrangThai = status;
            entity.CoSoID = model.CoSoId;
            entity.UpdatedAt = DateTime.Now;
            if (entity.NhanSuID == 0)
                _context.NhanSus.Add(entity);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã lưu hồ sơ nhân sự.", id = entity.NhanSuID });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaMem(int id)
        {
            var entity = await _context.NhanSus.FirstOrDefaultAsync(x => x.NhanSuID == id && !x.IsDeleted);
            if (entity == null)
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ nhân sự." });
            if (await HasFutureTeachingAsync(id))
                return Conflict(new { success = false, message = "Không thể xóa hồ sơ khi còn phân công hoặc lớp học tương lai." });
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.Now;
            entity.UpdatedAt = DateTime.Now;
            entity.TrangThai = "Tạm nghỉ";
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã chuyển hồ sơ vào thùng rác." });
        }

        [HttpGet]
        public async Task<IActionResult> ThungRac(int page = 1)
        {
            const int pageSize = 10;
            page = Math.Max(page, 1);
            var query = _context.NhanSus.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.IsDeleted).OrderByDescending(x => x.DeletedAt);
            var total = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new { id = x.NhanSuID, maNhanSu = $"NS-{x.NhanSuID:D3}", hoTen = x.HoTen, deletedAt = x.DeletedAt })
                .ToListAsync();
            return Json(new { success = true, items, page, totalPages, total });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KhoiPhuc(int id)
        {
            var entity = await _context.NhanSus.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.NhanSuID == id && x.IsDeleted);
            if (entity == null)
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ trong thùng rác." });
            if (!await _context.CoSos.AnyAsync(x => x.CoSoID == entity.CoSoID && x.TrangThai))
                return Conflict(new { success = false, message = "Cơ sở của hồ sơ đang tạm ngưng; hãy kích hoạt cơ sở trước." });
            entity.IsDeleted = false;
            entity.DeletedAt = null;
            entity.TrangThai = "Tạm nghỉ";
            entity.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã khôi phục hồ sơ ở trạng thái tạm nghỉ." });
        }

        private async Task<bool> HasFutureTeachingAsync(int nhanSuId)
        {
            var today = DateTime.Today;
            return await _context.LopHocs.AnyAsync(x => x.GiaoVienPhuTrachID == nhanSuId
                && !x.IsDeleted && x.NgayKetThuc >= today && x.TrangThai != "Đã kết thúc")
                || await _context.PhanCongDays.AnyAsync(x => x.NhanSuID == nhanSuId && !x.IsDeleted
                    && x.LichHoc != null && x.LichHoc.NgayHoc >= today && x.TrangThai != "Từ chối");
        }

        private static string NormalizeStatus(string? status)
            => status?.Trim() switch
            {
                "Đang làm việc" => "Đang làm",
                "Đang làm" => "Đang làm",
                "Thử việc" => "Thử việc",
                "Tạm nghỉ" => "Tạm nghỉ",
                _ => string.Empty
            };
    }

    public sealed class CoSoOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class NhanSuDto
    {
        public int NhanVienId { get; set; }
        public int CoSoId { get; set; }
        public string? HoTen { get; set; }
        public string? VaiTro { get; set; }
        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }
        public string? ChuyenMon { get; set; }
        public decimal LuongCoBan { get; set; }
        public string? TrangThai { get; set; }
    }
}