using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using QuanLyTrungTamNgoaiNgu.Data;
using System.Data;
using System.Security.Claims;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    [Authorize(Roles = "Admin,QuanLy")]
    public class CoSoController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<CoSoController> _logger;

        public CoSoController(ApplicationDbContext db, ILogger<CoSoController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // Đọc đúng các bảng SQL Server hiện có: dbo.CoSo, dbo.NhanSu,
        // dbo.LichSuDieuChuyen. Không chỉnh Model hay ApplicationDbContext.
        private SqlConnection CreateConnection() =>
            new SqlConnection(_db.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Chưa cấu hình kết nối SQL Server."));

        private static void Add(SqlCommand command, string name, SqlDbType type, object? value, int size = 0)
        {
            SqlParameter parameter = size > 0
                ? command.Parameters.Add(name, type, size)
                : command.Parameters.Add(name, type);
            parameter.Value = value ?? DBNull.Value;
        }

        private IActionResult Fail(Exception ex, string operation)
        {
            _logger.LogError(ex, "Lỗi CoSoController: {Operation}", operation);
            return StatusCode(500, new
            {
                success = false,
                message = "Không thể xử lý yêu cầu. Vui lòng kiểm tra kết nối và thử lại."
            });
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> DanhSachCoSo(int page = 1, int pageSize = 10, string? keyword = null)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 50);
            keyword = (keyword ?? "").Trim();
            if (keyword.Length > 100) keyword = keyword[..100];
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();
                const string condition = @"
                    c.IsDeleted = 0
                    AND (@keyword = N'' OR c.TenCoSo LIKE N'%' + @keyword + N'%'
                         OR c.DiaChi LIKE N'%' + @keyword + N'%'
                         OR c.SoDienThoai LIKE '%' + @keyword + '%'
                         OR ('CS-' + RIGHT('000' + CONVERT(VARCHAR(11), c.CoSoID), 3)) LIKE '%' + @keyword + '%')";

                using var countCmd = new SqlCommand("SELECT COUNT(*) FROM dbo.CoSo c WHERE " + condition, conn);
                Add(countCmd, "@keyword", SqlDbType.NVarChar, keyword, 100);
                var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                var totalPages = (int)Math.Ceiling(total / (double)pageSize);
                if (totalPages > 0 && page > totalPages) page = totalPages;

                var sql = @"
                    SELECT c.CoSoID, c.TenCoSo, c.DiaChi, c.SoDienThoai, c.TrangThai,
                      (SELECT COUNT(*) FROM dbo.PhongHoc p
                       WHERE p.CoSoID = c.CoSoID AND p.IsDeleted = 0) AS SoPhong,
                      (SELECT COUNT(*) FROM dbo.NhanSu n
                       WHERE n.CoSoID = c.CoSoID AND n.IsDeleted = 0) AS SoNhanSu
                    FROM dbo.CoSo c WHERE " + condition + @"
                    ORDER BY c.CoSoID DESC
                    OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY";
                using var cmd = new SqlCommand(sql, conn);
                Add(cmd, "@keyword", SqlDbType.NVarChar, keyword, 100);
                Add(cmd, "@skip", SqlDbType.Int, (page - 1) * pageSize);
                Add(cmd, "@pageSize", SqlDbType.Int, pageSize);

                var rows = new List<object>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    rows.Add(new
                    {
                        coSoId = id,
                        maCoSo = $"CS-{id:D3}",
                        tenCoSo = reader.GetString(1),
                        diaChi = reader.GetString(2),
                        soDienThoai = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        trangThai = reader.GetBoolean(4),
                        soPhong = reader.GetInt32(5),
                        soNhanSu = reader.GetInt32(6)
                    });
                }
                return Json(new { success = true, data = rows, page, pageSize, total, totalPages });
            }
            catch (Exception ex) { return Fail(ex, "DanhSachCoSo"); }
        }

        // Danh sách chọn cơ sở cho modal: không phụ thuộc trang hiện tại.
        [HttpGet]
        public async Task<IActionResult> DanhSachCoSoLuaChon()
        {
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT CoSoID, TenCoSo FROM dbo.CoSo
                    WHERE IsDeleted = 0 AND TrangThai = 1
                    ORDER BY TenCoSo, CoSoID", conn);
                var rows = new List<object>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    rows.Add(new { coSoId = reader.GetInt32(0), tenCoSo = reader.GetString(1) });
                return Json(new { success = true, data = rows });
            }
            catch (Exception ex) { return Fail(ex, "DanhSachCoSoLuaChon"); }
        }

        [HttpGet]
        public async Task<IActionResult> DanhSachNhanSu()
        {
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();
                const string sql = @"
                    SELECT n.NhanSuID, n.HoTen, n.LoaiNhanSu, n.CoSoID, c.TenCoSo
                    FROM dbo.NhanSu n
                    INNER JOIN dbo.CoSo c ON c.CoSoID = n.CoSoID
                    WHERE n.IsDeleted = 0 AND n.TrangThai = N'Đang làm'
                      AND c.IsDeleted = 0 AND c.TrangThai = 1
                    ORDER BY n.HoTen, n.NhanSuID";
                using var cmd = new SqlCommand(sql, conn);
                var rows = new List<object>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new
                    {
                        nhanSuId = reader.GetInt32(0),
                        hoTen = reader.GetString(1),
                        loaiNhanSu = reader.GetString(2),
                        coSoId = reader.GetInt32(3),
                        tenCoSo = reader.GetString(4)
                    });
                }
                return Json(new { success = true, data = rows });
            }
            catch (Exception ex) { return Fail(ex, "DanhSachNhanSu"); }
        }

        [HttpGet]
        public async Task<IActionResult> DanhSachLichSu(int page = 1, int pageSize = 10)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 50);
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();
                using var countCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM dbo.LichSuDieuChuyen WHERE IsDeleted = 0", conn);
                int total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                int totalPages = (int)Math.Ceiling(total / (double)pageSize);
                if (totalPages > 0 && page > totalPages) page = totalPages;

                const string sql = @"
                    SELECT ls.DieuChuyenID, ls.NhanSuID,
                           ISNULL(n.HoTen, N'[Nhân sự không còn tồn tại]'),
                           ISNULL(n.LoaiNhanSu, N''),
                           ISNULL(cu.TenCoSo, N'[Cơ sở không còn tồn tại]'),
                           ISNULL(moi.TenCoSo, N'[Cơ sở không còn tồn tại]'),
                           ISNULL(ls.LyDo, N''),
                           ISNULL(nd.TenDangNhap, N'Chưa duyệt'),
                           ls.NgayDeXuat, ls.NgayHieuLuc, ls.TrangThai
                    FROM dbo.LichSuDieuChuyen ls
                    LEFT JOIN dbo.NhanSu n ON n.NhanSuID = ls.NhanSuID
                    LEFT JOIN dbo.CoSo cu ON cu.CoSoID = ls.CoSoCuID
                    LEFT JOIN dbo.CoSo moi ON moi.CoSoID = ls.CoSoMoiID
                    LEFT JOIN dbo.NguoiDung nd ON nd.NguoiDungID = ls.NguoiDuyetID
                    WHERE ls.IsDeleted = 0
                    ORDER BY ls.DieuChuyenID DESC
                    OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY";
                using var cmd = new SqlCommand(sql, conn);
                Add(cmd, "@skip", SqlDbType.Int, (page - 1) * pageSize);
                Add(cmd, "@pageSize", SqlDbType.Int, pageSize);
                var rows = new List<object>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new
                    {
                        dieuChuyenId = reader.GetInt32(0),
                        nhanSuId = reader.GetInt32(1),
                        hoTen = reader.GetString(2),
                        loaiNhanSu = reader.GetString(3),
                        coSoCu = reader.GetString(4),
                        coSoMoi = reader.GetString(5),
                        lyDo = reader.GetString(6),
                        nguoiDuyet = reader.GetString(7),
                        ngayDeXuat = reader.GetDateTime(8),
                        ngayHieuLuc = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9),
                        trangThai = reader.GetString(10)
                    });
                }
                return Json(new { success = true, data = rows, page, pageSize, total, totalPages });
            }
            catch (Exception ex) { return Fail(ex, "DanhSachLichSu"); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuCoSo([FromBody] CoSoDto? model)
        {
            if (model == null || model.CoSoId < 0)
                return BadRequest(new { success = false, message = "Thông tin cơ sở không hợp lệ." });
            var name = model.TenCoSo?.Trim() ?? "";
            var address = model.DiaChi?.Trim() ?? "";
            var phone = model.SoDienThoai?.Trim();
            if (name.Length == 0 || address.Length == 0)
                return BadRequest(new { success = false, message = "Vui lòng nhập tên và địa chỉ cơ sở." });
            if (name.Length > 100 || address.Length > 250 || phone?.Length > 20)
                return BadRequest(new { success = false, message = "Tên, địa chỉ hoặc số điện thoại quá dài." });

            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();
                const string duplicateSql = @"
                    SELECT COUNT(*) FROM dbo.CoSo
                    WHERE IsDeleted = 0 AND TenCoSo = @name AND CoSoID <> @id";
                using (var duplicate = new SqlCommand(duplicateSql, conn))
                {
                    Add(duplicate, "@name", SqlDbType.NVarChar, name, 100);
                    Add(duplicate, "@id", SqlDbType.Int, model.CoSoId);
                    if (Convert.ToInt32(await duplicate.ExecuteScalarAsync()) != 0)
                        return Conflict(new { success = false, message = "Tên cơ sở đã tồn tại." });
                }

                if (model.CoSoId == 0)
                {
                    using var cmd = new SqlCommand(@"
                        INSERT INTO dbo.CoSo(TenCoSo, DiaChi, SoDienThoai, TrangThai,
                                             IsDeleted, CreatedAt)
                        OUTPUT INSERTED.CoSoID
                        VALUES(@name, @address, @phone, 1, 0, GETDATE())", conn);
                    Add(cmd, "@name", SqlDbType.NVarChar, name, 100);
                    Add(cmd, "@address", SqlDbType.NVarChar, address, 250);
                    Add(cmd, "@phone", SqlDbType.VarChar, phone, 20);
                    int id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    return Json(new { success = true, message = "Đã thêm cơ sở mới.", coSoId = id });
                }
                else
                {
                    using var cmd = new SqlCommand(@"
                        UPDATE dbo.CoSo
                        SET TenCoSo = @name, DiaChi = @address, SoDienThoai = @phone,
                            UpdatedAt = GETDATE()
                        WHERE CoSoID = @id AND IsDeleted = 0", conn);
                    Add(cmd, "@name", SqlDbType.NVarChar, name, 100);
                    Add(cmd, "@address", SqlDbType.NVarChar, address, 250);
                    Add(cmd, "@phone", SqlDbType.VarChar, phone, 20);
                    Add(cmd, "@id", SqlDbType.Int, model.CoSoId);
                    if (await cmd.ExecuteNonQueryAsync() == 0)
                        return NotFound(new { success = false, message = "Không tìm thấy cơ sở cần sửa." });
                    return Json(new { success = true, message = "Đã cập nhật cơ sở.", coSoId = model.CoSoId });
                }
            }
            catch (Exception ex) { return Fail(ex, "LuuCoSo"); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DoiTrangThaiCoSo([FromBody] DoiTrangThaiCoSoDto? model)
        {
            if (model == null || model.CoSoId <= 0)
                return BadRequest(new { success = false, message = "Cơ sở không hợp lệ." });
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    UPDATE dbo.CoSo SET TrangThai = @status, UpdatedAt = GETDATE()
                    WHERE CoSoID = @id AND IsDeleted = 0", conn);
                Add(cmd, "@status", SqlDbType.Bit, model.TrangThai);
                Add(cmd, "@id", SqlDbType.Int, model.CoSoId);
                if (await cmd.ExecuteNonQueryAsync() == 0)
                    return NotFound(new { success = false, message = "Không tìm thấy cơ sở." });
                return Json(new
                {
                    success = true,
                    message = model.TrangThai ? "Đã kích hoạt lại cơ sở." : "Đã tạm ngưng cơ sở."
                });
            }
            catch (Exception ex) { return Fail(ex, "DoiTrangThaiCoSo"); }
        }

        // GET: /CoSo/DanhSachCoSoDaXoa - thùng rác, chỉ đọc các bản ghi xóa mềm.
        [HttpGet]
        public async Task<IActionResult> DanhSachCoSoDaXoa(int page = 1, int pageSize = 10, string? keyword = null)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);
            keyword = (keyword ?? "").Trim();
            if (keyword.Length > 100) keyword = keyword[..100];
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();
                const string condition = @"
                    c.IsDeleted = 1 AND
                    (@keyword = N'' OR c.TenCoSo LIKE N'%' + @keyword + N'%'
                     OR c.DiaChi LIKE N'%' + @keyword + N'%'
                     OR ('CS-' + RIGHT('000' + CONVERT(VARCHAR(11), c.CoSoID), 3)) LIKE '%' + @keyword + '%')";
                using var countCmd = new SqlCommand("SELECT COUNT(*) FROM dbo.CoSo c WHERE " + condition, conn);
                Add(countCmd, "@keyword", SqlDbType.NVarChar, keyword, 100);
                int total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                int totalPages = (int)Math.Ceiling(total / (double)pageSize);
                if (totalPages > 0 && page > totalPages) page = totalPages;

                const string select = @"
                    SELECT c.CoSoID, c.TenCoSo, c.DiaChi, c.SoDienThoai, c.DeletedAt
                    FROM dbo.CoSo c WHERE ";
                using var cmd = new SqlCommand(select + condition + @"
                    ORDER BY c.DeletedAt DESC, c.CoSoID DESC
                    OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY", conn);
                Add(cmd, "@keyword", SqlDbType.NVarChar, keyword, 100);
                Add(cmd, "@skip", SqlDbType.Int, (page - 1) * pageSize);
                Add(cmd, "@pageSize", SqlDbType.Int, pageSize);
                var rows = new List<object>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    rows.Add(new
                    {
                        coSoId = id,
                        maCoSo = $"CS-{id:D3}",
                        tenCoSo = reader.GetString(1),
                        diaChi = reader.GetString(2),
                        soDienThoai = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        deletedAt = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4)
                    });
                }
                return Json(new { success = true, data = rows, page, pageSize, total, totalPages });
            }
            catch (Exception ex) { return Fail(ex, "DanhSachCoSoDaXoa"); }
        }

        // POST: /CoSo/XoaMemCoSo
        // Không xóa cơ sở còn phòng học, nhân sự hoặc lớp học chưa xóa mềm.
        // Không cascade. Các lịch sử cũ vẫn giữ nguyên để phục vụ kiểm toán.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaMemCoSo([FromBody] CoSoIdDto? model)
        {
            if (model == null || model.CoSoId <= 0)
                return BadRequest(new { success = false, message = "Mã cơ sở không hợp lệ." });
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();
                await using var trans = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    // Khóa hàng cơ sở để tránh vừa xóa vừa chuyển nhân sự tới cơ sở đó.
                    using (var lockCmd = new SqlCommand(@"
                        SELECT CoSoID FROM dbo.CoSo WITH (UPDLOCK, HOLDLOCK)
                        WHERE CoSoID = @id AND IsDeleted = 0", conn, trans))
                    {
                        Add(lockCmd, "@id", SqlDbType.Int, model.CoSoId);
                        if (await lockCmd.ExecuteScalarAsync() == null)
                        {
                            await trans.RollbackAsync();
                            return NotFound(new { success = false, message = "Cơ sở không tồn tại hoặc đã được xóa mềm." });
                        }
                    }

                    using (var checkCmd = new SqlCommand(@"
                        SELECT
                          (SELECT COUNT(*) FROM dbo.NhanSu WHERE CoSoID = @id AND IsDeleted = 0),
                          (SELECT COUNT(*) FROM dbo.PhongHoc WHERE CoSoID = @id AND IsDeleted = 0),
                          (SELECT COUNT(*) FROM dbo.LopHoc WHERE CoSoID = @id AND IsDeleted = 0)", conn, trans))
                    {
                        Add(checkCmd, "@id", SqlDbType.Int, model.CoSoId);
                        await using var reader = await checkCmd.ExecuteReaderAsync();
                        await reader.ReadAsync();
                        var soNhanSu = reader.GetInt32(0);
                        var soPhong = reader.GetInt32(1);
                        var soLop = reader.GetInt32(2);
                        await reader.CloseAsync();
                        if (soNhanSu > 0 || soPhong > 0 || soLop > 0)
                        {
                            await trans.RollbackAsync();
                            return Conflict(new
                            {
                                success = false,
                                message = $"Không thể xóa mềm: cơ sở còn {soNhanSu} nhân sự, {soPhong} phòng học và {soLop} lớp học chưa xóa mềm. Hãy xử lý các dữ liệu liên quan trước."
                            });
                        }
                    }

                    using (var cmd = new SqlCommand(@"
                        UPDATE dbo.CoSo
                        SET IsDeleted = 1, DeletedAt = GETDATE(), UpdatedAt = GETDATE(), TrangThai = 0
                        WHERE CoSoID = @id AND IsDeleted = 0", conn, trans))
                    {
                        Add(cmd, "@id", SqlDbType.Int, model.CoSoId);
                        if (await cmd.ExecuteNonQueryAsync() != 1)
                            throw new DBConcurrencyException("Cơ sở đã bị thay đổi bởi yêu cầu khác.");
                    }
                    await trans.CommitAsync();
                    return Json(new { success = true, message = "Đã chuyển cơ sở vào thùng rác. Dữ liệu không bị xóa vĩnh viễn." });
                }
                catch
                {
                    await trans.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex) { return Fail(ex, "XoaMemCoSo"); }
        }

        // POST: /CoSo/KhoiPhucCoSo
        // Khôi phục ở trạng thái tạm ngưng; người quản lý chủ động kích hoạt lại.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KhoiPhucCoSo([FromBody] CoSoIdDto? model)
        {
            if (model == null || model.CoSoId <= 0)
                return BadRequest(new { success = false, message = "Mã cơ sở không hợp lệ." });
            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();
                await using var trans = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    string name;
                    using (var selectCmd = new SqlCommand(@"
                        SELECT TenCoSo FROM dbo.CoSo WITH (UPDLOCK, HOLDLOCK)
                        WHERE CoSoID = @id AND IsDeleted = 1", conn, trans))
                    {
                        Add(selectCmd, "@id", SqlDbType.Int, model.CoSoId);
                        var result = await selectCmd.ExecuteScalarAsync();
                        if (result == null)
                        {
                            await trans.RollbackAsync();
                            return NotFound(new { success = false, message = "Không tìm thấy cơ sở trong thùng rác." });
                        }
                        name = (string)result;
                    }
                    using (var duplicateCmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM dbo.CoSo WITH (UPDLOCK, HOLDLOCK)
                        WHERE IsDeleted = 0 AND CoSoID <> @id AND TenCoSo = @name", conn, trans))
                    {
                        Add(duplicateCmd, "@id", SqlDbType.Int, model.CoSoId);
                        Add(duplicateCmd, "@name", SqlDbType.NVarChar, name, 100);
                        if (Convert.ToInt32(await duplicateCmd.ExecuteScalarAsync()) > 0)
                        {
                            await trans.RollbackAsync();
                            return Conflict(new { success = false, message = "Đã tồn tại cơ sở đang sử dụng trùng tên. Hãy giải quyết trùng tên trước khi khôi phục." });
                        }
                    }
                    using (var updateCmd = new SqlCommand(@"
                        UPDATE dbo.CoSo
                        SET IsDeleted = 0, DeletedAt = NULL, UpdatedAt = GETDATE(), TrangThai = 0
                        WHERE CoSoID = @id AND IsDeleted = 1", conn, trans))
                    {
                        Add(updateCmd, "@id", SqlDbType.Int, model.CoSoId);
                        if (await updateCmd.ExecuteNonQueryAsync() != 1)
                            throw new DBConcurrencyException("Cơ sở vừa bị thay đổi bởi yêu cầu khác.");
                    }
                    await trans.CommitAsync();
                    return Json(new { success = true, message = "Đã khôi phục cơ sở ở trạng thái tạm ngưng. Bạn có thể kích hoạt lại trong danh sách cơ sở." });
                }
                catch
                {
                    await trans.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex) { return Fail(ex, "KhoiPhucCoSo"); }
        }

        // Phê duyệt ngay. Transaction + UPDLOCK giúp tránh 2 yêu cầu điều chuyển
        // cùng một nhân sự ghi lại CoSoCuID không nhất quán.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DieuChuyenNhanSu([FromBody] DieuChuyenDto? model)
        {
            if (model == null || model.NhanSuId <= 0 || model.CoSoMoiId <= 0)
                return BadRequest(new { success = false, message = "Chọn nhân sự và cơ sở chuyển đến." });
            var reason = model.LyDo?.Trim() ?? "";
            if (reason.Length == 0 || reason.Length > 300)
                return BadRequest(new { success = false, message = "Lý do cần có từ 1 đến 300 ký tự." });

            var approverId = HttpContext.Session.GetInt32("NguoiDungID");
            if (!approverId.HasValue)
            {
                var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(claim, out var parsed)) approverId = parsed;
            }
            if (!approverId.HasValue || approverId.Value <= 0)
                return Unauthorized(new { success = false, message = "Không xác định được tài khoản đăng nhập." });

            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();
                await using var trans = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    using (var userCmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM dbo.NguoiDung
                        WHERE NguoiDungID = @id AND TrangThai = 1 AND IsDeleted = 0", conn, trans))
                    {
                        Add(userCmd, "@id", SqlDbType.Int, approverId.Value);
                        if (Convert.ToInt32(await userCmd.ExecuteScalarAsync()) == 0)
                        {
                            await trans.RollbackAsync();
                            return Unauthorized(new { success = false, message = "Tài khoản duyệt không còn hoạt động." });
                        }
                    }

                    int oldBranch;
                    string staffName;
                    using (var staffCmd = new SqlCommand(@"
                        SELECT CoSoID, HoTen FROM dbo.NhanSu WITH (UPDLOCK, HOLDLOCK)
                        WHERE NhanSuID = @id AND IsDeleted = 0 AND TrangThai = N'Đang làm'", conn, trans))
                    {
                        Add(staffCmd, "@id", SqlDbType.Int, model.NhanSuId);
                        await using var reader = await staffCmd.ExecuteReaderAsync();
                        if (!await reader.ReadAsync())
                        {
                            await reader.CloseAsync();
                            await trans.RollbackAsync();
                            return BadRequest(new { success = false, message = "Nhân sự không tồn tại hoặc không còn làm việc." });
                        }
                        oldBranch = reader.GetInt32(0);
                        staffName = reader.GetString(1);
                    }

                    if (oldBranch == model.CoSoMoiId)
                    {
                        await trans.RollbackAsync();
                        return BadRequest(new { success = false, message = "Không thể điều chuyển về cơ sở hiện tại." });
                    }

                    string newBranchName;
                    using (var branchCmd = new SqlCommand(@"
                        SELECT CoSoID, TenCoSo FROM dbo.CoSo WITH (HOLDLOCK)
                        WHERE CoSoID IN (@oldId, @newId) AND TrangThai = 1 AND IsDeleted = 0", conn, trans))
                    {
                        Add(branchCmd, "@oldId", SqlDbType.Int, oldBranch);
                        Add(branchCmd, "@newId", SqlDbType.Int, model.CoSoMoiId);
                        bool hasOld = false, hasNew = false;
                        newBranchName = "";
                        await using var reader = await branchCmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            var id = reader.GetInt32(0);
                            if (id == oldBranch) hasOld = true;
                            if (id == model.CoSoMoiId)
                            {
                                hasNew = true;
                                newBranchName = reader.GetString(1);
                            }
                        }
                        if (!hasOld || !hasNew)
                        {
                            await reader.CloseAsync();
                            await trans.RollbackAsync();
                            return BadRequest(new { success = false, message = "Cơ sở cũ hoặc mới không tồn tại / đang tạm ngưng." });
                        }
                    }

                    using (var updateCmd = new SqlCommand(@"
                        UPDATE dbo.NhanSu SET CoSoID = @newId, UpdatedAt = GETDATE()
                        WHERE NhanSuID = @staffId AND CoSoID = @oldId", conn, trans))
                    {
                        Add(updateCmd, "@newId", SqlDbType.Int, model.CoSoMoiId);
                        Add(updateCmd, "@staffId", SqlDbType.Int, model.NhanSuId);
                        Add(updateCmd, "@oldId", SqlDbType.Int, oldBranch);
                        if (await updateCmd.ExecuteNonQueryAsync() != 1)
                            throw new DBConcurrencyException("Nhân sự vừa được thay đổi bởi yêu cầu khác.");
                    }

                    int transferId;
                    using (var insertCmd = new SqlCommand(@"
                        INSERT INTO dbo.LichSuDieuChuyen
                            (NhanSuID, CoSoCuID, CoSoMoiID, NgayDeXuat, NgayHieuLuc,
                             NguoiDeXuatID, NguoiDuyetID, TrangThai, LyDo,
                             IsDeleted, CreatedAt)
                        OUTPUT INSERTED.DieuChuyenID
                        VALUES(@staffId, @oldId, @newId, GETDATE(), GETDATE(),
                               NULL, @approver, N'Đã phê duyệt', @reason,
                               0, GETDATE())", conn, trans))
                    {
                        Add(insertCmd, "@staffId", SqlDbType.Int, model.NhanSuId);
                        Add(insertCmd, "@oldId", SqlDbType.Int, oldBranch);
                        Add(insertCmd, "@newId", SqlDbType.Int, model.CoSoMoiId);
                        Add(insertCmd, "@approver", SqlDbType.Int, approverId.Value);
                        Add(insertCmd, "@reason", SqlDbType.NVarChar, reason, 300);
                        transferId = Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
                    }
                    await trans.CommitAsync();
                    return Json(new
                    {
                        success = true,
                        message = $"Đã điều chuyển {staffName} sang {newBranchName}.",
                        dieuChuyenId = transferId
                    });
                }
                catch
                {
                    await trans.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex) { return Fail(ex, "DieuChuyenNhanSu"); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BaoCaoDuLieu([FromBody] BaoCaoFilterDto? filter)
        {
            if (!TryBuildReportRange(filter, out var range, out var error))
                return BadRequest(new { success = false, message = error });

            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();

                var result = new BaoCaoTongHopDto
                {
                    KyBaoCao = range,
                    CapNhatLuc = DateTime.Now
                };

                const string sql = @"
DECLARE @fromExclusive datetime2 = @fromDate;
DECLARE @toExclusive datetime2 = @toDate;

SELECT CASE WHEN @groupLevel = 0 THEN CONVERT(date, p.NgayThanhToan)
            ELSE DATEFROMPARTS(YEAR(p.NgayThanhToan), MONTH(p.NgayThanhToan), 1) END AS Bucket,
       SUM(p.SoTien) AS Total
FROM dbo.ThanhToan p
WHERE p.IsDeleted = 0 AND p.NgayThanhToan >= @fromExclusive AND p.NgayThanhToan < @toExclusive
  AND p.SoTien > 0
GROUP BY CASE WHEN @groupLevel = 0 THEN CONVERT(date, p.NgayThanhToan)
              ELSE DATEFROMPARTS(YEAR(p.NgayThanhToan), MONTH(p.NgayThanhToan), 1) END
ORDER BY Bucket;

WITH FirstRegistration AS
(
    SELECT d.HocVienID, MIN(d.NgayDangKy) AS FirstDate
    FROM dbo.DangKy d
    WHERE d.IsDeleted = 0 AND d.TrangThai NOT IN (N'Hủy', N'Đã hủy')
    GROUP BY d.HocVienID
)
SELECT CASE WHEN @groupLevel = 0 THEN CONVERT(date, f.FirstDate)
            ELSE DATEFROMPARTS(YEAR(f.FirstDate), MONTH(f.FirstDate), 1) END AS Bucket,
       COUNT(*) AS NewStudents,
       0 AS ActiveStudents,
       0 AS CompletedStudents
FROM FirstRegistration f
WHERE f.FirstDate >= @fromExclusive AND f.FirstDate < @toExclusive
GROUP BY CASE WHEN @groupLevel = 0 THEN CONVERT(date, f.FirstDate)
              ELSE DATEFROMPARTS(YEAR(f.FirstDate), MONTH(f.FirstDate), 1) END
UNION ALL
SELECT CASE WHEN @groupLevel = 0 THEN CONVERT(date, d.NgayDangKy)
            ELSE DATEFROMPARTS(YEAR(d.NgayDangKy), MONTH(d.NgayDangKy), 1) END,
       0,
       SUM(CASE WHEN d.TrangThai = N'Đang học' THEN 1 ELSE 0 END),
       SUM(CASE WHEN d.TrangThai IN (N'Hoàn thành', N'Đã hoàn thành') OR d.KetQua IS NOT NULL THEN 1 ELSE 0 END)
FROM dbo.DangKy d
WHERE d.IsDeleted = 0 AND d.TrangThai NOT IN (N'Hủy', N'Đã hủy')
  AND d.NgayDangKy >= @fromExclusive AND d.NgayDangKy < @toExclusive
GROUP BY CASE WHEN @groupLevel = 0 THEN CONVERT(date, d.NgayDangKy)
              ELSE DATEFROMPARTS(YEAR(d.NgayDangKy), MONTH(d.NgayDangKy), 1) END
ORDER BY Bucket;

SELECT k.KhoaHocID, k.TenKhoaHoc, COUNT(DISTINCT d.HocVienID) AS StudentCount
FROM dbo.DangKy d
INNER JOIN dbo.LopHoc l ON l.LopHocID = d.LopHocID AND l.IsDeleted = 0
INNER JOIN dbo.KhoaHoc k ON k.KhoaHocID = l.KhoaHocID AND k.IsDeleted = 0
WHERE d.IsDeleted = 0 AND d.TrangThai NOT IN (N'Hủy', N'Đã hủy')
  AND d.NgayDangKy >= @fromExclusive AND d.NgayDangKy < @toExclusive
GROUP BY k.KhoaHocID, k.TenKhoaHoc
ORDER BY k.TenKhoaHoc;

SELECT c.CoSoID, c.TenCoSo,
       (SELECT COUNT(*) FROM dbo.PhongHoc p WHERE p.CoSoID = c.CoSoID AND p.IsDeleted = 0) AS RoomCount,
       (SELECT COUNT(*) FROM dbo.NhanSu n WHERE n.CoSoID = c.CoSoID AND n.IsDeleted = 0 AND n.TrangThai = N'Đang làm') AS StaffCount
FROM dbo.CoSo c
WHERE c.IsDeleted = 0 AND c.TrangThai = 1 AND (@coSoId IS NULL OR c.CoSoID = @coSoId)
ORDER BY c.TenCoSo, c.CoSoID;

SELECT l.LopHocID, l.TenLop,
       SUM(CASE WHEN dd.TrangThai IN (N'Đúng giờ', N'Có mặt') THEN 1 ELSE 0 END) AS OnTime,
       SUM(CASE WHEN dd.TrangThai IN (N'Đi muộn', N'Muộn') THEN 1 ELSE 0 END) AS Late,
       SUM(CASE WHEN dd.TrangThai IN (N'Vắng có phép', N'Có phép') THEN 1 ELSE 0 END) AS Excused,
       SUM(CASE WHEN dd.TrangThai IN (N'Vắng không phép', N'Vắng') THEN 1 ELSE 0 END) AS Unexcused,
       COUNT(dd.DiemDanhID) AS Marked,
       (SELECT COUNT(*) FROM dbo.DangKy d WHERE d.LopHocID = l.LopHocID AND d.IsDeleted = 0 AND d.TrangThai NOT IN (N'Hủy', N'Đã hủy'))
       * (SELECT COUNT(*) FROM dbo.LichHoc lh2 WHERE lh2.LopHocID = l.LopHocID AND lh2.IsDeleted = 0
                                      AND lh2.NgayHoc >= @fromExclusive AND lh2.NgayHoc < @toExclusive
                                      AND lh2.TrangThai NOT IN (N'Đã hủy', N'Hủy')) AS Expected
FROM dbo.LopHoc l
INNER JOIN dbo.LichHoc lh ON lh.LopHocID = l.LopHocID AND lh.IsDeleted = 0
LEFT JOIN dbo.DiemDanh dd ON dd.LichHocID = lh.LichHocID AND dd.IsDeleted = 0
WHERE l.IsDeleted = 0 AND lh.NgayHoc >= @fromExclusive AND lh.NgayHoc < @toExclusive
  AND lh.TrangThai NOT IN (N'Đã hủy', N'Hủy')
  AND (@coSoId IS NULL OR l.CoSoID = @coSoId)
GROUP BY l.LopHocID, l.TenLop
ORDER BY l.TenLop, l.LopHocID;";

                await using var command = new SqlCommand(sql, conn);
                Add(command, "@fromDate", SqlDbType.DateTime2, range.TuNgay);
                Add(command, "@toDate", SqlDbType.DateTime2, range.DenNgay.AddDays(1));
                Add(command, "@groupLevel", SqlDbType.Int, range.MucNhom == "Ngày" ? 0 : 1);
                Add(command, "@coSoId", SqlDbType.Int, filter?.CoSoId);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var value = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                    result.DoanhThu.Add(new BaoCaoSeriesPointDto
                    {
                        Nhan = reader.GetDateTime(0).ToString(range.MucNhom == "Ngày" ? "dd/MM" : "MM/yyyy"),
                        GiaTri = value
                    });
                    result.TongDoanhThu += value;
                }

                await reader.NextResultAsync();
                var growth = new Dictionary<string, BaoCaoGrowthPointDto>();
                while (await reader.ReadAsync())
                {
                    var key = reader.GetDateTime(0).ToString(range.MucNhom == "Ngày" ? "dd/MM" : "MM/yyyy");
                    if (!growth.TryGetValue(key, out var point))
                    {
                        point = new BaoCaoGrowthPointDto { Nhan = key };
                        growth[key] = point;
                    }
                    point.HocVienMoi += reader.GetInt32(1);
                    point.DangHoc += reader.GetInt32(2);
                    point.HoanThanh += reader.GetInt32(3);
                }
                result.TangTruongHocVien = growth.Values.ToList();
                result.HocVienMoi = result.TangTruongHocVien.Sum(x => x.HocVienMoi);
                result.HocVienDangHoc = result.TangTruongHocVien.Sum(x => x.DangHoc);
                result.HocVienHoanThanh = result.TangTruongHocVien.Sum(x => x.HoanThanh);

                await reader.NextResultAsync();
                var distributionTotal = 0;
                var distribution = new List<(string Name, int Count)>();
                while (await reader.ReadAsync())
                {
                    var count = reader.GetInt32(2);
                    distribution.Add((reader.GetString(1), count));
                    distributionTotal += count;
                }
                result.PhanBoKhoaHoc = distribution.Select(x => new BaoCaoDistributionDto
                {
                    Nhan = x.Name,
                    SoLuong = x.Count,
                    TyLe = distributionTotal == 0 ? 0 : Math.Round(x.Count * 100m / distributionTotal, 2)
                }).ToList();

                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                    result.QuyMoCoSo.Add(new BaoCaoBranchSizeDto
                    {
                        CoSoId = reader.GetInt32(0),
                        TenCoSo = reader.GetString(1),
                        SoPhong = reader.GetInt32(2),
                        SoNhanSu = reader.GetInt32(3)
                    });

                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    var onTime = reader.GetInt32(2);
                    var late = reader.GetInt32(3);
                    var excused = reader.GetInt32(4);
                    var unexcused = reader.GetInt32(5);
                    var marked = reader.GetInt32(6);
                    var expected = reader.GetInt32(7);
                    result.ChuyenCan.Add(new BaoCaoAttendanceDto
                    {
                        Nhan = reader.GetString(1),
                        DungGio = onTime,
                        DiMuon = late,
                        VangCoPhep = excused,
                        VangKhongPhep = unexcused,
                        ChuaDiemDanh = Math.Max(0, expected - marked),
                        TongDaDiemDanh = marked
                    });
                }

                return Json(new { success = true, data = result });
            }
            catch (Exception ex) { return Fail(ex, "BaoCaoDuLieu"); }
        }

        [HttpGet]
        public async Task<IActionResult> BaoCaoCoSoLuaChon()
            => await DanhSachCoSoLuaChon();

        private static bool TryBuildReportRange(BaoCaoFilterDto? filter, out BaoCaoRange range, out string error)
        {
            filter ??= new BaoCaoFilterDto();
            var now = DateTime.Now;
            var type = (filter.KieuThoiGian ?? "month").Trim().ToLowerInvariant();
            DateTime from;
            DateTime to;
            if (type == "quarter")
            {
                var year = filter.Nam ?? now.Year;
                var quarter = filter.Quy ?? ((now.Month - 1) / 3 + 1);
                if (year < 1 || quarter is < 1 or > 4) { range = new(); error = "Quý hoặc năm không hợp lệ."; return false; }
                var firstMonth = (quarter - 1) * 3 + 1;
                from = new DateTime(year, firstMonth, 1);
                to = from.AddMonths(3).AddDays(-1);
            }
            else if (type == "year")
            {
                var year = filter.Nam ?? now.Year;
                if (year < 1 || year > 9999) { range = new(); error = "Năm không hợp lệ."; return false; }
                from = new DateTime(year, 1, 1);
                to = new DateTime(year, 12, 31);
            }
            else if (type == "range")
            {
                if (!filter.TuNgay.HasValue || !filter.DenNgay.HasValue) { range = new(); error = "Vui lòng chọn đầy đủ khoảng ngày."; return false; }
                from = filter.TuNgay.Value.Date;
                to = filter.DenNgay.Value.Date;
                if (to < from) { range = new(); error = "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu."; return false; }
            }
            else
            {
                var year = filter.Nam ?? now.Year;
                var month = filter.Thang ?? now.Month;
                if (year < 1 || year > 9999 || month is < 1 or > 12) { range = new(); error = "Tháng hoặc năm không hợp lệ."; return false; }
                from = new DateTime(year, month, 1);
                to = from.AddMonths(1).AddDays(-1);
            }

            range = new BaoCaoRange
            {
                TuNgay = from,
                DenNgay = to,
                MucNhom = (type == "month" && from.Day == 1 && to.Month == from.Month) ? "Ngày" : "Tháng",
                NhanKy = from == to ? from.ToString("dd/MM/yyyy") : $"{from:dd/MM/yyyy} - {to:dd/MM/yyyy}"
            };
            error = "";
            return true;
        }
    }

    public class CoSoIdDto
    {
        public int CoSoId { get; set; }
    }

    public class CoSoDto
    {
        public int CoSoId { get; set; }
        public string? TenCoSo { get; set; }
        public string? DiaChi { get; set; }
        public string? SoDienThoai { get; set; }
    }

    public class DoiTrangThaiCoSoDto
    {
        public int CoSoId { get; set; }
        public bool TrangThai { get; set; }
    }

    public class DieuChuyenDto
    {
        public int NhanSuId { get; set; }
        public int CoSoMoiId { get; set; }
        public string? LyDo { get; set; }
    }
}
