using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
    public class PhanCongDay : BaseEntity
    {
        [Key]
        public int PhanCongID { get; set; }

        [Required]
        public int LichHocID { get; set; }

        [Required]
        public int NhanSuID { get; set; }

        [Required]
        [StringLength(20)]
        public string VaiTroDay { get; set; }

        [Required]
        [StringLength(20)]
        public string TrangThai { get; set; } = "Chờ duyệt";

        public int? NguoiDuyetID { get; set; }

        public DateTime NgayPhanCong { get; set; } = DateTime.Now;

        [StringLength(300)]
        public string? LyDo { get; set; }

        public LichHoc? LichHoc { get; set; }

        public NhanSu? NhanSu { get; set; }

        public NguoiDung? NguoiDuyet { get; set; }
    }
}