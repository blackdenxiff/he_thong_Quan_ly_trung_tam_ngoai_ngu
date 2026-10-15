using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
//using QLTrungTamTiengAnh_Web.Data;
using QuanLyTrungTamNgoaiNgu.Data;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class TestDbController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TestDbController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            try
            {
                bool connected = _context.Database.CanConnect();

                if (connected)
                {
                    return Content("Kết nối SQL Server thành công!");
                }

                return Content("Không kết nối được SQL Server!");
            }
            catch (Exception ex)
            {
                return Content("Lỗi kết nối: " + ex.Message);
            }
        }
    }
}