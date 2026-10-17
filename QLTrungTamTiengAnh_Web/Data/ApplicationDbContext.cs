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
<<<<<<< HEAD
<<<<<<< HEAD

            modelBuilder.Entity<NguoiDung>().ToTable("NguoiDung");
            modelBuilder.Entity<VaiTro>().ToTable("VaiTro");
            modelBuilder.Entity<HocVien>().ToTable("HocVien");
            modelBuilder.Entity<NhanSu>().ToTable("NhanSu");

=======
=======

>>>>>>> sang
            modelBuilder.Entity<LopHoc>().ToTable("LopHoc");
            modelBuilder.Entity<LichHoc>().ToTable("LichHoc");
            modelBuilder.Entity<BangLuong>().ToTable("BangLuong");
            modelBuilder.Entity<NhanSu>().ToTable("NhanSu");
            modelBuilder.Entity<DiemDanh>().ToTable("DiemDanh");
            modelBuilder.Entity<PhongHoc>().ToTable("PhongHoc");
            modelBuilder.Entity<DangKy>().ToTable("DangKy");
            //10/13 sang thê,
            modelBuilder.Entity<HocVien>().ToTable("HocVien");
            modelBuilder.Entity<HoaDon>().ToTable("HoaDon");
            modelBuilder.Entity<ThanhToan>().ToTable("ThanhToan");
<<<<<<< HEAD
>>>>>>> sang
=======


            modelBuilder.Entity<NguoiDung>().ToTable("NguoiDung");
            modelBuilder.Entity<VaiTro>().ToTable("VaiTro");
            modelBuilder.Entity<HocVien>().ToTable("HocVien");
            modelBuilder.Entity<NhanSu>().ToTable("NhanSu");


>>>>>>> sang
            // CoSoCuID -> CoSo
            modelBuilder.Entity<LichSuDieuChuyen>()
                .HasOne(x => x.CoSoCu)
                .WithMany(x => x.LichSuDieuChuyenCosocu)
                .HasForeignKey(x => x.CoSoCuID)
                .OnDelete(DeleteBehavior.Restrict);

            // CoSoMoiID -> CoSo
            modelBuilder.Entity<LichSuDieuChuyen>()
                .HasOne(x => x.CoSoMoi)
                .WithMany(x => x.LichSuDieuChuyenCosomoi)
                .HasForeignKey(x => x.CoSoMoiID)
                .OnDelete(DeleteBehavior.Restrict);

            // NguoiDeXuatID -> NhanSu
            modelBuilder.Entity<LichSuDieuChuyen>()
                .HasOne(x => x.NguoiDeXuat)
                .WithMany()
                .HasForeignKey(x => x.NguoiDeXuatID)
                .OnDelete(DeleteBehavior.Restrict);

            // NguoiDuyetID -> NguoiDung
            modelBuilder.Entity<LichSuDieuChuyen>()
                .HasOne(x => x.NguoiDuyet)
                .WithMany()
                .HasForeignKey(x => x.NguoiDuyetID)
                .OnDelete(DeleteBehavior.Restrict);

            // Apply global query filter for soft-delete on all entities that inherit BaseEntity
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var clrType = entityType.ClrType;
                if (clrType == null)
                    continue;

                if (typeof(QuanLyTrungTamNgoaiNgu.Models.BaseEntity).IsAssignableFrom(clrType))
                {
                    var parameter = Expression.Parameter(clrType, "e");
                    var property = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                    var compare = Expression.Equal(property, Expression.Constant(false));
                    var lambda = Expression.Lambda(compare, parameter);
                    modelBuilder.Entity(clrType).HasQueryFilter(lambda);
                }
            }

        }
    }
}