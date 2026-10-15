using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
    public class TaiLieu : BaseEntity
    {
        [Key]
        public int TaiLieuID { get; set; }

        [Required]
        public int KhoaHocID { get; set; }

        [Required]
        [StringLength(150)]
        public string TenTaiLieu { get; set; }

        [StringLength(30)]
        public string? LoaiTaiLieu { get; set; }

        [Required]
        [StringLength(500)]
        public string DuongDan { get; set; }

        [StringLength(500)]
        public string? MoTa { get; set; }

        public DateTime NgayDang { get; set; } = DateTime.Now;

        [Required]
        [StringLength(20)]
        public string TrangThai { get; set; } = "Đang dùng";

        public KhoaHoc? KhoaHoc { get; set; }
    }
}