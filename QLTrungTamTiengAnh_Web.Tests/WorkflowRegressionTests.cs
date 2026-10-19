using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QLTrungTamTiengAnh_Web.Controllers;
using QLTrungTamTiengAnh_Web.Services;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;
using Xunit;

namespace QLTrungTamTiengAnh_Web.Tests;

public sealed class WorkflowRegressionTests
{
    [Fact]
    public async Task TeacherLoginRedirectsToAllowedTeachingPage()
    {
        await using var database = await TestDatabase.CreateAsync();
        var role = new VaiTro { TenVaiTro = "Giáo viên" };
        var user = new NguoiDung { TenDangNhap = "giaovien_demo", VaiTro = role, TrangThai = true };
        user.MatKhauHash = new PasswordHasher<NguoiDung>().HashPassword(user, "ValidPassword123");
        var branch = TestDatabase.Branch();
        user.NhanSu = new NhanSu { CoSo = branch, HoTen = "Giáo viên Demo", LoaiNhanSu = "Giáo viên", NgayVaoLam = DateTime.Today, TrangThai = "Đang làm" };
        database.Context.Add(user);
        await database.Context.SaveChangesAsync();

        var auth = new RecordingAuthenticationService();
        var services = new ServiceCollection().AddSingleton<IAuthenticationService>(auth).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services, Session = new TestSession() };
        var controller = new AccountController(database.Context) { ControllerContext = new ControllerContext { HttpContext = http } };
        var result = Assert.IsType<JsonResult>(await controller.Login(new LoginRequestDto
        {
            Username = "giaovien_demo", Password = "ValidPassword123"
        }));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("/LichHoc", json.RootElement.GetProperty("redirectUrl").GetString());
        Assert.Equal("Giáo viên", auth.Principal?.FindFirstValue(ClaimTypes.Role));
        Assert.Equal(user.NhanSu.NhanSuID, http.Session.GetInt32("NhanSuID"));
    }

    [Fact]
    public async Task WrongPasswordDoesNotCreateIdentity()
    {
        await using var database = await TestDatabase.CreateAsync();
        var user = new NguoiDung { TenDangNhap = "giaovien_demo", VaiTro = new VaiTro { TenVaiTro = "Giáo viên" }, TrangThai = true };
        user.MatKhauHash = new PasswordHasher<NguoiDung>().HashPassword(user, "CorrectPassword123");
        user.NhanSu = new NhanSu { CoSo = TestDatabase.Branch(), HoTen = "GV", LoaiNhanSu = "Giáo viên", NgayVaoLam = DateTime.Today, TrangThai = "Đang làm" };
        database.Context.Add(user); await database.Context.SaveChangesAsync();
        var auth = new RecordingAuthenticationService();
        var services = new ServiceCollection().AddSingleton<IAuthenticationService>(auth).BuildServiceProvider();
        var controller = new AccountController(database.Context) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services, Session = new TestSession() } } };
        var result = Assert.IsType<JsonResult>(await controller.Login(new LoginRequestDto { Username = "giaovien_demo", Password = "wrong" }));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Null(auth.Principal);
    }

    [Fact]
    public void HomeRoutesTeacherAwayFromAdminReport()
    {
        var controller = new HomeController { ControllerContext = ControllerWithRole("Giáo viên") };
        var result = Assert.IsType<RedirectToActionResult>(controller.Index());
        Assert.Equal("LichHoc", result.ControllerName);
    }

    [Fact]
    public async Task EnrollmentCreatesRealRegistrationAndPendingCommitmentAtomically()
    {
        await using var database = await TestDatabase.CreateAsync();
        var data = await database.SeedTrainingDataAsync();
        var service = new EnrollmentService(database.Context);
        var result = await service.EnrollAsync(new EnrollmentRequest(data.Student.HocVienID, data.Class.LopHocID, true, "Ca tối"));
        var registration = await database.Context.DangKys.SingleAsync();
        var commitment = await database.Context.CamKets.SingleAsync();
        Assert.Equal(data.Class.LopHocID, registration.LopHocID);
        Assert.Equal(data.Course.HocPhi, registration.HocPhiApDung);
        Assert.Equal(registration.DangKyID, commitment.DangKyID);
        Assert.Equal("Chờ duyệt", commitment.TrangThai);
        Assert.True(result.CommitmentCreated);
    }

    [Fact]
    public async Task EnrollmentRejectsDuplicateAndDoesNotCreateSecondCommitment()
    {
        await using var database = await TestDatabase.CreateAsync();
        var data = await database.SeedTrainingDataAsync();
        var service = new EnrollmentService(database.Context);
        await service.EnrollAsync(new EnrollmentRequest(data.Student.HocVienID, data.Class.LopHocID, true, null));
        var error = await Assert.ThrowsAsync<EnrollmentException>(() => service.EnrollAsync(new EnrollmentRequest(data.Student.HocVienID, data.Class.LopHocID, true, null)));
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal(1, await database.Context.DangKys.CountAsync());
        Assert.Equal(1, await database.Context.CamKets.CountAsync());
    }

    [Fact]
    public async Task TeacherCanGradeExactAssignedSubmissionAndZeroIsValid()
    {
        await using var database = await TestDatabase.CreateAsync();
        var data = await database.SeedTrainingDataAsync(withTeacherAccount: true);
        var assignment = new BaiTap { LopHocID = data.Class.LopHocID, TieuDe = "Bài 1", TrangThai = "Đang mở" };
        database.Context.BaiTaps.Add(assignment); await database.Context.SaveChangesAsync();
        var submission = new ChiTietNopBai { BaiTapID = assignment.BaiTapID, HocVienID = data.Student.HocVienID, LanNop = 1, ThoiGianNop = DateTime.Now };
        database.Context.ChiTietNopBais.Add(submission); await database.Context.SaveChangesAsync();
        var controller = new BaiTapController(database.Context, new TestEnvironment()) { ControllerContext = ControllerWithRole("Giáo viên", data.TeacherUserId) };
        var result = await controller.LuuDiem(new GradeSubmissionRequest { ChiTietNopBaiId = submission.ChiTietNopBaiID, Diem = 0, NhanXet = "Cần làm lại" }, default);
        Assert.IsType<JsonResult>(result);
        await database.Context.Entry(submission).ReloadAsync();
        Assert.Equal(0, submission.Diem);
        Assert.Equal(data.Teacher.NhanSuID, submission.NguoiChamID);
        Assert.NotNull(submission.NgayCham);
    }

    [Fact]
    public async Task DifferentTeacherCannotGradeSubmissionOutsideAssignment()
    {
        await using var database = await TestDatabase.CreateAsync();
        var data = await database.SeedTrainingDataAsync(withTeacherAccount: true);
        var otherUser = new NguoiDung { TenDangNhap = "other", MatKhauHash = "hash", VaiTro = new VaiTro { TenVaiTro = "Giáo viên" }, TrangThai = true };
        var otherTeacher = new NhanSu { NguoiDung = otherUser, CoSoID = data.Branch.CoSoID, HoTen = "Giáo viên B", LoaiNhanSu = "Giáo viên", NgayVaoLam = DateTime.Today, TrangThai = "Đang làm" };
        var assignment = new BaiTap { LopHocID = data.Class.LopHocID, TieuDe = "Bài 1", TrangThai = "Đang mở" };
        database.Context.AddRange(otherTeacher, assignment); await database.Context.SaveChangesAsync();
        var submission = new ChiTietNopBai { BaiTapID = assignment.BaiTapID, HocVienID = data.Student.HocVienID, LanNop = 1 };
        database.Context.Add(submission); await database.Context.SaveChangesAsync();
        var controller = new BaiTapController(database.Context, new TestEnvironment()) { ControllerContext = ControllerWithRole("Giáo viên", otherUser.NguoiDungID) };
        var result = await controller.LuuDiem(new GradeSubmissionRequest { ChiTietNopBaiId = submission.ChiTietNopBaiID, Diem = 90 }, default);
        Assert.IsType<ForbidResult>(result);
        await database.Context.Entry(submission).ReloadAsync();
        Assert.Null(submission.Diem);
    }

    [Fact]
    public async Task CourseStatusRemainsExactDatabaseValue()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Context.KhoaHocs.Add(new KhoaHoc { TenKhoaHoc = "Khóa A", SoBuoi = 10, HocPhi = 100, TrangThai = "Đang mở" });
        await database.Context.SaveChangesAsync();
        var controller = new KhoaHocController(database.Context, new TestEnvironment());
        var result = Assert.IsType<JsonResult>(await controller.DanhSach());
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        Assert.Equal("Đang mở", json.RootElement.GetProperty("items")[0].GetProperty("trangThai").GetString());
        Assert.Equal("Đang mở", json.RootElement.GetProperty("statuses")[0].GetString());
    }

    [Fact]
    public async Task CommitmentListKeepsNullableClassLinkAndAppliesExactStatusFilter()
    {
        await using var database = await TestDatabase.CreateAsync();
        var student = new HocVien { HoTen = "Học viên", TrangThai = "Đang học" };
        var course = new KhoaHoc { TenKhoaHoc = "Khóa", SoBuoi = 10, HocPhi = 100, TrangThai = "Đang mở" };
        database.Context.AddRange(
            new CamKet { HocVien = student, KhoaHoc = course, DangKyID = null, TrangThai = "Chờ duyệt" },
            new CamKet { HocVien = student, KhoaHoc = course, DangKyID = null, TrangThai = "Từ chối" });
        await database.Context.SaveChangesAsync();
        var controller = new CamKetController(database.Context) { ControllerContext = ControllerWithRole("Admin") };
        var all = Assert.IsType<ViewResult>(await controller.Index(null, null, null, null, null, default));
        Assert.Equal(2, Assert.IsAssignableFrom<List<CamKet>>(all.Model).Count);
        var rejected = Assert.IsType<ViewResult>(await controller.Index(null, null, null, "Từ chối", null, default));
        var item = Assert.Single(Assert.IsAssignableFrom<List<CamKet>>(rejected.Model));
        Assert.Equal("Từ chối", item.TrangThai);
        Assert.Null(item.DangKyID);
    }

    private static ControllerContext ControllerWithRole(string role, int userId = 1)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test");
        return new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
    }
}

internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    public ApplicationDbContext Context { get; }
    private TestDatabase(SqliteConnection connection, ApplicationDbContext context) { _connection = connection; Context = context; }
    public static async Task<TestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var context = new SqliteTestContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        return new TestDatabase(connection, context);
    }
    public static CoSo Branch() => new() { TenCoSo = "Cơ sở Test", DiaChi = "Test", TrangThai = true };
    public async Task<TrainingData> SeedTrainingDataAsync(bool withTeacherAccount = false)
    {
        var branch = Branch();
        var teacher = new NhanSu { CoSo = branch, HoTen = "Giáo viên A", LoaiNhanSu = "Giáo viên", NgayVaoLam = DateTime.Today, TrangThai = "Đang làm" };
        var teacherUserId = 0;
        if (withTeacherAccount)
        {
            var user = new NguoiDung { TenDangNhap = "teacher", MatKhauHash = "hash", VaiTro = new VaiTro { TenVaiTro = "Giáo viên" }, TrangThai = true };
            teacher.NguoiDung = user; teacherUserId = user.NguoiDungID;
        }
        var course = new KhoaHoc { TenKhoaHoc = "TOEIC", SoBuoi = 20, HocPhi = 4_500_000, TrangThai = "Đang mở" };
        var classEntity = new LopHoc { TenLop = "T01", KhoaHoc = course, CoSo = branch, GiaoVienPhuTrach = teacher, SiSoToiDa = 20, NgayBatDau = DateTime.Today.AddDays(2), NgayKetThuc = DateTime.Today.AddMonths(2), TrangThai = "Đang học" };
        var student = new HocVien { HoTen = "Học viên A", TrangThai = "Chờ xếp lớp" };
        Context.AddRange(classEntity, student); await Context.SaveChangesAsync();
        teacherUserId = teacher.NguoiDungID ?? teacherUserId;
        return new TrainingData(branch, teacher, teacherUserId, course, classEntity, student);
    }
    public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await _connection.DisposeAsync(); }
}
internal sealed record TrainingData(CoSo Branch, NhanSu Teacher, int TeacherUserId, KhoaHoc Course, LopHoc Class, HocVien Student);

internal sealed class SqliteTestContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Model.FindEntityType(typeof(NhanSu))?.RemoveCheckConstraint("CK_NhanSu_Loai");
    }
}

internal sealed class RecordingAuthenticationService : IAuthenticationService
{
    public ClaimsPrincipal? Principal { get; private set; }
    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());
    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) { Principal = principal; return Task.CompletedTask; }
    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) { Principal = null; return Task.CompletedTask; }
}

internal sealed class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _values = [];
    public bool IsAvailable => true;
    public string Id { get; } = Guid.NewGuid().ToString();
    public IEnumerable<string> Keys => _values.Keys;
    public void Clear() => _values.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _values.Remove(key);
    public void Set(string key, byte[] value) => _values[key] = value;
    public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
}

internal sealed class TestEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "Tests";
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    public string WebRootPath { get; set; } = Path.GetTempPath();
    public string EnvironmentName { get; set; } = "Testing";
    public string ContentRootPath { get; set; } = Path.GetTempPath();
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
}
