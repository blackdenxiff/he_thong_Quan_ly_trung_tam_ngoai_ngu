using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
<<<<<<< HEAD
    public class DangKy
=======
    public class DangKy : BaseEntity
>>>>>>> origin/main
    {
        [Key]
        public int DangKyID { get; set; }

        [Required]
        public int HocVienID { get; set; }

        [Required]
        public int LopHocID { get; set; }

        public DateTime NgayDangKy { get; set; } = DateTime.Now;

        [Required]
        public decimal HocPhiApDung { get; set; }

        [Required]
        [StringLength(20)]
        public string TrangThai { get; set; } = "Đang học";

        public decimal? DiemCuoiKhoa { get; set; }

        [StringLength(20)]
        public string? KetQua { get; set; }

        public bool DuocHocLaiMienPhi { get; set; } = false;

        [StringLength(500)]
        public string? GhiChu { get; set; }

        public HocVien? HocVien { get; set; }

        public LopHoc? LopHoc { get; set; }

        public ICollection<HoaDon>? HoaDons { get; set; }

        public ICollection<CamKet>? CamKets { get; set; }
    }
}