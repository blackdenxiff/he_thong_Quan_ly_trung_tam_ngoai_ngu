using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
    public class NhanSu : BaseEntity
    {
        [Key]
        public int NhanSuID { get; set; }

        public int? NguoiDungID { get; set; }

        [Required]
        public int CoSoID { get; set; }

        [Required]
        [StringLength(100)]
        public string HoTen { get; set; }

        public DateTime? NgaySinh { get; set; }

        [StringLength(10)]
        public string? GioiTinh { get; set; }

        [StringLength(20)]
        public string? SoDienThoai { get; set; }

        [StringLength(100)]
        public string? Email { get; set; }

        [Required]
        [StringLength(20)]
        public string LoaiNhanSu { get; set; }

        [StringLength(100)]
        public string? TrinhDo { get; set; }

        public decimal? LuongCoBan { get; set; }

        public decimal? LuongTheoGio { get; set; }

        [Required]
        public DateTime NgayVaoLam { get; set; }

        [StringLength(100)]
        public string? NhiemVu { get; set; }

        [Required]
        [StringLength(20)]
        public string TrangThai { get; set; } = "Đang làm";

        public NguoiDung? NguoiDung { get; set; }

        public CoSo? CoSo { get; set; }

        public ICollection<LopHoc>? LopHocs { get; set; }

        public ICollection<PhanCongDay>? PhanCongDays { get; set; }

        public ICollection<ChiTietNopBai>? ChiTietNopBais { get; set; }

        public ICollection<HoaDon>? HoaDons { get; set; }

        public ICollection<ThanhToan>? ThanhToans { get; set; }

        public ICollection<BangLuong>? BangLuongs { get; set; }

        public ICollection<LichSuDieuChuyen>? LichSuDieuChuyens { get; set; }

        public ICollection<CamKet>? CamKetsDeXuat { get; set; }
    }
}