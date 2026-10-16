using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
    public class HocVien : BaseEntity
    {
        [Key]
        public int HocVienID { get; set; }

        public int? NguoiDungID { get; set; }

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

        [StringLength(50)]
        public string? TrinhDoDauVao { get; set; }

        public decimal? DiemDauVao { get; set; }

        [StringLength(500)]
        public string? NhanXetDauVao { get; set; }

        [Required]
        [StringLength(20)]
        public string TrangThai { get; set; } = "Đang học";

        public NguoiDung? NguoiDung { get; set; }

        public ICollection<DangKy>? DangKys { get; set; }

        public ICollection<DiemDanh>? DiemDanhs { get; set; }

        public ICollection<ChiTietNopBai>? ChiTietNopBais { get; set; }

        public ICollection<CamKet>? CamKets { get; set; }

        public ICollection<HoaDon>? HoaDons { get; set; }
    }
}