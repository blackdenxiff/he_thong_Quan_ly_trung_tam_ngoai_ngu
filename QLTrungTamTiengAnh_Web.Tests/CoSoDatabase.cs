using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QLTrungTamTiengAnh_Web.Controllers;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;

namespace QLTrungTamTiengAnh_Web.Tests;

// A relational database shared by fresh contexts within one test. Each test owns
// its own connection, so data cannot leak between tests or into a user's SQL Server.
internal sealed class CoSoDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public DbContextOptions<ApplicationDbContext> Options { get; private set; } = null!;

    public const int SourceId = 1;
    public const int DestinationId = 2;
    public const int InactiveId = 3;
    public const int StaffId = 1;
    public const int ApproverId = 1;

    public static async Task<CoSoDatabase> CreateAsync()
    {
        var database = new CoSoDatabase();
        await database.connection.OpenAsync();
        database.Options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(database.connection).Options;

        await using var context = database.CreateContext();
        await context.Database.EnsureCreatedAsync();
        context.CoSos.AddRange(
            Branch(SourceId, "CS-CG", "Cầu Giấy"),
            Branch(DestinationId, "CS-TX", "Thanh Xuân"),
            Branch(InactiveId, "CS-CU", "Cơ sở tạm dừng", active: false));
        context.VaiTros.Add(new VaiTro { VaiTroID = 1, TenVaiTro = "Admin" });
        context.NguoiDungs.Add(new NguoiDung
        {
            NguoiDungID = ApproverId, TenDangNhap = "test-admin",
            MatKhauHash = "unused-by-controller-tests", VaiTroID = 1, TrangThai = true
        });
        context.NhanSus.Add(new NhanSu
        {
            NhanSuID = StaffId, CoSoID = SourceId, HoTen = "Nguyễn Văn An",
            LoaiNhanSu = "Giáo viên", NgayVaoLam = DateTime.Today.AddYears(-1),
            TrangThai = "Đang làm"
        });
        await context.SaveChangesAsync();
        return database;
    }

    private static CoSo Branch(int id, string code, string name, bool active = true) => new()
    {
        CoSoID = id, MaCoSo = code, TenCoSo = name,
        DiaChi = "123 Đường thử nghiệm", TrangThai = active
    };

    public ApplicationDbContext CreateContext() => new SqliteApplicationDbContext(Options);

    public CoSoController CreateController(ApplicationDbContext context, int approverId = ApproverId) => new(
        context, NullLogger<CoSoController>.Instance)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, approverId.ToString()),
                    new Claim(ClaimTypes.Name, "test-admin"),
                    new Claim(ClaimTypes.Role, "Admin")
                ], "Test"))
            }
        }
    };

    public async Task<int> AddRoomAsync(int branchId = SourceId, string name = "P101")
    {
        await using var context = CreateContext();
        var room = new PhongHoc { CoSoID = branchId, TenPhong = name, SucChua = 30 };
        context.PhongHocs.Add(room);
        await context.SaveChangesAsync();
        return room.PhongHocID;
    }

    public async Task AddFutureScheduleAsync(int roomId, int classCapacity = 25, int daysFromToday = 2)
    {
        await using var context = CreateContext();
        var course = new KhoaHoc { TenKhoaHoc = "IELTS", SoBuoi = 20, HocPhi = 1000000 };
        var classroom = new LopHoc
        {
            TenLop = "IELTS 01", KhoaHoc = course, CoSoID = SourceId,
            GiaoVienPhuTrachID = StaffId, SiSoToiDa = classCapacity,
            NgayBatDau = DateTime.Today, NgayKetThuc = DateTime.Today.AddMonths(1),
            TrangThai = "Đang học"
        };
        context.LichHocs.Add(new LichHoc
        {
            LopHoc = classroom, PhongHocID = roomId, NgayHoc = DateTime.Today.AddDays(daysFromToday),
            GioBatDau = TimeSpan.FromHours(18), GioKetThuc = TimeSpan.FromHours(20)
        });
        await context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await connection.DisposeAsync();

    private sealed class SqliteApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // SQLite has no native time type. Store ticks in this test provider so
            // ordering by end time exercises the SQL query rather than failing translation.
            modelBuilder.Entity<LichHoc>().Property(x => x.GioBatDau)
                .HasConversion(value => value.Ticks, ticks => TimeSpan.FromTicks(ticks));
            modelBuilder.Entity<LichHoc>().Property(x => x.GioKetThuc)
                .HasConversion(value => value.Ticks, ticks => TimeSpan.FromTicks(ticks));
        }
    }
}
