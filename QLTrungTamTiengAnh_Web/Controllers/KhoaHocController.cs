
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Models;
using QuanLyTrungTamNgoaiNgu.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class KhoaHocController : Controller
    {
        private readonly ApplicationDbContext _context;

        public KhoaHocController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /KhoaHoc
        // Giữ nguyên cách mở View của thành viên phụ trách giao diện
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var danhSachKhoaHoc = await _context.KhoaHocs
                .AsNoTracking()
                .OrderBy(k => k.KhoaHocID)
                .ToListAsync();
              
            return View(danhSachKhoaHoc);
        }

        // GET: /KhoaHoc/DanhSach
        // Tìm kiếm, lọc và phân trang qua AJAX
        [HttpGet]
        public async Task<IActionResult> DanhSach(
            string? tuKhoa,
            string? trinhDo,
            string? trangThai,
            int page = 1,
            int pageSize = 10)
        {
            if (page < 1) page = 1;

            // Giới hạn số bản ghi mỗi trang
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IQueryable<KhoaHoc> query =
                _context.KhoaHocs.AsNoTracking();

            // Tìm theo tên khóa học

            // Tìm theo tên khóa học hoặc mã KH-0001
            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                tuKhoa = tuKhoa.Trim();

                // Bỏ tiền tố KH- để lấy ID, ví dụ KH-0001 -> 1
                string maTimKiem = tuKhoa;

                if (maTimKiem.StartsWith(
                    "KH-", StringComparison.OrdinalIgnoreCase))
                {
                    maTimKiem = maTimKiem.Substring(3);
                }

                bool timTheoMa = int.TryParse(
                    maTimKiem, out int khoaHocId);

                if (timTheoMa)
                {
                    query = query.Where(k =>
                        k.TenKhoaHoc.Contains(tuKhoa) ||
                        k.KhoaHocID == khoaHocId);
                }
                else
                {
                    query = query.Where(k =>
                        k.TenKhoaHoc.Contains(tuKhoa));
                }
            }


            // Lọc theo trình độ
            if (!string.IsNullOrWhiteSpace(trinhDo))
            {
                query = query.Where(k =>
                    k.TrinhDo == trinhDo);
            }

            // Lọc theo trạng thái
            if (!string.IsNullOrWhiteSpace(trangThai))
            {
                query = query.Where(k =>
                    k.TrangThai == trangThai);
            }

            int totalRecords = await query.CountAsync();

            var danhSach = await query
                .OrderBy(k => k.KhoaHocID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(k => new
                {
                    k.KhoaHocID,
                    k.TenKhoaHoc,
                    k.TrinhDo,
                    k.MoTa,
                    k.SoBuoi,
                    k.HocPhi,
                    k.TrangThai
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                data = danhSach,
                page,
                pageSize,
                totalRecords,
                totalPages = (int)Math.Ceiling(
                    totalRecords / (double)pageSize)
            });
        }

        // POST: /KhoaHoc/LuuKhoaHoc
        // Thêm mới hoặc cập nhật khóa học qua AJAX
        [HttpPost]
        public async Task<IActionResult> LuuKhoaHoc(
            [FromBody] KhoaHocDto? model)
        {
            if (model == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Dữ liệu khóa học không hợp lệ!"
                });
            }

            // Kiểm tra dữ liệu tại Controller,
            // không cần sửa Model
            if (string.IsNullOrWhiteSpace(model.TenKhoaHoc))
            {
                return Json(new
                {
                    success = false,
                    message = "Vui lòng nhập tên khóa học!"
                });
            }

            if (model.TenKhoaHoc.Trim().Length > 100)
            {
                return Json(new
                {
                    success = false,
                    message = "Tên khóa học không được vượt quá 100 ký tự!"
                });
            }

            if (model.CapDo?.Length > 50)
            {
                return Json(new
                {
                    success = false,
                    message = "Trình độ không được vượt quá 50 ký tự!"
                });
            }

            if (model.MoTa?.Length > 500)
            {
                return Json(new
                {
                    success = false,
                    message = "Mô tả không được vượt quá 500 ký tự!"
                });
            }

            if (model.SoBuoiHoc < 1 || model.SoBuoiHoc > 120)
            {
                return Json(new
                {
                    success = false,
                    message = "Số buổi học phải từ 1 đến 120!"
                });
            }

            if (model.HocPhiNiemYet < 0 ||
                model.HocPhiNiemYet > 9999999999.99m)
            {
                return Json(new
                {
                    success = false,
                    message = "Học phí không hợp lệ!"
                });
            }

            string trangThai = string.IsNullOrWhiteSpace(model.TrangThai)
                ? "Đang mở"
                : model.TrangThai.Trim();

            if (trangThai.Length > 20)
            {
                return Json(new
                {
                    success = false,
                    message = "Trạng thái không được vượt quá 20 ký tự!"
                });
            }

            try
            {
                KhoaHoc khoaHoc;

                if (model.KhoaHocId > 0)
                {
                    // Cập nhật khóa học đã tồn tại
                    khoaHoc = await _context.KhoaHocs
                        .FirstOrDefaultAsync(k =>
                            k.KhoaHocID == model.KhoaHocId);

                    if (khoaHoc == null)
                    {
                        return Json(new
                        {
                            success = false,
                            message = "Không tìm thấy khóa học cần sửa!"
                        });
                    }
                }
                else
                {
                    // Thêm khóa học mới
                    khoaHoc = new KhoaHoc();
                    _context.KhoaHocs.Add(khoaHoc);
                }

                khoaHoc.TenKhoaHoc = model.TenKhoaHoc.Trim();
                khoaHoc.TrinhDo = model.CapDo?.Trim();
                khoaHoc.MoTa = model.MoTa?.Trim();
                khoaHoc.SoBuoi = model.SoBuoiHoc;
                khoaHoc.HocPhi = model.HocPhiNiemYet;
                khoaHoc.TrangThai = trangThai;

                await _context.SaveChangesAsync();
                var daLuu = await _context.KhoaHocs
    .AsNoTracking()
    .AnyAsync(k => k.KhoaHocID == khoaHoc.KhoaHocID);
                return Json(new
                {
                    success = true,
                    message = "Lưu khóa học thành công!",
                    khoaHocId = khoaHoc.KhoaHocID,
                    daLuu
                });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.InnerException?.Message ?? ex.Message
                });
            }
        }

        // POST: /KhoaHoc/XoaKhoaHoc
        // Ngừng mở khóa học thay vì xóa vật lý,
        // tránh làm mất liên kết với lớp học hoặc tài liệu

        [HttpPost]
        public async Task<IActionResult> XoaKhoaHoc(int khoaHocId)
        {
            if (khoaHocId <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "Mã khóa học không hợp lệ!"
                });
            }

            var khoaHoc = await _context.KhoaHocs
                .FirstOrDefaultAsync(k => k.KhoaHocID == khoaHocId);

            if (khoaHoc == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Không tìm thấy khóa học!"
                });
            }

            try
            {
                _context.KhoaHocs.Remove(khoaHoc);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Xóa khóa học thành công!"
                });
            }
            catch (DbUpdateException)
            {
                return Json(new
                {
                    success = false,
                    message = "Không thể xóa khóa học vì đang có dữ liệu liên quan, chẳng hạn lớp học hoặc tài liệu."
                });
            }
        }



        // DTO chỉ dùng trong Controller, không sửa Model KhoaHoc
        public class KhoaHocDto
        {
            public int KhoaHocId { get; set; }

            public string? MaKhoaHoc { get; set; }

            public string? TenKhoaHoc { get; set; }

            public string? CapDo { get; set; }

            public int SoBuoiHoc { get; set; }

            public decimal HocPhiNiemYet { get; set; }

            public string? MoTa { get; set; }

            public string? TrangThai { get; set; }
        }
    }
}