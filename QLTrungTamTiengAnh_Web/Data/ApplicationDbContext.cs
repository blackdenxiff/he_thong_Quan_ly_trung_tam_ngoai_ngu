using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Models;

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
            // Ánh xạ Model KhoaHoc vào đúng tên bảng trong SQL Server
            // Hải thêm ngày 13/10
            modelBuilder.Entity<KhoaHoc>().ToTable("KhoaHoc");
            modelBuilder.Entity<NhanSu>().ToTable("NhanSu");
            modelBuilder.Entity<PhongHoc>().ToTable("PhongHoc");
            modelBuilder.Entity<LichHoc>().ToTable("LichHoc");
            modelBuilder.Entity<DangKy>().ToTable("DangKy");
            modelBuilder.Entity<LopHoc>().ToTable("LopHoc");
            modelBuilder.Entity<HocVien>().ToTable("HocVien");
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
        }
    }
}