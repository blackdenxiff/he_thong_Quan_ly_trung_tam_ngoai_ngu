using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ========================= LOGIN / LOGOUT =========================

        [HttpGet]
        public IActionResult Login()
        {
            if (Request.Cookies.TryGetValue("RememberedUser", out var rememberedUser))
                ViewBag.RememberedUser = rememberedUser;

            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied(string? returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto model)
        {
            if (model == null ||
                string.IsNullOrWhiteSpace(model.Username) ||
                string.IsNullOrWhiteSpace(model.Password))
            {
                return Json(new { success = false, message = "Vui lòng nhập tên đăng nhập và mật khẩu." });
            }

            var username = model.Username.Trim();
            var user = await _context.NguoiDungs
                .Include(x => x.VaiTro)
                .Include(x => x.HocVien)
                .Include(x => x.NhanSu)
                .FirstOrDefaultAsync(x => x.TenDangNhap == username && x.TrangThai && !x.IsDeleted);

            if (user == null)
                return Json(new { success = false, message = "Tài khoản hoặc mật khẩu không chính xác." });

            var hasher = new PasswordHasher<NguoiDung>();
            PasswordVerificationResult verify;
            try
            {
                verify = hasher.VerifyHashedPassword(user, user.MatKhauHash, model.Password);
            }
            catch
            {
                verify = PasswordVerificationResult.Failed;
            }

            if (verify == PasswordVerificationResult.Failed)
                return Json(new { success = false, message = "Tài khoản hoặc mật khẩu không chính xác." });

            var role = user.VaiTro?.TenVaiTro ?? "User";
            var fullName = user.HocVien?.HoTen ?? user.NhanSu?.HoTen ?? user.TenDangNhap;

            HttpContext.Session.SetString("Username", user.TenDangNhap);
            HttpContext.Session.SetString("UserRole", role);
            HttpContext.Session.SetString("UserFullName", fullName);
            HttpContext.Session.SetInt32("NguoiDungID", user.NguoiDungID);

            if (user.NhanSu != null)
                HttpContext.Session.SetInt32("NhanSuID", user.NhanSu.NhanSuID);
            if (user.HocVien != null)
                HttpContext.Session.SetInt32("HocVienID", user.HocVien.HocVienID);

            if (model.RememberMe)
            {
                Response.Cookies.Append("RememberedUser", user.TenDangNhap, new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddDays(30),
                    HttpOnly = true,
                    IsEssential = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax
                });
            }
            else
            {
                Response.Cookies.Delete("RememberedUser");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.NguoiDungID.ToString()),
                new Claim(ClaimTypes.Name, fullName),
                new Claim(ClaimTypes.Role, role)
            };

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = model.RememberMe });

            return Json(new
            {
                success = true,
                message = $"Đăng nhập thành công. Xin chào {fullName}.",
                role,
                redirectUrl = user.DoiMatKhauLanDau ? "/Account/DoiMatKhau" : "/"
            });
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // ========================= ACCOUNT MANAGEMENT =========================

        [HttpGet]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> Index()
        {
            ViewBag.VaiTros = await _context.VaiTros
                .AsNoTracking()
                .OrderBy(x => x.VaiTroID)
                .Select(x => new RoleOptionDto
                {
                    VaiTroID = x.VaiTroID,
                    TenVaiTro = x.TenVaiTro
                })
                .ToListAsync();

            return View();
        }

        [HttpGet]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> GetAccounts(
            string keyword = "",
            string role = "",
            string trangThai = "",
            int page = 1,
            int pageSize = 10)
        {
            page = Math.Clamp(page, 1, 1000000);
            pageSize = 10; // Cố định 10 tài khoản/trang, không cho client tăng.
            keyword = (keyword ?? "").Trim();
            role = (role ?? "").Trim();
            trangThai = (trangThai ?? "").Trim();

            var query = _context.NguoiDungs
                .AsNoTracking()
                .Where(u => !u.IsDeleted)
                .Include(u => u.VaiTro)
                .Include(u => u.HocVien)
                .Include(u => u.NhanSu)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(u =>
                    u.TenDangNhap.Contains(keyword) ||
                    (u.HocVien != null &&
                        ((u.HocVien.HoTen != null && u.HocVien.HoTen.Contains(keyword)) ||
                         (u.HocVien.Email != null && u.HocVien.Email.Contains(keyword)))) ||
                    (u.NhanSu != null &&
                        ((u.NhanSu.HoTen != null && u.NhanSu.HoTen.Contains(keyword)) ||
                         (u.NhanSu.Email != null && u.NhanSu.Email.Contains(keyword)))));
            }

            if (!string.IsNullOrWhiteSpace(role) && role != "all" && role != "0")
            {
                if (int.TryParse(role, out var roleId))
                    query = query.Where(u => u.VaiTroID == roleId);
                else
                    query = query.Where(u => u.VaiTro != null && u.VaiTro.TenVaiTro == role);
            }

            if (trangThai == "Hoạt động" || trangThai == "active" || trangThai == "1")
                query = query.Where(u => u.TrangThai);
            else if (trangThai == "Đã khóa" || trangThai == "locked" || trangThai == "0")
                query = query.Where(u => !u.TrangThai);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages > 0 && page > totalPages)
                page = totalPages;

            var items = await query
                .OrderBy(u => u.NguoiDungID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    nguoiDungId = u.NguoiDungID,
                    tenDangNhap = u.TenDangNhap,
                    hoTen = u.HocVien != null
                        ? u.HocVien.HoTen
                        : (u.NhanSu != null ? u.NhanSu.HoTen : u.TenDangNhap),
                    email = u.HocVien != null
                        ? u.HocVien.Email
                        : (u.NhanSu != null ? u.NhanSu.Email : null),
                    vaiTro = u.VaiTro != null ? u.VaiTro.TenVaiTro : "Chưa phân quyền",
                    vaiTroId = u.VaiTroID,
                    trangThai = u.TrangThai ? "Hoạt động" : "Đã khóa"
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                items,
                currentPage = page,
                totalPages,
                totalItems
            });
        }

        // Chỉ tải 10 hồ sơ chưa có tài khoản theo từ khóa và trang.
        [HttpGet]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> GetUnlinkedPersons(string loai, string keyword = "", int page = 1)
        {
            const int pageSize = 10;
            page = Math.Clamp(page, 1, 1000000);
            loai = (loai ?? "").Trim();
            keyword = (keyword ?? "").Trim();

            if (keyword.Length > 100)
                return BadRequest(new { success = false, message = "Từ khóa quá dài." });

            if (loai.Equals("HocVien", StringComparison.OrdinalIgnoreCase))
            {
                var query = _context.HocViens.AsNoTracking().Where(h => h.NguoiDungID == null);
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    if (keyword.StartsWith("HV", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!int.TryParse(keyword.Substring(2), out int hocVienId))
                            return Json(new
                            {
                                success = true,
                                items = Array.Empty<object>(),
                                currentPage = 1,
                                totalPages = 0,
                                totalItems = 0,
                                hasMore = false
                            });
                        query = query.Where(h => h.HocVienID == hocVienId);
                    }
                    else query = query.Where(h => h.HoTen.Contains(keyword) ||
                        h.HocVienID.ToString().Contains(keyword));
                }

                var totalItems = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
                var rows = await query.OrderBy(h => h.HocVienID)
                    .Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(h => new { id = h.HocVienID, hoTen = h.HoTen, ngaySinh = h.NgaySinh })
                    .ToListAsync();
                return Json(new
                {
                    success = true,
                    items = rows.Select(x => new
                    {
                        x.id,
                        ma = "HV" + x.id.ToString("D3"),
                        x.hoTen,
                        ngaySinh = x.ngaySinh.HasValue ? x.ngaySinh.Value.ToString("dd/MM/yyyy") : ""
                    }),
                    currentPage = page,
                    totalPages,
                    totalItems,
                    hasMore = page < totalPages
                });
            }

            if (loai.Equals("NhanSu", StringComparison.OrdinalIgnoreCase) ||
                loai.Equals("GiangVien", StringComparison.OrdinalIgnoreCase))
            {
                var query = _context.NhanSus.AsNoTracking().Where(n => n.NguoiDungID == null);
                if (loai.Equals("GiangVien", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(n => n.LoaiNhanSu == "Giáo viên" || n.LoaiNhanSu == "Cả hai");
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    if (keyword.StartsWith("NS", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!int.TryParse(keyword.Substring(2), out int nhanSuId))
                            return Json(new
                            {
                                success = true,
                                items = Array.Empty<object>(),
                                currentPage = 1,
                                totalPages = 0,
                                totalItems = 0,
                                hasMore = false
                            });
                        query = query.Where(n => n.NhanSuID == nhanSuId);
                    }
                    else query = query.Where(n => n.HoTen.Contains(keyword) ||
                        n.NhanSuID.ToString().Contains(keyword));
                }

                var totalItems = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
                var rows = await query.OrderBy(n => n.NhanSuID)
                    .Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(n => new { id = n.NhanSuID, hoTen = n.HoTen, ngaySinh = n.NgaySinh })
                    .ToListAsync();
                return Json(new
                {
                    success = true,
                    items = rows.Select(x => new
                    {
                        x.id,
                        ma = "NS" + x.id.ToString("D3"),
                        x.hoTen,
                        ngaySinh = x.ngaySinh.HasValue ? x.ngaySinh.Value.ToString("dd/MM/yyyy") : ""
                    }),
                    currentPage = page,
                    totalPages,
                    totalItems,
                    hasMore = page < totalPages
                });
            }

            return BadRequest(new { success = false, message = "Loại đối tượng không hợp lệ." });
        }

        // Thống kê dùng COUNT tại SQL Server, không tải toàn bộ tài khoản về trình duyệt.
        [HttpGet]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> GetAccountStats()
        {
            var accounts = _context.NguoiDungs.AsNoTracking();
            var totalAccounts = await accounts.CountAsync();
            var adminManagers = await accounts.CountAsync(u => u.VaiTro != null &&
                (u.VaiTro.TenVaiTro == "Admin" || u.VaiTro.TenVaiTro == "QuanLy" ||
                 u.VaiTro.TenVaiTro == "Quản lý"));
            var staffAccounts = await accounts.CountAsync(u => u.NhanSu != null);
            var lockedAccounts = await accounts.CountAsync(u => !u.TrangThai);

            return Json(new { success = true, totalAccounts, adminManagers, staffAccounts, lockedAccounts });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> CapTaiKhoanDonLe([FromBody] CapTaiKhoanDonLeDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.loai) ||
                model.personId <= 0 || string.IsNullOrWhiteSpace(model.tenDangNhap) ||
                model.vaiTroId <= 0)
                return Json(new { success = false, message = "Vui lòng nhập đủ thông tin." });

            var username = model.tenDangNhap.Trim();
            if (username.Length > 50)
                return Json(new { success = false, message = "Tên đăng nhập không được vượt quá 50 ký tự." });
            if (username.Equals("admin", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "Tên đăng nhập admin không được sử dụng." });

            var role = await _context.VaiTros.FindAsync(model.vaiTroId);
            if (role == null)
                return Json(new { success = false, message = "Vai trò không tồn tại." });

            if (await _context.NguoiDungs.IgnoreQueryFilters().AnyAsync(u => u.TenDangNhap == username))
                return Json(new { success = false, message = "Tên đăng nhập đã tồn tại, kể cả trong thùng rác." });

            try
            {
                using var tx = await _context.Database.BeginTransactionAsync();
                var hasher = new PasswordHasher<NguoiDung>();
                var user = new NguoiDung
                {
                    TenDangNhap = username,
                    VaiTroID = model.vaiTroId,
                    TrangThai = true,
                    DoiMatKhauLanDau = true,
                    CreatedAt = DateTime.Now
                };

                if (model.loai.Equals("HocVien", StringComparison.OrdinalIgnoreCase))
                {
                    var person = await _context.HocViens
                        .FirstOrDefaultAsync(h => h.HocVienID == model.personId && h.NguoiDungID == null);
                    if (person == null)
                        return Json(new { success = false, message = "Học viên không tồn tại hoặc đã có tài khoản." });

                    var password = person.NgaySinh?.ToString("ddMMyyyy") ?? "123456";
                    user.MatKhauHash = hasher.HashPassword(user, password);
                    _context.NguoiDungs.Add(user);
                    await _context.SaveChangesAsync();

                    person.NguoiDungID = user.NguoiDungID;
                    person.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
                else if (model.loai.Equals("NhanSu", StringComparison.OrdinalIgnoreCase) ||
                         model.loai.Equals("GiangVien", StringComparison.OrdinalIgnoreCase))
                {
                    var person = await _context.NhanSus
                        .FirstOrDefaultAsync(n => n.NhanSuID == model.personId && n.NguoiDungID == null);
                    if (person == null)
                        return Json(new { success = false, message = "Nhân sự không tồn tại hoặc đã có tài khoản." });

                    var password = person.NgaySinh?.ToString("ddMMyyyy") ?? "123456";
                    user.MatKhauHash = hasher.HashPassword(user, password);
                    _context.NguoiDungs.Add(user);
                    await _context.SaveChangesAsync();

                    person.NguoiDungID = user.NguoiDungID;
                    person.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
                else
                {
                    return Json(new { success = false, message = "Loại đối tượng không hợp lệ." });
                }

                await tx.CommitAsync();
                return Json(new
                {
                    success = true,
                    message = "Tạo tài khoản thành công. Mật khẩu mặc định theo ngày sinh (ddMMyyyy), nếu không có ngày sinh là 123456.",
                    nguoiDungId = user.NguoiDungID
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CapTaiKhoanDonLe error: {ex}");
                return Json(new { success = false, message = "Không thể tạo tài khoản. Kiểm tra log lỗi của ứng dụng." });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> CapTaiKhoanHangLoat([FromBody] CapTaiKhoanHangLoatDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.loai) || model.vaiTroId <= 0)
                return Json(new { success = false, message = "Vui lòng chọn nhóm đối tượng và vai trò." });

            var roleExists = await _context.VaiTros.AnyAsync(r => r.VaiTroID == model.vaiTroId);
            if (!roleExists)
                return Json(new { success = false, message = "Vai trò không tồn tại." });

            var loai = model.loai.Trim();
            var hasher = new PasswordHasher<NguoiDung>();
            var created = 0;

            try
            {
                using var tx = await _context.Database.BeginTransactionAsync();

                if (loai.Equals("HocVien", StringComparison.OrdinalIgnoreCase))
                {
                    var people = await _context.HocViens.Where(h => h.NguoiDungID == null).ToListAsync();
                    foreach (var p in people)
                    {
                        var username = $"HV{p.HocVienID}";
                        if (await _context.NguoiDungs.IgnoreQueryFilters().AnyAsync(u => u.TenDangNhap == username))
                            continue;

                        var user = new NguoiDung
                        {
                            TenDangNhap = username,
                            VaiTroID = model.vaiTroId,
                            TrangThai = true,
                            DoiMatKhauLanDau = true,
                            CreatedAt = DateTime.Now
                        };
                        user.MatKhauHash = hasher.HashPassword(user, p.NgaySinh?.ToString("ddMMyyyy") ?? "123456");
                        _context.NguoiDungs.Add(user);
                        await _context.SaveChangesAsync();
                        p.NguoiDungID = user.NguoiDungID;
                        p.UpdatedAt = DateTime.Now;
                        created++;
                    }
                    await _context.SaveChangesAsync();
                }
                else if (loai.Equals("NhanSu", StringComparison.OrdinalIgnoreCase) ||
                         loai.Equals("GiangVien", StringComparison.OrdinalIgnoreCase))
                {
                    var people = await _context.NhanSus.Where(n => n.NguoiDungID == null).ToListAsync();
                    foreach (var p in people)
                    {
                        var username = $"NS{p.NhanSuID}";
                        if (await _context.NguoiDungs.IgnoreQueryFilters().AnyAsync(u => u.TenDangNhap == username))
                            continue;

                        var user = new NguoiDung
                        {
                            TenDangNhap = username,
                            VaiTroID = model.vaiTroId,
                            TrangThai = true,
                            DoiMatKhauLanDau = true,
                            CreatedAt = DateTime.Now
                        };
                        user.MatKhauHash = hasher.HashPassword(user, p.NgaySinh?.ToString("ddMMyyyy") ?? "123456");
                        _context.NguoiDungs.Add(user);
                        await _context.SaveChangesAsync();
                        p.NguoiDungID = user.NguoiDungID;
                        p.UpdatedAt = DateTime.Now;
                        created++;
                    }
                    await _context.SaveChangesAsync();
                }
                else
                {
                    return Json(new { success = false, message = "Loại đối tượng không hợp lệ." });
                }

                await tx.CommitAsync();
                return Json(new { success = true, created, message = $"Đã tạo {created} tài khoản." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CapTaiKhoanHangLoat error: {ex}");
                return Json(new { success = false, message = "Không thể cấp tài khoản hàng loạt. Kiểm tra log lỗi." });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> DoiTrangThaiKhoa(int id, bool khoa)
        {
            if (id <= 0)
                return Json(new { success = false, message = "ID tài khoản không hợp lệ." });

            var user = await _context.NguoiDungs.FirstOrDefaultAsync(u => u.NguoiDungID == id && !u.IsDeleted);
            if (user == null)
                return Json(new { success = false, message = "Không tìm thấy tài khoản." });

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (khoa && currentUserId == id.ToString())
                return Json(new { success = false, message = "Bạn không thể tự khóa tài khoản đang đăng nhập." });

            user.TrangThai = !khoa;
            user.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = khoa ? "Đã khóa tài khoản." : "Đã mở khóa tài khoản.",
                trangThai = user.TrangThai ? "Hoạt động" : "Đã khóa"
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> SoftDelete([FromBody] int id)
        {
            if (id <= 0)
                return Json(new { success = false, message = "ID không hợp lệ." });

            var user = await _context.NguoiDungs.FirstOrDefaultAsync(u => u.NguoiDungID == id && !u.IsDeleted);
            if (user == null)
                return Json(new { success = false, message = "Tài khoản không tồn tại hoặc đã bị xóa." });

            if (User.FindFirstValue(ClaimTypes.NameIdentifier) == id.ToString())
                return Json(new { success = false, message = "Bạn không thể xóa mềm tài khoản đang đăng nhập." });

            user.IsDeleted = true;
            user.DeletedAt = DateTime.Now;
            user.TrangThai = false;
            user.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã chuyển tài khoản vào thùng rác.", nguoiDungId = id });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> CapNhatQuyen(int id, int vaiTroMoi)
        {
            if (id <= 0 || vaiTroMoi <= 0)
                return Json(new { success = false, message = "Tài khoản hoặc vai trò không hợp lệ." });

            var user = await _context.NguoiDungs.FirstOrDefaultAsync(u => u.NguoiDungID == id && !u.IsDeleted);
            if (user == null)
                return Json(new { success = false, message = "Không tìm thấy tài khoản." });

            if (!await _context.VaiTros.AnyAsync(r => r.VaiTroID == vaiTroMoi))
                return Json(new { success = false, message = "Vai trò không tồn tại." });

            user.VaiTroID = vaiTroMoi;
            user.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var roleName = await _context.VaiTros
                .Where(r => r.VaiTroID == vaiTroMoi)
                .Select(r => r.TenVaiTro)
                .FirstAsync();

            return Json(new { success = true, message = "Cập nhật vai trò thành công.", vaiTro = roleName, vaiTroId = vaiTroMoi });
        }

        // ========================= PASSWORD CHANGE =========================

        [HttpGet]
        [Authorize]
        public IActionResult DoiMatKhau() => View();

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> DoiMatKhau([FromBody] DoiMatKhauDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.OldPassword) ||
                string.IsNullOrWhiteSpace(model.NewPassword) ||
                string.IsNullOrWhiteSpace(model.ConfirmNewPassword))
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin." });

            if (model.NewPassword.Length < 8)
                return Json(new { success = false, message = "Mật khẩu mới phải có ít nhất 8 ký tự." });

            if (model.NewPassword != model.ConfirmNewPassword)
                return Json(new { success = false, message = "Mật khẩu xác nhận không khớp." });

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Json(new { success = false, message = "Không xác định được người dùng." });

            var user = await _context.NguoiDungs.FirstOrDefaultAsync(u => u.NguoiDungID == userId && !u.IsDeleted);
            if (user == null)
                return Json(new { success = false, message = "Người dùng không tồn tại." });

            var hasher = new PasswordHasher<NguoiDung>();
            PasswordVerificationResult verify;
            try { verify = hasher.VerifyHashedPassword(user, user.MatKhauHash, model.OldPassword); }
            catch { verify = PasswordVerificationResult.Failed; }

            if (verify == PasswordVerificationResult.Failed)
                return Json(new { success = false, message = "Mật khẩu cũ không đúng." });

            user.MatKhauHash = hasher.HashPassword(user, model.NewPassword);
            user.DoiMatKhauLanDau = false;
            user.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đổi mật khẩu thành công." });
        }

        // ========================= RECYCLE BIN =========================

        [HttpGet]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> Trash(string keyword = "", int page = 1)
        {
            const int pageSize = 10;
            page = Math.Clamp(page, 1, 1000000);
            keyword = (keyword ?? "").Trim();
            if (keyword.Length > 100)
                return BadRequest(new { success = false, message = "Từ khóa quá dài." });

            var query = _context.NguoiDungs.IgnoreQueryFilters().AsNoTracking()
                .Where(u => u.IsDeleted);
            if (keyword.Length > 0)
                query = query.Where(u => u.TenDangNhap.Contains(keyword));

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
            var items = await query
                .OrderByDescending(u => u.DeletedAt)
                .ThenByDescending(u => u.NguoiDungID)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(u => new
                {
                    nguoiDungId = u.NguoiDungID,
                    tenDangNhap = u.TenDangNhap,
                    deletedAt = u.DeletedAt,
                    vaiTro = u.VaiTro != null ? u.VaiTro.TenVaiTro : "Chưa xác định"
                })
                .ToListAsync();

            return Json(new { success = true, items, currentPage = page, totalPages, totalItems });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> Restore(int id)
        {
            var user = await _context.NguoiDungs.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.NguoiDungID == id && u.IsDeleted);

            if (user == null)
                return Json(new { success = false, message = "Tài khoản không tồn tại trong thùng rác." });

            user.IsDeleted = false;
            user.DeletedAt = null;
            user.TrangThai = true;
            user.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Khôi phục tài khoản thành công." });
        }

    }

    public class LoginRequestDto
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public bool RememberMe { get; set; }
    }

    public class CapTaiKhoanDonLeDto
    {
        public string loai { get; set; } = "";
        public int personId { get; set; }
        public string tenDangNhap { get; set; } = "";
        public string matKhauPlain { get; set; } = "";
        public int vaiTroId { get; set; }
    }

    public class CapTaiKhoanHangLoatDto
    {
        public string loai { get; set; } = "";
        public int vaiTroId { get; set; }
    }

    public class DoiMatKhauDto
    {
        public string OldPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
        public string ConfirmNewPassword { get; set; } = "";
    }

    public class RoleOptionDto
    {
        public int VaiTroID { get; set; }
        public string TenVaiTro { get; set; } = "";
    }
}
