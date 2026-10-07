using Microsoft.AspNetCore.Mvc;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class BaiTapController : Controller
    {
        private readonly IWebHostEnvironment _environment;

        public BaiTapController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        // GET: /BaiTap/NopBai
        public IActionResult NopBai()
        {
            return View();
        }

        // POST: /BaiTap/UploadBaiTap (Xử lý nhận tệp tin và ràng buộc validation)
        [HttpPost]
        public async Task<IActionResult> UploadBaiTap(int baiTapId, int hocVienId, IFormFile fileNop)
        {
            // 1. Kiểm tra có chọn file hay không
            if (fileNop == null || fileNop.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn tệp tin bài làm để nộp!" });
            }

            // 2. Ràng buộc kích thước file (Tối đa 15MB)
            const long maxFileSize = 15 * 1024 * 1024;
            if (fileNop.Length > maxFileSize)
            {
                return Json(new { success = false, message = "Dung lượng tệp vượt quá giới hạn cho phép (Tối đa 15MB)!" });
            }

            // 3. Ràng buộc định dạng đuôi file (.pdf, .docx, .doc, .zip, .rar)
            var allowedExtensions = new[] { ".pdf", ".docx", ".doc", ".zip", ".rar" };
            var extension = Path.GetExtension(fileNop.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                return Json(new
                {
                    success = false,
                    message = "Định dạng tệp không hợp lệ! Hệ thống chỉ chấp nhận: .pdf, .docx, .doc, .zip, .rar"
                });
            }

            try
            {
                // 4. Tạo tên file duy nhất tránh bị trùng/ghi đè (Dạng: BT{id}_HV{id}_{timestamp}{ext})
                string uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "baitap");
                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                string uniqueFileName = $"BT{baiTapId}_HV{hocVienId}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                string filePath = Path.Combine(uploadFolder, uniqueFileName);

                // 5. Lưu file vào thư mục wwwroot/uploads/baitap
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await fileNop.CopyToAsync(fileStream);
                }

                // Đường dẫn URL ảo để lưu vào cột ChiTietNopBai.DuongDan trong Database
                string fileRelativeUrl = $"/uploads/baitap/{uniqueFileName}";

                return Json(new
                {
                    success = true,
                    message = "Nộp bài tập thành công!",
                    fileUrl = fileRelativeUrl,
                    fileName = fileNop.FileName,
                    submitTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Đã xảy ra lỗi khi lưu file: " + ex.Message });
            }
        }
    }
}