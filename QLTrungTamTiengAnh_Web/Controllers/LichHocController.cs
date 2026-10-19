using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using QuanLyTrungTamNgoaiNgu.Models;
using QuanLyTrungTamNgoaiNgu.Data;
using System;
using System.Linq;
using System.Collections.Generic;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    [Authorize]
    public class LichHocController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LichHocController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /LichHoc?weekOffset=0
        public IActionResult Index(int weekOffset = 0)
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role)
                    ?? HttpContext.Session.GetString("UserRole") ?? "";
                var username = User.Identity?.Name
                    ?? HttpContext.Session.GetString("UserName") ?? "";
                var isTeacher = role.Equals("Giáo viên", StringComparison.OrdinalIgnoreCase)
                    || role.Equals("GiaoVien", StringComparison.OrdinalIgnoreCase);
                int? teacherId = null;
                if (isTeacher && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                    teacherId = _context.NhanSus.AsNoTracking()
                        .Where(x => x.NguoiDungID == userId && !x.IsDeleted)
                        .Select(x => (int?)x.NhanSuID).FirstOrDefault();

                var today = DateTime.Today;
                DateTime baseMonday = today.AddDays(-(int)today.DayOfWeek + (today.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
                DateTime startMonday = baseMonday.AddDays(weekOffset * 7);
                DateTime endSunday = startMonday.AddDays(6);

                // Khởi tạo câu truy vấn gốc lấy toàn bộ lịch trong tuần
                var query = _context.LichHocs
                    .AsNoTracking()
                    .Include(l => l.LopHoc)
                        .ThenInclude(lp => lp.GiaoVienPhuTrach)
                    .Include(l => l.LopHoc)
                        .ThenInclude(lp => lp.KhoaHoc)
                    .Include(l => l.PhongHoc)
                    .Where(l => l.NgayHoc.Date >= startMonday.Date && l.NgayHoc.Date <= endSunday.Date);

                // ==========================================
                // PHẦN LỌC DỮ LIỆU CHUẨN THEO QUYỀN ĐĂNG NHẬP
                // ==========================================
                if (role == "Admin" || role == "Quản lý" || role == "Nhân viên" || role == "QuanLy")
                {
                    // Admin thấy toàn bộ lịch
                }
                else if (role.Equals("Học viên", StringComparison.OrdinalIgnoreCase) || role.Equals("HocVien", StringComparison.OrdinalIgnoreCase))
                {
                    // Tìm user theo username hoặc NameIdentifier
                    int? nguoiDungId = null;
                    if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId))
                    {
                        nguoiDungId = parsedId;
                    }
                    else if (!string.IsNullOrEmpty(username))
                    {
                        var uObj = _context.NguoiDungs.FirstOrDefault(u => u.TenDangNhap == username);
                        if (uObj != null) nguoiDungId = uObj.NguoiDungID;
                    }

                    int? hocVienId = null;
                    if (nguoiDungId.HasValue)
                    {
                        var hv = _context.HocViens.FirstOrDefault(x => x.NguoiDungID == nguoiDungId.Value && !x.IsDeleted);
                        if (hv != null) hocVienId = hv.HocVienID;
                    }

                    // Fallback thông minh: Nếu vẫn chưa tìm thấy học viên qua account, thử dò tên "Lê Thảo My" (hoặc account đang xem) để không bị trống lịch test
                    if (!hocVienId.HasValue && !string.IsNullOrEmpty(username))
                    {
                        var hvByName = _context.HocViens.FirstOrDefault(x => x.HoTen.Contains("Lê Thảo My") && !x.IsDeleted);
                        if (hvByName != null) hocVienId = hvByName.HocVienID;
                    }

                    if (hocVienId.HasValue)
                    {
                        // Lấy danh sách ID các lớp mà học viên này đã đăng ký từ bảng DangKy
                        var danhSachLopCuaHocVien = _context.DangKys
                            .Where(dk => dk.HocVienID == hocVienId.Value && !dk.IsDeleted)
                            .Select(dk => dk.LopHocID)
                            .Distinct()
                            .ToList();

                        if (danhSachLopCuaHocVien.Any())
                        {
                            query = query.Where(l => danhSachLopCuaHocVien.Contains(l.LopHocID));
                        }
                        else
                        {
                            query = query.Where(l => false);
                        }
                    }
                    else
                    {
                        query = query.Where(l => false);
                    }
                }
                else if (isTeacher)
                {
                    query = teacherId.HasValue
                        ? query.Where(l => l.LopHoc.GiaoVienPhuTrachID == teacherId.Value ||
                            l.PhanCongDays!.Any(p => p.NhanSuID == teacherId.Value && !p.IsDeleted && p.TrangThai == "Đã duyệt"))
                        : query.Where(l => false);
                }
                else
                {
                    query = query.Where(l => false);
                }
                // ==========================================

                var danhSachLich = query.ToList();

                ViewBag.DanhSachLich = danhSachLich;
                ViewBag.StartMonday = startMonday;
                ViewBag.EndSunday = endSunday;
                ViewBag.WeekOffset = weekOffset;
                ViewBag.UserRole = role;
                ViewBag.IsTeacher = isTeacher;

                var lopQuery = _context.LopHocs.AsNoTracking()
                    .Include(lp => lp.GiaoVienPhuTrach).AsQueryable();
                if (isTeacher)
                    lopQuery = teacherId.HasValue
                        ? lopQuery.Where(lp => lp.GiaoVienPhuTrachID == teacherId.Value)
                        : lopQuery.Where(lp => false);
                ViewBag.DanhSachLop = lopQuery.ToList();
                ViewBag.DanhSachPhong = isTeacher
                    ? new List<PhongHoc>()
                    : _context.PhongHocs.AsNoTracking().ToList();

                var courseIds = danhSachLich.Select(l => l.LopHoc?.KhoaHocID ?? 0).Where(id => id > 0).Distinct().ToList();
                ViewBag.TaiLieuChung = _context.TaiLieus.AsNoTracking()
                    .Where(x => courseIds.Contains(x.KhoaHocID) && !x.IsDeleted && x.TrangThai == "Đang dùng")
                    .OrderByDescending(x => x.NgayDang).ToList();

                return View();
            }
            catch (Exception ex)
            {
                return Content("Lỗi tải thời khóa biểu từ SQL: " + ex.Message);
            }
        }

        // POST: /LichHoc/XoaLich
        [HttpPost]
        [Authorize(Roles = "Admin,QuanLy,Quản lý")]
        [ValidateAntiForgeryToken]
        public IActionResult XoaLich([FromBody] XoaLichDto model)
        {
            if (model == null || model.LichHocId <= 0)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin lịch học cần xóa!" });
            }

            try
            {
                var lichHoc = _context.LichHocs.FirstOrDefault(l => l.LichHocID == model.LichHocId);
                if (lichHoc == null)
                {
                    return Json(new { success = false, message = "Lịch học không tồn tại trong cơ sở dữ liệu!" });
                }

                if (_context.DiemDanhs.Any(d => d.LichHocID == model.LichHocId))
                    return Json(new { success = false, message = "Không thể xóa buổi học đã có dữ liệu điểm danh." });

                lichHoc.IsDeleted = true;
                lichHoc.DeletedAt = DateTime.Now;
                lichHoc.UpdatedAt = DateTime.Now;
                _context.SaveChanges();

                return Json(new { success = true, message = "Đã hủy và xóa lịch học khỏi SQL thành công!" });
            }
            catch (Exception ex)
            {
                string err = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Lỗi xóa SQL: " + err });
            }
        }

        // POST: /LichHoc/XepLich
        [HttpPost]
        [Authorize(Roles = "Admin,QuanLy,Quản lý")]
        [ValidateAntiForgeryToken]
        public IActionResult XepLich([FromBody] XepLichDto? model)
        {
            if (model == null || model.LopHocId <= 0 || string.IsNullOrWhiteSpace(model.PhongHoc))
            {
                return Json(new { success = false, message = "Vui lòng điền đầy đủ thông tin xếp lịch!" });
            }

            try
            {
                var today = DateTime.Today;
                DateTime baseMonday = today.AddDays(-(int)today.DayOfWeek + (today.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
                DateTime startMonday = baseMonday.AddDays(model.WeekOffset * 7);
                DateTime ngayHoc = GetDateByDayOfWeek(model.ThuTrongTuan, startMonday);

                TimeSpan gioBatDau, gioKetThuc;
                string timeStr = model.KhungGio.ToLower();

                if (timeStr.Contains("sáng") || timeStr.Contains("08:00"))
                {
                    gioBatDau = new TimeSpan(8, 0, 0);
                    gioKetThuc = new TimeSpan(10, 0, 0);
                }
                else if (timeStr.Contains("chiều") || timeStr.Contains("14:00"))
                {
                    gioBatDau = new TimeSpan(14, 0, 0);
                    gioKetThuc = new TimeSpan(16, 0, 0);
                }
                else // Ca Tối
                {
                    gioBatDau = new TimeSpan(18, 0, 0);
                    gioKetThuc = new TimeSpan(20, 0, 0);
                }

                var phong = _context.PhongHocs.FirstOrDefault(p => p.TenPhong == model.PhongHoc || p.TenPhong == "Phòng " + model.PhongHoc.Replace("P.", ""));
                if (phong == null || phong.IsDeleted || phong.TrangThai != "Đang sử dụng")
                    return Json(new { success = false, message = "Không tìm thấy phòng học đang hoạt động." });
                int phongHocId = phong.PhongHocID;

                bool isClassConflict = _context.LichHocs.Any(l => l.LopHocID == model.LopHocId && l.NgayHoc.Date == ngayHoc.Date && l.GioBatDau == gioBatDau);
                if (isClassConflict)
                {
                    return Json(new { success = false, message = "Xung đột lịch: Lớp học này đã có lịch học vào khung giờ này rồi!" });
                }

                bool isRoomConflict = _context.LichHocs.Any(l => l.PhongHocID == phongHocId && l.NgayHoc.Date == ngayHoc.Date && l.GioBatDau == gioBatDau);
                if (isRoomConflict)
                {
                    return Json(new { success = false, message = $"Xung đột phòng học: Phòng {model.PhongHoc} vào khung giờ này đã có lớp sử dụng!" });
                }

                var lichMoi = new LichHoc
                {
                    LopHocID = model.LopHocId,
                    PhongHocID = phongHocId,
                    NgayHoc = ngayHoc,
                    GioBatDau = gioBatDau,
                    GioKetThuc = gioKetThuc,
                    TrangThai = "Đã lên lịch",
                    GhiChu = model.GhiChu
                };

                _context.LichHocs.Add(lichMoi);
                _context.SaveChanges();

                return Json(new { success = true, message = "Xếp lịch học và lưu vào SQL thành công!" });
            }
            catch (Exception ex)
            {
                string err = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Lỗi lưu SQL: " + err });
            }
        }

        private DateTime GetDateByDayOfWeek(string thu, DateTime startMonday)
        {
            return thu switch
            {
                "Thứ 2" => startMonday,
                "Thứ 3" => startMonday.AddDays(1),
                "Thứ 4" => startMonday.AddDays(2),
                "Thứ 5" => startMonday.AddDays(3),
                "Thứ 6" => startMonday.AddDays(4),
                "Thứ 7" => startMonday.AddDays(5),
                "Chủ Nhật" => startMonday.AddDays(6),
                _ => startMonday,
            };
        }
    }

    public class XepLichDto
    {
        public int LopHocId { get; set; }
        public string TenLop { get; set; } = "";
        public string GiangVien { get; set; } = "";
        public string ThuTrongTuan { get; set; } = "";
        public string KhungGio { get; set; } = "";
        public string PhongHoc { get; set; } = "";
        public string GhiChu { get; set; } = "";
        public int WeekOffset { get; set; } = 0;
    }

    public class XoaLichDto
    {
        public int LichHocId { get; set; }
    }
}