using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
    public class ThanhToan : BaseEntity
    {
        [Key]
        public int ThanhToanID { get; set; }

        [Required]
        public int HoaDonID { get; set; }

        [Required]
        public int NhanSuID { get; set; }

        public DateTime NgayThanhToan { get; set; } = DateTime.Now;

        [Required]
        public decimal SoTien { get; set; }

        [Required]
        [StringLength(30)]
        public string PhuongThuc { get; set; }

        [StringLength(300)]
        public string? GhiChu { get; set; }

        public HoaDon? HoaDon { get; set; }

        public NhanSu? NhanSu { get; set; }
    }
}