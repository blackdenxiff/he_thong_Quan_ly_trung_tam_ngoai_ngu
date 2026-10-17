using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using QuanLyTrungTamNgoaiNgu.Models;
using QuanLyTrungTamNgoaiNgu.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    [Authorize]
    public class DiemDanhController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DiemDanhController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /DiemDanh?lopHocId=1&lichHocId=1
        public IActionResult Index(int? lopHocId, int? lichHocId)
        {
            try
            {
                // 1. Đọc Session thông tin đăng nhập
                var role = User.FindFirstValue(ClaimTypes.Role)
                           ?? HttpContext.Session.GetString("UserRole")
                           ?? HttpContext.Session.GetString("Role")
                           ?? "";
                var username = HttpContext.Session.GetString("UserName")
                              ?? HttpContext.Session.GetString("TenDangNhap")
                              ?? HttpContext.Session.GetString("Username")
                              ?? "";

                string normalizedRole = role.Trim().ToLower();
                bool isHocVien = normalizedRole.Contains("học viên") || normalizedRole.Contains("hocvien") || normalizedRole.Contains("student");
                bool isTeacher = normalizedRole.Contains("giáo viên") || normalizedRole.Contains("giaovien");
                int? teacherId = null;
                if (isTeacher && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                    teacherId = _context.NhanSus.Where(x => x.NguoiDungID == userId && !x.IsDeleted)
                        .Select(x => (int?)x.NhanSuID).FirstOrDefault();

                ViewBag.UserRole = role;
                ViewBag.IsHocVien = isHocVien;

                if (isHocVien)
                {
                    // ========================================================
                    // PHÂN QUYỀN HỌC VIÊN: Chỉ xem thông tin của chính mình
                    // ========================================================
                    var user = _context.NguoiDungs.FirstOrDefault(u => u.TenDangNhap.ToLower() == username.ToLower());
                    var hocVien = user != null ? _context.HocViens.FirstOrDefault(hv => hv.NguoiDungID == user.NguoiDungID) : null;

                    if (hocVien == null)
                    {
                        int? sessionHvId = HttpContext.Session.GetInt32("HocVienID");
                        if (sessionHvId.HasValue)
                        {
                            hocVien = _context.HocViens.FirstOrDefault(hv => hv.HocVienID == sessionHvId.Value);
                        }
                    }

                    if (hocVien != null)
                    {
                        // Lấy danh sách lớp mà học viên này đã đăng ký
                        var danhSachLopId = _context.DangKys
                            .Where(dk => dk.HocVienID == hocVien.HocVienID && (dk.IsDeleted == false || dk.IsDeleted == null))
                            .Select(dk => dk.LopHocID)
                            .ToList();

                        var danhSachLop = _context.LopHocs
                            .Where(l => danhSachLopId.Contains(l.LopHocID))
                            .ToList();
                        ViewBag.DanhSachLop = danhSachLop;

                        int selectedLopId = lopHocId ?? (danhSachLop.FirstOrDefault()?.LopHocID ?? 0);
                        ViewBag.SelectedLopId = selectedLopId;

                        // Lấy danh sách lịch/buổi học của lớp đang chọn
                        var danhSachLich = _context.LichHocs
                            .Where(l => l.LopHocID == selectedLopId)
                            .ToList();
                        ViewBag.DanhSachLich = danhSachLich;

                        int selectedLichId = lichHocId ?? (danhSachLich.FirstOrDefault()?.LichHocID ?? 0);
                        ViewBag.SelectedLichId = selectedLichId;

                        // Ép danh sách hiển thị CHỈ CÓ DUY NHẤT học viên này
                        var danhSachHocVien = new List<HocVien> { hocVien };
                        ViewBag.DanhSachHocVien = danhSachHocVien;

                        // Dữ liệu điểm danh buổi học đang chọn
                        var duLieuDiemDanh = _context.DiemDanhs
                            .Where(d => d.LichHocID == selectedLichId && d.HocVienID == hocVien.HocVienID)
                            .ToList();
                        ViewBag.DuLieuDiemDanh = duLieuDiemDanh;

                        // Thống kê chuyên cần của học viên trong toàn bộ lớp học này
                        var listLichOfClass = danhSachLich.Select(s => s.LichHocID).ToList();
                        var allAttendance = _context.DiemDanhs
                            .Where(d => listLichOfClass.Contains(d.LichHocID) && d.HocVienID == hocVien.HocVienID)
                            .ToList();

                        ViewBag.StatTotal = listLichOfClass.Count;
                        ViewBag.StatPresent = allAttendance.Count(d => d.TrangThai == "Có mặt");
                        ViewBag.StatAbsent = allAttendance.Count(d => d.TrangThai == "Vắng");
                        ViewBag.StatLate = allAttendance.Count(d => d.TrangThai == "Đi muộn" || d.TrangThai == "Nghỉ phép" || d.TrangThai == "Có phép");
                    }
                    else
                    {
                        ViewBag.DanhSachLop = new List<LopHoc>();
                        ViewBag.DanhSachLich = new List<LichHoc>();
                        ViewBag.DanhSachHocVien = new List<HocVien>();
                        ViewBag.DuLieuDiemDanh = new List<DiemDanh>();
                    }
                }
                else
                {
                    // ========================================================
                    // Giáo viên chỉ xem lớp phụ trách; quản trị viên xem toàn bộ.
                    // ========================================================
                    var lopQuery = _context.LopHocs.AsNoTracking().Where(x => !x.IsDeleted);
                    if (isTeacher)
                        lopQuery = teacherId.HasValue
                            ? lopQuery.Where(x => x.GiaoVienPhuTrachID == teacherId.Value)
                            : lopQuery.Where(x => false);
                    var danhSachLop = lopQuery.ToList();
                    ViewBag.DanhSachLop = danhSachLop;

                    int selectedLopId = lopHocId ?? (danhSachLop.FirstOrDefault()?.LopHocID ?? 0);
                    ViewBag.SelectedLopId = selectedLopId;

                    var danhSachLichQuery = _context.LichHocs.AsNoTracking()
                        .Where(l => l.LopHocID == selectedLopId);
                    if (isTeacher)
                        danhSachLichQuery = teacherId.HasValue
                            ? danhSachLichQuery.Where(l => l.LopHoc!.GiaoVienPhuTrachID == teacherId.Value ||
                                l.PhanCongDays!.Any(p => p.NhanSuID == teacherId.Value && !p.IsDeleted && p.TrangThai == "Đã duyệt"))
                            : danhSachLichQuery.Where(l => false);
                    var danhSachLich = danhSachLichQuery.ToList();
                    ViewBag.DanhSachLich = danhSachLich;

                    int selectedLichId = lichHocId ?? (danhSachLich.FirstOrDefault()?.LichHocID ?? 0);
                    ViewBag.SelectedLichId = selectedLichId;

                    var listHocVienId = _context.DangKys
                        .Where(dk => dk.LopHocID == selectedLopId && (dk.IsDeleted == false || dk.IsDeleted == null))
                        .Select(dk => dk.HocVienID)
                        .ToList();

                    var danhSachHocVien = _context.HocViens
                        .Where(hv => listHocVienId.Contains(hv.HocVienID))
                        .ToList();
                    ViewBag.DanhSachHocVien = danhSachHocVien;

                    var duLieuDiemDanh = _context.DiemDanhs
                        .Where(d => d.LichHocID == selectedLichId)
                        .ToList();
                    ViewBag.DuLieuDiemDanh = duLieuDiemDanh;
                }

                return View();
            }
            catch (Exception ex)
            {
                return Content("Lỗi tải trang điểm danh: " + ex.Message);
            }
        }

        // Action nhận yêu cầu AJAX cập nhật trạng thái điểm danh
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateAttendance([FromBody] AttendanceUpdateDto model)
        {
            var role = HttpContext.Session.GetString("UserRole")
                       ?? HttpContext.Session.GetString("Role")
                       ?? "";
            string normalizedRole = role.Trim().ToLower();

            // CHẶN HỌC VIÊN CẬP NHẬT ĐIỂM DANH Ở CẤP ĐỘ SERVER
            if (normalizedRole.Contains("học viên") || normalizedRole.Contains("hocvien") || normalizedRole.Contains("student"))
            {
                return Json(new { success = false, message = "Học viên chỉ có quyền xem, không thể tự chỉnh sửa điểm danh!" });
            }

            if (model == null || model.StudentId <= 0 || model.ScheduleId <= 0 ||
                !new[] { "Có mặt", "Đi muộn", "Vắng", "Nghỉ phép", "Chưa điểm danh" }.Contains(model.Status))
            {
                return Json(new { success = false, message = "Dữ liệu điểm danh không hợp lệ!" });
            }

            var schedule = _context.LichHocs.AsNoTracking().FirstOrDefault(x => x.LichHocID == model.ScheduleId && !x.IsDeleted);
            var enrolled = schedule != null && _context.DangKys.Any(x => x.LopHocID == schedule.LopHocID && x.HocVienID == model.StudentId && !x.IsDeleted && x.TrangThai == "Đang học");
            if (!enrolled)
                return Json(new { success = false, message = "Học viên không thuộc lớp của buổi học này." });

            if (User.IsInRole("GiaoVien") || User.IsInRole("Giáo viên"))
            {
                var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdText, out var userId))
                    return Forbid();
                var teacherId = _context.NhanSus.Where(x => x.NguoiDungID == userId && !x.IsDeleted).Select(x => x.NhanSuID).FirstOrDefault();
                var assigned = teacherId > 0 && _context.LichHocs.Any(x => x.LichHocID == model.ScheduleId &&
                    (x.LopHoc!.GiaoVienPhuTrachID == teacherId ||
                     x.PhanCongDays!.Any(p => p.NhanSuID == teacherId && !p.IsDeleted && p.TrangThai == "Đã duyệt")));
                if (!assigned) return Forbid();
            }

            try
            {
                var diemDanhItem = _context.DiemDanhs
                    .FirstOrDefault(d => d.LichHocID == model.ScheduleId && d.HocVienID == model.StudentId);

                if (diemDanhItem == null)
                {
                    var diemDanhMoi = new DiemDanh
                    {
                        LichHocID = model.ScheduleId,
                        HocVienID = model.StudentId,
                        TrangThai = model.Status,
                        GhiChu = model.Note
                    };
                    _context.DiemDanhs.Add(diemDanhMoi);
                }
                else
                {
                    diemDanhItem.TrangThai = model.Status;
                    diemDanhItem.GhiChu = model.Note;
                }

                _context.SaveChanges();

                return Json(new { success = true, message = "Đã lưu điểm danh thành công!" });
            }
            catch (Exception ex)
            {
                string err = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Lỗi lưu SQL: " + err });
            }
        }
    }

    public class AttendanceUpdateDto
    {
        public int ScheduleId { get; set; }
        public int StudentId { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
    }
}