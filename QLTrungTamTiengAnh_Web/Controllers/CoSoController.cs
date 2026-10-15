using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    [Authorize(Roles = "Admin,QuanLy")]
    [AutoValidateAntiforgeryToken]
    public class CoSoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CoSoController> _logger;
        private static readonly string[] RoomStatuses = ["Đang sử dụng", "Bảo trì", "Ngừng sử dụng"];
        private static readonly string[] FinishedStatuses =
            ["Đã hủy", "Đã huỷ", "Hủy", "Huỷ", "Đã kết thúc", "Kết thúc", "Hoàn thành", "Đã hoàn thành"];

        public CoSoController(ApplicationDbContext context, ILogger<CoSoController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> DuLieu()
        {
            try
            {
                var coSos = await _context.CoSos.AsNoTracking()
                    .OrderBy(x => x.TenCoSo)
                    .Select(x => new
                    {
                        coSoId = x.CoSoID,
                        maCoSo = x.MaCoSo,
                        tenCoSo = x.TenCoSo,
                        diaChi = x.DiaChi,
                        soDienThoai = x.SoDienThoai,
                        trangThai = x.TrangThai,
                        soPhong = _context.PhongHocs.Count(p => p.CoSoID == x.CoSoID),
                        soNhanSu = _context.NhanSus.Count(n => n.CoSoID == x.CoSoID && n.TrangThai == "Đang làm")
                    }).ToListAsync();

                var phongHocs = await _context.PhongHocs.AsNoTracking()
                    .Where(x => x.CoSo != null && !x.CoSo.IsDeleted)
                    .OrderBy(x => x.CoSo!.TenCoSo).ThenBy(x => x.TenPhong)
                    .Select(x => new
                    {
                        phongHocId = x.PhongHocID,
                        coSoId = x.CoSoID,
                        tenCoSo = x.CoSo!.TenCoSo,
                        tenPhong = x.TenPhong,
                        sucChua = x.SucChua,
                        trangThai = x.TrangThai,
                        ghiChu = x.GhiChu
                    }).ToListAsync();

                var nhanSus = await _context.NhanSus.AsNoTracking()
                    .Where(x => x.TrangThai == "Đang làm" && x.CoSo != null && !x.CoSo.IsDeleted)
                    .OrderBy(x => x.HoTen)
                    .Select(x => new
                    {
                        nhanSuId = x.NhanSuID,
                        hoTen = x.HoTen,
                        coSoId = x.CoSoID,
                        tenCoSo = x.CoSo!.TenCoSo
                    }).ToListAsync();

                // Retain names in the audit history even if a referenced record is later soft-deleted.
                var dieuChuyens = await _context.LichSuDieuChuyens.IgnoreQueryFilters().AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .OrderByDescending(x => x.NgayHieuLuc ?? x.NgayDeXuat)
                    .ThenByDescending(x => x.DieuChuyenID)
                    .Select(x => new
                    {
                        dieuChuyenId = x.DieuChuyenID,
                        hoTen = x.NhanSu != null ? x.NhanSu.HoTen : "Nhân sự đã xóa",
                        coSoCu = x.CoSoCu != null ? x.CoSoCu.TenCoSo : "Cơ sở đã xóa",
                        coSoMoi = x.CoSoMoi != null ? x.CoSoMoi.TenCoSo : "Cơ sở đã xóa",
                        lyDo = x.LyDo,
                        nguoiDuyet = x.NguoiDuyet != null
                            ? (x.NguoiDuyet.NhanSu != null ? x.NguoiDuyet.NhanSu.HoTen : x.NguoiDuyet.TenDangNhap)
                            : "Chưa phê duyệt",
                        ngayHieuLuc = x.NgayHieuLuc,
                        trangThai = x.TrangThai
                    }).ToListAsync();

                return Json(new { success = true, coSos, phongHocs, nhanSus, dieuChuyens });
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
                _logger.LogError(ex, "Không thể tải dữ liệu cơ sở và phòng học.");
                return Failure("Không thể tải dữ liệu. Vui lòng kiểm tra kết nối cơ sở dữ liệu và thử lại.", 503);
            }
        }

        [HttpPost]
        public async Task<IActionResult> LuuCoSo([FromBody] CoSoDto? model)
        {
            if (model == null || !ModelState.IsValid)
                return InvalidInput();

            var code = (model.MaCoSo ?? "").Trim().ToUpperInvariant();
            var name = (model.TenCoSo ?? "").Trim();
            var address = (model.DiaChi ?? "").Trim();
            var phone = model.SoDienThoai?.Trim();
            if (model.CoSoId < 0 || code.Length is 0 or > 30 || name.Length is 0 or > 100 ||
                address.Length is 0 or > 250 || phone?.Length > 20)
                return Failure("Vui lòng nhập mã cơ sở (tối đa 30 ký tự), tên (100), địa chỉ (250) và số điện thoại (20) hợp lệ.");

            return await Mutate(async () =>
            {
                if (await _context.CoSos.AnyAsync(x => x.MaCoSo == code && x.CoSoID != model.CoSoId))
                    return Failure("Mã cơ sở đã tồn tại. Vui lòng chọn mã khác.", 409);

                CoSo? branch;
                if (model.CoSoId == 0)
                {
                    branch = new CoSo { TrangThai = true };
                    _context.CoSos.Add(branch);
                }
                else
                {
                    branch = await _context.CoSos.FirstOrDefaultAsync(x => x.CoSoID == model.CoSoId);
                    if (branch == null)
                        return Failure("Cơ sở không tồn tại hoặc đã bị xóa.", 404);
                    branch.UpdatedAt = DateTime.Now;
                }

                branch.MaCoSo = code;
                branch.TenCoSo = name;
                branch.DiaChi = address;
                branch.SoDienThoai = string.IsNullOrEmpty(phone) ? null : phone;
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã lưu thông tin cơ sở.", coSoId = branch.CoSoID });
            });
        }

        [HttpPost]
        public async Task<IActionResult> DoiTrangThaiCoSo([FromBody] TrangThaiCoSoDto? model)
        {
            if (model == null || !ModelState.IsValid || model.CoSoId <= 0)
                return InvalidInput();

            return await Mutate(async () =>
            {
                var branch = await _context.CoSos.FirstOrDefaultAsync(x => x.CoSoID == model.CoSoId);
                if (branch == null)
                    return Failure("Cơ sở không tồn tại hoặc đã bị xóa.", 404);

                if (!model.TrangThai && branch.TrangThai)
                {
                    if (await _context.NhanSus.AnyAsync(x => x.CoSoID == branch.CoSoID && x.TrangThai == "Đang làm"))
                        return Failure("Cơ sở còn nhân sự đang làm việc. Vui lòng điều chuyển nhân sự trước khi ngừng hoạt động.", 409);

                    var now = DateTime.Now;
                    if (await _context.LopHocs.AnyAsync(x => x.CoSoID == branch.CoSoID &&
                        x.NgayKetThuc.Date >= now.Date && !FinishedStatuses.Contains(x.TrangThai)))
                        return Failure("Cơ sở còn lớp học đang diễn ra hoặc sắp mở. Vui lòng xử lý các lớp trước khi ngừng hoạt động.", 409);

                    var branchRoomIds = _context.PhongHocs.IgnoreQueryFilters()
                        .Where(x => x.CoSoID == branch.CoSoID).Select(x => x.PhongHocID);
                    var branchClassIds = _context.LopHocs.IgnoreQueryFilters()
                        .Where(x => x.CoSoID == branch.CoSoID).Select(x => x.LopHocID);
                    if (await ActiveSchedules(now).AnyAsync(x =>
                        branchRoomIds.Contains(x.PhongHocID) || branchClassIds.Contains(x.LopHocID)))
                        return Failure("Cơ sở còn lịch học chưa kết thúc. Vui lòng xử lý lịch học trước khi ngừng hoạt động.", 409);
                }

                branch.TrangThai = model.TrangThai;
                branch.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                return Json(new
                {
                    success = true,
                    message = model.TrangThai ? "Đã mở lại hoạt động cơ sở." : "Đã ngừng hoạt động cơ sở.",
                    coSoId = branch.CoSoID,
                    trangThai = branch.TrangThai
                });
            });
        }

        [HttpPost]
        public async Task<IActionResult> LuuPhongHoc([FromBody] PhongHocDto? model)
        {
            if (model == null || !ModelState.IsValid)
                return InvalidInput();

            var name = (model.TenPhong ?? "").Trim();
            var status = (model.TrangThai ?? "").Trim();
            var note = model.GhiChu?.Trim();
            if (model.PhongHocId < 0 || model.CoSoId <= 0 || name.Length is 0 or > 50 || model.SucChua <= 0 ||
                !RoomStatuses.Contains(status) || note?.Length > 300)
                return Failure("Vui lòng chọn cơ sở, nhập tên phòng (tối đa 50 ký tự), sức chứa lớn hơn 0, trạng thái hợp lệ và ghi chú tối đa 300 ký tự.");

            return await Mutate(async () =>
            {
                var branch = await _context.CoSos.FirstOrDefaultAsync(x => x.CoSoID == model.CoSoId);
                if (branch == null || !branch.TrangThai)
                    return Failure("Cơ sở không tồn tại hoặc đã ngừng hoạt động. Vui lòng chọn cơ sở đang hoạt động.", 409);

                if (await _context.PhongHocs.AnyAsync(x => x.CoSoID == model.CoSoId &&
                    x.TenPhong == name && x.PhongHocID != model.PhongHocId))
                    return Failure("Tên phòng đã tồn tại trong cơ sở này.", 409);

                PhongHoc? room;
                if (model.PhongHocId == 0)
                {
                    room = new PhongHoc();
                    _context.PhongHocs.Add(room);
                }
                else
                {
                    room = await _context.PhongHocs.FirstOrDefaultAsync(x => x.PhongHocID == model.PhongHocId);
                    if (room == null)
                        return Failure("Phòng học không tồn tại hoặc đã bị xóa.", 404);

                    var schedules = ActiveSchedules(DateTime.Now).Where(x => x.PhongHocID == room.PhongHocID);
                    if ((room.CoSoID != model.CoSoId || status != "Đang sử dụng") && await schedules.AnyAsync())
                        return Failure("Phòng còn lịch học chưa kết thúc. Không thể chuyển cơ sở, bảo trì hoặc ngừng sử dụng phòng.", 409);

                    var oversizedClassIds = _context.LopHocs.IgnoreQueryFilters()
                        .Where(x => x.SiSoToiDa > model.SucChua).Select(x => x.LopHocID);
                    if (model.SucChua < room.SucChua && await schedules.AnyAsync(x =>
                        oversizedClassIds.Contains(x.LopHocID)))
                        return Failure("Sức chứa mới nhỏ hơn sĩ số tối đa của lớp đã được xếp lịch trong phòng.", 409);
                    room.UpdatedAt = DateTime.Now;
                }

                room.CoSoID = model.CoSoId;
                room.TenPhong = name;
                room.SucChua = model.SucChua;
                room.TrangThai = status;
                room.GhiChu = string.IsNullOrEmpty(note) ? null : note;
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã lưu phòng học.", phongHocId = room.PhongHocID });
            });
        }

        [HttpPost]
        public async Task<IActionResult> XoaPhongHoc([FromBody] XoaPhongHocDto? model)
        {
            if (model == null || !ModelState.IsValid || model.PhongHocId <= 0)
                return InvalidInput();

            return await Mutate(async () =>
            {
                var room = await _context.PhongHocs.FirstOrDefaultAsync(x => x.PhongHocID == model.PhongHocId);
                if (room == null)
                    return Failure("Phòng học không tồn tại hoặc đã bị xóa.", 404);

                if (await ActiveSchedules(DateTime.Now).AnyAsync(x => x.PhongHocID == room.PhongHocID))
                    return Failure("Phòng còn lịch học chưa kết thúc. Vui lòng xử lý lịch học trước khi xóa phòng.", 409);

                room.IsDeleted = true;
                room.DeletedAt = DateTime.Now;
                room.UpdatedAt = room.DeletedAt;
                room.TrangThai = "Ngừng sử dụng";
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã xóa phòng học. Lịch sử các buổi học được giữ lại.", phongHocId = room.PhongHocID });
            });
        }

        [HttpPost]
        public async Task<IActionResult> DieuChuyenNhanSu([FromBody] DieuChuyenDto? model)
        {
            if (model == null || !ModelState.IsValid)
                return InvalidInput();

            var reason = (model.LyDo ?? "").Trim();
            if (model.NhanSuId <= 0 || model.CoSoHienTaiId <= 0 || model.CoSoMoiId <= 0 || reason.Length is 0 or > 300)
                return Failure("Vui lòng chọn nhân sự, cơ sở hiện tại, cơ sở chuyển đến và nhập lý do từ 1 đến 300 ký tự.");

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var approverId))
                return Failure("Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.", 401);

            return await Mutate(async () =>
            {
                var approver = await _context.NguoiDungs.AsNoTracking().FirstOrDefaultAsync(x =>
                    x.NguoiDungID == approverId && x.TrangThai && x.VaiTro != null &&
                    (x.VaiTro.TenVaiTro == "Admin" || x.VaiTro.TenVaiTro == "QuanLy"));
                if (approver == null)
                    return Failure("Tài khoản không còn quyền phê duyệt. Vui lòng đăng nhập lại.", 403);

                var staff = await _context.NhanSus.FirstOrDefaultAsync(x => x.NhanSuID == model.NhanSuId);
                if (staff == null)
                    return Failure("Nhân sự không tồn tại hoặc đã bị xóa.", 404);
                if (staff.TrangThai != "Đang làm")
                    return Failure("Chỉ được điều chuyển nhân sự đang làm việc.", 409);
                if (staff.CoSoID != model.CoSoHienTaiId)
                    return Failure("Cơ sở hiện tại của nhân sự đã thay đổi. Vui lòng tải lại dữ liệu trước khi điều chuyển.", 409);
                if (staff.CoSoID == model.CoSoMoiId)
                    return Failure("Cơ sở chuyển đến phải khác cơ sở hiện tại.");

                var source = await _context.CoSos.AsNoTracking().FirstOrDefaultAsync(x => x.CoSoID == staff.CoSoID);
                if (source == null)
                    return Failure("Cơ sở hiện tại của nhân sự không còn tồn tại.", 409);
                var destination = await _context.CoSos.AsNoTracking().FirstOrDefaultAsync(x => x.CoSoID == model.CoSoMoiId);
                if (destination == null || !destination.TrangThai)
                    return Failure("Cơ sở chuyển đến không tồn tại hoặc đã ngừng hoạt động.", 409);

                var now = DateTime.Now;
                var proposerId = await _context.NhanSus.Where(x => x.NguoiDungID == approverId)
                    .Select(x => (int?)x.NhanSuID).FirstOrDefaultAsync();
                var history = new LichSuDieuChuyen
                {
                    NhanSuID = staff.NhanSuID,
                    CoSoCuID = staff.CoSoID,
                    CoSoMoiID = destination.CoSoID,
                    LyDo = reason,
                    NgayDeXuat = now,
                    NgayHieuLuc = now,
                    NguoiDeXuatID = proposerId,
                    NguoiDuyetID = approverId,
                    TrangThai = "Đã duyệt",
                    CreatedAt = now
                };
                staff.CoSoID = destination.CoSoID;
                staff.UpdatedAt = now;
                _context.LichSuDieuChuyens.Add(history);
                await _context.SaveChangesAsync();
                return Json(new
                {
                    success = true,
                    message = $"Đã phê duyệt điều chuyển {staff.HoTen} sang {destination.TenCoSo}.",
                    dieuChuyenId = history.DieuChuyenID
                });
            });
        }

        private IQueryable<LichHoc> ActiveSchedules(DateTime now)
        {
            var today = now.Date;
            var time = now.TimeOfDay;
            return _context.LichHocs.Where(x =>
                (x.NgayHoc.Date > today || (x.NgayHoc.Date == today && x.GioKetThuc > time)) &&
                !FinishedStatuses.Contains(x.TrangThai));
        }

        // Read the prerequisites and write the change in one transaction. In particular, a
        // transfer cannot race another transfer or suspension of its destination branch.
        private async Task<IActionResult> Mutate(Func<Task<IActionResult>> operation)
        {
            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var result = await operation();
                if (result is JsonResult json && (json.StatusCode == null || json.StatusCode < 400))
                    await transaction.CommitAsync();
                return result;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Dữ liệu cơ sở đã bị thay đổi đồng thời.");
                return Failure("Dữ liệu đã được thay đổi bởi người khác. Vui lòng tải lại và thử lại.", 409);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Không thể lưu thay đổi cơ sở hoặc phòng học.");
                return Failure("Không thể lưu thay đổi do dữ liệu bị trùng hoặc có ràng buộc liên quan. Vui lòng tải lại và kiểm tra dữ liệu.", 409);
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                _logger.LogWarning(ex, "Xung đột giao dịch cơ sở.");
                return Failure("Có thao tác khác đang cập nhật dữ liệu này. Vui lòng tải lại và thử lại.", 409);
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
                _logger.LogError(ex, "Không thể thực hiện thao tác quản lý cơ sở.");
                return Failure("Chưa thể lưu thay đổi. Vui lòng kiểm tra kết nối cơ sở dữ liệu và thử lại.", 503);
            }
        }

        private JsonResult InvalidInput() => Failure("Dữ liệu không hợp lệ. Vui lòng kiểm tra các trường bắt buộc và độ dài nội dung.");
        private JsonResult Failure(string message, int statusCode = 400) =>
            new(new { success = false, message }) { StatusCode = statusCode };
    }

    public class CoSoDto
    {
        [Range(0, int.MaxValue)]
        public int CoSoId { get; set; }
        [Required, StringLength(30)]
        public string MaCoSo { get; set; } = "";
        [Required, StringLength(100)]
        public string TenCoSo { get; set; } = "";
        [Required, StringLength(250)]
        public string DiaChi { get; set; } = "";
        [StringLength(20)]
        public string? SoDienThoai { get; set; }
    }

    public class TrangThaiCoSoDto
    {
        [Range(1, int.MaxValue)]
        public int CoSoId { get; set; }
        [JsonRequired]
        public bool TrangThai { get; set; }
    }

    public class PhongHocDto
    {
        [Range(0, int.MaxValue)]
        public int PhongHocId { get; set; }
        [Range(1, int.MaxValue)]
        public int CoSoId { get; set; }
        [Required, StringLength(50)]
        public string TenPhong { get; set; } = "";
        [Range(1, int.MaxValue)]
        public int SucChua { get; set; }
        [Required, StringLength(20)]
        public string TrangThai { get; set; } = "Đang sử dụng";
        [StringLength(300)]
        public string? GhiChu { get; set; }
    }

    public class XoaPhongHocDto
    {
        [Range(1, int.MaxValue)]
        public int PhongHocId { get; set; }
    }

    public class DieuChuyenDto
    {
        [Range(1, int.MaxValue)]
        public int NhanSuId { get; set; }
        [Range(1, int.MaxValue)]
        public int CoSoHienTaiId { get; set; }
        [Range(1, int.MaxValue)]
        public int CoSoMoiId { get; set; }
        [Required, StringLength(300)]
        public string LyDo { get; set; } = "";
    }
}
