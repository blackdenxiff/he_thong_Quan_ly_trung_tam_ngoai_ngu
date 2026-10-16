using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
    public class BangLuong : BaseEntity
    {
        [Key]
        public int BangLuongID { get; set; }

        [Required]
        public int NhanSuID { get; set; }

        [Required]
        [StringLength(7)]
        public string ThangNam { get; set; }

        public decimal? SoNgayCong { get; set; }

        public decimal? SoGioDay { get; set; }

        public decimal? LuongCoBan { get; set; }

        public decimal PhuCap { get; set; } = 0;

        public decimal Thuong { get; set; } = 0;

        public decimal KhauTru { get; set; } = 0;

        [Required]
        public decimal ThucLinh { get; set; }

        [Required]
        [StringLength(20)]
        public string TrangThai { get; set; } = "Tạm tính";

        public NhanSu? NhanSu { get; set; }
    }
}