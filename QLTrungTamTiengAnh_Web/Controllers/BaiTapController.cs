using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;

namespace QLTrungTamTiengAnh_Web.Controllers;

[Authorize]
public sealed class BaiTapController(ApplicationDbContext context, IWebHostEnvironment environment) : Controller
{
    private static readonly HashSet<string> TeacherRoles = ["Giáo viên", "GiaoVien", "Giảng viên"];
    private static readonly HashSet<string> ManagerRoles = ["Admin", "QuanLy", "Quản lý"];
    private static readonly HashSet<string> AllowedExtensions = [".pdf", ".docx", ".doc", ".zip", ".rar"];
    private const long MaxFileSize = 15 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> NopBai(int? baiTapId, CancellationToken cancellationToken)
    {
        var role = CurrentRole();
        if (TeacherRoles.Contains(role)) return RedirectToAction(nameof(ChamBai));
        if (role != "Học viên") return Forbid();

        var student = await GetCurrentStudentAsync(cancellationToken);
        if (student == null) return Forbid();
        var allowedAssignments = context.BaiTaps.AsNoTracking().Where(x => !x.IsDeleted && x.LopHoc != null &&
            x.LopHoc.DangKys!.Any(d => d.HocVienID == student.HocVienID && !d.IsDeleted && d.TrangThai == "Đang học"));
        var assignment = baiTapId.HasValue
            ? await allowedAssignments.Include(x => x.LopHoc).FirstOrDefaultAsync(x => x.BaiTapID == baiTapId, cancellationToken)
            : await allowedAssignments.Include(x => x.LopHoc).OrderByDescending(x => x.NgayGiao).FirstOrDefaultAsync(cancellationToken);
        if (baiTapId.HasValue && assignment == null) return Forbid();

        ViewBag.HocVienCurrent = student;
        ViewBag.BaiTapCurrent = assignment;
        var submissions = assignment == null
            ? []
            : await context.ChiTietNopBais.AsNoTracking()
                .Where(x => x.BaiTapID == assignment.BaiTapID && x.HocVienID == student.HocVienID && !x.IsDeleted)
                .OrderByDescending(x => x.LanNop).ToListAsync(cancellationToken);
        return View(submissions);
    }

