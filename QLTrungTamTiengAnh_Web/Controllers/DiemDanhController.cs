using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using QuanLyTrungTamNgoaiNgu.Models;
using QuanLyTrungTamNgoaiNgu.Data;
using System;
using System.Collections.Generic;
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
                // 1. Đọc Session thông tin đăng nhập
                var role = HttpContext.Session.GetString("UserRole")
                           ?? HttpContext.Session.GetString("Role")
                           ?? "";
                var username = HttpContext.Session.GetString("UserName")
                              ?? HttpContext.Session.GetString("TenDangNhap")
                              ?? HttpContext.Session.GetString("Username")
                              ?? "";

                string normalizedRole = role.Trim().ToLower();
                bool isHocVien = normalizedRole.Contains("học viên") || normalizedRole.Contains("hocvien") || normalizedRole.Contains("student");

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
                    // PHÂN QUYỀN ADMIN / GIÁO VIÊN / QUẢN LÝ (Full quyền)
                    // ========================================================
                    var danhSachLop = _context.LopHocs.ToList();
                    ViewBag.DanhSachLop = danhSachLop;

                    int selectedLopId = lopHocId ?? (danhSachLop.FirstOrDefault()?.LopHocID ?? 0);
                    ViewBag.SelectedLopId = selectedLopId;

                    var danhSachLich = _context.LichHocs
                        .Where(l => l.LopHocID == selectedLopId)
                        .ToList();
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