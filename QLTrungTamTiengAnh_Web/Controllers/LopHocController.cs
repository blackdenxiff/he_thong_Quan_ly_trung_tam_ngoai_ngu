using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    [Authorize(Roles = "Admin,QuanLy,Quản lý,Nhân viên,Giáo viên,GiaoVien")]
    public class LopHocController : Controller
    {
        private static readonly string[] AllowedStatuses = { "Sắp mở", "Đang diễn ra", "Đã kết thúc" };
        private readonly ApplicationDbContext _context;
        public LopHocController(ApplicationDbContext context) => _context = context;
        private bool IsManager() => User.IsInRole("Admin") || User.IsInRole("QuanLy") || User.IsInRole("Quản lý");
        private bool IsTeacher() => User.IsInRole("Giáo viên") || User.IsInRole("GiaoVien");
        private async Task<int?> CurrentTeacherIdAsync() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? await _context.NhanSus.Where(x => x.NguoiDungID == userId && !x.IsDeleted).Select(x => (int?)x.NhanSuID).FirstOrDefaultAsync()
            : null;
        private async Task<int?> CurrentStaffBranchIdAsync()
        {
            if (IsManager()) return null;
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return null;
            return await _context.NhanSus.Where(x => x.NguoiDungID == userId && !x.IsDeleted).Select(x => (int?)x.CoSoID).FirstOrDefaultAsync();
        }
        [HttpGet] public async Task<IActionResult> Index()
        {
            var branchId = await CurrentStaffBranchIdAsync();
            ViewBag.IsTeacher = IsTeacher();
            ViewBag.KhoaHocs = await _context.KhoaHocs.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.TenKhoaHoc).Select(x => new LopHocOptionDto { Id = x.KhoaHocID, Name = x.TenKhoaHoc }).ToListAsync();
            ViewBag.CoSos = await _context.CoSos.AsNoTracking().Where(x => !x.IsDeleted && x.TrangThai && (IsManager() || x.CoSoID == branchId)).OrderBy(x => x.TenCoSo).Select(x => new LopHocOptionDto { Id = x.CoSoID, Name = x.TenCoSo }).ToListAsync();
            ViewBag.GiaoViens = await _context.NhanSus.AsNoTracking().Where(x => !x.IsDeleted && x.TrangThai == "Đang làm" && (IsManager() || x.CoSoID == branchId) && (x.LoaiNhanSu == "Giáo viên" || x.LoaiNhanSu == "Giảng viên")).OrderBy(x => x.HoTen).Select(x => new LopHocOptionDto { Id = x.NhanSuID, Name = x.HoTen }).ToListAsync();
            return View();
        }
        [HttpGet] public async Task<IActionResult> DanhSach(string? keyword = null, int? khoaHocId = null, string? trangThai = null, int page = 1)
        {
            page = Math.Max(page, 1); keyword = (keyword ?? "").Trim(); if (keyword.Length > 100) return BadRequest(new { success = false, message = "Từ khóa không được vượt quá 100 ký tự." });
            var branchId = await CurrentStaffBranchIdAsync();
            var teacherId = IsTeacher() ? await CurrentTeacherIdAsync() : null;
            var query = _context.LopHocs.AsNoTracking().Where(x => !x.IsDeleted &&
                (IsManager() || (IsTeacher() ? x.GiaoVienPhuTrachID == teacherId : x.CoSoID == branchId)))
                .Select(x => new LopHocListItemDto { Id = x.LopHocID, TenLop = x.TenLop, KhoaHocId = x.KhoaHocID, TenKhoaHoc = x.KhoaHoc != null ? x.KhoaHoc.TenKhoaHoc : "", CoSoId = x.CoSoID, TenCoSo = x.CoSo != null ? x.CoSo.TenCoSo : "", GiaoVienId = x.GiaoVienPhuTrachID, GiaoVien = x.GiaoVienPhuTrach != null ? x.GiaoVienPhuTrach.HoTen : "", SiSoToiDa = x.SiSoToiDa, SoDangKy = x.DangKys!.Count(d => !d.IsDeleted), NgayBatDau = x.NgayBatDau, NgayKetThuc = x.NgayKetThuc, TrangThai = x.TrangThai });
            if (keyword.Length > 0) query = query.Where(x => x.TenLop.Contains(keyword) || x.TenKhoaHoc.Contains(keyword) || x.GiaoVien.Contains(keyword) || x.TenCoSo.Contains(keyword));
            if (khoaHocId > 0) query = query.Where(x => x.KhoaHocId == khoaHocId);
            if (!string.IsNullOrWhiteSpace(trangThai) && AllowedStatuses.Contains(trangThai.Trim())) query = query.Where(x => x.TrangThai == trangThai.Trim());
            const int pageSize = 10;
            var total = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
            var items = await query.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return Json(new { success = true, items, page, pageSize, total, totalPages });
        }
        [HttpPost, Authorize(Roles = "Admin,QuanLy,Quản lý"), ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuLopHoc([FromBody] LopHocDto? model)
        {
            if (model == null || model.LopHocId < 0 || string.IsNullOrWhiteSpace(model.TenLop) || model.KhoaHocId <= 0 || model.CoSoId <= 0 || model.GiaoVienPhuTrachId <= 0 || model.SiSoToiDa < 1 || model.SiSoToiDa > 40 || !model.NgayBatDau.HasValue || !model.NgayKetThuc.HasValue || model.NgayKetThuc.Value.Date <= model.NgayBatDau.Value.Date || !AllowedStatuses.Contains(model.TrangThai?.Trim() ?? "")) return BadRequest(new { success = false, message = "Thông tin lớp học không hợp lệ." });
            var branchId = await CurrentStaffBranchIdAsync();
            if (!IsManager() && (branchId == null || model.CoSoId != branchId)) return Forbid();
            if (!await _context.KhoaHocs.AnyAsync(x => x.KhoaHocID == model.KhoaHocId && !x.IsDeleted) || !await _context.CoSos.AnyAsync(x => x.CoSoID == model.CoSoId && !x.IsDeleted && x.TrangThai) || !await _context.NhanSus.AnyAsync(x => x.NhanSuID == model.GiaoVienPhuTrachId && x.CoSoID == model.CoSoId && !x.IsDeleted && x.TrangThai == "Đang làm" && (x.LoaiNhanSu == "Giáo viên" || x.LoaiNhanSu == "Giảng viên"))) return BadRequest(new { success = false, message = "Khóa học, cơ sở hoặc giáo viên không hợp lệ." });
            var entity = model.LopHocId == 0 ? new LopHoc() : await _context.LopHocs.FirstOrDefaultAsync(x => x.LopHocID == model.LopHocId && !x.IsDeleted && (IsManager() || x.CoSoID == branchId));
            if (entity == null) return NotFound(new { success = false, message = "Không tìm thấy lớp học." });
            if (model.SiSoToiDa < await _context.DangKys.CountAsync(x => x.LopHocID == model.LopHocId && !x.IsDeleted && x.TrangThai == "Đang học")) return BadRequest(new { success = false, message = "Sĩ số không thể nhỏ hơn số đăng ký hiện tại." });
            entity.TenLop = model.TenLop.Trim(); entity.KhoaHocID = model.KhoaHocId; entity.CoSoID = model.CoSoId; entity.GiaoVienPhuTrachID = model.GiaoVienPhuTrachId; entity.SiSoToiDa = model.SiSoToiDa; entity.NgayBatDau = model.NgayBatDau.Value.Date; entity.NgayKetThuc = model.NgayKetThuc.Value.Date; entity.TrangThai = model.TrangThai!.Trim(); entity.UpdatedAt = DateTime.Now; if (entity.LopHocID == 0) _context.LopHocs.Add(entity); await _context.SaveChangesAsync(); return Json(new { success = true, message = "Đã lưu lớp học.", id = entity.LopHocID });
        }
        [HttpPost, Authorize(Roles = "Admin,QuanLy,Quản lý"), ValidateAntiForgeryToken] public async Task<IActionResult> XoaLopHoc(int lopHocId)
        {
            var branchId = await CurrentStaffBranchIdAsync();
            var e = await _context.LopHocs.FirstOrDefaultAsync(x => x.LopHocID == lopHocId && !x.IsDeleted && (IsManager() || x.CoSoID == branchId));
            if (e == null) return NotFound(new { success = false, message = "Không tìm thấy lớp học." });
            if (await _context.DangKys.AnyAsync(x => x.LopHocID == lopHocId && !x.IsDeleted)) return Conflict(new { success = false, message = "Không thể xóa lớp học đã có đăng ký." });
            e.IsDeleted = true; e.DeletedAt = DateTime.Now; e.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã xóa lớp học." });
        }
        public sealed class LopHocOptionDto { public int Id { get; set; } public string Name { get; set; } = ""; }
        public sealed class LopHocListItemDto { public int Id { get; set; } public string TenLop { get; set; } = ""; public int KhoaHocId { get; set; } public string TenKhoaHoc { get; set; } = ""; public int CoSoId { get; set; } public string TenCoSo { get; set; } = ""; public int GiaoVienId { get; set; } public string GiaoVien { get; set; } = ""; public int SiSoToiDa { get; set; } public int SoDangKy { get; set; } public DateTime NgayBatDau { get; set; } public DateTime NgayKetThuc { get; set; } public string TrangThai { get; set; } = ""; }
        public sealed class LopHocDto { public int LopHocId { get; set; } public string? TenLop { get; set; } public int KhoaHocId { get; set; } public int CoSoId { get; set; } public int GiaoVienPhuTrachId { get; set; } public int SiSoToiDa { get; set; } public DateTime? NgayBatDau { get; set; } public DateTime? NgayKetThuc { get; set; } public string? TrangThai { get; set; } }
    }
}