    [HttpPost]
    [Authorize(Roles = "Học viên")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBaiTap(int baiTapId, IFormFile? fileNop, CancellationToken cancellationToken)
    {
        var student = await GetCurrentStudentAsync(cancellationToken);
        if (student == null) return Forbid();
        if (fileNop == null || fileNop.Length == 0)
            return BadRequest(new { success = false, message = "Vui lòng chọn tệp bài làm để nộp." });
        if (fileNop.Length > MaxFileSize)
            return BadRequest(new { success = false, message = "Dung lượng tệp vượt quá 15 MB." });
        var extension = Path.GetExtension(fileNop.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return BadRequest(new { success = false, message = "Chỉ chấp nhận PDF, Word, ZIP hoặc RAR." });

        var assignment = await context.BaiTaps.AsNoTracking().FirstOrDefaultAsync(x =>
            x.BaiTapID == baiTapId && !x.IsDeleted && x.TrangThai == "Đang mở" && x.LopHoc != null &&
            x.LopHoc.DangKys!.Any(d => d.HocVienID == student.HocVienID && !d.IsDeleted && d.TrangThai == "Đang học"), cancellationToken);
        if (assignment == null) return Forbid();

        var nextAttempt = (await context.ChiTietNopBais.Where(x =>
            x.BaiTapID == baiTapId && x.HocVienID == student.HocVienID && !x.IsDeleted)
            .MaxAsync(x => (int?)x.LanNop, cancellationToken) ?? 0) + 1;
        var storageName = $"{Guid.NewGuid():N}{extension}";
        var storageDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "BaiTap");
        Directory.CreateDirectory(storageDirectory);
        var physicalPath = Path.Combine(storageDirectory, storageName);
        await using (var stream = System.IO.File.Create(physicalPath))
            await fileNop.CopyToAsync(stream, cancellationToken);

        var submission = new ChiTietNopBai
        {
            BaiTapID = baiTapId, HocVienID = student.HocVienID, LanNop = nextAttempt,
            DuongDan = "private:" + storageName, ThoiGianNop = DateTime.Now
        };
        context.ChiTietNopBais.Add(submission);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch { System.IO.File.Delete(physicalPath); throw; }
        return Json(new { success = true, message = $"Đã nộp bài lần {nextAttempt}.", id = submission.ChiTietNopBaiID });
    }

    [HttpGet]
    [Authorize(Roles = "Giáo viên,GiaoVien,Giảng viên")]
    public async Task<IActionResult> ChamBai(int? lopHocId, int? baiTapId, int? hocVienId,
        string? trangThai, int page = 1, CancellationToken cancellationToken = default)
    {
        var teacherId = await GetCurrentTeacherIdAsync(cancellationToken);
        if (!teacherId.HasValue) return Forbid();
        const int pageSize = 10;
        page = Math.Max(page, 1);
        var allowedClassIds = AllowedClassIds(teacherId.Value);
        var query = context.ChiTietNopBais.AsNoTracking()
            .Where(x => !x.IsDeleted && x.BaiTap != null && allowedClassIds.Contains(x.BaiTap.LopHocID));
        if (lopHocId.HasValue) query = query.Where(x => x.BaiTap!.LopHocID == lopHocId);
        if (baiTapId.HasValue) query = query.Where(x => x.BaiTapID == baiTapId);
        if (hocVienId.HasValue) query = query.Where(x => x.HocVienID == hocVienId);
        if (trangThai == "da-cham") query = query.Where(x => x.Diem != null);
        if (trangThai == "chua-cham") query = query.Where(x => x.Diem == null);

        var total = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        if (totalPages > 0) page = Math.Min(page, totalPages);
        var entities = await query.OrderBy(x => x.Diem != null).ThenByDescending(x => x.ThoiGianNop)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(x => x.HocVien).Include(x => x.BaiTap)!.ThenInclude(x => x.LopHoc)
            .Include(x => x.NguoiCham).ToListAsync(cancellationToken);
        var rows = entities.Select(x => new BaiTapSubmissionRow
        {
            ChiTietNopBaiId = x.ChiTietNopBaiID, HocVienId = x.HocVienID,
            MaHocVien = $"HV-{x.HocVienID:D3}", HocVien = x.HocVien?.HoTen ?? "Học viên không còn tồn tại",
            BaiTap = x.BaiTap?.TieuDe ?? "Bài tập không còn tồn tại", LopHoc = x.BaiTap?.LopHoc?.TenLop ?? "Lớp không còn tồn tại",
            LanNop = x.LanNop, ThoiGianNop = x.ThoiGianNop,
            TenFile = string.IsNullOrWhiteSpace(x.DuongDan) ? "Không có tệp" : Path.GetFileName(x.DuongDan.Replace("private:", "")),
            Diem = x.Diem, NhanXet = x.NhanXet, NguoiCham = x.NguoiCham?.HoTen, NgayCham = x.NgayCham
        }).ToList();

        var classes = await context.LopHocs.AsNoTracking().Where(x => allowedClassIds.Contains(x.LopHocID))
            .OrderBy(x => x.TenLop).Select(x => new SelectOption(x.LopHocID, x.TenLop)).ToListAsync(cancellationToken);
        var assignments = await context.BaiTaps.AsNoTracking().Where(x => allowedClassIds.Contains(x.LopHocID) && (!lopHocId.HasValue || x.LopHocID == lopHocId))
            .OrderBy(x => x.TieuDe).Select(x => new SelectOption(x.BaiTapID, x.TieuDe)).ToListAsync(cancellationToken);
        var studentRows = await context.DangKys.AsNoTracking().Where(x => allowedClassIds.Contains(x.LopHocID) && !x.IsDeleted && x.TrangThai == "Đang học" && (!lopHocId.HasValue || x.LopHocID == lopHocId))
            .Select(x => new { x.HocVienID, Name = x.HocVien != null ? x.HocVien.HoTen : "Học viên không còn tồn tại" }).Distinct().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var students = studentRows.Select(x => new SelectOption(x.HocVienID, x.Name)).ToList();

        return View(new ChamBaiViewModel
        {
            Items = rows, Classes = classes, Assignments = assignments, Students = students,
            LopHocId = lopHocId, BaiTapId = baiTapId, HocVienId = hocVienId, TrangThai = trangThai ?? "",
            Page = page, TotalPages = totalPages, TotalItems = total
        });
    }

    [HttpPost]
    [Authorize(Roles = "Giáo viên,GiaoVien,Giảng viên")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LuuDiem([FromBody] GradeSubmissionRequest? request, CancellationToken cancellationToken)
    {
        if (request == null || request.ChiTietNopBaiId <= 0 || !request.Diem.HasValue || request.Diem is < 0 or > 100)
            return BadRequest(new { success = false, message = "Điểm phải nằm trong khoảng 0 đến 100." });
        if ((request.NhanXet?.Length ?? 0) > 500)
            return BadRequest(new { success = false, message = "Nhận xét không được vượt quá 500 ký tự." });
        var teacherId = await GetCurrentTeacherIdAsync(cancellationToken);
        if (!teacherId.HasValue) return Forbid();
        var submission = await context.ChiTietNopBais.Include(x => x.BaiTap)
            .FirstOrDefaultAsync(x => x.ChiTietNopBaiID == request.ChiTietNopBaiId && !x.IsDeleted, cancellationToken);
        if (submission?.BaiTap == null) return NotFound(new { success = false, message = "Không tìm thấy lần nộp bài." });
        if (!await AllowedClassIds(teacherId.Value).AnyAsync(x => x == submission.BaiTap.LopHocID, cancellationToken)) return Forbid();

        submission.Diem = request.Diem.Value;
        submission.NhanXet = string.IsNullOrWhiteSpace(request.NhanXet) ? null : request.NhanXet.Trim();
        submission.NguoiChamID = teacherId.Value;
        submission.NgayCham = DateTime.Now;
        submission.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync(cancellationToken);
        return Json(new { success = true, message = "Đã lưu kết quả chấm bài.", ngayCham = submission.NgayCham });
    }

    [HttpGet]
    public async Task<IActionResult> TaiBaiNop(int id, CancellationToken cancellationToken)
    {
        var submission = await context.ChiTietNopBais.AsNoTracking().Include(x => x.BaiTap)
            .FirstOrDefaultAsync(x => x.ChiTietNopBaiID == id && !x.IsDeleted, cancellationToken);
        if (submission?.BaiTap == null || string.IsNullOrWhiteSpace(submission.DuongDan)) return NotFound();
        var role = CurrentRole();
        var allowed = ManagerRoles.Contains(role);
        if (role == "Học viên") allowed = (await GetCurrentStudentAsync(cancellationToken))?.HocVienID == submission.HocVienID;
        else if (TeacherRoles.Contains(role))
        {
            var teacherId = await GetCurrentTeacherIdAsync(cancellationToken);
            allowed = teacherId.HasValue && await AllowedClassIds(teacherId.Value).AnyAsync(x => x == submission.BaiTap.LopHocID, cancellationToken);
        }
        if (!allowed) return Forbid();
        var physicalPath = ResolveSubmissionPath(submission.DuongDan);
        if (physicalPath == null || !System.IO.File.Exists(physicalPath)) return NotFound();
        var contentType = Path.GetExtension(physicalPath).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf", ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".zip" => "application/zip", ".rar" => "application/vnd.rar", _ => "application/octet-stream"
        };
        return PhysicalFile(physicalPath, contentType, Path.GetFileName(physicalPath));
    }

    private IQueryable<int> AllowedClassIds(int teacherId) => context.LopHocs.AsNoTracking()
        .Where(x => !x.IsDeleted && (x.GiaoVienPhuTrachID == teacherId || x.LichHocs!.Any(l =>
            !l.IsDeleted && l.PhanCongDays!.Any(p => !p.IsDeleted && p.NhanSuID == teacherId && p.TrangThai == "Đã duyệt"))))
        .Select(x => x.LopHocID);
    private string CurrentRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    private async Task<int?> GetCurrentTeacherIdAsync(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return null;
        return await context.NhanSus.AsNoTracking().Where(x => x.NguoiDungID == userId && !x.IsDeleted)
            .Select(x => (int?)x.NhanSuID).FirstOrDefaultAsync(cancellationToken);
    }
    private async Task<HocVien?> GetCurrentStudentAsync(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return null;
        return await context.HocViens.AsNoTracking().FirstOrDefaultAsync(x => x.NguoiDungID == userId && !x.IsDeleted, cancellationToken);
    }
    private string? ResolveSubmissionPath(string storedPath)
    {
        if (storedPath.StartsWith("private:", StringComparison.Ordinal))
            return Path.Combine(environment.ContentRootPath, "App_Data", "BaiTap", Path.GetFileName(storedPath[8..]));
        return Path.Combine(environment.WebRootPath, "uploads", "baitap", Path.GetFileName(storedPath));
    }
}

public sealed class ChamBaiViewModel
{
    public List<BaiTapSubmissionRow> Items { get; init; } = [];
    public List<SelectOption> Classes { get; init; } = [];
    public List<SelectOption> Assignments { get; init; } = [];
    public List<SelectOption> Students { get; init; } = [];
    public int? LopHocId { get; init; }
    public int? BaiTapId { get; init; }
    public int? HocVienId { get; init; }
    public string TrangThai { get; init; } = "";
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalItems { get; init; }
}
public sealed record SelectOption(int Id, string Name);
public sealed class BaiTapSubmissionRow
{
    public int ChiTietNopBaiId { get; init; }
    public int HocVienId { get; init; }
    public string MaHocVien { get; init; } = "";
    public string HocVien { get; init; } = "";
    public string BaiTap { get; init; } = "";
    public string LopHoc { get; init; } = "";
    public int LanNop { get; init; }
    public DateTime ThoiGianNop { get; init; }
    public string TenFile { get; init; } = "";
    public decimal? Diem { get; init; }
    public string? NhanXet { get; init; }
    public string? NguoiCham { get; init; }
    public DateTime? NgayCham { get; init; }
}
public sealed class GradeSubmissionRequest
{
    public int ChiTietNopBaiId { get; set; }
    public decimal? Diem { get; set; }
    public string? NhanXet { get; set; }
}
