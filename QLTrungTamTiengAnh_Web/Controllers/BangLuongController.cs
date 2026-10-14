using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Models;
using QuanLyTrungTamNgoaiNgu.Data;
using System;
using System.Linq;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class BangLuongController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BangLuongController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /BangLuong
        public IActionResult Index()
        {
            return View();
        }

        // API: Lấy toàn bộ danh sách bảng lương và danh sách nhân sự từ SQL Server
        [HttpGet]
        public IActionResult LayDanhSachBangLuong()
        {
            try
            {
                var danhSachLuong = _context.BangLuongs
                    .Include(b => b.NhanSu)
                    .OrderByDescending(b => b.BangLuongID)
                    .Select(b => new
                    {
                        bangLuongId = b.BangLuongID,
                        nhanVienId = b.NhanSuID,
                        hoTen = b.NhanSu != null ? b.NhanSu.HoTen : "Không rõ",
                        chucVu = "Nhân sự trung tâm",
                        thangNam = b.ThangNam,
                        luongCoBan = b.LuongCoBan ?? 0,
                        soGioDay = b.SoGioDay ?? 0,
                        phuCap = b.PhuCap,
                        thuong = b.Thuong,
                        khauTru = b.KhauTru,
                        thucLinh = b.ThucLinh,
                        trangThai = b.TrangThai ?? "Tạm tính"
                    })
                    .ToList();

                var danhSachNhanSu = _context.NhanSus
                    .Select(n => new
                    {
                        nhanVienId = n.NhanSuID,
                        hoTen = n.HoTen ?? "Chưa rõ",
                        luongCoBan = n.LuongCoBan ?? 0
                    })
                    .ToList();

                return Json(new { success = true, data = danhSachLuong, nhanSuList = danhSachNhanSu });
            }
            catch (Exception ex)
            {
                string err = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Lỗi tải dữ liệu: " + err });
            }
        }

        // POST: /BangLuong/LuuBangLuong (Lưu phiếu lương mới từ giao diện xuống SQL)
        [HttpPost]
        public IActionResult LuuBangLuong([FromBody] BangLuongRequestDto model)
        {
            if (model == null || model.NhanVienId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn nhân sự hợp lệ!" });
            }

            try
            {
                decimal thucLinh = model.LuongCoBan + (model.SoGioDay * model.DonGiaGio) + model.PhuCap + model.Thuong - model.KhauTru;

                var bangLuongMoi = new BangLuong
                {
                    NhanSuID = model.NhanVienId,
                    ThangNam = model.ThangNam ?? "10/2026",
                    LuongCoBan = model.LuongCoBan,
                    SoGioDay = model.SoGioDay,
                    PhuCap = model.PhuCap,
                    Thuong = model.Thuong,
                    KhauTru = model.KhauTru,
                    ThucLinh = thucLinh,
                    TrangThai = "Chờ duyệt"
                };

                _context.BangLuongs.Add(bangLuongMoi);
                _context.SaveChanges();

                return Json(new { success = true, message = "Lập và lưu bảng lương thành công vào CSDL!" });
            }
            catch (Exception ex)
            {
                string err = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Lỗi lưu SQL: " + err });
            }
        }

        // POST: /BangLuong/ChiTraLuong (Giải ngân lương)
        [HttpPost]
        public IActionResult ChiTraLuong(int bangLuongId)
        {
            if (bangLuongId <= 0)
            {
                return Json(new { success = false, message = "Mã bảng lương không hợp lệ!" });
            }

            try
            {
                var item = _context.BangLuongs.Find(bangLuongId);
                if (item == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy phiếu lương!" });
                }

                item.TrangThai = "Đã chi trả";
                _context.SaveChanges();

                return Json(new { success = true, message = "Đã xác nhận giải ngân lương thành công!" });
            }
            catch (Exception ex)
            {
                string err = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Lỗi SQL: " + err });
            }
        }
    }

    public class BangLuongRequestDto
    {
        public int NhanVienId { get; set; }
        public string ThangNam { get; set; } = "10/2026";
        public decimal LuongCoBan { get; set; }
        public decimal SoGioDay { get; set; }
        public decimal DonGiaGio { get; set; }
        public decimal PhuCap { get; set; }
        public decimal Thuong { get; set; }
        public decimal KhauTru { get; set; }
    }
}