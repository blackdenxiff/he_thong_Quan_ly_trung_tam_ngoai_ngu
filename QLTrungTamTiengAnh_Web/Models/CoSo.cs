using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
    public class CoSo : BaseEntity
    {
        [Key]
        public int CoSoID { get; set; }

        [Required]
        [StringLength(100)]
        public string TenCoSo { get; set; }

        [Required]
        [StringLength(250)]
        public string DiaChi { get; set; }

        [StringLength(20)]
        public string? SoDienThoai { get; set; }

        public bool TrangThai { get; set; } = true;

        public ICollection<PhongHoc>? PhongHocs { get; set; }

        public ICollection<NhanSu>? NhanSus { get; set; }

        public ICollection<LopHoc>? LopHocs { get; set; }

        public ICollection<LichSuDieuChuyen> LichSuDieuChuyenCosocu { get; set; }
            = new List<LichSuDieuChuyen>();

        public ICollection<LichSuDieuChuyen> LichSuDieuChuyenCosomoi { get; set; }
            = new List<LichSuDieuChuyen>();
    }
}