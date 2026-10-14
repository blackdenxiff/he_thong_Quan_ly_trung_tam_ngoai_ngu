using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        // GET: /LichHoc
        public IActionResult Index()
        {
            try
            {
                // 1. Lấy danh sách lịch học từ SQL, bao gồm thông tin Lớp học, Giảng viên và Phòng học
                var danhSachLich = _context.LichHocs
                    .Include(l => l.LopHoc)
                        .ThenInclude(lp => lp.GiaoVienPhuTrach)
                    .Include(l => l.PhongHoc)
                    .ToList();

                ViewBag.DanhSachLich = danhSachLich;

                // 2. Lấy danh sách lớp học để hiển thị trong Modal Xếp lịch mới
                var danhSachLop = _context.LopHocs
                    .Include(lp => lp.GiaoVienPhuTrach)
                    .ToList();
                ViewBag.DanhSachLop = danhSachLop;

                // 3. Lấy danh sách phòng học từ SQL
                var danhSachPhong = _context.PhongHocs.ToList();
                ViewBag.DanhSachPhong = danhSachPhong;

                return View();
            }
            catch (Exception ex)
            {
                return Content("Lỗi tải thời khóa biểu từ SQL: " + ex.Message);
            }
        }

        // POST: /LichHoc/XoaLich (AJAX xóa lịch học và các điểm danh liên quan khỏi SQL)
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

                // Xóa các bản ghi điểm danh liên quan trước để tránh lỗi ràng buộc khóa ngoại (FK)
                var danhSachDiemDanh = _context.DiemDanhs.Where(d => d.LichHocID == model.LichHocId).ToList();
                if (danhSachDiemDanh.Any())
                {
                    _context.DiemDanhs.RemoveRange(danhSachDiemDanh);
                }

                // Tiến hành xóa lịch học
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

        // POST: /LichHoc/XepLich (AJAX lưu ca học mới vào SQL Server & kiểm tra trùng phòng + trùng lớp)
        [HttpPost]
        // POST: /LichHoc/XepLich (AJAX lưu ca học mới vào SQL Server & kiểm tra trùng phòng + trùng lớp)
        // POST: /LichHoc/XepLich (AJAX lưu ca học mới vào SQL Server)
        [HttpPost]
        public IActionResult XepLich([FromBody] XepLichDto model)
        {
            if (model == null || model.LopHocId <= 0 || string.IsNullOrWhiteSpace(model.PhongHoc))
            {
                return Json(new { success = false, message = "Vui lòng điền đầy đủ thông tin xếp lịch!" });
            }

            try
            {
                DateTime ngayHoc = GetDateByDayOfWeek(model.ThuTrongTuan);

                // Kiểm tra linh hoạt chuỗi KhungGio người dùng gửi lên
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

                // 1. Kiểm tra lớp học đã có lịch vào khung giờ đó chưa
                bool isClassConflict = _context.LichHocs.Any(l => l.LopHocID == model.LopHocId && l.NgayHoc.Date == ngayHoc.Date && l.GioBatDau == gioBatDau);
                if (isClassConflict)
                {
                    return Json(new { success = false, message = "Xung đột lịch: Lớp học này đã có lịch học vào khung giờ này rồi!" });
                }

                // 2. Kiểm tra phòng học có bị chiếm dụng không
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

        private DateTime GetDateByDayOfWeek(string thu)
        {
            DateTime baseDate = new DateTime(2026, 10, 05); // Thứ 2 ngày 05/10/2026
            return thu switch
            {
                "Thứ 2" => baseDate,
                "Thứ 3" => baseDate.AddDays(1),
                "Thứ 4" => baseDate.AddDays(2),
                "Thứ 5" => baseDate.AddDays(3),
                "Thứ 6" => baseDate.AddDays(4),
                "Thứ 7" => baseDate.AddDays(5),
                "Chủ Nhật" => baseDate.AddDays(6),
                _ => baseDate,
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
    }

    public class XoaLichDto
    {
        public int LichHocId { get; set; }
    }
}