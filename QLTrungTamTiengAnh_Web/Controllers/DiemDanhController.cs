using Microsoft.AspNetCore.Mvc;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class DiemDanhController : Controller
    {
        // GET: /DiemDanh
        public IActionResult Index()
        {
            return View();
        }

        // Action nhận yêu cầu AJAX cập nhật trạng thái điểm danh
        [HttpPost]
        public IActionResult UpdateAttendance([FromBody] AttendanceUpdateDto model)
        {
            if (model == null)
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });
            }

            // TODO: Kết nối Entity Framework để cập nhật bảng DiemDanh trong CSDL
            // Tạm thời trả về JSON thành công để kiểm thử giao diện Frontend
            return Json(new
            {
                success = true,
                message = "Cập nhật điểm danh thành công!",
                studentId = model.StudentId,
                status = model.Status
            });
        }
    }

    public class AttendanceUpdateDto
    {
        public int ScheduleId { get; set; }
        public int StudentId { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
    }
}