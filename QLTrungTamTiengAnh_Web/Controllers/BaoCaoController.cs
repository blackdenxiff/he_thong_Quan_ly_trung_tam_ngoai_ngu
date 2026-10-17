using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;
using QLTrungTamTiengAnh_Web.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    [Authorize(Roles = "Admin,QuanLy,Quản lý")]
    public class BaoCaoController : Controller
    {
        private readonly IBaoCaoService _service;
        private readonly ILogger<BaoCaoController> _logger;

        public BaoCaoController(IBaoCaoService service, ILogger<BaoCaoController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> Branches(CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.GetBranchOptionsAsync(cancellationToken);
                return Json(new { success = true, data });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception exception)
            {
                return Fail(exception, "Branches");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Data([FromBody] BaoCaoFilterDto? filter, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.GetReportAsync(filter, cancellationToken);
                return Json(new { success = true, data });
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { success = false, message = exception.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception exception)
            {
                return Fail(exception, "Data");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportExcel([FromBody] BaoCaoFilterDto? filter, CancellationToken cancellationToken)
        {
            try
            {
                var report = await _service.GetReportAsync(filter, cancellationToken);
                return File(BuildExcel(report),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"BaoCaoTongHop_{report.KyBaoCao.TuNgay:yyyy-MM}.xlsx");
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { success = false, message = exception.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception exception)
            {
                return Fail(exception, "ExportExcel");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportPdf([FromBody] BaoCaoFilterDto? filter, CancellationToken cancellationToken)
        {
            try
            {
                var report = await _service.GetReportAsync(filter, cancellationToken);
                return File(BuildPdf(report), "application/pdf", $"BaoCaoTongHop_{report.KyBaoCao.TuNgay:yyyy-MM}.pdf");
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { success = false, message = exception.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception exception)
            {
                return Fail(exception, "ExportPdf");
            }
        }

        private IActionResult Fail(Exception exception, string operation)
        {
            _logger.LogError(exception, "Lỗi BaoCaoController: {Operation}", operation);
            return StatusCode(500, new
            {
                success = false,
                message = "Không thể xử lý báo cáo. Vui lòng thử lại."
            });
        }

        private static byte[] BuildExcel(BaoCaoTongHopDto report)
        {
            using var workbook = new XLWorkbook();
            AddSummarySheet(workbook, report);
            AddRevenueSheet(workbook, report);
            AddGrowthSheet(workbook, report);
            AddDistributionSheet(workbook, report);
            AddBranchSheet(workbook, report);
            AddAttendanceSheet(workbook, report);
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static byte[] BuildPdf(BaoCaoTongHopDto report)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            return Document.Create(document => document.Page(page =>
            {
                page.Margin(30);
                page.Header().Text("BÁO CÁO TỔNG HỢP").Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                page.Content().Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text($"Kỳ báo cáo: {report.KyBaoCao.NhanKy}");
                    column.Item().Text($"Cập nhật: {report.CapNhatLuc:dd/MM/yyyy HH:mm:ss}");
                    column.Item().Text($"Doanh thu thực thu: {report.TongDoanhThu:N0} VNĐ");
                    column.Item().Text($"Học viên mới: {report.HocVienMoi} | Đang học: {report.HocVienDangHoc} | Hoàn thành: {report.HocVienHoanThanh}");
                    PdfTable(column, "Doanh thu học phí", new[] { "Mốc", "VNĐ" }, report.DoanhThu.Select(x => new[] { x.Nhan, x.GiaTri.ToString("N0") }));
                    PdfTable(column, "Tăng trưởng học viên", new[] { "Mốc", "Mới", "Đang học", "Hoàn thành" }, report.TangTruongHocVien.Select(x => new[] { x.Nhan, x.HocVienMoi.ToString(), x.DangHoc.ToString(), x.HoanThanh.ToString() }));
                    PdfTable(column, "Phân bố theo khóa học", new[] { "Khóa học", "Số lượng", "%" }, report.PhanBoKhoaHoc.Select(x => new[] { x.Nhan, x.SoLuong.ToString(), x.TyLe.ToString("N2") }));
                    PdfTable(column, "Quy mô hiện tại theo cơ sở", new[] { "Cơ sở", "Phòng", "Nhân sự" }, report.QuyMoCoSo.Select(x => new[] { x.TenCoSo, x.SoPhong.ToString(), x.SoNhanSu.ToString() }));
                    PdfTable(column, "Chuyên cần & điểm danh", new[] { "Lớp", "Đúng giờ", "Muộn", "Có phép", "Không phép", "Chưa điểm danh" }, report.ChuyenCan.Select(x => new[] { x.Nhan, x.DungGio.ToString(), x.DiMuon.ToString(), x.VangCoPhep.ToString(), x.VangKhongPhep.ToString(), x.ChuaDiemDanh.ToString() }));
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Trang ");
                    text.CurrentPageNumber();
                });
            })).GeneratePdf();
        }

        private static void AddSummarySheet(XLWorkbook workbook, BaoCaoTongHopDto report)
        {
            var sheet = workbook.Worksheets.Add("Tổng hợp");
            sheet.Cell(1, 1).Value = "BÁO CÁO TỔNG HỢP";
            sheet.Cell(2, 1).Value = "Kỳ báo cáo"; sheet.Cell(2, 2).Value = report.KyBaoCao.NhanKy;
            sheet.Cell(3, 1).Value = "Cập nhật"; sheet.Cell(3, 2).Value = report.CapNhatLuc;
            sheet.Cell(5, 1).Value = "Doanh thu thực thu (VNĐ)"; sheet.Cell(5, 2).Value = report.TongDoanhThu;
            sheet.Cell(6, 1).Value = "Học viên mới"; sheet.Cell(6, 2).Value = report.HocVienMoi;
            sheet.Cell(7, 1).Value = "Đang học"; sheet.Cell(7, 2).Value = report.HocVienDangHoc;
            sheet.Cell(8, 1).Value = "Hoàn thành"; sheet.Cell(8, 2).Value = report.HocVienHoanThanh;
            FormatSheet(sheet);
        }

        private static void AddRevenueSheet(XLWorkbook workbook, BaoCaoTongHopDto report)
        {
            var sheet = workbook.Worksheets.Add("Doanh thu"); AddHeaders(sheet, "Mốc", "Doanh thu VNĐ");
            for (var i = 0; i < report.DoanhThu.Count; i++) { sheet.Cell(i + 2, 1).Value = report.DoanhThu[i].Nhan; sheet.Cell(i + 2, 2).Value = report.DoanhThu[i].GiaTri; }
            FormatSheet(sheet);
        }

        private static void AddGrowthSheet(XLWorkbook workbook, BaoCaoTongHopDto report)
        {
            var sheet = workbook.Worksheets.Add("Tăng trưởng"); AddHeaders(sheet, "Mốc", "Học viên mới", "Đang học", "Hoàn thành");
            for (var i = 0; i < report.TangTruongHocVien.Count; i++) { var row = report.TangTruongHocVien[i]; sheet.Cell(i + 2, 1).Value = row.Nhan; sheet.Cell(i + 2, 2).Value = row.HocVienMoi; sheet.Cell(i + 2, 3).Value = row.DangHoc; sheet.Cell(i + 2, 4).Value = row.HoanThanh; }
            FormatSheet(sheet);
        }

        private static void AddDistributionSheet(XLWorkbook workbook, BaoCaoTongHopDto report)
        {
            var sheet = workbook.Worksheets.Add("Phân bố"); AddHeaders(sheet, "Nhóm", "Số lượng", "Tỷ lệ %");
            for (var i = 0; i < report.PhanBoKhoaHoc.Count; i++) { var row = report.PhanBoKhoaHoc[i]; sheet.Cell(i + 2, 1).Value = row.Nhan; sheet.Cell(i + 2, 2).Value = row.SoLuong; sheet.Cell(i + 2, 3).Value = row.TyLe; }
            FormatSheet(sheet);
        }

        private static void AddBranchSheet(XLWorkbook workbook, BaoCaoTongHopDto report)
        {
            var sheet = workbook.Worksheets.Add("Quy mô cơ sở"); AddHeaders(sheet, "Cơ sở", "Phòng học", "Nhân sự");
            for (var i = 0; i < report.QuyMoCoSo.Count; i++) { var row = report.QuyMoCoSo[i]; sheet.Cell(i + 2, 1).Value = row.TenCoSo; sheet.Cell(i + 2, 2).Value = row.SoPhong; sheet.Cell(i + 2, 3).Value = row.SoNhanSu; }
            FormatSheet(sheet);
        }

        private static void AddAttendanceSheet(XLWorkbook workbook, BaoCaoTongHopDto report)
        {
            var sheet = workbook.Worksheets.Add("Chuyên cần"); AddHeaders(sheet, "Lớp", "Đúng giờ", "Đi muộn", "Vắng có phép", "Vắng không phép", "Chưa điểm danh");
            for (var i = 0; i < report.ChuyenCan.Count; i++) { var row = report.ChuyenCan[i]; sheet.Cell(i + 2, 1).Value = row.Nhan; sheet.Cell(i + 2, 2).Value = row.DungGio; sheet.Cell(i + 2, 3).Value = row.DiMuon; sheet.Cell(i + 2, 4).Value = row.VangCoPhep; sheet.Cell(i + 2, 5).Value = row.VangKhongPhep; sheet.Cell(i + 2, 6).Value = row.ChuaDiemDanh; }
            FormatSheet(sheet);
        }

        private static void AddHeaders(IXLWorksheet sheet, params string[] headers)
        {
            for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Row(1).Style.Font.Bold = true;
            sheet.Row(1).Style.Fill.BackgroundColor = XLColor.LightBlue;
        }

        private static void FormatSheet(IXLWorksheet sheet)
        {
            sheet.Columns().AdjustToContents();
            sheet.SheetView.FreezeRows(1);
        }

        private static void PdfTable(ColumnDescriptor column, string title, string[] headers, IEnumerable<string[]> rows)
        {
            column.Item().Text(title).Bold().FontSize(12);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns => { foreach (var _ in headers) columns.RelativeColumn(); });
                table.Header(header => { foreach (var item in headers) header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(item).Bold(); });
                foreach (var row in rows) foreach (var item in row) table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item);
            });
        }
    }
}
