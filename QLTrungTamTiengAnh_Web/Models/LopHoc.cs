using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
    public class LopHoc
    {
        [Key]
        public int LopHocID { get; set; }

        [Required]
        [StringLength(100)]
        public string TenLop { get; set; }

        [Required]
        public int KhoaHocID { get; set; }

        [Required]
        public int CoSoID { get; set; }

        [Required]
        public int GiaoVienPhuTrachID { get; set; }

        [Required]
        public int SiSoToiDa { get; set; }

        [Required]
        public DateTime NgayBatDau { get; set; }

        [Required]
        public DateTime NgayKetThuc { get; set; }

        [Required]
        [StringLength(20)]
        public string TrangThai { get; set; } = "Dự kiến";

        public KhoaHoc? KhoaHoc { get; set; }

        public CoSo? CoSo { get; set; }

        public NhanSu? GiaoVienPhuTrach { get; set; }

        public ICollection<DangKy>? DangKys { get; set; }

        public ICollection<LichHoc>? LichHocs { get; set; }

        public ICollection<BaiTap>? BaiTaps { get; set; }
    }
}