using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Models;
using QuanLyTrungTamNgoaiNgu.Data;
using System;
using System.Linq;

namespace QLTrungTamTiengAnh_Web.Controllers
{
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
                // 1. Lấy danh sách tất cả lớp học từ SQL
                var danhSachLop = _context.LopHocs.ToList();
                ViewBag.DanhSachLop = danhSachLop;

                // Xác định lớp đang chọn (mặc định lấy lớp đầu tiên)
                int selectedLopId = lopHocId ?? (danhSachLop.FirstOrDefault()?.LopHocID ?? 0);
                ViewBag.SelectedLopId = selectedLopId;

                // 2. Lấy danh sách lịch học (buổi học) CHỈ THUỘC LỚP ĐƯỢC CHỌN
                var danhSachLich = _context.LichHocs
                    .Where(l => l.LopHocID == selectedLopId)
                    .ToList();
                ViewBag.DanhSachLich = danhSachLich;

                int selectedLichId = lichHocId ?? (danhSachLich.FirstOrDefault()?.LichHocID ?? 0);
                ViewBag.SelectedLichId = selectedLichId;

                // 3. Lấy danh sách học viên THUỘC LỚP ĐÓ thông qua bảng trung gian DangKy
                var listHocVienId = _context.DangKys
                    .Where(dk => dk.LopHocID == selectedLopId)
                    .Select(dk => dk.HocVienID)
                    .ToList();

                var danhSachHocVien = _context.HocViens
                    .Where(hv => listHocVienId.Contains(hv.HocVienID))
                    .ToList();
                ViewBag.DanhSachHocVien = danhSachHocVien;

                // 4. Lấy dữ liệu điểm danh cũ của buổi học này (nếu có)
                var duLieuDiemDanh = _context.DiemDanhs
                    .Where(d => d.LichHocID == selectedLichId)
                    .ToList();
                ViewBag.DuLieuDiemDanh = duLieuDiemDanh;

                return View();
            }
            catch (Exception ex)
            {
                return Content("Lỗi tải trang điểm danh: " + ex.Message);
            }
        }

        // Action nhận yêu cầu AJAX cập nhật trạng thái điểm danh và lưu vào SQL Server
        [HttpPost]
        public IActionResult UpdateAttendance([FromBody] AttendanceUpdateDto model)
        {
            if (model == null || model.StudentId <= 0 || model.ScheduleId <= 0)
            {
                return Json(new { success = false, message = "Dữ liệu điểm danh không hợp lệ!" });
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