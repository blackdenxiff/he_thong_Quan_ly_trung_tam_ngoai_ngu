using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class HoaDonController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HoaDonController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /HoaDon (Hiển thị danh sách hóa đơn từ CSDL)
        public async Task<IActionResult> Index(string searchString, string statusFilter)
        {
            // 1. Đọc Session xác định quyền và tên đăng nhập
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

            var query = _context.HoaDons
                .Include(h => h.HocVien)
                .Include(h => h.ThanhToans)
                .AsQueryable();

            // 2. Phân quyền Lọc dữ liệu theo đối tượng đăng nhập
            if (isHocVien)
            {
                // Tìm Học Viên tương ứng với tài khoản đăng nhập
                var user = await _context.NguoiDungs.FirstOrDefaultAsync(u => u.TenDangNhap.ToLower() == username.ToLower());
                var hocVien = user != null ? await _context.HocViens.FirstOrDefaultAsync(hv => hv.NguoiDungID == user.NguoiDungID) : null;

                if (hocVien == null)
                {
                    int? sessionHvId = HttpContext.Session.GetInt32("HocVienID");
                    if (sessionHvId.HasValue)
                    {
                        hocVien = await _context.HocViens.FirstOrDefaultAsync(hv => hv.HocVienID == sessionHvId.Value);
                    }
                }

                if (hocVien != null)
                {
                    // Chỉ lấy danh sách hóa đơn của CHÍNH HỌC VIÊN ĐÓ
                    query = query.Where(h => h.HocVienID == hocVien.HocVienID);
                }
                else
                {
                    // Không tìm thấy thông tin học viên -> Trả về danh sách rỗng
                    query = query.Where(h => false);
                }
            }

            // 3. Xử lý tìm kiếm theo từ khóa
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(h => h.HoaDonID.ToString().Contains(searchString)
                                      || (h.HocVien != null && h.HocVien.HoTen.Contains(searchString))
                                      || (h.HocVien != null && h.HocVien.SoDienThoai.Contains(searchString)));
            }

            // 4. Lọc theo trạng thái thanh toán
            if (!string.IsNullOrEmpty(statusFilter))
            {
                if (statusFilter == "Đã thu đủ" || statusFilter == "Đã thanh toán")
                {
                    query = query.Where(h => h.TrangThai == "Đã thu đủ" || h.TrangThai == "Đã thanh toán");
                }
                else if (statusFilter == "Chưa đủ")
                {
                    query = query.Where(h => h.TrangThai != "Đã thu đủ" && h.TrangThai != "Đã thanh toán");
                }
            }

            var listHoaDon = await query.OrderByDescending(h => h.NgayLap).ToListAsync();

            // Lấy danh sách học viên đổ vào Modal lập hóa đơn (Chỉ dành cho Admin / Nhân viên)
            if (!isHocVien)
            {
                ViewBag.DanhSachHocVien = await _context.HocViens
                    .OrderBy(hv => hv.HoTen)
                    .ToListAsync();
            }

            return View(listHoaDon);
        }

        // POST: /HoaDon/LapHoaDon (Lập hóa đơn mới - Chặn Học viên)
        [HttpPost]
        public IActionResult LapHoaDon([FromBody] HoaDonRequestDto model)
        {
            var role = HttpContext.Session.GetString("UserRole") ?? HttpContext.Session.GetString("Role") ?? "";
            string normalizedRole = role.Trim().ToLower();
            if (normalizedRole.Contains("học viên") || normalizedRole.Contains("hocvien") || normalizedRole.Contains("student"))
            {
                return Json(new { success = false, message = "Học viên không có quyền phát hành hóa đơn!" });
            }

            if (model == null || model.HocVienId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn học viên hợp lệ!" });
            }

            if (model.GiamGia < 0 || model.GiamGia > model.TongTien)
            {
                return Json(new { success = false, message = "Số tiền giảm giá không hợp lệ (phải từ 0 đến tổng tiền)!" });
            }

            try
            {
                decimal tongPhaiTra = model.TongTien - model.GiamGia;

                var hoaDonMoi = new HoaDon
                {
                    HocVienID = model.HocVienId,
                    DangKyID = model.DangKyId > 0 ? model.DangKyId : (int?)null,
                    NhanSuLapID = 1,
                    NgayLap = DateTime.Now,
                    TongTien = model.TongTien,
                    GiamGia = model.GiamGia,
                    TongPhaiTra = tongPhaiTra,
                    TrangThai = "Chưa đủ",
                    GhiChu = model.GhiChu ?? ""
                };

                _context.HoaDons.Add(hoaDonMoi);
                _context.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "Phát hành hóa đơn học phí thành công và đã lưu vào CSDL!",
                    data = new
                    {
                        HoaDonId = hoaDonMoi.HoaDonID,
                        model.HocVienId,
                        model.DangKyId,
                        model.TongTien,
                        model.GiamGia,
                        tongPhaiTra,
                        GhiChu = hoaDonMoi.GhiChu
                    }
                });
            }
            catch (Exception ex)
            {
                string errMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Lỗi SQL: " + errMessage });
            }
        }

        // POST: /HoaDon/ThuTien (Ghi nhận đợt thanh toán - Chặn Học viên)
        [HttpPost]
        public IActionResult ThuTien([FromBody] ThanhToanRequestDto model)
        {
            var role = HttpContext.Session.GetString("UserRole") ?? HttpContext.Session.GetString("Role") ?? "";
            string normalizedRole = role.Trim().ToLower();
            if (normalizedRole.Contains("học viên") || normalizedRole.Contains("hocvien") || normalizedRole.Contains("student"))
            {
                return Json(new { success = false, message = "Học viên không có quyền thực hiện chức năng thu tiền!" });
            }

            if (model == null || model.HoaDonId <= 0)
            {
                return Json(new { success = false, message = "Mã hóa đơn không hợp lệ!" });
            }

            if (model.SoTien <= 0)
            {
                return Json(new { success = false, message = "Số tiền thanh toán phải lớn hơn 0 đồng!" });
            }

            try
            {
                var hoaDon = _context.HoaDons
                    .Include(h => h.ThanhToans)
                    .FirstOrDefault(h => h.HoaDonID == model.HoaDonId);

                if (hoaDon == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy hóa đơn trong cơ sở dữ liệu!" });
                }

                decimal daThuCu = hoaDon.ThanhToans != null ? hoaDon.ThanhToans.Sum(t => t.SoTien) : 0;
                decimal tongDaThuMoi = daThuCu + model.SoTien;
                decimal conLai = hoaDon.TongPhaiTra - tongDaThuMoi;

                if (tongDaThuMoi > hoaDon.TongPhaiTra)
                {
                    return Json(new { success = false, message = "Số tiền thu không được lớn hơn số tiền công nợ còn lại!" });
                }

                var thanhToanMoi = new ThanhToan
                {
                    HoaDonID = model.HoaDonId,
                    NhanSuID = 1,
                    NgayThanhToan = DateTime.Now,
                    SoTien = model.SoTien,
                    PhuongThuc = model.PhuongThuc ?? "Chuyển khoản QR",
                    GhiChu = model.GhiChu ?? ""
                };
                _context.ThanhToans.Add(thanhToanMoi);

                if (conLai <= 0)
                {
                    hoaDon.TrangThai = "Đã thu đủ";
                }
                else
                {
                    hoaDon.TrangThai = "Chưa đủ";
                }

                _context.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = $"Đã xác nhận thu {model.SoTien:N0} đ thành công và cập nhật vào CSDL!",
                    trangThaiMoi = hoaDon.TrangThai,
                    daThuMoi = tongDaThuMoi,
                    conLaiMoi = conLai > 0 ? conLai : 0
                });
            }
            catch (Exception ex)
            {
                string errMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Lỗi SQL: " + errMessage });
            }
        }

        public class HoaDonRequestDto
        {
            public int HocVienId { get; set; }
            public int? DangKyId { get; set; }
            public decimal TongTien { get; set; }
            public decimal GiamGia { get; set; }
            public string? GhiChu { get; set; }
        }

        public class ThanhToanRequestDto
        {
            public int HoaDonId { get; set; }
            public decimal SoTien { get; set; }
            public string? PhuongThuc { get; set; }
            public string? GhiChu { get; set; }
        }
    }
}