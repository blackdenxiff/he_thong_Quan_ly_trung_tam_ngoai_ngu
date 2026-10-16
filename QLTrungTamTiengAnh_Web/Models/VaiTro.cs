using System.ComponentModel.DataAnnotations;

namespace QuanLyTrungTamNgoaiNgu.Models
{
    public class VaiTro : BaseEntity
    {
        [Key]
        public int VaiTroID { get; set; }

        [Required]
        [StringLength(30)]
        public string TenVaiTro { get; set; }

        [StringLength(200)]
        public string? MoTa { get; set; }

        public ICollection<NguoiDung>? NguoiDungs { get; set; }
    }
}