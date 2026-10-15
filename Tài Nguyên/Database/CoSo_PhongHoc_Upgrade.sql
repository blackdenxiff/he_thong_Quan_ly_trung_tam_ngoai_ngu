/* Chạy trong database QLTrungTamTiengAnh hiện có, trước khi mở /CoSo.
   Không tạo lại database hoặc ghi đè dữ liệu. Có thể chạy lặp lại.
   Hãy sao lưu database trước khi nâng cấp trên máy đang sử dụng. */
USE [QLTrungTamTiengAnh];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    -- Các bảng được module và chức năng đăng nhập truy vấn đều kế thừa BaseEntity.
    DECLARE @tables TABLE (Name sysname);
    INSERT INTO @tables VALUES (N'CoSo'), (N'PhongHoc'), (N'NhanSu'),
        (N'LichSuDieuChuyen'), (N'LopHoc'), (N'LichHoc'),
        (N'NguoiDung'), (N'VaiTro'), (N'HocVien');
    DECLARE @name sysname, @sql nvarchar(max);
    DECLARE table_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT Name FROM @tables;
    OPEN table_cursor;
    FETCH NEXT FROM table_cursor INTO @name;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF OBJECT_ID(N'dbo.' + @name, N'U') IS NULL
            THROW 51000, N'Thiếu bảng dữ liệu. Hãy kiểm tra database được chọn.', 1;

        SET @sql = N'';
        IF COL_LENGTH(N'dbo.' + @name, N'IsDeleted') IS NULL
            SET @sql += N'ALTER TABLE dbo.' + QUOTENAME(@name)
                + N' ADD IsDeleted bit NOT NULL DEFAULT (0) WITH VALUES;';
        IF COL_LENGTH(N'dbo.' + @name, N'DeletedAt') IS NULL
            SET @sql += N'ALTER TABLE dbo.' + QUOTENAME(@name) + N' ADD DeletedAt datetime2 NULL;';
        IF COL_LENGTH(N'dbo.' + @name, N'CreatedAt') IS NULL
            SET @sql += N'ALTER TABLE dbo.' + QUOTENAME(@name)
                + N' ADD CreatedAt datetime2 NOT NULL DEFAULT (SYSDATETIME()) WITH VALUES;';
        IF COL_LENGTH(N'dbo.' + @name, N'UpdatedAt') IS NULL
            SET @sql += N'ALTER TABLE dbo.' + QUOTENAME(@name) + N' ADD UpdatedAt datetime2 NULL;';
        EXEC sys.sp_executesql @sql;
        FETCH NEXT FROM table_cursor INTO @name;
    END;
    CLOSE table_cursor;
    DEALLOCATE table_cursor;

    IF COL_LENGTH(N'dbo.CoSo', N'MaCoSo') IS NULL
        ALTER TABLE dbo.CoSo ADD MaCoSo nvarchar(30) NULL;

    EXEC sys.sp_executesql N'
        UPDATE dbo.CoSo SET MaCoSo = N''CS-'' + CONVERT(nvarchar(10), CoSoID)
        WHERE MaCoSo IS NULL OR LTRIM(RTRIM(MaCoSo)) = N'''';
        IF EXISTS (SELECT MaCoSo FROM dbo.CoSo WHERE IsDeleted = 0
                   GROUP BY MaCoSo HAVING COUNT(*) > 1)
            THROW 51001, N''Có mã cơ sở trùng nhau. Hãy sửa dữ liệu trùng trước khi nâng cấp.'', 1;
        ALTER TABLE dbo.CoSo ALTER COLUMN MaCoSo nvarchar(30) NOT NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.CoSo'')
                       AND name = N''IX_CoSo_MaCoSo'')
            CREATE UNIQUE INDEX IX_CoSo_MaCoSo ON dbo.CoSo(MaCoSo) WHERE IsDeleted = 0;
    ';

    -- Xóa mềm phòng vẫn giữ lịch sử; tên phòng có thể dùng lại sau khi xóa.
    IF EXISTS (SELECT 1 FROM sys.key_constraints
               WHERE parent_object_id = OBJECT_ID(N'dbo.PhongHoc')
               AND name = N'UQ_PhongHoc_CoSo_TenPhong')
        ALTER TABLE dbo.PhongHoc DROP CONSTRAINT UQ_PhongHoc_CoSo_TenPhong;
    EXEC sys.sp_executesql N'
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.PhongHoc'')
                       AND name = N''IX_PhongHoc_CoSoID_TenPhong'')
            CREATE UNIQUE INDEX IX_PhongHoc_CoSoID_TenPhong
            ON dbo.PhongHoc(CoSoID, TenPhong) WHERE IsDeleted = 0;
    ';

    IF COL_LENGTH(N'dbo.NguoiDung', N'DoiMatKhauLanDau') IS NULL
        ALTER TABLE dbo.NguoiDung ADD DoiMatKhauLanDau bit NOT NULL DEFAULT (0) WITH VALUES;

    -- Script gốc dùng DATE; datetime2 giữ lại cả giờ phê duyệt.
    -- SQL Server yêu cầu gỡ DEFAULT trước khi đổi kiểu; giữ nguyên biểu thức cũ.
    DECLARE @defaultName sysname, @defaultDefinition nvarchar(max);
    SELECT @defaultName = dc.name, @defaultDefinition = dc.definition
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.LichSuDieuChuyen') AND c.name = N'NgayDeXuat';
    IF @defaultName IS NOT NULL
    BEGIN
        SET @sql = N'ALTER TABLE dbo.LichSuDieuChuyen DROP CONSTRAINT ' + QUOTENAME(@defaultName);
        EXEC sys.sp_executesql @sql;
    END;
    ALTER TABLE dbo.LichSuDieuChuyen ALTER COLUMN NgayDeXuat datetime2 NOT NULL;
    IF @defaultName IS NOT NULL
    BEGIN
        SET @sql = N'ALTER TABLE dbo.LichSuDieuChuyen ADD CONSTRAINT ' + QUOTENAME(@defaultName)
            + N' DEFAULT ' + @defaultDefinition + N' FOR NgayDeXuat';
        EXEC sys.sp_executesql @sql;
    END;
    ALTER TABLE dbo.LichSuDieuChuyen ALTER COLUMN NgayHieuLuc datetime2 NULL;

    COMMIT TRANSACTION;
    PRINT N'Đã cập nhật schema cho Cơ sở, Phòng học và Điều chuyển.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
