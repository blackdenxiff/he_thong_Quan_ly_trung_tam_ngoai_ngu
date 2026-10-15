using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
<<<<<<< HEAD
    public class PhongHoc
=======
    public class PhongHoc : BaseEntity
>>>>>>> origin/main
    {
        [Key]
        public int PhongHocID { get; set; }

        [Required]
        public int CoSoID { get; set; }

        [Required]
        [StringLength(50)]
        public string TenPhong { get; set; }

        [Required]
        public int SucChua { get; set; }

        [Required]
        [StringLength(20)]
        public string TrangThai { get; set; } = "Đang sử dụng";

        [StringLength(300)]
        public string? GhiChu { get; set; }

        public CoSo? CoSo { get; set; }

        public ICollection<LichHoc>? LichHocs { get; set; }
    }
}