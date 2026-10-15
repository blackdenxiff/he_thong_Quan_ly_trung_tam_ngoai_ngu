using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
<<<<<<< HEAD
    public class ChiTietNopBai
=======
    public class ChiTietNopBai : BaseEntity
>>>>>>> origin/main
    {
        [Key]
        public int ChiTietNopBaiID { get; set; }

        [Required]
        public int BaiTapID { get; set; }

        [Required]
        public int HocVienID { get; set; }

        [Required]
        public int LanNop { get; set; } = 1;

        [StringLength(500)]
        public string? DuongDan { get; set; }

        public DateTime ThoiGianNop { get; set; } = DateTime.Now;

        public decimal? Diem { get; set; }

        [StringLength(500)]
        public string? NhanXet { get; set; }

        public int? NguoiChamID { get; set; }

        public DateTime? NgayCham { get; set; }

        public BaiTap? BaiTap { get; set; }

        public HocVien? HocVien { get; set; }

        public NhanSu? NguoiCham { get; set; }
    }
}