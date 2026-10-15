using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
<<<<<<< HEAD
    public class LichHoc
=======
    public class LichHoc : BaseEntity
>>>>>>> origin/main
    {
        [Key]
        public int LichHocID { get; set; }

        [Required]
        public int LopHocID { get; set; }

        [Required]
        public int PhongHocID { get; set; }

        [Required]
        public DateTime NgayHoc { get; set; }

        [Required]
        public TimeSpan GioBatDau { get; set; }

        [Required]
        public TimeSpan GioKetThuc { get; set; }

        [Required]
        [StringLength(20)]
        public string TrangThai { get; set; } = "Dự kiến";

        [StringLength(300)]
        public string? GhiChu { get; set; }

        public LopHoc? LopHoc { get; set; }

        public PhongHoc? PhongHoc { get; set; }

        public ICollection<PhanCongDay>? PhanCongDays { get; set; }

        public ICollection<DiemDanh>? DiemDanhs { get; set; }
    }
}