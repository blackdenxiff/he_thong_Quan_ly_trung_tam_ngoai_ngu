
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class HocVienController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HocVienController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /HocVien
        public IActionResult Index()
        {
            return View();
        }

        // GET: /HocVien/DanhSachLopDangKy
        [HttpGet]
        public async Task<IActionResult> DanhSachLopDangKy()
        {
            var lopHocs = await _context.LopHocs
                .AsNoTracking()
                .Where(l => !l.IsDeleted)
                .OrderBy(l => l.NgayBatDau)
                .Select(l => new
                {
                    l.LopHocID,
                    l.TenLop,
                    l.TrangThai,
                    l.NgayBatDau,
                    l.NgayKetThuc,
                    l.SiSoToiDa,
                    SoLuongDangKy = l.DangKys.Count(d =>
                        d.TrangThai != "Đã hủy")
                })
                .ToListAsync();

            var data = lopHocs
                .Where(l =>
                    l.TrangThai != "Đã kết thúc" &&
                    l.SoLuongDangKy < l.SiSoToiDa)
                .Select(l => new
                {
                    l.LopHocID,
                    l.TenLop,
                    l.TrangThai,
                    NgayBatDau = l.NgayBatDau.ToString("dd/MM/yyyy"),
                    l.SiSoToiDa,
                    l.SoLuongDangKy
                })
                .ToList();

            return Json(new
            {
                success = true,
                data
            });
        }

        // GET: /HocVien/DanhSach
        // Lấy danh sách, tìm kiếm, lọc và phân trang
        [HttpGet]
        public async Task<IActionResult> DanhSach(
            string? tuKhoa,
            string? trinhDo,
            string? trangThai,
            int page = 1,
            int pageSize = 10)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.HocViens
    .AsNoTracking()
    .Where(h => h.TrangThai != "Đã xóa")
    .AsQueryable();

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                tuKhoa = tuKhoa.Trim();

                query = query.Where(h =>
                    h.HoTen.Contains(tuKhoa) ||
                    (h.SoDienThoai != null &&
                     h.SoDienThoai.Contains(tuKhoa)) ||
                    (h.Email != null &&
                     h.Email.Contains(tuKhoa)) ||
                    h.HocVienID.ToString().Contains(tuKhoa));
            }

            if (!string.IsNullOrWhiteSpace(trinhDo))
            {
                query = query.Where(h =>
                    h.TrinhDoDauVao == trinhDo);
            }

            if (!string.IsNullOrWhiteSpace(trangThai))
            {
                query = query.Where(h =>
                    h.TrangThai == trangThai);
            }

            var total = await query.CountAsync();

            var danhSach = await query
                .OrderByDescending(h => h.HocVienID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(h => new
                {
                    h.HocVienID,
                    h.HoTen,
                    h.GioiTinh,
                    h.NgaySinh,
                    h.SoDienThoai,
                    h.Email,
                    h.TrinhDoDauVao,
                    h.DiemDauVao,
                    h.NhanXetDauVao,
                    h.TrangThai
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                data = danhSach,
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(
                    total / (double)pageSize)
            });
        }

        // POST: /HocVien/LuuHocVien
        // Thêm mới hoặc cập nhật hồ sơ học viên
        [HttpPost]
        public async Task<IActionResult> LuuHocVien(
            [FromBody] HocVienDto? model)
        {
            if (model == null ||
                string.IsNullOrWhiteSpace(model.HoTen))
            {
                return Json(new
                {
                    success = false,
                    message = "Vui lòng nhập họ tên học viên!"
                });
            }

            if (model.HoTen.Trim().Length > 100)
            {
                return Json(new
                {
                    success = false,
                    message = "Họ tên không được vượt quá 100 ký tự!"
                });
            }

            if (model.DiemDauVao.HasValue &&
                (model.DiemDauVao < 0 ||
                 model.DiemDauVao > 100))
            {
                return Json(new
                {
                    success = false,
                    message = "Điểm đầu vào phải nằm trong khoảng 0 đến 100!"
                });
            }

            if (model.SoDienThoai?.Length > 20 ||
                model.Email?.Length > 100 ||
                model.GioiTinh?.Length > 10 ||
                model.TrinhDoDauVao?.Length > 50 ||
                model.NhanXetDauVao?.Length > 500)
            {
                return Json(new
                {
                    success = false,
                    message = "Một hoặc nhiều trường vượt quá độ dài cho phép!"
                });
            }

            if (model.NgaySinh.HasValue &&
                model.NgaySinh.Value.Date > DateTime.Today)
            {
                return Json(new
                {
                    success = false,
                    message = "Ngày sinh không được lớn hơn ngày hiện tại!"
                });
            }

            HocVien hocVien;

            if (model.HocVienID > 0)
            {
                // Cập nhật học viên đã có
                hocVien = await _context.HocViens
                    .FirstOrDefaultAsync(h =>
                        h.HocVienID == model.HocVienID);

                if (hocVien == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Không tìm thấy học viên cần sửa!"
                    });
                }
            }
            else
            {
                // Tạo hồ sơ mới
                hocVien = new HocVien
                {
                    TrangThai = "Chờ xếp lớp"
                };

                _context.HocViens.Add(hocVien);
            }

            hocVien.HoTen = model.HoTen.Trim();
            hocVien.GioiTinh = model.GioiTinh;
            hocVien.NgaySinh = model.NgaySinh;
            hocVien.SoDienThoai = model.SoDienThoai?.Trim();
            hocVien.Email = model.Email?.Trim();
            hocVien.TrinhDoDauVao = model.TrinhDoDauVao;
            hocVien.DiemDauVao = model.DiemDauVao;
            hocVien.NhanXetDauVao = model.NhanXetDauVao?.Trim();

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Lưu hồ sơ học viên thành công!",
                data = new
                {
                    hocVien.HocVienID,
                    hocVien.HoTen,
                    hocVien.GioiTinh,
                    hocVien.NgaySinh,
                    hocVien.SoDienThoai,
                    hocVien.Email,
                    hocVien.TrinhDoDauVao,
                    hocVien.DiemDauVao,
                    hocVien.NhanXetDauVao,
                    hocVien.TrangThai
                }
            });
        }

        // POST: /HocVien/XoaHocVien
        [HttpPost]
        public async Task<IActionResult> XoaHocVien(
            [FromBody] XoaHocVienDto? model)
        {
            if (model == null || model.HocVienID <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "Mã học viên không hợp lệ!"
                });
            }

            var hocVien = await _context.HocViens
                .FirstOrDefaultAsync(h =>
                    h.HocVienID == model.HocVienID);

            if (hocVien == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Không tìm thấy học viên!"
                });
            }

            // Không xóa hồ sơ nếu còn dữ liệu liên quan
            var coDangKy = await _context.DangKys
                .AnyAsync(d => d.HocVienID == model.HocVienID);

            var coCamKet = await _context.CamKets
                .AnyAsync(c => c.HocVienID == model.HocVienID);

            var coHoaDon = await _context.HoaDons
                .AnyAsync(h => h.HocVienID == model.HocVienID);

            if (coDangKy || coCamKet || coHoaDon)
            {
                return Json(new
                {
                    success = false,
                    message = "Học viên đã có dữ liệu đăng ký, cam kết hoặc hóa đơn. Không thể xóa trực tiếp hồ sơ này."
                });
            }

            _context.HocViens.Remove(hocVien);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Xóa hồ sơ học viên thành công!"
            });
        }
    }

    public class HocVienDto
    {
        public int HocVienID { get; set; }

        public string HoTen { get; set; } = string.Empty;

        public DateTime? NgaySinh { get; set; }

        public string? SoDienThoai { get; set; }

        public string? Email { get; set; }

        public string? GioiTinh { get; set; }

        public string? TrinhDoDauVao { get; set; }

        public decimal? DiemDauVao { get; set; }

        public string? NhanXetDauVao { get; set; }
    }

    public class XoaHocVienDto
    {
        public int HocVienID { get; set; }
    }
}
