using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;

namespace QLTrungTamTiengAnh_Web.Controllers;

[Authorize]
public sealed class CamKetController(ApplicationDbContext context) : Controller
{
    private static readonly HashSet<string> ManagerRoles = ["Admin", "QuanLy", "Quản lý"];
    private static readonly HashSet<string> TeacherRoles = ["Giáo viên", "GiaoVien", "Giảng viên"];

    [HttpGet]
    public async Task<IActionResult> Index(int? page, int? hocVienId, int? khoaHocId,
        string? trangThai, string? keyword, CancellationToken cancellationToken)
    {
        var role = CurrentRole();
        if (role == "Nhân viên") return Forbid();
        var query = context.CamKets.AsNoTracking()
            .Include(x => x.HocVien).Include(x => x.KhoaHoc)
            .Include(x => x.DangKy).ThenInclude(x => x!.LopHoc)
            .AsQueryable();

        if (role == "Học viên")
        {
            var studentId = await GetCurrentStudentIdAsync(cancellationToken);
            if (!studentId.HasValue) return Forbid();
            hocVienId = studentId;
        }
        else if (TeacherRoles.Contains(role))
        {
            var teacherId = await GetCurrentTeacherIdAsync(cancellationToken);
            if (!teacherId.HasValue) return Forbid();
            query = query.Where(x => x.DangKy != null && x.DangKy.LopHoc != null &&
                (x.DangKy.LopHoc.GiaoVienPhuTrachID == teacherId ||
                 x.DangKy.LopHoc.LichHocs!.Any(l => !l.IsDeleted && l.PhanCongDays!.Any(p =>
                     !p.IsDeleted && p.NhanSuID == teacherId && p.TrangThai == "Đã duyệt"))));
        }
        else if (!ManagerRoles.Contains(role)) return Forbid();

        if (hocVienId.HasValue) query = query.Where(x => x.HocVienID == hocVienId.Value);
        if (khoaHocId.HasValue) query = query.Where(x => x.KhoaHocID == khoaHocId.Value);
        trangThai = trangThai?.Trim();
        keyword = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(trangThai)) query = query.Where(x => x.TrangThai == trangThai);
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.HocVien != null && x.HocVien.HoTen.Contains(keyword));

        const int pageSize = 10;
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var pageNumber = Math.Max(page ?? 1, 1);
        if (totalPages > 0) pageNumber = Math.Min(pageNumber, totalPages);
        var items = await query.OrderByDescending(x => x.NgayLap).ThenByDescending(x => x.CamKetID)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.FilterHocVienId = hocVienId;
        ViewBag.FilterKhoaHocId = khoaHocId;
        ViewBag.FilterTrangThai = trangThai ?? "";
        ViewBag.FilterKeyword = keyword ?? "";
        ViewBag.HasFilter = hocVienId.HasValue || khoaHocId.HasValue ||
            !string.IsNullOrWhiteSpace(trangThai) || !string.IsNullOrWhiteSpace(keyword);
        ViewBag.UserRole = role;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> ChiTiet(int id, CancellationToken cancellationToken)
    {
        var item = await context.CamKets.AsNoTracking()
            .Include(x => x.HocVien).Include(x => x.KhoaHoc)
            .Include(x => x.DangKy).ThenInclude(x => x!.LopHoc).ThenInclude(x => x!.GiaoVienPhuTrach)
            .Include(x => x.GiaoVienDeXuat).Include(x => x.NguoiDuyet)
            .FirstOrDefaultAsync(x => x.CamKetID == id, cancellationToken);
        if (item == null) return NotFound();
        if (!await CanAccessAsync(item, cancellationToken)) return Forbid();
        return View(item);
    }

    [HttpPost]
    [Authorize(Roles = "Giáo viên,GiaoVien,Giảng viên")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeXuatDiem(int camKetId, decimal diemCuoiKhoa,
        string? ghiChu, CancellationToken cancellationToken)
    {
        if (diemCuoiKhoa is < 0 or > 100)
            return BadRequest(new { success = false, message = "Điểm phải nằm từ 0 đến 100." });
        if ((ghiChu?.Length ?? 0) > 500)
            return BadRequest(new { success = false, message = "Ghi chú không được vượt quá 500 ký tự." });
        var teacherId = await GetCurrentTeacherIdAsync(cancellationToken);
        if (!teacherId.HasValue) return Forbid();
        var item = await context.CamKets.Include(x => x.DangKy).ThenInclude(x => x!.LopHoc)
            .FirstOrDefaultAsync(x => x.CamKetID == camKetId, cancellationToken);
        if (item == null) return NotFound(new { success = false, message = "Không tìm thấy cam kết." });
        if (!await TeacherCanAccessClassAsync(item.DangKy?.LopHocID, teacherId.Value, cancellationToken)) return Forbid();
        if (item.TrangThai != "Chờ duyệt")
            return Conflict(new { success = false, message = "Chỉ có thể đề xuất điểm cho cam kết đang Chờ duyệt." });

        item.DiemCuoiKhoa = diemCuoiKhoa;
        item.GiaoVienDeXuatID = teacherId.Value;
        item.GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim();
        item.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync(cancellationToken);
        return Json(new { success = true, message = "Đã gửi đề xuất điểm." });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,QuanLy,Quản lý")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duyet(int camKetId, bool approved,
        string? ghiChuTuChoi, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Forbid();
        var item = await context.CamKets.Include(x => x.DangKy)
            .FirstOrDefaultAsync(x => x.CamKetID == camKetId, cancellationToken);
        if (item == null) return NotFound(new { success = false, message = "Không tìm thấy cam kết." });
        if (item.TrangThai != "Chờ duyệt")
            return Conflict(new { success = false, message = "Cam kết không còn ở trạng thái Chờ duyệt." });

        item.TrangThai = approved ? "Đã duyệt" : "Từ chối";
        item.NguoiDuyetID = userId;
        item.NgayDuyet = DateTime.Now;
        item.GhiChu = approved ? null : (string.IsNullOrWhiteSpace(ghiChuTuChoi) ? null : ghiChuTuChoi.Trim());
        item.UpdatedAt = DateTime.Now;
        if (approved && item.DangKy != null && item.DiemCuoiKhoa.HasValue)
        {
            item.DangKy.DiemCuoiKhoa = item.DiemCuoiKhoa;
            item.DangKy.KetQua = item.DiemCuoiKhoa >= 50 ? "Đỗ" : "Rớt";
            item.DangKy.UpdatedAt = DateTime.Now;
        }
        await context.SaveChangesAsync(cancellationToken);
        return Json(new { success = true, message = approved ? "Đã duyệt cam kết." : "Đã từ chối cam kết." });
    }

    private async Task<bool> CanAccessAsync(CamKet item, CancellationToken cancellationToken)
    {
        var role = CurrentRole();
        if (ManagerRoles.Contains(role)) return true;
        if (role == "Học viên") return (await GetCurrentStudentIdAsync(cancellationToken)) == item.HocVienID;
        if (!TeacherRoles.Contains(role)) return false;
        var teacherId = await GetCurrentTeacherIdAsync(cancellationToken);
        return teacherId.HasValue && await TeacherCanAccessClassAsync(item.DangKy?.LopHocID, teacherId.Value, cancellationToken);
    }

    private Task<bool> TeacherCanAccessClassAsync(int? classId, int teacherId, CancellationToken cancellationToken)
    {
        if (!classId.HasValue) return Task.FromResult(false);
        return context.LopHocs.AsNoTracking().AnyAsync(x => x.LopHocID == classId && !x.IsDeleted &&
            (x.GiaoVienPhuTrachID == teacherId || x.LichHocs!.Any(l => !l.IsDeleted && l.PhanCongDays!.Any(p =>
                !p.IsDeleted && p.NhanSuID == teacherId && p.TrangThai == "Đã duyệt"))), cancellationToken);
    }
    private string CurrentRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    private async Task<int?> GetCurrentTeacherIdAsync(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return null;
        return await context.NhanSus.AsNoTracking().Where(x => x.NguoiDungID == userId && !x.IsDeleted)
            .Select(x => (int?)x.NhanSuID).FirstOrDefaultAsync(cancellationToken);
    }
    private async Task<int?> GetCurrentStudentIdAsync(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return null;
        return await context.HocViens.AsNoTracking().Where(x => x.NguoiDungID == userId && !x.IsDeleted)
            .Select(x => (int?)x.HocVienID).FirstOrDefaultAsync(cancellationToken);
    }
}
