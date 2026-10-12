using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
    public class KhoaHoc
    {
        [Key]
        public int KhoaHocID { get; set; }

        [Required]
        [StringLength(100)]
        public string TenKhoaHoc { get; set; }

        [StringLength(50)]
        public string? TrinhDo { get; set; }

        [StringLength(500)]
        public string? MoTa { get; set; }

        [Required]
        public int SoBuoi { get; set; }

        [Required]
        public decimal HocPhi { get; set; }

        [Required]
        [StringLength(20)]
        public string TrangThai { get; set; } = "Đang mở";

        public ICollection<LopHoc>? LopHocs { get; set; }

        public ICollection<TaiLieu>? TaiLieus { get; set; }

        public ICollection<CamKet>? CamKets { get; set; }
    }
}