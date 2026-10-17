using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    [Authorize(Roles = "Admin,QuanLy,Quản lý")]
    public class KhoaHocController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        private static readonly IReadOnlyDictionary<string, string> AllowedDocumentTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".pdf"] = "application/pdf",
                [".doc"] = "application/msword",
                [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                [".ppt"] = "application/vnd.ms-powerpoint",
                [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                [".xls"] = "application/vnd.ms-excel",
                [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                [".txt"] = "text/plain"
            };

        private const long MaxDocumentSize = 10 * 1024 * 1024;

        public KhoaHocController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> DanhSach(string? keyword = null, string? trinhDo = null,
            string? trangThai = null, int page = 1, int pageSize = 10)
        {
            page = Math.Max(page, 1);
            pageSize = 10;
            keyword = (keyword ?? string.Empty).Trim();
            trinhDo = (trinhDo ?? string.Empty).Trim();
            trangThai = NormalizeStatus(trangThai);
            if (keyword.Length > 100)
                return BadRequest(new { success = false, message = "Từ khóa không được vượt quá 100 ký tự." });

            var query = _context.KhoaHocs.AsNoTracking().Where(x => !x.IsDeleted);
            if (keyword.Length > 0)
                query = query.Where(x => x.TenKhoaHoc.Contains(keyword)
                    || x.KhoaHocID.ToString().Contains(keyword));
            if (trinhDo.Length > 0)
                query = query.Where(x => x.TrinhDo == trinhDo);
            if (trangThai.Length > 0)
                query = query.Where(x => x.TrangThai == trangThai);

            var total = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
            var items = await query.OrderByDescending(x => x.KhoaHocID)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new
                {
                    id = x.KhoaHocID,
                    maKhoaHoc = $"KH-{x.KhoaHocID:D3}",
                    tenKhoaHoc = x.TenKhoaHoc,
                    trinhDo = x.TrinhDo ?? string.Empty,
                    soBuoi = x.SoBuoi,
                    hocPhi = x.HocPhi,
                    moTa = x.MoTa ?? string.Empty,
                    trangThai = x.TrangThai,
                    soLop = x.LopHocs!.Count(l => !l.IsDeleted && l.TrangThai != "Đã kết thúc")
                }).ToListAsync();

            var stats = new
            {
                total = await _context.KhoaHocs.CountAsync(),
                enrolling = await _context.KhoaHocs.CountAsync(x => x.TrangThai == "Đang mở"),
                activeClasses = await _context.LopHocs.CountAsync(x => !x.IsDeleted && x.TrangThai != "Đã kết thúc"),
                totalLessons = await _context.KhoaHocs.SumAsync(x => (int?)x.SoBuoi) ?? 0
            };
            return Json(new { success = true, items, page, pageSize, total, totalPages, stats });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuKhoaHoc([FromBody] KhoaHocDto? model)
        {
            if (model == null || model.KhoaHocId < 0)
                return BadRequest(new { success = false, message = "Thông tin khóa học không hợp lệ." });
            var name = model.TenKhoaHoc?.Trim() ?? string.Empty;
            var level = model.CapDo?.Trim() ?? string.Empty;
            var description = model.MoTa?.Trim() ?? string.Empty;
            var status = NormalizeStatus(model.TrangThai);
            if (name.Length == 0 || name.Length > 100 || level.Length > 50 || description.Length > 500)
                return BadRequest(new { success = false, message = "Tên, trình độ hoặc mô tả khóa học không hợp lệ." });
            if (model.SoBuoiHoc < 1 || model.SoBuoiHoc > 120)
                return BadRequest(new { success = false, message = "Số buổi học phải từ 1 đến 120." });
            if (model.HocPhiNiemYet < 0)
                return BadRequest(new { success = false, message = "Học phí niêm yết không được âm." });
            if (status.Length == 0)
                return BadRequest(new { success = false, message = "Trạng thái khóa học không hợp lệ." });

            var entity = model.KhoaHocId == 0
                ? new KhoaHoc()
                : await _context.KhoaHocs.FirstOrDefaultAsync(x => x.KhoaHocID == model.KhoaHocId && !x.IsDeleted);
            if (entity == null)
                return NotFound(new { success = false, message = "Không tìm thấy khóa học." });
            entity.TenKhoaHoc = name;
            entity.TrinhDo = string.IsNullOrWhiteSpace(level) ? null : level;
            entity.SoBuoi = model.SoBuoiHoc;
            entity.HocPhi = model.HocPhiNiemYet;
            entity.MoTa = string.IsNullOrWhiteSpace(description) ? null : description;
            entity.TrangThai = status;
            entity.UpdatedAt = DateTime.Now;
            if (entity.KhoaHocID == 0)
                _context.KhoaHocs.Add(entity);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã lưu khóa học.", id = entity.KhoaHocID, maKhoaHoc = $"KH-{entity.KhoaHocID:D3}" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaKhoaHoc(int khoaHocId)
        {
            var entity = await _context.KhoaHocs.FirstOrDefaultAsync(x => x.KhoaHocID == khoaHocId && !x.IsDeleted);
            if (entity == null)
                return NotFound(new { success = false, message = "Không tìm thấy khóa học." });
            var hasClasses = await _context.LopHocs.AnyAsync(x => x.KhoaHocID == khoaHocId && !x.IsDeleted && x.TrangThai != "Đã kết thúc");
            var hasEnrollments = await _context.DangKys.AnyAsync(x => x.LopHoc != null && x.LopHoc.KhoaHocID == khoaHocId && !x.IsDeleted);
            if (hasClasses || hasEnrollments)
                return Conflict(new { success = false, message = "Không thể xóa mềm khóa học còn lớp hoặc đăng ký liên quan đang hiệu lực." });
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.Now;
            entity.UpdatedAt = DateTime.Now;
            entity.TrangThai = "Tạm ngưng";
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã chuyển khóa học vào thùng rác." });
        }

        [HttpGet]
        public async Task<IActionResult> DanhSachTaiLieu(int khoaHocId)
        {
            if (!await _context.KhoaHocs.AsNoTracking().AnyAsync(x => x.KhoaHocID == khoaHocId && !x.IsDeleted))
                return NotFound(new { success = false, message = "Không tìm thấy khóa học." });

            var items = await _context.TaiLieus.AsNoTracking()
                .Where(x => x.KhoaHocID == khoaHocId && !x.IsDeleted)
                .OrderByDescending(x => x.NgayDang)
                .Select(x => new
                {
                    id = x.TaiLieuID,
                    ten = x.TenTaiLieu,
                    loai = x.LoaiTaiLieu ?? string.Empty,
                    moTa = x.MoTa ?? string.Empty,
                    ngayDang = x.NgayDang.ToString("dd/MM/yyyy HH:mm")
                }).ToListAsync();

            return Json(new { success = true, items });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadTaiLieu(int khoaHocId, IFormFile? file, string? moTa)
        {
            if (!await _context.KhoaHocs.AsNoTracking().AnyAsync(x => x.KhoaHocID == khoaHocId && !x.IsDeleted))
                return NotFound(new { success = false, message = "Không tìm thấy khóa học." });
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Vui lòng chọn tệp tài liệu." });
            if (file.Length > MaxDocumentSize)
                return BadRequest(new { success = false, message = "Tệp tài liệu không được vượt quá 10 MB." });

            var originalName = Path.GetFileName(file.FileName ?? string.Empty);
            var extension = Path.GetExtension(originalName);
            if (string.IsNullOrWhiteSpace(originalName) || originalName != file.FileName ||
                !AllowedDocumentTypes.TryGetValue(extension, out var contentType))
                return BadRequest(new { success = false, message = "Định dạng không hợp lệ. Chỉ nhận PDF, Word, PowerPoint, Excel hoặc TXT." });
            if (!string.IsNullOrWhiteSpace(file.ContentType) &&
                !string.Equals(file.ContentType, contentType, StringComparison.OrdinalIgnoreCase) &&
                !(extension.Equals(".doc", StringComparison.OrdinalIgnoreCase) && file.ContentType == "application/octet-stream"))
                return BadRequest(new { success = false, message = "Kiểu tệp không khớp với phần mở rộng." });
            if (originalName.Length > 150 || moTa?.Length > 500)
                return BadRequest(new { success = false, message = "Tên hoặc mô tả tài liệu quá dài." });

            var uploadRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "tailieu"));
            Directory.CreateDirectory(uploadRoot);
            var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var storedPath = Path.GetFullPath(Path.Combine(uploadRoot, storedName));
            if (!storedPath.StartsWith(uploadRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Đường dẫn tệp không hợp lệ." });

            try
            {
                await using (var stream = new FileStream(storedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await file.CopyToAsync(stream);

                var entity = new TaiLieu
                {
                    KhoaHocID = khoaHocId,
                    TenTaiLieu = originalName,
                    LoaiTaiLieu = extension.TrimStart('.').ToUpperInvariant(),
                    DuongDan = $"tailieu/{storedName}",
                    MoTa = string.IsNullOrWhiteSpace(moTa) ? null : moTa.Trim(),
                    NgayDang = DateTime.Now,
                    TrangThai = "Đang dùng"
                };
                _context.TaiLieus.Add(entity);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã tải tài liệu lên.", id = entity.TaiLieuID });
            }
            catch
            {
                if (System.IO.File.Exists(storedPath))
                    System.IO.File.Delete(storedPath);
                throw;
            }
        }

        [HttpGet]
        public async Task<IActionResult> TaiTaiLieu(int taiLieuId)
        {
            var entity = await _context.TaiLieus.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TaiLieuID == taiLieuId && !x.IsDeleted && x.KhoaHoc != null && !x.KhoaHoc.IsDeleted);
            if (entity == null)
                return NotFound();

            var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "tailieu"));
            var relativePath = (entity.DuongDan ?? string.Empty).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var path = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(path))
                return NotFound();

            return PhysicalFile(path, GetContentType(entity.LoaiTaiLieu), entity.TenTaiLieu, enableRangeProcessing: true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaTaiLieu(int taiLieuId)
        {
            var entity = await _context.TaiLieus.FirstOrDefaultAsync(x => x.TaiLieuID == taiLieuId && !x.IsDeleted);
            if (entity == null)
                return NotFound(new { success = false, message = "Không tìm thấy tài liệu." });

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.Now;
            entity.UpdatedAt = DateTime.Now;
            entity.TrangThai = "Đã xóa";
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã chuyển tài liệu vào thùng rác." });
        }

        private static string GetContentType(string? extension)
        {
            var key = extension?.StartsWith(".") == true ? extension : "." + extension;
            return AllowedDocumentTypes.TryGetValue(key, out var type) ? type : "application/octet-stream";
        }

        private static string NormalizeStatus(string? value)
            => value?.Trim() switch
            {
                "Đang tuyển sinh" => "Đang mở",
                "Đang mở" => "Đang mở",
                "Tạm ngưng" => "Tạm ngưng",
                _ => string.Empty
            };
    }

    public sealed class KhoaHocDto
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