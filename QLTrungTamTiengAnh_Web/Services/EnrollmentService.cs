using System.Data;
using Microsoft.EntityFrameworkCore;
using QuanLyTrungTamNgoaiNgu.Data;
using QuanLyTrungTamNgoaiNgu.Models;

namespace QLTrungTamTiengAnh_Web.Services;

public sealed record EnrollmentRequest(int HocVienId, int LopHocId, bool CreateCommitment, string? Note);
public sealed record EnrollmentResult(int DangKyId, decimal Tuition, int ClassSize, bool CommitmentCreated);

public sealed class EnrollmentException(string message, int statusCode = StatusCodes.Status400BadRequest)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public interface IEnrollmentService
{
    Task<EnrollmentResult> EnrollAsync(EnrollmentRequest request, CancellationToken cancellationToken = default);
}

public sealed class EnrollmentService(ApplicationDbContext context) : IEnrollmentService
{
    private static readonly string[] ClosedClassStatuses = ["Đã kết thúc", "Đã hủy", "Đã huỷ", "Hủy", "Huỷ"];

    public async Task<EnrollmentResult> EnrollAsync(
        EnrollmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.HocVienId <= 0 || request.LopHocId <= 0)
            throw new EnrollmentException("Vui lòng chọn học viên và lớp học hợp lệ.");

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var student = await context.HocViens
            .FirstOrDefaultAsync(x => x.HocVienID == request.HocVienId && !x.IsDeleted, cancellationToken)
            ?? throw new EnrollmentException("Không tìm thấy học viên.", StatusCodes.Status404NotFound);

        var classEntity = await context.LopHocs
            .Include(x => x.KhoaHoc)
            .FirstOrDefaultAsync(x => x.LopHocID == request.LopHocId && !x.IsDeleted, cancellationToken)
            ?? throw new EnrollmentException("Không tìm thấy lớp học.", StatusCodes.Status404NotFound);

        if (classEntity.KhoaHoc == null)
            throw new EnrollmentException("Lớp học chưa liên kết với khóa học hợp lệ.");
        if (ClosedClassStatuses.Contains(classEntity.TrangThai))
            throw new EnrollmentException("Lớp không còn nhận đăng ký.", StatusCodes.Status409Conflict);

        var duplicate = await context.DangKys.AnyAsync(x =>
            x.HocVienID == request.HocVienId && x.LopHocID == request.LopHocId &&
            !x.IsDeleted && x.TrangThai == "Đang học", cancellationToken);
        if (duplicate)
            throw new EnrollmentException("Học viên đã đăng ký lớp này.", StatusCodes.Status409Conflict);

        var classSize = await context.DangKys.CountAsync(x =>
            x.LopHocID == request.LopHocId && !x.IsDeleted && x.TrangThai == "Đang học", cancellationToken);
        if (classSize >= classEntity.SiSoToiDa)
            throw new EnrollmentException("Lớp đã đủ sĩ số.", StatusCodes.Status409Conflict);

        if (await HasScheduleConflictAsync(request.HocVienId, request.LopHocId, cancellationToken))
            throw new EnrollmentException("Lịch học bị trùng với lớp học viên đã đăng ký.", StatusCodes.Status409Conflict);

        var registration = new DangKy
        {
            HocVienID = student.HocVienID,
            LopHocID = classEntity.LopHocID,
            NgayDangKy = DateTime.Now,
            HocPhiApDung = classEntity.KhoaHoc.HocPhi,
            TrangThai = "Đang học",
            GhiChu = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        };
        context.DangKys.Add(registration);
        student.TrangThai = "Đang học";
        student.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync(cancellationToken);

        if (request.CreateCommitment)
        {
            context.CamKets.Add(new CamKet
            {
                HocVienID = student.HocVienID,
                KhoaHocID = classEntity.KhoaHocID,
                DangKyID = registration.DangKyID,
                NgayLap = DateTime.Now,
                TrangThai = "Chờ duyệt",
                DiemDauVao = student.DiemDauVao
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new EnrollmentResult(
            registration.DangKyID,
            registration.HocPhiApDung,
            classSize + 1,
            request.CreateCommitment);
    }

    private async Task<bool> HasScheduleConflictAsync(
        int studentId,
        int newClassId,
        CancellationToken cancellationToken)
    {
        var enrolledClassIds = await context.DangKys.AsNoTracking()
            .Where(x => x.HocVienID == studentId && !x.IsDeleted && x.TrangThai == "Đang học")
            .Select(x => x.LopHocID)
            .ToListAsync(cancellationToken);
        if (enrolledClassIds.Count == 0)
            return false;

        var newSchedules = await context.LichHocs.AsNoTracking()
            .Where(x => x.LopHocID == newClassId && !x.IsDeleted && !ClosedClassStatuses.Contains(x.TrangThai))
            .Select(x => new { x.NgayHoc, x.GioBatDau, x.GioKetThuc })
            .ToListAsync(cancellationToken);
        if (newSchedules.Count == 0)
            return false;

        var existingSchedules = await context.LichHocs.AsNoTracking()
            .Where(x => enrolledClassIds.Contains(x.LopHocID) && !x.IsDeleted && !ClosedClassStatuses.Contains(x.TrangThai))
            .Select(x => new { x.NgayHoc, x.GioBatDau, x.GioKetThuc })
            .ToListAsync(cancellationToken);

        return newSchedules.Any(a => existingSchedules.Any(b =>
            a.NgayHoc.Date == b.NgayHoc.Date &&
            a.GioBatDau < b.GioKetThuc && b.GioBatDau < a.GioKetThuc));
    }
}
