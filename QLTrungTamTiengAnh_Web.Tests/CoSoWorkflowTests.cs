using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLTrungTamTiengAnh_Web.Controllers;
using QuanLyTrungTamNgoaiNgu.Models;
using Xunit;

namespace QLTrungTamTiengAnh_Web.Tests;

public sealed class CoSoWorkflowTests
{
    [Fact]
    public async Task Saving_branch_persists_after_context_is_recreated()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        await using (var context = database.CreateContext())
        {
            var response = await database.CreateController(context).LuuCoSo(new CoSoDto
            {
                MaCoSo = " CS-HBT ", TenCoSo = " Hai Bà Trưng ",
                DiaChi = " 789 Bạch Mai ", SoDienThoai = "02439998888"
            });
            AssertSuccess(response);
        }

        await using var reopened = database.CreateContext();
        var branch = await reopened.CoSos.SingleAsync(x => x.MaCoSo == "CS-HBT");
        Assert.Equal("Hai Bà Trưng", branch.TenCoSo);
        Assert.Equal("789 Bạch Mai", branch.DiaChi);
        Assert.True(branch.TrangThai);
    }

    [Fact]
    public async Task Editing_branch_keeps_existing_rooms_and_staff()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        var roomId = await database.AddRoomAsync();
        await using (var context = database.CreateContext())
        {
            AssertSuccess(await database.CreateController(context).LuuCoSo(new CoSoDto
            {
                CoSoId = CoSoDatabase.SourceId, MaCoSo = "CS-CG", TenCoSo = "Cầu Giấy mới",
                DiaChi = "Địa chỉ mới", SoDienThoai = "02438889999"
            }));
        }

        await using var reopened = database.CreateContext();
        Assert.Equal("Cầu Giấy mới", (await reopened.CoSos.FindAsync(CoSoDatabase.SourceId))!.TenCoSo);
        Assert.Equal(CoSoDatabase.SourceId, (await reopened.PhongHocs.FindAsync(roomId))!.CoSoID);
        Assert.Equal(CoSoDatabase.SourceId, (await reopened.NhanSus.FindAsync(CoSoDatabase.StaffId))!.CoSoID);
    }

    [Fact]
    public async Task Duplicate_branch_code_is_rejected_without_adding_a_branch()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        await using var context = database.CreateContext();
        AssertFailure(await database.CreateController(context).LuuCoSo(new CoSoDto
        {
            MaCoSo = " cs-cg ", TenCoSo = "Cơ sở trùng mã", DiaChi = "Địa chỉ"
        }));
        Assert.Equal(3, await context.CoSos.CountAsync());
    }

    [Theory]
    [InlineData("", "Tên cơ sở", "Địa chỉ")]
    [InlineData("CS-MOI", " ", "Địa chỉ")]
    [InlineData("CS-MOI", "Tên cơ sở", " ")]
    public async Task Missing_required_branch_fields_are_rejected(string code, string name, string address)
    {
        await using var database = await CoSoDatabase.CreateAsync();
        await using var context = database.CreateContext();
        AssertFailure(await database.CreateController(context).LuuCoSo(new CoSoDto
        {
            MaCoSo = code, TenCoSo = name, DiaChi = address
        }));
        Assert.Equal(3, await context.CoSos.CountAsync());
    }

    [Fact]
    public async Task Branch_with_working_staff_cannot_be_suspended()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        await using var context = database.CreateContext();
        AssertFailure(await database.CreateController(context).DoiTrangThaiCoSo(new TrangThaiCoSoDto
        {
            CoSoId = CoSoDatabase.SourceId, TrangThai = false
        }));
        context.ChangeTracker.Clear();
        Assert.True((await context.CoSos.FindAsync(CoSoDatabase.SourceId))!.TrangThai);
    }

    [Fact]
    public async Task Empty_branch_can_be_suspended_and_reactivated()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var controller = database.CreateController(context);
        AssertSuccess(await controller.DoiTrangThaiCoSo(new TrangThaiCoSoDto
        {
            CoSoId = CoSoDatabase.DestinationId, TrangThai = false
        }));
        context.ChangeTracker.Clear();
        Assert.False((await context.CoSos.FindAsync(CoSoDatabase.DestinationId))!.TrangThai);
        AssertSuccess(await controller.DoiTrangThaiCoSo(new TrangThaiCoSoDto
        {
            CoSoId = CoSoDatabase.DestinationId, TrangThai = true
        }));
        context.ChangeTracker.Clear();
        Assert.True((await context.CoSos.FindAsync(CoSoDatabase.DestinationId))!.TrangThai);
    }

    [Fact]
    public async Task Room_names_are_unique_within_a_branch_and_reusable_in_another_branch()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        await database.AddRoomAsync();
        await using var context = database.CreateContext();
        var controller = database.CreateController(context);
        AssertFailure(await controller.LuuPhongHoc(Room(CoSoDatabase.SourceId, " P101 ")));
        AssertSuccess(await controller.LuuPhongHoc(Room(CoSoDatabase.DestinationId, "P101")));

        await using var reopened = database.CreateContext();
        Assert.Equal(2, await reopened.PhongHocs.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Nonpositive_room_capacity_is_rejected(int capacity)
    {
        await using var database = await CoSoDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var request = Room(CoSoDatabase.SourceId, "P102");
        request.SucChua = capacity;
        AssertFailure(await database.CreateController(context).LuuPhongHoc(request));
        Assert.Empty(await context.PhongHocs.ToListAsync());
    }

    [Fact]
    public async Task Room_cannot_be_created_at_an_inactive_branch()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        await using var context = database.CreateContext();
        AssertFailure(await database.CreateController(context)
            .LuuPhongHoc(Room(CoSoDatabase.InactiveId, "P101")));
        Assert.Empty(await context.PhongHocs.ToListAsync());
    }

    [Fact]
    public async Task Deleting_room_with_only_past_lessons_soft_deletes_room_and_preserves_lessons()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        var roomId = await database.AddRoomAsync();
        await database.AddFutureScheduleAsync(roomId, daysFromToday: -2);
        await using (var context = database.CreateContext())
        {
            AssertSuccess(await database.CreateController(context).XoaPhongHoc(new XoaPhongHocDto
            {
                PhongHocId = roomId
            }));
        }

        await using var reopened = database.CreateContext();
        Assert.Empty(await reopened.PhongHocs.ToListAsync());
        var deleted = await reopened.PhongHocs.IgnoreQueryFilters().SingleAsync();
        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedAt);
        Assert.Single(await reopened.LichHocs.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Scheduled_room_cannot_be_deleted_moved_or_reduced_below_class_capacity()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        var roomId = await database.AddRoomAsync();
        await database.AddFutureScheduleAsync(roomId, classCapacity: 25);
        await using var context = database.CreateContext();
        var controller = database.CreateController(context);
        AssertFailure(await controller.XoaPhongHoc(new XoaPhongHocDto { PhongHocId = roomId }));

        var move = Room(CoSoDatabase.DestinationId, "P101");
        move.PhongHocId = roomId;
        AssertFailure(await controller.LuuPhongHoc(move));

        var reduction = Room(CoSoDatabase.SourceId, "P101");
        reduction.PhongHocId = roomId;
        reduction.SucChua = 20;
        AssertFailure(await controller.LuuPhongHoc(reduction));

        await using var reopened = database.CreateContext();
        var persisted = await reopened.PhongHocs.SingleAsync();
        Assert.Equal(CoSoDatabase.SourceId, persisted.CoSoID);
        Assert.Equal(30, persisted.SucChua);
        Assert.Single(await reopened.LichHocs.ToListAsync());
    }

    [Theory]
    [InlineData("Bảo trì")]
    [InlineData("Ngừng sử dụng")]
    public async Task Scheduled_room_cannot_be_taken_out_of_service(string status)
    {
        await using var database = await CoSoDatabase.CreateAsync();
        var roomId = await database.AddRoomAsync();
        await database.AddFutureScheduleAsync(roomId);
        await using var context = database.CreateContext();
        var request = Room(CoSoDatabase.SourceId, "P101");
        request.PhongHocId = roomId;
        request.TrangThai = status;
        AssertFailure(await database.CreateController(context).LuuPhongHoc(request));
        context.ChangeTracker.Clear();
        Assert.Equal("Đang sử dụng", (await context.PhongHocs.FindAsync(roomId))!.TrangThai);
    }

    [Fact]
    public async Task Future_schedule_still_protects_room_after_class_is_soft_deleted()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        var roomId = await database.AddRoomAsync();
        await database.AddFutureScheduleAsync(roomId);
        await using (var setup = database.CreateContext())
        {
            (await setup.LopHocs.SingleAsync()).IsDeleted = true;
            await setup.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        AssertFailure(await database.CreateController(context)
            .XoaPhongHoc(new XoaPhongHocDto { PhongHocId = roomId }));
        context.ChangeTracker.Clear();
        Assert.False((await context.PhongHocs.FindAsync(roomId))!.IsDeleted);
    }

    [Fact]
    public async Task Ongoing_lesson_today_protects_room()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        var roomId = await database.AddRoomAsync();
        await database.AddFutureScheduleAsync(roomId, daysFromToday: 0);
        await using (var setup = database.CreateContext())
        {
            var lesson = await setup.LichHocs.SingleAsync();
            lesson.GioBatDau = TimeSpan.Zero;
            lesson.GioKetThuc = TimeSpan.FromDays(1) - TimeSpan.FromTicks(1);
            await setup.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        AssertFailure(await database.CreateController(context)
            .XoaPhongHoc(new XoaPhongHocDto { PhongHocId = roomId }));
        context.ChangeTracker.Clear();
        Assert.False((await context.PhongHocs.FindAsync(roomId))!.IsDeleted);
    }

    [Theory]
    [InlineData("Đã hủy")]
    [InlineData("Hoàn thành")]
    public async Task Cancelled_or_completed_lesson_does_not_block_room_deletion(string status)
    {
        await using var database = await CoSoDatabase.CreateAsync();
        var roomId = await database.AddRoomAsync();
        await database.AddFutureScheduleAsync(roomId);
        await using (var setup = database.CreateContext())
        {
            (await setup.LichHocs.SingleAsync()).TrangThai = status;
            await setup.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        AssertSuccess(await database.CreateController(context)
            .XoaPhongHoc(new XoaPhongHocDto { PhongHocId = roomId }));
        Assert.Single(await context.LichHocs.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Transfer_persists_staff_and_approved_audit_record_together()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        await using (var context = database.CreateContext())
        {
            AssertSuccess(await database.CreateController(context).DieuChuyenNhanSu(Transfer()));
        }

        await using var reopened = database.CreateContext();
        Assert.Equal(CoSoDatabase.DestinationId,
            (await reopened.NhanSus.FindAsync(CoSoDatabase.StaffId))!.CoSoID);
        var history = await reopened.LichSuDieuChuyens.SingleAsync();
        Assert.Equal(CoSoDatabase.StaffId, history.NhanSuID);
        Assert.Equal(CoSoDatabase.SourceId, history.CoSoCuID);
        Assert.Equal(CoSoDatabase.DestinationId, history.CoSoMoiID);
        Assert.Equal(CoSoDatabase.ApproverId, history.NguoiDuyetID);
        Assert.Equal("Đã duyệt", history.TrangThai);
        Assert.Equal("Tăng cường giáo viên", history.LyDo);
        Assert.NotNull(history.NgayHieuLuc);
    }

    [Theory]
    [InlineData(CoSoDatabase.SourceId, CoSoDatabase.SourceId, "Cùng cơ sở")]
    [InlineData(CoSoDatabase.DestinationId, CoSoDatabase.SourceId, "Nguồn đã thay đổi")]
    [InlineData(CoSoDatabase.SourceId, CoSoDatabase.InactiveId, "Đích ngừng hoạt động")]
    [InlineData(CoSoDatabase.SourceId, CoSoDatabase.DestinationId, " ")]
    public async Task Invalid_transfer_keeps_staff_and_history_unchanged(int source, int destination, string reason)
    {
        await using var database = await CoSoDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var request = Transfer();
        request.CoSoHienTaiId = source;
        request.CoSoMoiId = destination;
        request.LyDo = reason;
        AssertFailure(await database.CreateController(context).DieuChuyenNhanSu(request));

        await using var reopened = database.CreateContext();
        Assert.Equal(CoSoDatabase.SourceId,
            (await reopened.NhanSus.FindAsync(CoSoDatabase.StaffId))!.CoSoID);
        Assert.Empty(await reopened.LichSuDieuChuyens.ToListAsync());
    }

    [Fact]
    public async Task Repeating_stale_transfer_does_not_create_another_history_record()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        await using var first = database.CreateContext();
        AssertSuccess(await database.CreateController(first).DieuChuyenNhanSu(Transfer()));

        await using var second = database.CreateContext();
        AssertFailure(await database.CreateController(second).DieuChuyenNhanSu(Transfer()));
        Assert.Single(await second.LichSuDieuChuyens.ToListAsync());
    }

    [Fact]
    public async Task Transfer_rejects_an_approver_not_present_in_the_database()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        await using var context = database.CreateContext();
        AssertFailure(await database.CreateController(context, approverId: 999).DieuChuyenNhanSu(Transfer()));
        context.ChangeTracker.Clear();
        Assert.Equal(CoSoDatabase.SourceId,
            (await context.NhanSus.FindAsync(CoSoDatabase.StaffId))!.CoSoID);
        Assert.Empty(await context.LichSuDieuChuyens.ToListAsync());
    }

    [Fact]
    public async Task Transfer_rolls_back_staff_change_if_audit_insert_fails()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        await using (var context = database.CreateContext())
        {
            // Simulate a database failure after the staff update has been prepared.
            // A new context below verifies the committed database, not tracked entities.
            await context.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER fail_transfer_audit BEFORE INSERT ON LichSuDieuChuyen " +
                "BEGIN SELECT RAISE(ABORT, 'simulated audit storage failure'); END;");
            AssertFailure(await database.CreateController(context).DieuChuyenNhanSu(Transfer()));
        }

        await using var reopened = database.CreateContext();
        Assert.Equal(CoSoDatabase.SourceId,
            (await reopened.NhanSus.FindAsync(CoSoDatabase.StaffId))!.CoSoID);
        Assert.Empty(await reopened.LichSuDieuChuyens.ToListAsync());
    }

    [Fact]
    public async Task Data_endpoint_reads_persisted_counts_and_transfer_history()
    {
        await using var database = await CoSoDatabase.CreateAsync();
        await database.AddRoomAsync();
        await using (var context = database.CreateContext())
        {
            AssertSuccess(await database.CreateController(context).DieuChuyenNhanSu(Transfer()));
        }

        await using var reopened = database.CreateContext();
        var result = Assert.IsType<JsonResult>(await database.CreateController(reopened).DuLieu());
        var data = JsonSerializer.SerializeToElement(result.Value);
        Assert.True(data.GetProperty("success").GetBoolean(), data.ToString());
        var source = data.GetProperty("coSos").EnumerateArray()
            .Single(x => x.GetProperty("coSoId").GetInt32() == CoSoDatabase.SourceId);
        Assert.Equal(1, source.GetProperty("soPhong").GetInt32());
        Assert.Equal(0, source.GetProperty("soNhanSu").GetInt32());
        Assert.Equal(CoSoDatabase.DestinationId,
            Assert.Single(data.GetProperty("nhanSus").EnumerateArray()).GetProperty("coSoId").GetInt32());
        Assert.Single(data.GetProperty("phongHocs").EnumerateArray());
        var history = Assert.Single(data.GetProperty("dieuChuyens").EnumerateArray());
        Assert.Equal("Nguyễn Văn An", history.GetProperty("hoTen").GetString());
        Assert.Equal("test-admin", history.GetProperty("nguoiDuyet").GetString());
    }

    private static PhongHocDto Room(int branch, string name) => new()
    {
        CoSoId = branch, TenPhong = name, SucChua = 30, TrangThai = "Đang sử dụng"
    };

    private static DieuChuyenDto Transfer() => new()
    {
        NhanSuId = CoSoDatabase.StaffId, CoSoHienTaiId = CoSoDatabase.SourceId,
        CoSoMoiId = CoSoDatabase.DestinationId, LyDo = "Tăng cường giáo viên"
    };

    private static void AssertSuccess(IActionResult result)
    {
        var response = Assert.IsType<JsonResult>(result);
        var data = JsonSerializer.SerializeToElement(response.Value);
        Assert.True(data.GetProperty("success").GetBoolean(), data.ToString());
        Assert.True(response.StatusCode is null or >= 200 and < 300);
    }

    private static void AssertFailure(IActionResult result)
    {
        var response = Assert.IsType<JsonResult>(result);
        var data = JsonSerializer.SerializeToElement(response.Value);
        Assert.False(data.GetProperty("success").GetBoolean(), data.ToString());
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("message").GetString()));
        Assert.InRange(response.StatusCode ?? 0, 400, 499);
    }
}
