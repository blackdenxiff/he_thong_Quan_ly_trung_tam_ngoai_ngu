using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
    public class CamKet : BaseEntity
    {
        [Key]
        public int CamKetID { get; set; }

        [Required]
        public int HocVienID { get; set; }

        [Required]
        public int KhoaHocID { get; set; }

        public int? DangKyID { get; set; }

        public DateTime NgayLap { get; set; } = DateTime.Now;

        [Required]
        [StringLength(20)]
        public string TrangThai { get; set; } = "Chờ duyệt";

        public decimal? DiemDauVao { get; set; }

        public decimal? DiemYeuCauCuoiKhoa { get; set; }

        public decimal? DiemCuoiKhoa { get; set; }

        public int? GiaoVienDeXuatID { get; set; }

        public int? NguoiDuyetID { get; set; }

        public DateTime? NgayDuyet { get; set; }

        [StringLength(500)]
        public string? GhiChu { get; set; }

        public HocVien? HocVien { get; set; }

        public KhoaHoc? KhoaHoc { get; set; }

        public DangKy? DangKy { get; set; }

        public NhanSu? GiaoVienDeXuat { get; set; }

        public NguoiDung? NguoiDuyet { get; set; }
    }
}