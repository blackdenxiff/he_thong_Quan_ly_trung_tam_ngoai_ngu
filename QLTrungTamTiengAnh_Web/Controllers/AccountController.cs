using Microsoft.AspNetCore.Mvc;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // PHẦN F01: ĐĂNG NHẬP / ĐĂNG XUẤT (AUTHENTICATION)
        // ==========================================

        // GET: /Account/Login
        public IActionResult Login()
        {
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<QuanLyTrungTamNgoaiNgu.Models.NguoiDung>();
            string sampleHash = hasher.HashPassword(null, "123456"); // "123456" là mật khẩu bạn muốn đặt cho tài khoản
            System.Diagnostics.Debug.WriteLine("=== CHUOI HASH CUA BAN: " + sampleHash + " ===");

            // Kiểm tra nếu đã có Cookie ghi nhớ thì nạp sẵn tên đăng nhập
            if (Request.Cookies.TryGetValue("RememberedUser", out string rememberedUser))
            {
                ViewBag.RememberedUser = rememberedUser;
            }

            return View();
        }

        // POST: /Account/Login (Xử lý xác thực, Session và Cookie)
        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu!" });
            }

            // Tìm người dùng trong bảng NguoiDungs
            var user = await _context.NguoiDungs
                .Include(u => u.VaiTro)
                .Include(u => u.HocVien)
                .Include(u => u.NhanSu)
                .FirstOrDefaultAsync(u => u.TenDangNhap == model.Username && u.TrangThai);

            if (user == null)
            {
                return Json(new { success = false, message = "Tài khoản hoặc mật khẩu không chính xác!" });
            }

            var hasher = new PasswordHasher<NguoiDung>();
            PasswordVerificationResult verify;
            try
            {
                verify = hasher.VerifyHashedPassword(user, user.MatKhauHash, model.Password);
            }
            catch (FormatException)
            {
                verify = PasswordVerificationResult.Failed;
            }

            if (verify == PasswordVerificationResult.Failed)
            {
                return Json(new { success = false, message = "Tài khoản hoặc mật khẩu không chính xác!" });
            }

            string role = user.VaiTro?.TenVaiTro ?? "User";
            string fullName = user.HocVien?.HoTen ?? user.NhanSu?.HoTen ?? user.TenDangNhap;

            // 1. Lưu thông tin người dùng vào Session (Đầy đủ Key để tương thích hoàn toàn với LichHocController)
            HttpContext.Session.SetString("Username", model.Username);
            HttpContext.Session.SetString("UserName", model.Username);
            HttpContext.Session.SetString("TenDangNhap", model.Username);
            HttpContext.Session.SetString("UserRole", role);
            HttpContext.Session.SetString("Role", role);
            HttpContext.Session.SetString("UserFullName", fullName);

            // 2. Lưu ID cho phép kiểm chứng quyền hạn trên server-side
            if (user.NhanSu != null)
            {
                HttpContext.Session.SetInt32("NhanSuID", user.NhanSu.NhanSuID);
                var bytes = System.BitConverter.GetBytes(user.NhanSu.NhanSuID);
                HttpContext.Session.Set("NhanSuID", bytes);
            }
            if (user.HocVien != null)
            {
                HttpContext.Session.SetInt32("HocVienID", user.HocVien.HocVienID);
                var bytes = System.BitConverter.GetBytes(user.HocVien.HocVienID);
                HttpContext.Session.Set("HocVienID", bytes);
            }
            HttpContext.Session.SetInt32("NguoiDungID", user.NguoiDungID);

            // 3. Ghi nhớ Cookie nếu người dùng tích chọn "Ghi nhớ đăng nhập"
            if (model.RememberMe)
            {
                CookieOptions cookieOpts = new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddDays(30),
                    HttpOnly = true,
                    IsEssential = true
                };
                Response.Cookies.Append("RememberedUser", model.Username, cookieOpts);
            }
            else
            {
                Response.Cookies.Delete("RememberedUser");
            }

            // 4. Tạo claims và đăng nhập bằng Cookie authentication để hỗ trợ [Authorize]
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.NguoiDungID.ToString()),
                new Claim(ClaimTypes.Name, fullName),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                new AuthenticationProperties { IsPersistent = model.RememberMe });

            var redirect = user.DoiMatKhauLanDau ? "/Account/DoiMatKhau" : "/";

            return Json(new
            {
                success = true,
                message = $"Đăng nhập thành công! Xin chào {fullName}.",
                role = role,
                redirectUrl = redirect
            });
        }

        // GET: /Account/Logout (Xóa Session và trở về trang Login)
        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); // Hủy toàn bộ dữ liệu Session
            return RedirectToAction("Login");
        }

        // ==========================================
        // PHẦN F02: QUẢN LÝ TÀI KHOẢN & PHÂN QUYỀN (ADMIN)
        // ==========================================

        // GET: /Account/Index (Hiển thị bảng danh sách tài khoản & phân quyền)
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> Index()
        {
            var roles = await _context.VaiTros.OrderBy(r => r.VaiTroID)
                .Select(r => new { r.VaiTroID, r.TenVaiTro })
                .ToListAsync();
            ViewBag.VaiTros = roles;
            return View();
        }

        // GET: /Account/GetUnlinkedPersons?loai=HocVien|NhanSu
        [Authorize(Roles = "Admin,QuanLy")]
        [HttpGet]
        public async Task<IActionResult> GetUnlinkedPersons(string loai)
        {
            if (string.IsNullOrWhiteSpace(loai))
                return Json(new { success = false, message = "Thiếu loại đối tượng (loai)." });

            if (loai.Equals("HocVien", StringComparison.OrdinalIgnoreCase))
            {
                var list = await _context.HocViens
                    .Where(h => h.NguoiDungID == null)
                    .Select(h => new { h.HocVienID, h.HoTen, h.NgaySinh })
                    .ToListAsync();

                var items = list.Select(x => new
                {
                    id = x.HocVienID,
                    ma = "HV" + x.HocVienID.ToString("D3"),
                    hoTen = x.HoTen,
                    ngaySinh = x.NgaySinh.HasValue ? x.NgaySinh.Value.ToString("dd/MM/yyyy") : null
                }).ToList();

                return Json(new { success = true, items });
            }

            if (loai.Equals("NhanSu", StringComparison.OrdinalIgnoreCase) || loai.Equals("GiangVien", StringComparison.OrdinalIgnoreCase))
            {
                var list = await _context.NhanSus
                    .Where(n => n.NguoiDungID == null)
                    .Select(n => new { n.NhanSuID, n.HoTen, n.NgaySinh })
                    .ToListAsync();

                var items = list.Select(x => new
                {
                    id = x.NhanSuID,
                    ma = "NS" + x.NhanSuID.ToString("D3"),
                    hoTen = x.HoTen,
                    ngaySinh = x.NgaySinh.HasValue ? x.NgaySinh.Value.ToString("dd/MM/yyyy") : null
                }).ToList();

                return Json(new { success = true, items });
            }

            return Json(new { success = false, message = "Loại đối tượng không hợp lệ. Hãy dùng 'HocVien' hoặc 'NhanSu'." });
        }

        // POST: /Account/LuuTaiKhoan
        [HttpPost]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> LuuTaiKhoan([FromBody] TaiKhoanDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TenDangNhap))
            {
                return Json(new { success = false, message = "Vui lòng nhập tên đăng nhập!" });
            }

            if (model.TenDangNhap.Trim().ToLower() == "admin")
            {
                return Json(new { success = false, message = "Tên đăng nhập 'admin' đã tồn tại trong hệ thống!" });
            }

            return Json(new
            {
                success = true,
                message = "Khởi tạo tài khoản và phân quyền thành công!",
                data = model
            });
        }

        // POST: /Account/CapTaiKhoanDonLe
        [HttpPost]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> CapTaiKhoanDonLe([FromBody] CapTaiKhoanDonLeDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.loai) || model.personId <= 0 || string.IsNullOrWhiteSpace(model.tenDangNhap) || model.vaiTroId <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var role = await _context.VaiTros.FindAsync(model.vaiTroId);
            if (role == null) return Json(new { success = false, message = "Vai trò không tồn tại." });

            NguoiDung createdUser = null;
            try
            {
                if (model.loai.Equals("HocVien", StringComparison.OrdinalIgnoreCase))
                {
                    var hv = await _context.HocViens.FirstOrDefaultAsync(h => h.HocVienID == model.personId && h.NguoiDungID == null);
                    if (hv == null) return Json(new { success = false, message = "Học viên không tồn tại hoặc đã có tài khoản." });

                    var exists = await _context.NguoiDungs.AnyAsync(u => u.TenDangNhap == model.tenDangNhap);
                    if (exists) return Json(new { success = false, message = "Tên đăng nhập đã tồn tại." });

                    string defaultPwd = hv.NgaySinh?.ToString("ddMMyyyy") ?? "123456";

                    using (var tx = await _context.Database.BeginTransactionAsync())
                    {
                        var user = new NguoiDung
                        {
                            TenDangNhap = model.tenDangNhap,
                            VaiTroID = model.vaiTroId,
                            TrangThai = true,
                            DoiMatKhauLanDau = true,
                            CreatedAt = DateTime.Now
                        };
                        var hasher = new PasswordHasher<NguoiDung>();
                        user.MatKhauHash = hasher.HashPassword(user, defaultPwd);
                        _context.NguoiDungs.Add(user);
                        await _context.SaveChangesAsync();

                        hv.NguoiDungID = user.NguoiDungID;
                        hv.UpdatedAt = DateTime.Now;
                        _context.HocViens.Update(hv);
                        await _context.SaveChangesAsync();

                        await tx.CommitAsync();
                        createdUser = user;
                    }
                }
                else if (model.loai.Equals("NhanSu", StringComparison.OrdinalIgnoreCase) || model.loai.Equals("GiangVien", StringComparison.OrdinalIgnoreCase))
                {
                    var ns = await _context.NhanSus.FirstOrDefaultAsync(n => n.NhanSuID == model.personId && n.NguoiDungID == null);
                    if (ns == null) return Json(new { success = false, message = "Nhân sự/giảng viên không tồn tại hoặc đã có tài khoản." });

                    var exists = await _context.NguoiDungs.AnyAsync(u => u.TenDangNhap == model.tenDangNhap);
                    if (exists) return Json(new { success = false, message = "Tên đăng nhập đã tồn tại." });

                    string defaultPwd = ns.NgaySinh?.ToString("ddMMyyyy") ?? "123456";

                    using (var tx = await _context.Database.BeginTransactionAsync())
                    {
                        var user = new NguoiDung
                        {
                            TenDangNhap = model.tenDangNhap,
                            VaiTroID = model.vaiTroId,
                            TrangThai = true,
                            DoiMatKhauLanDau = true,
                            CreatedAt = DateTime.Now
                        };
                        var hasher = new PasswordHasher<NguoiDung>();
                        user.MatKhauHash = hasher.HashPassword(user, defaultPwd);
                        _context.NguoiDungs.Add(user);
                        await _context.SaveChangesAsync();

                        ns.NguoiDungID = user.NguoiDungID;
                        ns.UpdatedAt = DateTime.Now;
                        _context.NhanSus.Update(ns);
                        await _context.SaveChangesAsync();

                        await tx.CommitAsync();
                        createdUser = user;
                    }
                }
                else
                {
                    return Json(new { success = false, message = "Loại đối tượng không hợp lệ." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi nội bộ khi tạo tài khoản: " + ex.Message });
            }

            return Json(new { success = true, message = "Tạo tài khoản thành công.", nguoiDungId = createdUser?.NguoiDungID });
        }

        // POST: /Account/CapTaiKhoanHangLoat
        [HttpPost]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> CapTaiKhoanHangLoat([FromBody] CapTaiKhoanHangLoatDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.loai) || model.vaiTroId <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var role = await _context.VaiTros.FindAsync(model.vaiTroId);
            if (role == null) return Json(new { success = false, message = "Vai trò không tồn tại." });

            var loaiDoiTuong = model.loai;

            if (loaiDoiTuong.Equals("HocVien", StringComparison.OrdinalIgnoreCase))
            {
                var students = await _context.HocViens.Where(h => h.NguoiDungID == null).ToListAsync();
                var generated = students.Select(h => new { Profile = (object)h, Username = $"HV{h.HocVienID}" }).ToList();

                var usernames = generated.Select(g => g.Username).ToList();
                var dupInBatch = usernames.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                var existsInDb = await _context.NguoiDungs.Where(u => usernames.Contains(u.TenDangNhap)).Select(u => u.TenDangNhap).ToListAsync();

                var duplicates = dupInBatch.Union(existsInDb).ToList();
                if (duplicates.Any())
                {
                    return Json(new { success = false, message = "Trùng tên đăng nhập phát hiện.", duplicates });
                }

                int created = 0;
                try
                {
                    using (var tx = await _context.Database.BeginTransactionAsync())
                    {
                        var usersToAdd = new List<NguoiDung>();
                        var hasher = new PasswordHasher<NguoiDung>();
                        foreach (var g in generated)
                        {
                            var hv = (QuanLyTrungTamNgoaiNgu.Models.HocVien)g.Profile;
                            var user = new NguoiDung
                            {
                                TenDangNhap = g.Username,
                                VaiTroID = model.vaiTroId,
                                TrangThai = true,
                                DoiMatKhauLanDau = true,
                                CreatedAt = DateTime.Now
                            };
                            string defaultPwd = hv.NgaySinh?.ToString("ddMMyyyy") ?? "123456";
                            user.MatKhauHash = hasher.HashPassword(user, defaultPwd);
                            usersToAdd.Add(user);
                        }

                        _context.NguoiDungs.AddRange(usersToAdd);
                        await _context.SaveChangesAsync();

                        for (int i = 0; i < generated.Count; i++)
                        {
                            var hv = (QuanLyTrungTamNgoaiNgu.Models.HocVien)generated[i].Profile;
                            hv.NguoiDungID = usersToAdd[i].NguoiDungID;
                            hv.UpdatedAt = DateTime.Now;
                            _context.HocViens.Update(hv);
                            created++;
                        }
                        await _context.SaveChangesAsync();
                        await tx.CommitAsync();
                    }
                }
                catch (Exception)
                {
                    return Json(new { success = false, message = "Lỗi nội bộ khi tạo tài khoản hàng loạt." });
                }

                return Json(new { success = true, created });
            }
            else if (loaiDoiTuong.Equals("NhanSu", StringComparison.OrdinalIgnoreCase) || loaiDoiTuong.Equals("GiangVien", StringComparison.OrdinalIgnoreCase))
            {
                var staffs = await _context.NhanSus.Where(n => n.NguoiDungID == null).ToListAsync();
                var generated = staffs.Select(n => new { Profile = (object)n, Username = $"NS{n.NhanSuID}" }).ToList();

                var usernames = generated.Select(g => g.Username).ToList();
                var dupInBatch = usernames.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                var existsInDb = await _context.NguoiDungs.Where(u => usernames.Contains(u.TenDangNhap)).Select(u => u.TenDangNhap).ToListAsync();
                var duplicates = dupInBatch.Union(existsInDb).ToList();
                if (duplicates.Any())
                {
                    return Json(new { success = false, message = "Trùng tên đăng nhập phát hiện.", duplicates });
                }

                int created = 0;
                try
                {
                    using (var tx = await _context.Database.BeginTransactionAsync())
                    {
                        var usersToAdd = new List<NguoiDung>();
                        var hasher = new PasswordHasher<NguoiDung>();
                        foreach (var g in generated)
                        {
                            var ns = (QuanLyTrungTamNgoaiNgu.Models.NhanSu)g.Profile;
                            var user = new NguoiDung
                            {
                                TenDangNhap = g.Username,
                                VaiTroID = model.vaiTroId,
                                TrangThai = true,
                                DoiMatKhauLanDau = true,
                                CreatedAt = DateTime.Now
                            };
                            string defaultPwd = ns.NgaySinh?.ToString("ddMMyyyy") ?? "123456";
                            user.MatKhauHash = hasher.HashPassword(user, defaultPwd);
                            usersToAdd.Add(user);
                        }

                        _context.NguoiDungs.AddRange(usersToAdd);
                        await _context.SaveChangesAsync();

                        for (int i = 0; i < generated.Count; i++)
                        {
                            var ns = (QuanLyTrungTamNgoaiNgu.Models.NhanSu)generated[i].Profile;
                            ns.NguoiDungID = usersToAdd[i].NguoiDungID;
                            ns.UpdatedAt = DateTime.Now;
                            _context.NhanSus.Update(ns);
                            created++;
                        }

                        await _context.SaveChangesAsync();
                        await tx.CommitAsync();
                    }
                }
                catch (Exception)
                {
                    return Json(new { success = false, message = "Lỗi nội bộ khi tạo tài khoản hàng loạt." });
                }

                return Json(new { success = true, created });
            }

            return Json(new { success = false, message = "Loại đối tượng không hợp lệ." });
        }

        // GET: /Account/DoiMatKhau
        [Authorize]
        public IActionResult DoiMatKhau()
        {
            return View();
        }

        // POST: /Account/DoiMatKhau
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> DoiMatKhau([FromBody] DoiMatKhauDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.OldPassword) || string.IsNullOrWhiteSpace(model.NewPassword))
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin." });

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
                return Json(new { success = false, message = "Không xác định được người dùng." });

            var user = await _context.NguoiDungs.FindAsync(userId);
            if (user == null)
                return Json(new { success = false, message = "Người dùng không tồn tại." });

            var hasher = new PasswordHasher<NguoiDung>();
            PasswordVerificationResult verify;
            try
            {
                verify = hasher.VerifyHashedPassword(user, user.MatKhauHash, model.OldPassword);
            }
            catch (FormatException)
            {
                verify = PasswordVerificationResult.Failed;
            }

            if (verify == PasswordVerificationResult.Failed)
                return Json(new { success = false, message = "Mật khẩu cũ không đúng." });

            user.MatKhauHash = hasher.HashPassword(user, model.NewPassword);
            user.DoiMatKhauLanDau = false;
            user.UpdatedAt = DateTime.Now;
            _context.NguoiDungs.Update(user);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đổi mật khẩu thành công." });
        }

        // GET: /Account/Trash (Danh sách tài khoản đã xóa)
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> Trash()
        {
            var list = await _context.NguoiDungs.IgnoreQueryFilters().Where(u => u.IsDeleted)
                .Include(u => u.VaiTro)
                .Include(u => u.HocVien)
                .Include(u => u.NhanSu)
                .Select(u => new { nguoiDungId = u.NguoiDungID, tenDangNhap = u.TenDangNhap, deletedAt = u.DeletedAt, vaiTro = u.VaiTro.TenVaiTro })
                .ToListAsync();

            return Json(new { success = true, data = list });
        }

        // GET: /Account/GetAccounts (Lấy danh sách phân trang)
        [Authorize(Roles = "Admin,QuanLy")]
        [HttpGet]
        public async Task<IActionResult> GetAccounts(string keyword = "", string role = "", string trangThai = "", int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;

            var query = _context.NguoiDungs
                .Include(u => u.VaiTro)
                .Include(u => u.HocVien)
                .Include(u => u.NhanSu)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(u => u.TenDangNhap.Contains(keyword) || (u.HocVien != null && u.HocVien.HoTen.Contains(keyword)) || (u.NhanSu != null && u.NhanSu.HoTen.Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u => u.VaiTro != null && u.VaiTro.TenVaiTro == role);
            }

            if (!string.IsNullOrWhiteSpace(trangThai))
            {
                if (trangThai == "Hoạt động") query = query.Where(u => u.TrangThai == true);
                else if (trangThai == "Đã khóa") query = query.Where(u => u.TrangThai == false);
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var items = await query
                .OrderBy(u => u.NguoiDungID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    nguoiDungId = u.NguoiDungID,
                    tenDangNhap = u.TenDangNhap,
                    hoTen = u.HocVien != null ? u.HocVien.HoTen : (u.NhanSu != null ? u.NhanSu.HoTen : u.TenDangNhap),
                    email = u.HocVien != null ? u.HocVien.Email : (u.NhanSu != null ? u.NhanSu.Email : null),
                    vaiTro = u.VaiTro != null ? u.VaiTro.TenVaiTro : "",
                    trangThai = u.TrangThai ? "Hoạt động" : "Đã khóa"
                })
                .ToListAsync();

            return Json(new { success = true, items, currentPage = page, totalPages, totalItems });
        }

        // POST: /Account/Restore/{id}
        [HttpPost]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> Restore(int id)
        {
            var user = await _context.NguoiDungs.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.NguoiDungID == id && u.IsDeleted);
            if (user == null) return Json(new { success = false, message = "Người dùng không tồn tại." });
            user.IsDeleted = false;
            user.DeletedAt = null;
            user.UpdatedAt = DateTime.Now;
            user.TrangThai = true;
            _context.NguoiDungs.Update(user);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Khôi phục tài khoản thành công.", nguoiDungId = id });
        }

        // POST: /Account/HardDelete/{id}
        [HttpPost]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> HardDelete(int id)
        {
            var user = await _context.NguoiDungs.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.NguoiDungID == id && u.IsDeleted);
            if (user == null) return Json(new { success = false, message = "Người dùng không tồn tại." });
            _context.NguoiDungs.Remove(user);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // POST: /Account/DoiTrangThaiKhoa (Khóa hoặc Mở khóa tài khoản thực tế trong SQL)
        [HttpPost]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> DoiTrangThaiKhoa(int id, bool khoa)
        {
            var user = await _context.NguoiDungs.FindAsync(id);
            if (user == null)
            {
                return Json(new { success = false, message = "Tài khoản không tồn tại trong CSDL!" });
            }

            user.TrangThai = !khoa; // khoa = true -> TrangThai = false (Đã khóa)
            user.UpdatedAt = DateTime.Now;
            _context.NguoiDungs.Update(user);
            await _context.SaveChangesAsync();

            string trangThaiMoi = user.TrangThai ? "Hoạt động" : "Đã khóa";
            return Json(new
            {
                success = true,
                message = $"Đã {(khoa ? "khóa" : "mở khóa")} tài khoản thành công!",
                trangThai = trangThaiMoi
            });
        }

        // POST: /Account/SoftDelete (Xóa mềm tài khoản)
        [HttpPost]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> SoftDelete([FromBody] int id)
        {
            if (id <= 0) return Json(new { success = false, message = "Id không hợp lệ." });
            var user = await _context.NguoiDungs.FirstOrDefaultAsync(u => u.NguoiDungID == id && !u.IsDeleted);
            if (user == null) return Json(new { success = false, message = "Người dùng không tồn tại hoặc đã bị xóa." });
            user.IsDeleted = true;
            user.DeletedAt = DateTime.Now;
            user.TrangThai = false;
            user.UpdatedAt = DateTime.Now;
            _context.NguoiDungs.Update(user);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã xóa mềm tài khoản.", nguoiDungId = id });
        }

        // POST: /Account/CapNhatQuyen (Đổi vai trò hệ thống thực tế trong SQL)
        [HttpPost]
        [Authorize(Roles = "Admin,QuanLy")]
        public async Task<IActionResult> CapNhatQuyen(int id, int vaiTroId)
        {
            if (id <= 0 || vaiTroId <= 0)
            {
                return Json(new { success = false, message = "Dữ liệu phân quyền không hợp lệ!" });
            }

            var user = await _context.NguoiDungs.FindAsync(id);
            if (user == null)
            {
                return Json(new { success = false, message = "Tài khoản không tồn tại!" });
            }

            var role = await _context.VaiTros.FindAsync(vaiTroId);
            if (role == null)
            {
                return Json(new { success = false, message = "Vai trò không tồn tại trong CSDL!" });
            }

            user.VaiTroID = vaiTroId;
            user.UpdatedAt = DateTime.Now;
            _context.NguoiDungs.Update(user);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Đã cập nhật vai trò [{role.TenVaiTro}] cho tài khoản thành công!"
            });
        }
    }

    // ==========================================
    // CÁC DATA TRANSFER OBJECTS (DTO)
    // ==========================================

    public class LoginRequestDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool RememberMe { get; set; }
    }

    public class TaiKhoanDto
    {
        public int Id { get; set; }
        public string TenDangNhap { get; set; }
        public string MatKhau { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public string VaiTro { get; set; }   // Admin, Quản lý, Giáo viên, Nhân viên, Học viên
        public string TrangThai { get; set; } // Hoạt động, Đã khóa
    }

    public class CapTaiKhoanDonLeDto
    {
        public string loai { get; set; }
        public int personId { get; set; }
        public string tenDangNhap { get; set; }
        public string matKhauPlain { get; set; }
        public int vaiTroId { get; set; }
    }

    public class CapTaiKhoanHangLoatDto
    {
        public string loai { get; set; }
        public int vaiTroId { get; set; }
    }

    public class DoiMatKhauDto
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmNewPassword { get; set; }
    }
}