using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
    public class NguoiDung
    {
        [Key]
        public int NguoiDungID { get; set; }

        [Required]
        [StringLength(50)]
        public string TenDangNhap { get; set; }

        [Required]
        [StringLength(255)]
        public string MatKhauHash { get; set; }

        [Required]
        public int VaiTroID { get; set; }

        public bool TrangThai { get; set; } = true;

        public VaiTro? VaiTro { get; set; }

        public NhanSu? NhanSu { get; set; }

        public HocVien? HocVien { get; set; }

        public ICollection<PhanCongDay>? PhanCongDays { get; set; }

        public ICollection<CamKet>? CamKetsDuyet { get; set; }

        public ICollection<LichSuDieuChuyen>? LichSuDieuChuyensDuyet { get; set; }
    }
}