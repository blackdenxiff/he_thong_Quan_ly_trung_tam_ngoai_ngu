using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
    public class HoaDon : BaseEntity
    {
        [Key]
        public int HoaDonID { get; set; }

        [Required]
        public int HocVienID { get; set; }

        public int? DangKyID { get; set; }

        [Required]
        public int NhanSuLapID { get; set; }

        public DateTime NgayLap { get; set; } = DateTime.Now;

        [Required]
        public decimal TongTien { get; set; }

        [Required]
        public decimal GiamGia { get; set; } = 0;

        [Required]
        public decimal TongPhaiTra { get; set; }

        [Required]
        [StringLength(20)]
        public string TrangThai { get; set; } = "Chưa đủ";

        [StringLength(300)]
        public string? GhiChu { get; set; }

        public HocVien? HocVien { get; set; }

        public DangKy? DangKy { get; set; }

        public NhanSu? NhanSuLap { get; set; }

        public ICollection<ThanhToan>? ThanhToans { get; set; }
    }
}