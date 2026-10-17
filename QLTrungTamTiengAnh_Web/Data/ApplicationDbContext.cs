using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Models;
using System.Linq.Expressions;

namespace QuanLyTrungTamNgoaiNgu.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<VaiTro> VaiTros { get; set; }

        public DbSet<CoSo> CoSos { get; set; }

        public DbSet<PhongHoc> PhongHocs { get; set; }

        public DbSet<NguoiDung> NguoiDungs { get; set; }

        public DbSet<NhanSu> NhanSus { get; set; }

        public DbSet<HocVien> HocViens { get; set; }

        public DbSet<KhoaHoc> KhoaHocs { get; set; }

        public DbSet<LopHoc> LopHocs { get; set; }

        public DbSet<DangKy> DangKys { get; set; }

        public DbSet<LichHoc> LichHocs { get; set; }

        public DbSet<PhanCongDay> PhanCongDays { get; set; }

        public DbSet<DiemDanh> DiemDanhs { get; set; }

        public DbSet<BaiTap> BaiTaps { get; set; }

        public DbSet<ChiTietNopBai> ChiTietNopBais { get; set; }

        public DbSet<TaiLieu> TaiLieus { get; set; }

        public DbSet<CamKet> CamKets { get; set; }

        public DbSet<HoaDon> HoaDons { get; set; }

        public DbSet<ThanhToan> ThanhToans { get; set; }

        public DbSet<BangLuong> BangLuongs { get; set; }

        public DbSet<LichSuDieuChuyen> LichSuDieuChuyens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Ánh xạ DbSet sang đúng tên bảng trong SQL Server.
            modelBuilder.Entity<VaiTro>().ToTable("VaiTro");
            modelBuilder.Entity<CoSo>().ToTable("CoSo");
            modelBuilder.Entity<PhongHoc>().ToTable("PhongHoc");
            modelBuilder.Entity<NguoiDung>().ToTable("NguoiDung");
            modelBuilder.Entity<NhanSu>()
                .ToTable("NhanSu", table =>
                {
                    // Phải khớp CK_NhanSu_Loai trong script SQL.
                    table.HasCheckConstraint(
                        "CK_NhanSu_Loai",
                        "[LoaiNhanSu] IN (N'Nhân viên', N'Giáo viên', N'Cả hai')");
                });

            modelBuilder.Entity<HocVien>().ToTable("HocVien");
            modelBuilder.Entity<KhoaHoc>().ToTable("KhoaHoc");
            modelBuilder.Entity<LopHoc>().ToTable("LopHoc");
            modelBuilder.Entity<DangKy>().ToTable("DangKy");
            modelBuilder.Entity<LichHoc>().ToTable("LichHoc");
            modelBuilder.Entity<PhanCongDay>().ToTable("PhanCongDay");
            modelBuilder.Entity<DiemDanh>().ToTable("DiemDanh");
            modelBuilder.Entity<BaiTap>().ToTable("BaiTap");
            modelBuilder.Entity<ChiTietNopBai>().ToTable("ChiTietNopBai");
            modelBuilder.Entity<TaiLieu>().ToTable("TaiLieu");
            modelBuilder.Entity<CamKet>().ToTable("CamKet");
            modelBuilder.Entity<HoaDon>().ToTable("HoaDon");
            modelBuilder.Entity<ThanhToan>().ToTable("ThanhToan");
            modelBuilder.Entity<BangLuong>().ToTable("BangLuong");
            modelBuilder.Entity<LichSuDieuChuyen>().ToTable("LichSuDieuChuyen");

            // Các cột trong SQL khai báo VARCHAR, không phải NVARCHAR.
            modelBuilder.Entity<NguoiDung>()
                .Property(x => x.TenDangNhap)
                .HasColumnType("varchar(50)");

            modelBuilder.Entity<CoSo>()
                .Property(x => x.SoDienThoai)
                .HasColumnType("varchar(20)");

            modelBuilder.Entity<NhanSu>()
                .Property(x => x.SoDienThoai)
                .HasColumnType("varchar(20)");

            modelBuilder.Entity<NhanSu>()
                .Property(x => x.Email)
                .HasColumnType("varchar(100)");

            modelBuilder.Entity<HocVien>()
                .Property(x => x.SoDienThoai)
                .HasColumnType("varchar(20)");

            modelBuilder.Entity<HocVien>()
                .Property(x => x.Email)
                .HasColumnType("varchar(100)");

            // Một cơ sở có thể xuất hiện trong nhiều bản ghi điều chuyển
            // với vai trò cơ sở cũ.
            modelBuilder.Entity<LichSuDieuChuyen>()
                .HasOne(x => x.CoSoCu)
                .WithMany(x => x.LichSuDieuChuyenCosocu)
                .HasForeignKey(x => x.CoSoCuID)
                .OnDelete(DeleteBehavior.Restrict);

            // Một cơ sở có thể xuất hiện trong nhiều bản ghi điều chuyển
            // với vai trò cơ sở mới.
            modelBuilder.Entity<LichSuDieuChuyen>()
                .HasOne(x => x.CoSoMoi)
                .WithMany(x => x.LichSuDieuChuyenCosomoi)
                .HasForeignKey(x => x.CoSoMoiID)
                .OnDelete(DeleteBehavior.Restrict);

            // Người đề xuất là nhân sự.
            modelBuilder.Entity<LichSuDieuChuyen>()
                .HasOne(x => x.NguoiDeXuat)
                .WithMany()
                .HasForeignKey(x => x.NguoiDeXuatID)
                .OnDelete(DeleteBehavior.Restrict);

            // Người duyệt là người dùng.
            modelBuilder.Entity<LichSuDieuChuyen>()
                .HasOne(x => x.NguoiDuyet)
                .WithMany()
                .HasForeignKey(x => x.NguoiDuyetID)
                .OnDelete(DeleteBehavior.Restrict);

            // Lọc các entity kế thừa BaseEntity để ẩn bản ghi đã xóa mềm.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var clrType = entityType.ClrType;

                if (clrType == null ||
                    !typeof(BaseEntity).IsAssignableFrom(clrType))
                {
                    continue;
                }

                var parameter = Expression.Parameter(clrType, "e");
                var property = Expression.Property(
                    parameter,
                    nameof(BaseEntity.IsDeleted));

                var condition = Expression.Equal(
                    property,
                    Expression.Constant(false));

                var filter = Expression.Lambda(condition, parameter);

                modelBuilder.Entity(clrType).HasQueryFilter(filter);
            }
        }
    }
}