#nullable disable
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QLTrungTamTiengAnh_Web.Controllers
{
    public class BaiTapController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public BaiTapController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: /BaiTap/NopBai?baiTapId=1
        public async Task<IActionResult> NopBai(int? baiTapId)
        {
            try
            {
                var username = HttpContext.Session.GetString("UserName")
                              ?? HttpContext.Session.GetString("TenDangNhap")
                              ?? HttpContext.Session.GetString("Username")
                              ?? "";

                var user = await _context.NguoiDungs.FirstOrDefaultAsync(u => u.TenDangNhap.ToLower() == username.ToLower());
                var hocVien = user != null ? await _context.HocViens.FirstOrDefaultAsync(hv => hv.NguoiDungID == user.NguoiDungID) : null;

                if (hocVien == null)
                {
                    int? sessionHvId = HttpContext.Session.GetInt32("HocVienID");
                    if (sessionHvId.HasValue)
                    {
                        hocVien = await _context.HocViens.FirstOrDefaultAsync(hv => hv.HocVienID == sessionHvId.Value);
                    }
                }

                if (hocVien == null)
                {
                    hocVien = await _context.HocViens.FirstOrDefaultAsync();
                }

                ViewBag.HocVienCurrent = hocVien;

                int targetBaiTapId = baiTapId ?? 1;
                var baiTap = await _context.BaiTaps
                    .Include(b => b.LopHoc)
                    .FirstOrDefaultAsync(b => b.BaiTapID == targetBaiTapId);

                if (baiTap == null)
                {
                    baiTap = await _context.BaiTaps.FirstOrDefaultAsync();
                }

                ViewBag.BaiTapCurrent = baiTap;

                int hvId = hocVien?.HocVienID ?? 0;
                var lichSuNop = await _context.ChiTietNopBais
                    .Where(c => c.BaiTapID == targetBaiTapId && c.HocVienID == hvId && c.IsDeleted != true)
                    .OrderByDescending(c => c.LanNop)
                    .ToListAsync();

                return View(lichSuNop);
            }
            catch (Exception ex)
            {
                return Content("Lỗi nạp thông tin bài tập: " + ex.Message);
            }
        }

        // POST: /BaiTap/UploadBaiTap
        [HttpPost]
        public async Task<IActionResult> UploadBaiTap(int baiTapId, int hocVienId, IFormFile fileNop)
        {
            if (fileNop == null || fileNop.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn tệp tin bài làm để nộp!" });
            }

            const long maxFileSize = 15 * 1024 * 1024;
            if (fileNop.Length > maxFileSize)
            {
                return Json(new { success = false, message = "Dung lượng tệp vượt quá giới hạn cho phép (Tối đa 15MB)!" });
            }

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
                int maxLanNop = await _context.ChiTietNopBais
                    .Where(c => c.BaiTapID == baiTapId && c.HocVienID == hocVienId && c.IsDeleted != true)
                    .MaxAsync(c => (int?)c.LanNop) ?? 0;

                int lanNopMoi = maxLanNop + 1;

                string uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "baitap");
                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                string uniqueFileName = $"BT{baiTapId}_HV{hocVienId}_Lan{lanNopMoi}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                string filePath = Path.Combine(uploadFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await fileNop.CopyToAsync(fileStream);
                }

                string fileRelativeUrl = $"/uploads/baitap/{uniqueFileName}";

                var chiTietNop = new ChiTietNopBai
                {
                    BaiTapID = baiTapId,
                    HocVienID = hocVienId,
                    LanNop = lanNopMoi,
                    DuongDan = fileRelativeUrl,
                    ThoiGianNop = DateTime.Now,
                    IsDeleted = false,
                    CreatedAt = DateTime.Now
                };

                _context.ChiTietNopBais.Add(chiTietNop);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Đã nộp bài thành công (Lần {lanNopMoi})!",
                    lanNop = lanNopMoi,
                    fileUrl = fileRelativeUrl,
                    fileName = fileNop.FileName,
                    submitTime = chiTietNop.ThoiGianNop.ToString("dd/MM/yyyy HH:mm")
                });
            }
            catch (Exception ex)
            {
                string errMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Lỗi lưu SQL: " + errMessage });
            }
        }
    }
}