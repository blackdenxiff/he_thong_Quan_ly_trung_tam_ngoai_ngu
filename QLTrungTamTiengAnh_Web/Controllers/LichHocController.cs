using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using QuanLyTrungTamNgoaiNgu.Models;
using QuanLyTrungTamNgoaiNgu.Data;
using System;
using System.Linq;

namespace QLTrungTamTiengAnh_Web.Controllers
{
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
                // Lấy thông tin quyền và tên đăng nhập chuẩn từ Session (khi người dùng Login)
                var role = HttpContext.Session.GetString("UserRole") ?? "";
                var username = HttpContext.Session.GetString("UserName") ?? "";

                var today = DateTime.Today;
                DateTime baseMonday = today.AddDays(-(int)today.DayOfWeek + (today.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
                DateTime startMonday = baseMonday.AddDays(weekOffset * 7);
                DateTime endSunday = startMonday.AddDays(6);

                // Khởi tạo câu truy vấn gốc lấy toàn bộ lịch trong tuần
                var query = _context.LichHocs
                    .Include(l => l.LopHoc)
                        .ThenInclude(lp => lp.GiaoVienPhuTrach)
                    .Include(l => l.PhongHoc)
                    .Where(l => l.NgayHoc.Date >= startMonday.Date && l.NgayHoc.Date <= endSunday.Date);

                // ==========================================
                // PHẦN LỌC DỮ LIỆU CHUẨN THEO QUYỀN ĐĂNG NHẬP
                // ==========================================
                if (role == "Admin" || role == "Quản lý" || role == "Nhân viên")
                {
                    // Giữ nguyên query, không lọc gì cả -> Hiển thị FULL lịch
                }
                else if (role == "Học viên" && !string.IsNullOrEmpty(username))
                {
                    var user = _context.NguoiDungs.FirstOrDefault(u => u.TenDangNhap == username);
                    if (user != null)
                    {
                        // Lấy HocVienID từ NguoiDungID
                        var hocVien = _context.HocViens.FirstOrDefault(hv => hv.NguoiDungID == user.NguoiDungID);
                        if (hocVien != null)
                        {
                            // Lấy danh sách ID các lớp mà học viên này đã đăng ký
                            var danhSachLopCuaHocVien = _context.DangKys
                                .Where(dk => dk.HocVienID == hocVien.HocVienID && dk.IsDeleted == false)
                                .Select(dk => dk.LopHocID)
                                .ToList();

                            // Nếu học viên chưa đăng ký lớp nào, ép trả về mảng rỗng
                            if (!danhSachLopCuaHocVien.Any())
                            {
                                query = query.Where(l => false);
                            }
                            else
                            {
                                // Chỉ hiển thị lịch của các lớp có trong danh sách đã đăng ký
                                query = query.Where(l => danhSachLopCuaHocVien.Contains(l.LopHocID));
                            }
                        }
                        else
                        {
                            query = query.Where(l => false); // Lỗi không tìm thấy học viên
                        }
                    }
                    else
                    {
                        query = query.Where(l => false); // Lỗi không tìm thấy tài khoản
                    }
                }
                else if (role == "Giáo viên" && !string.IsNullOrEmpty(username))
                {
                    var user = _context.NguoiDungs.FirstOrDefault(u => u.TenDangNhap == username);
                    if (user != null)
                    {
                        // Lọc đúng theo cột GiaoVienPhuTrachID trong bảng LopHoc
                        query = query.Where(l => l.LopHoc.GiaoVienPhuTrachID == user.NguoiDungID);
                    }
                    else
                    {
                        query = query.Where(l => false);
                    }
                }
                else
                {
                    // Trường hợp Session rỗng hoặc quyền không hợp lệ (Chưa đăng nhập) -> Không cho xem
                    query = query.Where(l => false);
                }
                // ==========================================

                var danhSachLich = query.ToList();

                ViewBag.DanhSachLich = danhSachLich;
                ViewBag.StartMonday = startMonday;
                ViewBag.EndSunday = endSunday;
                ViewBag.WeekOffset = weekOffset;
                ViewBag.UserRole = role;

                // 2. Lấy danh sách lớp học
                var danhSachLop = _context.LopHocs
                    .Include(lp => lp.GiaoVienPhuTrach)
                    .ToList();
                ViewBag.DanhSachLop = danhSachLop;

                // 3. Lấy danh sách phòng học
                var danhSachPhong = _context.PhongHocs.ToList();
                ViewBag.DanhSachPhong = danhSachPhong;

                return View();
            }
            catch (Exception ex)
            {
                return Content("Lỗi tải thời khóa biểu từ SQL: " + ex.Message);
            }
        }

        // POST: /LichHoc/XoaLich
        [HttpPost]
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

                var danhSachDiemDanh = _context.DiemDanhs.Where(d => d.LichHocID == model.LichHocId).ToList();
                if (danhSachDiemDanh.Any())
                {
                    _context.DiemDanhs.RemoveRange(danhSachDiemDanh);
                }

                _context.LichHocs.Remove(lichHoc);
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
        public IActionResult XepLich([FromBody] XepLichDto model)
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
                int phongHocId = phong?.PhongHocID ?? 1;

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
        public string TenLop { get; set; }
        public string GiangVien { get; set; }
        public string ThuTrongTuan { get; set; }
        public string KhungGio { get; set; }
        public string PhongHoc { get; set; }
        public string GhiChu { get; set; }
        public int WeekOffset { get; set; } = 0;
    }

    public class XoaLichDto
    {
        public int LichHocId { get; set; }
    }
}