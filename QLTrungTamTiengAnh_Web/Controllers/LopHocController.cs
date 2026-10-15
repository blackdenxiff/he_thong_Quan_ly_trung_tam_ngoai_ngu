
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class LopHocController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LopHocController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /LopHoc
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Danh sách khóa học
            ViewBag.KhoaHocs = await _context.KhoaHocs
                .AsNoTracking()
                .OrderBy(x => x.TenKhoaHoc)
                .ToListAsync();

            // Danh sách giảng viên
            ViewBag.GiangViens = await _context.NhanSus
                .AsNoTracking()
                .Where(x => x.LoaiNhanSu == "Giáo viên"
                         && x.TrangThai == "Đang làm")
                .OrderBy(x => x.HoTen)
                .ToListAsync();

            // Danh sách phòng học
            ViewBag.PhongHocs = await _context.PhongHocs
                .AsNoTracking()
                .Where(x => x.TrangThai == "Đang sử dụng")
                .OrderBy(x => x.TenPhong)
                .ToListAsync();

            return View();
        }

        // GET: /LopHoc/DanhSach
        [HttpGet]
        public async Task<IActionResult> DanhSach(
    string? tuKhoa,
    int? khoaHocId,
    string? trangThai,
    int page = 1,
    int pageSize = 10)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.LopHocs
                .AsNoTracking()
                .Include(x => x.KhoaHoc)
                .Include(x => x.GiaoVienPhuTrach)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                tuKhoa = tuKhoa.Trim();

                query = query.Where(x =>
                    x.TenLop.Contains(tuKhoa) ||
                    (x.GiaoVienPhuTrach != null &&
                     x.GiaoVienPhuTrach.HoTen.Contains(tuKhoa)));
            }

            if (khoaHocId.HasValue && khoaHocId.Value > 0)
            {
                query = query.Where(x => x.KhoaHocID == khoaHocId.Value);
            }

            if (!string.IsNullOrWhiteSpace(trangThai))
            {
                query = query.Where(x => x.TrangThai == trangThai);
            }

            var totalRecords = await query.CountAsync();

            var lopHocs = await query
                .OrderByDescending(x => x.LopHocID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    lopHocId = x.LopHocID,
                    tenLop = x.TenLop,
                    khoaHocId = x.KhoaHocID,
                    tenKhoaHoc = x.KhoaHoc!.TenKhoaHoc,
                    soBuoi = x.KhoaHoc.SoBuoi,
                    trinhDo = x.KhoaHoc.TrinhDo,
                    coSoId = x.CoSoID,
                    giaoVienPhuTrachId = x.GiaoVienPhuTrachID,
                    giangVien = x.GiaoVienPhuTrach!.HoTen,
                    siSoToiDa = x.SiSoToiDa,
                    ngayBatDau = x.NgayBatDau,
                    ngayKetThuc = x.NgayKetThuc,
                    trangThai = x.TrangThai
                })
                .ToListAsync();

            // Phần xử lý lịch học và số học viên ở dưới
            // sẽ tiếp tục dùng danh sách lopHocs vừa phân trang.

            var ketQua = new List<object>();

            foreach (var lop in lopHocs)
            {
                var lich = await _context.LichHocs
                    .AsNoTracking()
                    .Include(x => x.PhongHoc)
                    .Where(x => x.LopHocID == lop.lopHocId)
                    .OrderBy(x => x.NgayHoc)
                    .FirstOrDefaultAsync();

                var soHocVien = await _context.DangKys
                    .CountAsync(x =>
                        x.LopHocID == lop.lopHocId &&
                        x.TrangThai == "Đang học");

                string lichHocMoTa = lich?.GhiChu ?? "";

                if (string.IsNullOrWhiteSpace(lichHocMoTa)
                    && lich != null)
                {
                    lichHocMoTa =
                        $"{lich.NgayHoc:dd/MM/yyyy} " +
                        $"({lich.GioBatDau:hh\\:mm} - " +
                        $"{lich.GioKetThuc:hh\\:mm})";
                }

                ketQua.Add(new
                {
                    lop.lopHocId,
                    maLop = lop.tenLop,
                    lop.tenLop,
                    lop.khoaHocId,
                    lop.tenKhoaHoc,
                    lop.soBuoi,
                    lop.trinhDo,
                    lop.coSoId,
                    lop.giaoVienPhuTrachId,
                    lop.giangVien,
                    phongHocId = lich?.PhongHocID,
                    phongHoc = lich?.PhongHoc?.TenPhong ?? "",
                    lop.siSoToiDa,
                    soHocVien,
                    lichHocMoTa,
                    ngayBatDau = lop.ngayBatDau.ToString("yyyy-MM-dd"),
                    ngayKetThuc = lop.ngayKetThuc.ToString("yyyy-MM-dd"),
                    trangThai = lop.trangThai
                });
            }

            return Json(new
            {
                success = true,
                data = ketQua,
                page,
                pageSize,
                totalRecords,
                totalPages = (int)Math.Ceiling(
         totalRecords / (double)pageSize)
            });

        }

        // POST: /LopHoc/LuuLopHoc
        [HttpPost]
        public async Task<IActionResult> LuuLopHoc(
            [FromBody] LopHocDto? model)
        {
            if (model == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Dữ liệu lớp học không hợp lệ!"
                });
            }

            if (string.IsNullOrWhiteSpace(model.TenLop))
            {
                return Json(new
                {
                    success = false,
                    message = "Vui lòng nhập mã lớp học!"
                });
            }

            if (model.TenLop.Trim().Length > 100)
            {
                return Json(new
                {
                    success = false,
                    message = "Tên lớp không được vượt quá 100 ký tự!"
                });
            }

            if (model.SiSoToiDa < 1 || model.SiSoToiDa > 40)
            {
                return Json(new
                {
                    success = false,
                    message = "Sĩ số tối đa phải từ 1 đến 40 học viên!"
                });
            }

            if (!DateTime.TryParseExact(
                model.NgayBatDau,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime ngayBatDau))
            {
                return Json(new
                {
                    success = false,
                    message = "Ngày khai giảng không hợp lệ!"
                });
            }

            if (!DateTime.TryParseExact(
                model.NgayKetThuc,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime ngayKetThuc))
            {
                return Json(new
                {
                    success = false,
                    message = "Vui lòng chọn ngày kết thúc lớp học!"
                });
            }

            if (ngayKetThuc.Date < ngayBatDau.Date)
            {
                return Json(new
                {
                    success = false,
                    message = "Ngày kết thúc phải sau hoặc bằng ngày khai giảng!"
                });
            }

            var khoaHoc = await _context.KhoaHocs
                .FirstOrDefaultAsync(x =>
                    x.KhoaHocID == model.KhoaHocId);

            if (khoaHoc == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Khóa học được chọn không tồn tại!"
                });
            }

            var giaoVien = await _context.NhanSus
                .FirstOrDefaultAsync(x =>
                    x.NhanSuID == model.GiaoVienPhuTrachId &&
                    x.LoaiNhanSu == "Giáo viên" &&
                    x.TrangThai == "Đang làm");

            if (giaoVien == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Giảng viên không tồn tại hoặc không còn làm việc!"
                });
            }

            var phong = await _context.PhongHocs
                .FirstOrDefaultAsync(x =>
                    x.PhongHocID == model.PhongHocId &&
                    x.TrangThai == "Đang sử dụng");

            if (phong == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Phòng học không tồn tại hoặc không sử dụng!"
                });
            }

            if (phong.CoSoID != giaoVien.CoSoID)
            {
                return Json(new
                {
                    success = false,
                    message = "Phòng học và giảng viên phải thuộc cùng cơ sở!"
                });
            }

            if (model.SiSoToiDa > phong.SucChua)
            {
                return Json(new
                {
                    success = false,
                    message = $"Sĩ số tối đa không được vượt quá sức chứa phòng ({phong.SucChua} người)!"
                });
            }

            var trangThaiHopLe = new[]
            {
                "Dự kiến",
                "Sắp mở",
                "Đang diễn ra",
                "Đã kết thúc"
            };

            if (string.IsNullOrWhiteSpace(model.TrangThai) ||
                !trangThaiHopLe.Contains(model.TrangThai))
            {
                return Json(new
                {
                    success = false,
                    message = "Trạng thái lớp học không hợp lệ!"
                });
            }

            bool laThemMoi = model.LopHocId <= 0;

            LopHoc lopHoc;

            if (laThemMoi)
            {
                lopHoc = new LopHoc
                {
                    TenLop = model.TenLop.Trim(),
                    KhoaHocID = model.KhoaHocId,
                    CoSoID = giaoVien.CoSoID,
                    GiaoVienPhuTrachID = model.GiaoVienPhuTrachId,
                    SiSoToiDa = model.SiSoToiDa,
                    NgayBatDau = ngayBatDau,
                    NgayKetThuc = ngayKetThuc,
                    TrangThai = model.TrangThai
                };

                _context.LopHocs.Add(lopHoc);
            }
            else
            {
                lopHoc = await _context.LopHocs
                    .FirstOrDefaultAsync(x =>
                        x.LopHocID == model.LopHocId);

                if (lopHoc == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Không tìm thấy lớp học cần sửa!"
                    });
                }

                lopHoc.TenLop = model.TenLop.Trim();
                lopHoc.KhoaHocID = model.KhoaHocId;
                lopHoc.CoSoID = giaoVien.CoSoID;
                lopHoc.GiaoVienPhuTrachID = model.GiaoVienPhuTrachId;
                lopHoc.SiSoToiDa = model.SiSoToiDa;
                lopHoc.NgayBatDau = ngayBatDau;
                lopHoc.NgayKetThuc = ngayKetThuc;
                lopHoc.TrangThai = model.TrangThai;
            }

            // Lưu lớp trước để lấy LopHocID do SQL Server sinh.
            await _context.SaveChangesAsync();

            // Giữ lịch định kỳ dưới dạng một chuỗi mô tả.
            // Không tạo một bản ghi riêng cho từng thứ trong tuần.
            var lich = await _context.LichHocs
                .Where(x => x.LopHocID == lopHoc.LopHocID)
                .OrderBy(x => x.LichHocID)
                .FirstOrDefaultAsync();

            var gio = Regex.Match(
                model.LichHocMoTa ?? "",
                @"(?<start>\d{1,2}:\d{2})\s*-\s*(?<end>\d{1,2}:\d{2})");

            TimeSpan gioBatDau = new TimeSpan(18, 0, 0);
            TimeSpan gioKetThuc = new TimeSpan(20, 0, 0);

            if (gio.Success &&
                TimeSpan.TryParse(gio.Groups["start"].Value, out var batDau) &&
                TimeSpan.TryParse(gio.Groups["end"].Value, out var ketThuc) &&
                ketThuc > batDau)
            {
                gioBatDau = batDau;
                gioKetThuc = ketThuc;
            }

            if (lich == null)
            {
                lich = new LichHoc
                {
                    LopHocID = lopHoc.LopHocID,
                    PhongHocID = phong.PhongHocID,
                    NgayHoc = ngayBatDau,
                    GioBatDau = gioBatDau,
                    GioKetThuc = gioKetThuc,
                    TrangThai = model.TrangThai,
                    GhiChu = model.LichHocMoTa?.Trim()
                };

                _context.LichHocs.Add(lich);
            }
            else
            {
                lich.PhongHocID = phong.PhongHocID;
                lich.NgayHoc = ngayBatDau;
                lich.GioBatDau = gioBatDau;
                lich.GioKetThuc = gioKetThuc;
                lich.TrangThai = model.TrangThai;
                lich.GhiChu = model.LichHocMoTa?.Trim();
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = laThemMoi
                    ? "Mở lớp học mới thành công!"
                    : "Cập nhật lớp học thành công!",
                lopHocId = lopHoc.LopHocID,
                maLop = lopHoc.TenLop
            });
        }

        // POST: /LopHoc/XoaLopHoc
        // Đóng lớp thay vì xóa cứng để bảo vệ dữ liệu đăng ký và điểm danh.


        [HttpPost]
        public async Task<IActionResult> XoaLopHoc(
            int lopHocId,
            bool xoaVinhVien = false)
        {
            var lopHoc = await _context.LopHocs
                .FirstOrDefaultAsync(x => x.LopHocID == lopHocId);

            if (lopHoc == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Không tìm thấy lớp học."
                });
            }

            // Lần 1: Chuyển lớp sang trạng thái Đã kết thúc
            if (!xoaVinhVien)
            {
                lopHoc.TrangThai = "Đã kết thúc";

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Đã kết thúc lớp học."
                });
            }

            // Lần 2: Chỉ cho phép xóa nếu lớp đã kết thúc
            if (lopHoc.TrangThai != "Đã kết thúc")
            {
                return Json(new
                {
                    success = false,
                    message = "Chỉ được xóa vĩnh viễn lớp đã kết thúc."
                });
            }

            // Xóa lịch học thuộc lớp trước
            var lichHocs = await _context.LichHocs
                .Where(x => x.LopHocID == lopHocId)
                .ToListAsync();

            _context.LichHocs.RemoveRange(lichHocs);

            // Sau đó xóa lớp
            _context.LopHocs.Remove(lopHoc);

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Đã xóa vĩnh viễn lớp học."
            });
        }



        public class LopHocDto
        {
            public int LopHocId { get; set; }
            public string? TenLop { get; set; }
            public int KhoaHocId { get; set; }
            public int GiaoVienPhuTrachId { get; set; }
            public int PhongHocId { get; set; }
            public int SiSoToiDa { get; set; }
            public string? NgayBatDau { get; set; }
            public string? NgayKetThuc { get; set; }
            public string? LichHocMoTa { get; set; }
            public string? TrangThai { get; set; }
        }
    }
}


