using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        // Tiêm ApplicationDbContext để kết nối CSDL
        public HoaDonController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /HoaDon (Hiển thị danh sách hóa đơn từ CSDL)
        public async Task<IActionResult> Index(string searchString, string statusFilter)
        {
            var query = _context.HoaDons
                .Include(h => h.HocVien)
                .Include(h => h.ThanhToans)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(h => h.HoaDonID.ToString().Contains(searchString)
                                      || h.HocVien.HoTen.Contains(searchString)
                                      || h.HocVien.SoDienThoai.Contains(searchString));
            }

            var listHoaDon = await query.OrderByDescending(h => h.NgayLap).ToListAsync();
            return View(listHoaDon);
        }

        // POST: /HoaDon/LapHoaDon (Lập hóa đơn học phí mới và lưu vào SQL)
        [HttpPost]
        public IActionResult LapHoaDon([FromBody] HoaDonRequestDto model)
        {
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
                    NhanSuLapID = 1, // ID nhân viên lập mặc định
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

        // POST: /HoaDon/ThuTien (Ghi nhận đợt thanh toán vào bảng ThanhToan và lưu vào SQL)
        // POST: /HoaDon/ThuTien (Ghi nhận đợt thanh toán, lưu vào bảng ThanhToan và cập nhật trạng thái HoaDon)
        [HttpPost]
        public IActionResult ThuTien([FromBody] ThanhToanRequestDto model)
        {
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
                // 1. Tìm hóa đơn trong CSDL kèm theo danh sách các lần thanh toán trước đó
                var hoaDon = _context.HoaDons
                    .Include(h => h.ThanhToans)
                    .FirstOrDefault(h => h.HoaDonID == model.HoaDonId);

                if (hoaDon == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy hóa đơn trong cơ sở dữ liệu!" });
                }

                // 2. Tính tổng tiền đã thu hiện tại + số tiền vừa thu đợt này
                decimal daThuCu = hoaDon.ThanhToans != null ? hoaDon.ThanhToans.Sum(t => t.SoTien) : 0;
                decimal tongDaThuMoi = daThuCu + model.SoTien;
                decimal conLai = hoaDon.TongPhaiTra - tongDaThuMoi;

                if (tongDaThuMoi > hoaDon.TongPhaiTra)
                {
                    return Json(new { success = false, message = "Số tiền thu không được lớn hơn số tiền công nợ còn lại!" });
                }

                // 3. Thêm bản ghi vào bảng ThanhToan
                var thanhToanMoi = new ThanhToan
                {
                    HoaDonID = model.HoaDonId,
                    NhanSuID = 1, // ID nhân viên thu tiền mặc định
                    NgayThanhToan = DateTime.Now,
                    SoTien = model.SoTien,
                    PhuongThuc = model.PhuongThuc ?? "Chuyển khoản QR",
                    GhiChu = model.GhiChu ?? ""
                };
                _context.ThanhToans.Add(thanhToanMoi);

                // 4. Cập nhật trạng thái của Hóa Đơn nếu đã trả đủ
                if (conLai <= 0)
                {
                    hoaDon.TrangThai = "Đã thu đủ";
                }
                else
                {
                    hoaDon.TrangThai = "Chưa đủ";
                }

                // Lưu thay đổi xuống CSDL SQL Server
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