using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using QuanLyTrungTamNgoaiNgu.Data;
using QLTrungTamTiengAnh_Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Đăng ký dịch vụ Session và HttpContextAccessor
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60); // Hết hạn sau 60 phút
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IBaoCaoService, BaoCaoService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();

// Register cookie authentication so [Authorize] and role claims work
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var claim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(claim, out var userId))
                {
                    context.RejectPrincipal();
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var active = await db.NguoiDungs.AsNoTracking()
                    .AnyAsync(x => x.NguoiDungID == userId && x.TrangThai && !x.IsDeleted);
                if (!active)
                    context.RejectPrincipal();
            }
        };
    });

// SỬA TẠI ĐÂY: Bỏ FallbackPolicy bắt buộc đăng nhập toàn hệ thống
// Dùng thuộc tính [Authorize] trên Controller/Action nào cần bảo mật thay vì ép toàn bộ web
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// SỬA TẠI ĐÂY: Dùng UseStaticFiles() thông thường nếu ứng dụng bị ngắt khi load file tĩnh (.css, .js)
app.UseStaticFiles();

app.UseRouting();

// Thứ tự Middleware ĐÚNG BẮT BUỘC: Session -> Authentication -> Authorization
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();