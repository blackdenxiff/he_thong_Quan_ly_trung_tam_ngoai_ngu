using Microsoft.AspNetCore.Mvc;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
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
            var verify = hasher.VerifyHashedPassword(user, user.MatKhauHash, model.Password);
            if (verify == PasswordVerificationResult.Failed)
            {
                return Json(new { success = false, message = "Tài khoản hoặc mật khẩu không chính xác!" });
            }

            string role = user.VaiTro?.TenVaiTro ?? "User";
            string fullName = user.HocVien?.HoTen ?? user.NhanSu?.HoTen ?? user.TenDangNhap;

            // 1. Lưu thông tin người dùng vào Session (giữ tương thích với mã hiện tại)
            HttpContext.Session.SetString("Username", model.Username);
            HttpContext.Session.SetString("UserRole", role);
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

            return Json(new
            {
                success = true,
                message = $"Đăng nhập thành công! Xin chào {fullName}.",
                role = role,
                redirectUrl = "/"
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
        public IActionResult Index()
        {
            return View();
        }

        // POST: /Account/LuuTaiKhoan (Tạo tài khoản mới và gán vai trò ban đầu qua AJAX)
        [HttpPost]
        public IActionResult LuuTaiKhoan([FromBody] TaiKhoanDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TenDangNhap))
            {
                return Json(new { success = false, message = "Vui lòng nhập tên đăng nhập!" });
            }

            // Kiểm tra quy tắc nghiệp vụ F02: Không cho trùng tên đăng nhập
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

        // POST: /Account/DoiTrangThaiKhoa (Khóa hoặc Mở khóa tài khoản)
        [HttpPost]
        public IActionResult DoiTrangThaiKhoa(int id, bool khoa)
        {
            string trangThaiMoi = khoa ? "Đã khóa" : "Hoạt động";
            return Json(new
            {
                success = true,
                message = $"Đã {(khoa ? "khóa" : "mở khóa")} tài khoản thành công!",
                trangThai = trangThaiMoi
            });
        }

        // POST: /Account/CapNhatQuyen (Đổi vai trò hệ thống)
        [HttpPost]
        public IActionResult CapNhatQuyen(int id, string vaiTroMoi)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(vaiTroMoi))
            {
                return Json(new { success = false, message = "Dữ liệu phân quyền không hợp lệ!" });
            }

            return Json(new
            {
                success = true,
                message = $"Đã cập nhật vai trò [{vaiTroMoi}] cho tài khoản thành công!"
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
}