using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
    public class BaiTap : BaseEntity
    {
        [Key]
        public int BaiTapID { get; set; }

        [Required]
        public int LopHocID { get; set; }

        [Required]
        [StringLength(150)]
        public string TieuDe { get; set; }

        [StringLength(1000)]
        public string? MoTa { get; set; }

        public DateTime NgayGiao { get; set; } = DateTime.Now;

        public DateTime? HanNop { get; set; }

        [Required]
        [StringLength(20)]
        public string TrangThai { get; set; } = "Đang mở";

        public LopHoc? LopHoc { get; set; }

        public ICollection<ChiTietNopBai>? ChiTietNopBais { get; set; }
    }
}