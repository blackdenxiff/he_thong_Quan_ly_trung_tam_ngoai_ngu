using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
    public class DiemDanh : BaseEntity
    {
        [Key]
        public int DiemDanhID { get; set; }

        [Required]
        public int LichHocID { get; set; }

        [Required]
        public int HocVienID { get; set; }

        [Required]
        [StringLength(20)]
        public string TrangThai { get; set; }

        [StringLength(300)]
        public string? GhiChu { get; set; }

        public LichHoc? LichHoc { get; set; }

        public HocVien? HocVien { get; set; }
    }
}