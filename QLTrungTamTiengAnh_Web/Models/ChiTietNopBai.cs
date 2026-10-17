using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // 1. Thêm using này nếu chưa có

namespace QuanLyTrungTamNgoaiNgu.Models
{
    // 2. Thêm thuộc tính này để ánh xạ đúng bảng trong SQL Server
    [Table("ChiTietNopBai")]
    public class ChiTietNopBai : BaseEntity
    {
        [Key]
        public int ChiTietNopBaiID { get; set; }

        [Required]
        public int BaiTapID { get; set; }

        [Required]
        public int HocVienID { get; set; }

        [Required]
        public int LanNop { get; set; } = 1;

        [StringLength(500)]
        public string? DuongDan { get; set; }

        public DateTime ThoiGianNop { get; set; } = DateTime.Now;

        public decimal? Diem { get; set; }

        [StringLength(500)]
        public string? NhanXet { get; set; }

        public int? NguoiChamID { get; set; }

        public DateTime? NgayCham { get; set; }

        public BaiTap? BaiTap { get; set; }

        public HocVien? HocVien { get; set; }

        public NhanSu? NguoiCham { get; set; }
    }
}