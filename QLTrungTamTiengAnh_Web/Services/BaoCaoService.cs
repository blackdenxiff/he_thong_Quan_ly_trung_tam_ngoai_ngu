using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QLTrungTamTiengAnh_Web.Controllers;
using QuanLyTrungTamNgoaiNgu.Data;
using System.Data;

namespace QLTrungTamTiengAnh_Web.Services
{
    public sealed class BaoCaoService : IBaoCaoService
    {
        private readonly ApplicationDbContext _db;

        public BaoCaoService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<BaoCaoBranchOptionDto>> GetBranchOptionsAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(@"
                SELECT CoSoID, TenCoSo
                FROM dbo.CoSo
                WHERE IsDeleted = 0 AND TrangThai = 1
                ORDER BY TenCoSo, CoSoID", connection);

            var result = new List<BaoCaoBranchOptionDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new BaoCaoBranchOptionDto
                {
                    CoSoId = reader.GetInt32(0),
                    TenCoSo = reader.GetString(1)
                });
            }

            return result;
        }

        public async Task<BaoCaoTongHopDto> GetReportAsync(
            BaoCaoFilterDto? filter,
            CancellationToken cancellationToken = default)
        {
            if (!TryBuildRange(filter, out var range, out var error))
                throw new ArgumentException(error);

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var result = new BaoCaoTongHopDto
            {
                KyBaoCao = range,
                CapNhatLuc = DateTime.Now
            };

            const string sql = @"
DECLARE @fromExclusive datetime2 = @fromDate;
DECLARE @toExclusive datetime2 = @toDate;

SELECT CASE WHEN @groupLevel = 0 THEN CONVERT(date, p.NgayThanhToan)
            ELSE DATEFROMPARTS(YEAR(p.NgayThanhToan), MONTH(p.NgayThanhToan), 1) END AS Bucket,
       SUM(p.SoTien) AS Total
FROM dbo.ThanhToan p
WHERE p.IsDeleted = 0 AND p.NgayThanhToan >= @fromExclusive AND p.NgayThanhToan < @toExclusive
  AND p.SoTien > 0
GROUP BY CASE WHEN @groupLevel = 0 THEN CONVERT(date, p.NgayThanhToan)
              ELSE DATEFROMPARTS(YEAR(p.NgayThanhToan), MONTH(p.NgayThanhToan), 1) END
ORDER BY Bucket;

WITH FirstRegistration AS
(
    SELECT d.HocVienID, MIN(d.NgayDangKy) AS FirstDate
    FROM dbo.DangKy d
    WHERE d.IsDeleted = 0 AND d.TrangThai NOT IN (N'Hủy', N'Đã hủy')
    GROUP BY d.HocVienID
)
SELECT CASE WHEN @groupLevel = 0 THEN CONVERT(date, f.FirstDate)
            ELSE DATEFROMPARTS(YEAR(f.FirstDate), MONTH(f.FirstDate), 1) END AS Bucket,
       COUNT(*) AS NewStudents,
       0 AS ActiveStudents,
       0 AS CompletedStudents
FROM FirstRegistration f
WHERE f.FirstDate >= @fromExclusive AND f.FirstDate < @toExclusive
GROUP BY CASE WHEN @groupLevel = 0 THEN CONVERT(date, f.FirstDate)
              ELSE DATEFROMPARTS(YEAR(f.FirstDate), MONTH(f.FirstDate), 1) END
UNION ALL
SELECT CASE WHEN @groupLevel = 0 THEN CONVERT(date, d.NgayDangKy)
            ELSE DATEFROMPARTS(YEAR(d.NgayDangKy), MONTH(d.NgayDangKy), 1) END,
       0,
       SUM(CASE WHEN d.TrangThai = N'Đang học' THEN 1 ELSE 0 END),
       SUM(CASE WHEN d.TrangThai IN (N'Hoàn thành', N'Đã hoàn thành') OR d.KetQua IS NOT NULL THEN 1 ELSE 0 END)
FROM dbo.DangKy d
WHERE d.IsDeleted = 0 AND d.TrangThai NOT IN (N'Hủy', N'Đã hủy')
  AND d.NgayDangKy >= @fromExclusive AND d.NgayDangKy < @toExclusive
GROUP BY CASE WHEN @groupLevel = 0 THEN CONVERT(date, d.NgayDangKy)
              ELSE DATEFROMPARTS(YEAR(d.NgayDangKy), MONTH(d.NgayDangKy), 1) END
ORDER BY Bucket;

SELECT k.KhoaHocID, k.TenKhoaHoc, COUNT(DISTINCT d.HocVienID) AS StudentCount
FROM dbo.DangKy d
INNER JOIN dbo.LopHoc l ON l.LopHocID = d.LopHocID AND l.IsDeleted = 0
INNER JOIN dbo.KhoaHoc k ON k.KhoaHocID = l.KhoaHocID AND k.IsDeleted = 0
WHERE d.IsDeleted = 0 AND d.TrangThai NOT IN (N'Hủy', N'Đã hủy')
  AND d.NgayDangKy >= @fromExclusive AND d.NgayDangKy < @toExclusive
GROUP BY k.KhoaHocID, k.TenKhoaHoc
ORDER BY k.TenKhoaHoc;

SELECT c.CoSoID, c.TenCoSo,
       (SELECT COUNT(*) FROM dbo.PhongHoc p WHERE p.CoSoID = c.CoSoID AND p.IsDeleted = 0) AS RoomCount,
       (SELECT COUNT(*) FROM dbo.NhanSu n WHERE n.CoSoID = c.CoSoID AND n.IsDeleted = 0 AND n.TrangThai = N'Đang làm') AS StaffCount
FROM dbo.CoSo c
WHERE c.IsDeleted = 0 AND c.TrangThai = 1 AND (@coSoId IS NULL OR c.CoSoID = @coSoId)
ORDER BY c.TenCoSo, c.CoSoID;

SELECT l.LopHocID, l.TenLop,
       SUM(CASE WHEN dd.TrangThai IN (N'Đúng giờ', N'Có mặt') THEN 1 ELSE 0 END) AS OnTime,
       SUM(CASE WHEN dd.TrangThai IN (N'Đi muộn', N'Muộn') THEN 1 ELSE 0 END) AS Late,
       SUM(CASE WHEN dd.TrangThai IN (N'Vắng có phép', N'Có phép') THEN 1 ELSE 0 END) AS Excused,
       SUM(CASE WHEN dd.TrangThai IN (N'Vắng không phép', N'Vắng') THEN 1 ELSE 0 END) AS Unexcused,
       COUNT(dd.DiemDanhID) AS Marked,
       (SELECT COUNT(*) FROM dbo.DangKy d WHERE d.LopHocID = l.LopHocID AND d.IsDeleted = 0 AND d.TrangThai NOT IN (N'Hủy', N'Đã hủy'))
       * (SELECT COUNT(*) FROM dbo.LichHoc lh2 WHERE lh2.LopHocID = l.LopHocID AND lh2.IsDeleted = 0
                                      AND lh2.NgayHoc >= @fromExclusive AND lh2.NgayHoc < @toExclusive
                                      AND lh2.TrangThai NOT IN (N'Đã hủy', N'Hủy')) AS Expected
FROM dbo.LopHoc l
INNER JOIN dbo.LichHoc lh ON lh.LopHocID = l.LopHocID AND lh.IsDeleted = 0
LEFT JOIN dbo.DiemDanh dd ON dd.LichHocID = lh.LichHocID AND dd.IsDeleted = 0
WHERE l.IsDeleted = 0 AND lh.NgayHoc >= @fromExclusive AND lh.NgayHoc < @toExclusive
  AND lh.TrangThai NOT IN (N'Đã hủy', N'Hủy')
  AND (@coSoId IS NULL OR l.CoSoID = @coSoId)
GROUP BY l.LopHocID, l.TenLop
ORDER BY l.TenLop, l.LopHocID;";

            await using var command = new SqlCommand(sql, connection);
            Add(command, "@fromDate", SqlDbType.DateTime2, range.TuNgay);
            Add(command, "@toDate", SqlDbType.DateTime2, range.DenNgay.AddDays(1));
            Add(command, "@groupLevel", SqlDbType.Int, range.MucNhom == "Ngày" ? 0 : 1);
            Add(command, "@coSoId", SqlDbType.Int, filter?.CoSoId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var value = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                result.DoanhThu.Add(new BaoCaoSeriesPointDto
                {
                    Nhan = reader.GetDateTime(0).ToString(range.MucNhom == "Ngày" ? "dd/MM" : "MM/yyyy"),
                    GiaTri = value
                });
                result.TongDoanhThu += value;
            }

            await reader.NextResultAsync(cancellationToken);
            var growth = new SortedDictionary<string, BaoCaoGrowthPointDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var key = reader.GetDateTime(0).ToString(range.MucNhom == "Ngày" ? "dd/MM" : "MM/yyyy");
                if (!growth.TryGetValue(key, out var point))
                {
                    point = new BaoCaoGrowthPointDto { Nhan = key };
                    growth[key] = point;
                }
                point.HocVienMoi += reader.GetInt32(1);
                point.DangHoc += reader.GetInt32(2);
                point.HoanThanh += reader.GetInt32(3);
            }
            result.TangTruongHocVien = growth.Values.ToList();
            result.HocVienMoi = result.TangTruongHocVien.Sum(x => x.HocVienMoi);
            result.HocVienDangHoc = result.TangTruongHocVien.Sum(x => x.DangHoc);
            result.HocVienHoanThanh = result.TangTruongHocVien.Sum(x => x.HoanThanh);

            await reader.NextResultAsync(cancellationToken);
            var distributionTotal = 0;
            var distribution = new List<(string Name, int Count)>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var count = reader.GetInt32(2);
                distribution.Add((reader.GetString(1), count));
                distributionTotal += count;
            }
            result.PhanBoKhoaHoc = distribution.Select(x => new BaoCaoDistributionDto
            {
                Nhan = x.Name,
                SoLuong = x.Count,
                TyLe = distributionTotal == 0 ? 0 : Math.Round(x.Count * 100m / distributionTotal, 2)
            }).ToList();

            await reader.NextResultAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.QuyMoCoSo.Add(new BaoCaoBranchSizeDto
                {
                    CoSoId = reader.GetInt32(0),
                    TenCoSo = reader.GetString(1),
                    SoPhong = reader.GetInt32(2),
                    SoNhanSu = reader.GetInt32(3)
                });
            }

            await reader.NextResultAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var onTime = reader.GetInt32(2);
                var late = reader.GetInt32(3);
                var excused = reader.GetInt32(4);
                var unexcused = reader.GetInt32(5);
                var marked = reader.GetInt32(6);
                var expected = reader.GetInt32(7);
                result.ChuyenCan.Add(new BaoCaoAttendanceDto
                {
                    Nhan = reader.GetString(1),
                    DungGio = onTime,
                    DiMuon = late,
                    VangCoPhep = excused,
                    VangKhongPhep = unexcused,
                    ChuaDiemDanh = Math.Max(0, expected - marked),
                    TongDaDiemDanh = marked
                });
            }

            return result;
        }

        private SqlConnection CreateConnection()
            => new(_db.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Chưa cấu hình kết nối SQL Server."));

        private static void Add(SqlCommand command, string name, SqlDbType type, object? value)
        {
            var parameter = command.Parameters.Add(name, type);
            parameter.Value = value ?? DBNull.Value;
        }

        private static bool TryBuildRange(BaoCaoFilterDto? filter, out BaoCaoRange range, out string error)
        {
            filter ??= new BaoCaoFilterDto();
            var now = DateTime.Now;
            var type = (filter.KieuThoiGian ?? "month").Trim().ToLowerInvariant();
            DateTime from;
            DateTime to;

            if (type == "quarter")
            {
                var year = filter.Nam ?? now.Year;
                var quarter = filter.Quy ?? ((now.Month - 1) / 3 + 1);
                if (year < 1 || year > 9999 || quarter is < 1 or > 4)
                {
                    range = new();
                    error = "Quý hoặc năm không hợp lệ.";
                    return false;
                }
                from = new DateTime(year, (quarter - 1) * 3 + 1, 1);
                to = from.AddMonths(3).AddDays(-1);
            }
            else if (type == "year")
            {
                var year = filter.Nam ?? now.Year;
                if (year < 1 || year > 9999)
                {
                    range = new();
                    error = "Năm không hợp lệ.";
                    return false;
                }
                from = new DateTime(year, 1, 1);
                to = new DateTime(year, 12, 31);
            }
            else if (type == "range")
            {
                if (!filter.TuNgay.HasValue || !filter.DenNgay.HasValue)
                {
                    range = new();
                    error = "Vui lòng chọn đầy đủ khoảng ngày.";
                    return false;
                }
                from = filter.TuNgay.Value.Date;
                to = filter.DenNgay.Value.Date;
                if (to < from)
                {
                    range = new();
                    error = "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.";
                    return false;
                }
            }
            else
            {
                var year = filter.Nam ?? now.Year;
                var month = filter.Thang ?? now.Month;
                if (year < 1 || year > 9999 || month is < 1 or > 12)
                {
                    range = new();
                    error = "Tháng hoặc năm không hợp lệ.";
                    return false;
                }
                from = new DateTime(year, month, 1);
                to = from.AddMonths(1).AddDays(-1);
            }

            range = new BaoCaoRange
            {
                TuNgay = from,
                DenNgay = to,
                MucNhom = type == "month" ? "Ngày" : "Tháng",
                NhanKy = from == to ? from.ToString("dd/MM/yyyy") : $"{from:dd/MM/yyyy} - {to:dd/MM/yyyy}"
            };
            error = "";
            return true;
        }
    }
}
