using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class BaoCaoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BaoCaoController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /BaoCao/Index
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, int? coSoId)
        {
            var userRole = HttpContext.Session.GetString("UserRole");

            // Chỉ Admin/Quản lý được truy cập báo cáo
            if (userRole != "Admin" && userRole != "Quản lý")
                return Unauthorized();

            // Mặc định lọc tháng hiện tại
            if (!fromDate.HasValue)
                fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!toDate.HasValue)
                toDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month), 23, 59, 59);

            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.CoSoId = coSoId;

            // === BÁOCÁO TÀI CHÍNH ===
            // Doanh thu thực thu (từ ThanhToan.NgayThanhToan)
            var revenueQuery = _context.ThanhToans
                .Where(p => p.NgayThanhToan >= fromDate && p.NgayThanhToan <= toDate);

            var totalRevenue = await revenueQuery.SumAsync(p => p.SoTien);

            // Công nợ (tính từng hóa đơn: TongPhaiTra - tổng thanh toán)
            decimal totalDebt = 0;
            var invoices = await _context.HoaDons
                .Include(h => h.ThanhToans)
                .ThenInclude(t => t.HoaDon)
                .Where(h => h.NgayLap >= fromDate && h.NgayLap <= toDate &&
                            (h.TrangThai == "Chưa đủ" || h.TrangThai == "Thanh toán một phần"))
                .ToListAsync();

            foreach (var invoice in invoices)
            {
                decimal paid = invoice.ThanhToans?.Sum(t => t.SoTien) ?? 0;
                decimal debt = invoice.TongPhaiTra - paid;
                if (debt > 0)
                    totalDebt += debt;
            }

            // === BÁOCÁO TẠO TẠO ===
            // Số học viên mới (đăng ký trong khoảng ngày)
            var newStudents = await _context.DangKys
                .Where(d => d.NgayDangKy >= fromDate && d.NgayDangKy <= toDate)
                .Select(d => d.HocVienID)
                .Distinct()
                .CountAsync();

            // Tỷ lệ hoàn thành (DangKy có DiemCuoiKhoa)
            int totalEnrollments = await _context.DangKys.CountAsync();
            int completedEnrollments = await _context.DangKys
                .Where(d => d.DiemCuoiKhoa.HasValue)
                .CountAsync();
            double completionRate = totalEnrollments > 0 ? (completedEnrollments * 100.0) / totalEnrollments : 0;

            // Tỷ lệ đỗ/rớt (từ DangKy.KetQua)
            int passCount = await _context.DangKys
                .Where(d => d.KetQua == "Đỗ")
                .CountAsync();
            int failCount = await _context.DangKys
                .Where(d => d.KetQua == "Rớt")
                .CountAsync();
            int totalWithResult = passCount + failCount;
            double passRate = totalWithResult > 0 ? (passCount * 100.0) / totalWithResult : 0;

            // === BÁO CÁO NHÂN SỰ ===
            // Tổng chi phí lương (BangLuong.ThucLinh, trạng thái "Xác nhận" hoặc "Tạm tính")
            var payrollData = await _context.BangLuongs
                .Where(b => b.TrangThai == "Xác nhận" || b.TrangThai == "Tạm tính")
                .GroupBy(b => b.ThangNam)
                .Select(g => new { ThangNam = g.Key, TongThucLinh = g.Sum(b => b.ThucLinh) })
                .OrderBy(x => x.ThangNam)
                .ToListAsync();

            decimal totalPayroll = payrollData.Sum(p => p.TongThucLinh);

            // Gán dữ liệu vào ViewBag
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalDebt = totalDebt;
            ViewBag.NewStudents = newStudents;
            ViewBag.CompletionRate = completionRate;
            ViewBag.PassRate = passRate;
            ViewBag.PassCount = passCount;
            ViewBag.FailCount = failCount;
            ViewBag.TotalPayroll = totalPayroll;
            ViewBag.PayrollData = payrollData;

            // Doanh thu theo tháng (cho biểu đồ)
            var monthlyRevenue = await _context.ThanhToans
                .Where(p => p.NgayThanhToan >= new DateTime(DateTime.Now.Year, 1, 1) && 
                           p.NgayThanhToan <= new DateTime(DateTime.Now.Year, 12, 31, 23, 59, 59))
                .GroupBy(p => p.NgayThanhToan.Month)
                .Select(g => new 
                { 
                    Month = g.Key, 
                    Total = g.Sum(p => p.SoTien)
                })
                .OrderBy(x => x.Month)
                .ToListAsync();

            ViewBag.MonthlyRevenueLabels = string.Join(",", Enumerable.Range(1, 12).Select(m => $"T{m}"));
            ViewBag.MonthlyRevenueData = string.Join(",", Enumerable.Range(1, 12).Select(m =>
            {
                var data = monthlyRevenue.FirstOrDefault(x => x.Month == m);
                return data?.Total ?? 0;
            }));

            return View();
        }

        // POST: /BaoCao/ExportExcel - Xuất Excel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportExcel(DateTime? fromDate, DateTime? toDate, int? coSoId)
        {
            var userRole = HttpContext.Session.GetString("UserRole");

            // Chỉ Admin/Quản lý được export
            if (userRole != "Admin" && userRole != "Quản lý")
                return Unauthorized();

            // Mặc định lọc tháng hiện tại
            if (!fromDate.HasValue)
                fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!toDate.HasValue)
                toDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month), 23, 59, 59);

            // Tập hợp dữ liệu (giống Index)
            var revenue = await _context.ThanhToans
                .Where(p => p.NgayThanhToan >= fromDate && p.NgayThanhToan <= toDate)
                .SumAsync(p => p.SoTien);

            var newStudents = await _context.DangKys
                .Where(d => d.NgayDangKy >= fromDate && d.NgayDangKy <= toDate)
                .Select(d => d.HocVienID)
                .Distinct()
                .CountAsync();

            // Tạo file Excel đơn giản (nếu chưa cài ClosedXML, trả về dữ liệu dạng CSV)
            var csvContent = "Báo Cáo Tổng Hợp\n";
            csvContent += $"Kỳ báo cáo: {fromDate:dd/MM/yyyy} - {toDate:dd/MM/yyyy}\n\n";
            csvContent += "Doanh Thu (VND)," + revenue + "\n";
            csvContent += "Học Viên Mới," + newStudents + "\n";

            var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
            return File(bytes, "text/csv", $"BaoCao_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }

        // API: /BaoCao/GetChartData - Lấy dữ liệu biểu đồ (tùy chọn)
        [HttpGet]
        public async Task<IActionResult> GetChartData(DateTime? fromDate, DateTime? toDate)
        {
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userRole != "Admin" && userRole != "Quản lý")
                return Unauthorized();

            if (!fromDate.HasValue)
                fromDate = new DateTime(DateTime.Now.Year, 1, 1);
            if (!toDate.HasValue)
                toDate = new DateTime(DateTime.Now.Year, 12, 31, 23, 59, 59);

            var monthlyData = await _context.ThanhToans
                .Where(p => p.NgayThanhToan >= fromDate && p.NgayThanhToan <= toDate)
                .GroupBy(p => p.NgayThanhToan.Month)
                .Select(g => new { Month = g.Key, Total = g.Sum(p => p.SoTien) })
                .OrderBy(x => x.Month)
                .ToListAsync();

            return Json(new
            {
                labels = Enumerable.Range(1, 12).Select(m => $"T{m}"),
                data = Enumerable.Range(1, 12).Select(m =>
                {
                    var data = monthlyData.FirstOrDefault(x => x.Month == m);
                    return data?.Total.ToString("F0") ?? "0";
                })
            });
        }
    }
}
