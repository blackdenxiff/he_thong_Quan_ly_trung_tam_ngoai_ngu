using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
<<<<<<< HEAD
    public class LichSuDieuChuyen
=======
    public class LichSuDieuChuyen : BaseEntity
>>>>>>> origin/main
    {
        [Key]
        public int DieuChuyenID { get; set; }

        [Required]
        public int NhanSuID { get; set; }

        [Required]
        public int CoSoCuID { get; set; }

        [Required]
        public int CoSoMoiID { get; set; }

        public DateTime NgayDeXuat { get; set; } = DateTime.Now;

        public DateTime? NgayHieuLuc { get; set; }

        public int? NguoiDeXuatID { get; set; }

        public int? NguoiDuyetID { get; set; }

        [Required]
        [StringLength(20)]
        public string TrangThai { get; set; } = "Chờ duyệt";

        [StringLength(300)]
        public string? LyDo { get; set; }

        public NhanSu? NhanSu { get; set; }

        public CoSo? CoSoCu { get; set; }

        public CoSo? CoSoMoi { get; set; }

        public NhanSu? NguoiDeXuat { get; set; }

        public NguoiDung? NguoiDuyet { get; set; }
    }
}