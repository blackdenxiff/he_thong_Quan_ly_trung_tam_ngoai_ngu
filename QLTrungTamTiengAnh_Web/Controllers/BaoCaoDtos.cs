using System;
using System.Collections.Generic;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public sealed class BaoCaoFilterDto
    {
        public string KieuThoiGian { get; set; } = "month";
        public int? Nam { get; set; }
        public int? Thang { get; set; }
        public int? Quy { get; set; }
        public DateTime? TuNgay { get; set; }
        public DateTime? DenNgay { get; set; }
        public int? CoSoId { get; set; }
    }

    public sealed class BaoCaoRange
    {
        public DateTime TuNgay { get; set; }
        public DateTime DenNgay { get; set; }
        public string NhanKy { get; set; } = "";
        public string MucNhom { get; set; } = "Ngày";
    }

    public sealed class BaoCaoTongHopDto
    {
        public BaoCaoRange KyBaoCao { get; set; } = new();
        public DateTime CapNhatLuc { get; set; }
        public decimal TongDoanhThu { get; set; }
        public int HocVienMoi { get; set; }
        public int HocVienDangHoc { get; set; }
        public int HocVienHoanThanh { get; set; }
        public List<BaoCaoSeriesPointDto> DoanhThu { get; set; } = new();
        public List<BaoCaoGrowthPointDto> TangTruongHocVien { get; set; } = new();
        public List<BaoCaoDistributionDto> PhanBoKhoaHoc { get; set; } = new();
        public List<BaoCaoBranchSizeDto> QuyMoCoSo { get; set; } = new();
        public List<BaoCaoAttendanceDto> ChuyenCan { get; set; } = new();
    }

    public sealed class BaoCaoSeriesPointDto
    {
        public string Nhan { get; set; } = "";
        public decimal GiaTri { get; set; }
    }

    public sealed class BaoCaoGrowthPointDto
    {
        public string Nhan { get; set; } = "";
        public int HocVienMoi { get; set; }
        public int DangHoc { get; set; }
        public int HoanThanh { get; set; }
    }

    public sealed class BaoCaoDistributionDto
    {
        public string Nhan { get; set; } = "";
        public int SoLuong { get; set; }
        public decimal TyLe { get; set; }
    }

    public sealed class BaoCaoBranchSizeDto
    {
        public int CoSoId { get; set; }
        public string TenCoSo { get; set; } = "";
        public int SoPhong { get; set; }
        public int SoNhanSu { get; set; }
    }

    public sealed class BaoCaoAttendanceDto
    {
        public string Nhan { get; set; } = "";
        public int DungGio { get; set; }
        public int DiMuon { get; set; }
        public int VangCoPhep { get; set; }
        public int VangKhongPhep { get; set; }
        public int ChuaDiemDanh { get; set; }
        public int TongDaDiemDanh { get; set; }
    }

    public sealed class BaoCaoBranchOptionDto
    {
        public int CoSoId { get; set; }
        public string TenCoSo { get; set; } = "";
    }
}
