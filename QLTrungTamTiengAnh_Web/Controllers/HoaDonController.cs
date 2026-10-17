using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    [Authorize(Roles = "Admin,QuanLy,Quản lý,Nhân viên,Học viên")]
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
                .Include(h => h.DangKy!).ThenInclude(d => d!.LopHoc)
                .Include(h => h.ThanhToans)
                .AsQueryable();

            // 2. Phân quyền và phạm vi dữ liệu theo tài khoản đăng nhập
            if (User.IsInRole("Nhân viên"))
            {
                var branchId = await CurrentStaffBranchIdAsync();
                query = branchId.HasValue
                    ? query.Where(h => h.DangKy != null && h.DangKy.LopHoc != null && h.DangKy.LopHoc.CoSoID == branchId.Value)
                    : query.Where(h => false);
            }
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
                var invoiceBranchId = await CurrentStaffBranchIdAsync();
                ViewBag.DanhSachHocVien = await _context.DangKys
                .AsNoTracking().Include(d => d.HocVien).Include(d => d.LopHoc)
                .Where(d => !d.IsDeleted && d.TrangThai == "Đang học" && d.HocVien != null && d.LopHoc != null)
                .Where(d => !User.IsInRole("Nhân viên") || d.LopHoc!.CoSoID == invoiceBranchId)
                .OrderBy(d => d.HocVien!.HoTen)
                .Select(d => new HoaDonStudentOptionDto { HocVienId = d.HocVienID, HoTen = d.HocVien!.HoTen, SoDienThoai = d.HocVien.SoDienThoai, LopHoc = d.LopHoc!.TenLop, HocPhi = d.HocPhiApDung, DangKyId = d.DangKyID })
                .ToListAsync();
            }

            return View(listHoaDon);
        }

        // POST: /HoaDon/LapHoaDon (Lập hóa đơn mới - Chặn Học viên)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> LapHoaDon([FromBody] HoaDonRequestDto? model)
        {
            if (User.IsInRole("Học viên"))
                return Forbid();

            if (model == null || model.HocVienId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn học viên hợp lệ!" });
            }

            if (model.GiamGia < 0 || model.GiamGia > model.TongTien)
            {
                return Json(new { success = false, message = "Số tiền giảm giá không hợp lệ (phải từ 0 đến tổng tiền)!" });
            }

            var staffId = await CurrentStaffIdAsync();
            if (!staffId.HasValue) return Forbid();
            var registration = model.DangKyId.HasValue ? await _context.DangKys.Include(d => d.LopHoc).FirstOrDefaultAsync(d => d.DangKyID == model.DangKyId && !d.IsDeleted && d.HocVienID == model.HocVienId) : null;
            if (registration == null) return BadRequest(new { success = false, message = "Hóa đơn phải gắn với một đăng ký đang tồn tại." });
            if (User.IsInRole("Nhân viên") && (await CurrentStaffBranchIdAsync()) != registration.LopHoc!.CoSoID) return Forbid();
            try
            {
                decimal tongPhaiTra = model.TongTien - model.GiamGia;

                var hoaDonMoi = new HoaDon
                {
                    HocVienID = model.HocVienId,
                    DangKyID = model.DangKyId > 0 ? model.DangKyId : (int?)null,
                    NhanSuLapID = staffId.Value,
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
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ThuTien([FromBody] ThanhToanRequestDto? model)
        {
            if (User.IsInRole("Học viên"))
                return Forbid();

            if (model == null || model.HoaDonId <= 0)
            {
                return Json(new { success = false, message = "Mã hóa đơn không hợp lệ!" });
            }

            if (model.SoTien <= 0)
            {
                return Json(new { success = false, message = "Số tiền thanh toán phải lớn hơn 0 đồng!" });
            }

            var staffId = await CurrentStaffIdAsync();
            if (!staffId.HasValue) return Forbid();
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
                    NhanSuID = staffId.Value,
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

        private async Task<int?> CurrentStaffIdAsync()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return null;
            return await _context.NhanSus.Where(x => x.NguoiDungID == userId && !x.IsDeleted && x.TrangThai == "Đang làm").Select(x => (int?)x.NhanSuID).FirstOrDefaultAsync();
        }

        private async Task<int?> CurrentStaffBranchIdAsync()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return null;
            return await _context.NhanSus.Where(x => x.NguoiDungID == userId && !x.IsDeleted).Select(x => (int?)x.CoSoID).FirstOrDefaultAsync();
        }

        public sealed class HoaDonStudentOptionDto { public int HocVienId { get; set; } public int DangKyId { get; set; } public string HoTen { get; set; } = ""; public string? SoDienThoai { get; set; } public string LopHoc { get; set; } = ""; public decimal HocPhi { get; set; } }

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