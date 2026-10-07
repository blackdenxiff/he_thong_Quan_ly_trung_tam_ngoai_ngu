using Microsoft.AspNetCore.Mvc;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class AccountController : Controller
    {
        // ==========================================
        // PHẦN F01: ĐĂNG NHẬP / ĐĂNG XUẤT (AUTHENTICATION)
        // ==========================================

        // GET: /Account/Login
        public IActionResult Login()
        {
            // Kiểm tra nếu đã có Cookie ghi nhớ thì nạp sẵn tên đăng nhập
            if (Request.Cookies.TryGetValue("RememberedUser", out string rememberedUser))
            {
                ViewBag.RememberedUser = rememberedUser;
            }

            return View();
        }

        // POST: /Account/Login (Xử lý xác thực, Session và Cookie)
        [HttpPost]
        public IActionResult Login([FromBody] LoginRequestDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu!" });
            }

            // Dữ liệu tài khoản mẫu đại diện cho các vai trò chuẩn theo tài liệu đặc tả (ACT-01 -> ACT-05)
            string role = "";
            string fullName = "";

            if (model.Username.ToLower() == "admin" && model.Password == "admin123")
            {
                role = "Admin";
                fullName = "Ban Quản Trị Hệ Thống";
            }
            else if (model.Username.ToLower() == "quanly" && model.Password == "ql123")
            {
                role = "Quản lý";
                fullName = "Nguyễn Quản Lý";
            }
            else if (model.Username.ToLower() == "gv01" && model.Password == "gv123")
            {
                role = "Giáo viên";
                fullName = "Trần Thị Giảng Viên";
            }
            else if (model.Username.ToLower() == "nv01" && model.Password == "nv123")
            {
                role = "Nhân viên";
                fullName = "Lê Thị Tư Vấn";
            }
            else if (model.Username.ToLower() == "hv01" && model.Password == "hv123")
            {
                role = "Học viên";
                fullName = "Lê Văn Học Viên";
            }
            else
            {
                return Json(new { success = false, message = "Tài khoản hoặc mật khẩu không chính xác!" });
            }

            // 1. Lưu thông tin người dùng vào Session
            HttpContext.Session.SetString("Username", model.Username);
            HttpContext.Session.SetString("UserRole", role);
            HttpContext.Session.SetString("UserFullName", fullName);

            // 2. Ghi nhận Cookie nếu người dùng tích chọn "Ghi nhớ đăng nhập"
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