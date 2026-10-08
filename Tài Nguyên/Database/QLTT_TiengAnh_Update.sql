/* ==========================================================================
   CSDL QUAN LY TRUNG TAM TIENG ANH - 20 BANG CHUAN HOA + DU LIEU MAU DAY DU
   ========================================================================== */
CREATE DATABASE QLTrungTamTiengAnh;
GO
USE QLTrungTamTiengAnh;
GO

-- 1. BẢNG VAI TRO
CREATE TABLE VaiTro (
    VaiTroID INT IDENTITY(1,1) PRIMARY KEY,
    TenVaiTro NVARCHAR(30) NOT NULL UNIQUE,
    MoTa NVARCHAR(200) NULL
);
GO

-- 2. BẢNG CO SO
CREATE TABLE CoSo (
    CoSoID INT IDENTITY(1,1) PRIMARY KEY,
    TenCoSo NVARCHAR(100) NOT NULL,
    DiaChi NVARCHAR(250) NOT NULL,
    SoDienThoai VARCHAR(20) NULL,
    TrangThai BIT NOT NULL DEFAULT 1
);
GO

-- 3. BẢNG PHONG HOC (Tạo ngay sau CoSo để đúng trật tự danh mục cơ sở vật chất)
CREATE TABLE PhongHoc (
    PhongHocID INT IDENTITY(1,1) PRIMARY KEY,
    CoSoID INT NOT NULL,
    TenPhong NVARCHAR(50) NOT NULL,
    SucChua INT NOT NULL,
    TrangThai NVARCHAR(20) NOT NULL DEFAULT N'Đang sử dụng',
    GhiChu NVARCHAR(300) NULL,
    CONSTRAINT FK_PhongHoc_CoSo FOREIGN KEY (CoSoID) REFERENCES CoSo(CoSoID),
    CONSTRAINT CK_PhongHoc_SucChua CHECK (SucChua > 0),
    CONSTRAINT UQ_PhongHoc_CoSo_TenPhong UNIQUE(CoSoID, TenPhong)
);
GO

-- 4. BẢNG NGUOI DUNG
CREATE TABLE NguoiDung (
    NguoiDungID INT IDENTITY(1,1) PRIMARY KEY,
    TenDangNhap VARCHAR(50) NOT NULL UNIQUE,
    MatKhauHash NVARCHAR(255) NOT NULL,
    VaiTroID INT NOT NULL,
    TrangThai BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_NguoiDung_VaiTro FOREIGN KEY (VaiTroID) REFERENCES VaiTro(VaiTroID)
);
GO

-- 5. BẢNG NHAN SU (Gộp Giáo viên + Nhân viên)
CREATE TABLE NhanSu (
    NhanSuID INT IDENTITY(1,1) PRIMARY KEY,
    NguoiDungID INT NULL UNIQUE,
    CoSoID INT NOT NULL,
    HoTen NVARCHAR(100) NOT NULL,
    NgaySinh DATE NULL,
    GioiTinh NVARCHAR(10) NULL,
    SoDienThoai VARCHAR(20) NULL,
    Email VARCHAR(100) NULL,
    LoaiNhanSu NVARCHAR(20) NOT NULL,
    TrinhDo NVARCHAR(100) NULL,
    LuongCoBan DECIMAL(12,2) NULL,
    LuongTheoGio DECIMAL(12,2) NULL,
    NgayVaoLam DATE NOT NULL,
    NhiemVu NVARCHAR(100) NULL,
    TrangThai NVARCHAR(20) NOT NULL DEFAULT N'Đang làm',
    CONSTRAINT FK_NhanSu_NguoiDung FOREIGN KEY (NguoiDungID) REFERENCES NguoiDung(NguoiDungID),
    CONSTRAINT FK_NhanSu_CoSo FOREIGN KEY (CoSoID) REFERENCES CoSo(CoSoID),
    CONSTRAINT CK_NhanSu_Loai CHECK (LoaiNhanSu IN (N'Nhân viên', N'Giáo viên', N'Cả hai')),
    CONSTRAINT CK_NhanSu_Luong CHECK ((LuongCoBan IS NULL OR LuongCoBan >= 0) AND (LuongTheoGio IS NULL OR LuongTheoGio >= 0))
);
GO

-- 6. BẢNG HOC VIEN
CREATE TABLE HocVien (
    HocVienID INT IDENTITY(1,1) PRIMARY KEY,
    NguoiDungID INT NULL UNIQUE,
    HoTen NVARCHAR(100) NOT NULL,
    NgaySinh DATE NULL,
    GioiTinh NVARCHAR(10) NULL,
    SoDienThoai VARCHAR(20) NULL,
    Email VARCHAR(100) NULL,
    TrinhDoDauVao NVARCHAR(50) NULL,
    DiemDauVao DECIMAL(5,2) NULL,
    NhanXetDauVao NVARCHAR(500) NULL,
    TrangThai NVARCHAR(20) NOT NULL DEFAULT N'Đang học',
    CONSTRAINT FK_HocVien_NguoiDung FOREIGN KEY (NguoiDungID) REFERENCES NguoiDung(NguoiDungID),
    CONSTRAINT CK_HocVien_DiemDauVao CHECK (DiemDauVao IS NULL OR DiemDauVao BETWEEN 0 AND 100)
);
GO

-- 7. BẢNG KHOA HOC
CREATE TABLE KhoaHoc (
    KhoaHocID INT IDENTITY(1,1) PRIMARY KEY,
    TenKhoaHoc NVARCHAR(100) NOT NULL,
    TrinhDo NVARCHAR(50) NULL,
    MoTa NVARCHAR(500) NULL,
    SoBuoi INT NOT NULL,
    HocPhi DECIMAL(12,2) NOT NULL,
    TrangThai NVARCHAR(20) NOT NULL DEFAULT N'Đang mở',
    CONSTRAINT CK_KhoaHoc_SoBuoi CHECK (SoBuoi > 0),
    CONSTRAINT CK_KhoaHoc_HocPhi CHECK (HocPhi >= 0)
);
GO

-- 8. BẢNG LOP HOC
CREATE TABLE LopHoc (
    LopHocID INT IDENTITY(1,1) PRIMARY KEY,
    TenLop NVARCHAR(100) NOT NULL,
    KhoaHocID INT NOT NULL,
    CoSoID INT NOT NULL,
    GiaoVienPhuTrachID INT NOT NULL,
    SiSoToiDa INT NOT NULL,
    NgayBatDau DATE NOT NULL,
    NgayKetThuc DATE NOT NULL,
    TrangThai NVARCHAR(20) NOT NULL DEFAULT N'Dự kiến',
    CONSTRAINT FK_LopHoc_KhoaHoc FOREIGN KEY (KhoaHocID) REFERENCES KhoaHoc(KhoaHocID),
    CONSTRAINT FK_LopHoc_CoSo FOREIGN KEY (CoSoID) REFERENCES CoSo(CoSoID),
    CONSTRAINT FK_LopHoc_GiaoVien FOREIGN KEY (GiaoVienPhuTrachID) REFERENCES NhanSu(NhanSuID),
    CONSTRAINT CK_LopHoc_SiSo CHECK (SiSoToiDa > 0),
    CONSTRAINT CK_LopHoc_Ngay CHECK (NgayKetThuc >= NgayBatDau)
);
GO

-- 9. BẢNG DANG KY (ĐÃ THÊM UNIQUE CHỐNG ĐĂNG KÝ TRÙNG)
CREATE TABLE DangKy (
    DangKyID INT IDENTITY(1,1) PRIMARY KEY,
    HocVienID INT NOT NULL,
    LopHocID INT NOT NULL,
    NgayDangKy DATE NOT NULL DEFAULT GETDATE(),
    HocPhiApDung DECIMAL(12,2) NOT NULL,
    TrangThai NVARCHAR(20) NOT NULL DEFAULT N'Đang học',
    DiemCuoiKhoa DECIMAL(5,2) NULL,
    KetQua NVARCHAR(20) NULL,
    DuocHocLaiMienPhi BIT NOT NULL DEFAULT 0,
    GhiChu NVARCHAR(500) NULL,
    CONSTRAINT FK_DangKy_HocVien FOREIGN KEY (HocVienID) REFERENCES HocVien(HocVienID),
    CONSTRAINT FK_DangKy_LopHoc FOREIGN KEY (LopHocID) REFERENCES LopHoc(LopHocID),
    CONSTRAINT UQ_DangKy_HocVien_Lop UNIQUE (HocVienID, LopHocID),
    CONSTRAINT CK_DangKy_HocPhi CHECK (HocPhiApDung >= 0),
    CONSTRAINT CK_DangKy_Diem CHECK (DiemCuoiKhoa IS NULL OR DiemCuoiKhoa BETWEEN 0 AND 100)
);
GO

-- 10. BẢNG LICH HOC
CREATE TABLE LichHoc (
    LichHocID INT IDENTITY(1,1) PRIMARY KEY,
    LopHocID INT NOT NULL,
    PhongHocID INT NOT NULL,
    NgayHoc DATE NOT NULL,
    GioBatDau TIME NOT NULL,
    GioKetThuc TIME NOT NULL,
    TrangThai NVARCHAR(20) NOT NULL DEFAULT N'Dự kiến',
    GhiChu NVARCHAR(300) NULL,
    CONSTRAINT FK_LichHoc_LopHoc FOREIGN KEY (LopHocID) REFERENCES LopHoc(LopHocID),
    CONSTRAINT FK_LichHoc_PhongHoc FOREIGN KEY (PhongHocID) REFERENCES PhongHoc(PhongHocID),
    CONSTRAINT CK_LichHoc_Gio CHECK (GioKetThuc > GioBatDau)
);
GO

-- 11. BẢNG PHAN CONG DAY
CREATE TABLE PhanCongDay (
    PhanCongID INT IDENTITY(1,1) PRIMARY KEY,
    LichHocID INT NOT NULL,
    NhanSuID INT NOT NULL,
    VaiTroDay NVARCHAR(20) NOT NULL,
    TrangThai NVARCHAR(20) NOT NULL DEFAULT N'Chờ duyệt',
    NguoiDuyetID INT NULL,
    NgayPhanCong DATETIME NOT NULL DEFAULT GETDATE(),
    LyDo NVARCHAR(300) NULL,
    CONSTRAINT FK_PhanCongDay_LichHoc FOREIGN KEY (LichHocID) REFERENCES LichHoc(LichHocID),
    CONSTRAINT FK_PhanCongDay_NhanSu FOREIGN KEY (NhanSuID) REFERENCES NhanSu(NhanSuID),
    CONSTRAINT FK_PhanCongDay_NguoiDuyet FOREIGN KEY (NguoiDuyetID) REFERENCES NguoiDung(NguoiDungID),
    CONSTRAINT CK_PhanCongDay_VaiTro CHECK (VaiTroDay IN (N'Chính', N'Dạy thay'))
);
GO

-- 12. BẢNG DIEM DANH
CREATE TABLE DiemDanh (
    DiemDanhID INT IDENTITY(1,1) PRIMARY KEY,
    LichHocID INT NOT NULL,
    HocVienID INT NOT NULL,
    TrangThai NVARCHAR(20) NOT NULL,
    GhiChu NVARCHAR(300) NULL,
    CONSTRAINT FK_DiemDanh_LichHoc FOREIGN KEY (LichHocID) REFERENCES LichHoc(LichHocID),
    CONSTRAINT FK_DiemDanh_HocVien FOREIGN KEY (HocVienID) REFERENCES HocVien(HocVienID),
    CONSTRAINT UQ_DiemDanh UNIQUE(LichHocID, HocVienID),
    CONSTRAINT CK_DiemDanh_TrangThai CHECK (TrangThai IN (N'Có mặt', N'Vắng', N'Đi muộn', N'Nghỉ phép'))
);
GO

-- 13. BẢNG BAI TAP
CREATE TABLE BaiTap (
    BaiTapID INT IDENTITY(1,1) PRIMARY KEY,
    LopHocID INT NOT NULL,
    TieuDe NVARCHAR(150) NOT NULL,
    MoTa NVARCHAR(1000) NULL,
    NgayGiao DATETIME NOT NULL DEFAULT GETDATE(),
    HanNop DATETIME NULL,
    TrangThai NVARCHAR(20) NOT NULL DEFAULT N'Đang mở',
    CONSTRAINT FK_BaiTap_LopHoc FOREIGN KEY (LopHocID) REFERENCES LopHoc(LopHocID),
    CONSTRAINT CK_BaiTap_HanNop CHECK (HanNop IS NULL OR HanNop >= NgayGiao)
);
GO

-- 14. BẢNG CHI TIET NOP BAI
CREATE TABLE ChiTietNopBai (
    ChiTietNopBaiID INT IDENTITY(1,1) PRIMARY KEY,
    BaiTapID INT NOT NULL,
    HocVienID INT NOT NULL,
    LanNop INT NOT NULL DEFAULT 1,
    DuongDan NVARCHAR(500) NULL,
    ThoiGianNop DATETIME NOT NULL DEFAULT GETDATE(),
    Diem DECIMAL(5,2) NULL,
    NhanXet NVARCHAR(500) NULL,
    NguoiChamID INT NULL,
    NgayCham DATETIME NULL,
    CONSTRAINT FK_ChiTietNopBai_BaiTap FOREIGN KEY (BaiTapID) REFERENCES BaiTap(BaiTapID),
    CONSTRAINT FK_ChiTietNopBai_HocVien FOREIGN KEY (HocVienID) REFERENCES HocVien(HocVienID),
    CONSTRAINT FK_ChiTietNopBai_NguoiCham FOREIGN KEY (NguoiChamID) REFERENCES NhanSu(NhanSuID),
    CONSTRAINT UQ_ChiTietNopBai UNIQUE(BaiTapID, HocVienID, LanNop),
    CONSTRAINT CK_ChiTietNopBai_LanNop CHECK (LanNop > 0),
    CONSTRAINT CK_ChiTietNopBai_Diem CHECK (Diem IS NULL OR Diem BETWEEN 0 AND 100)
);
GO

-- 15. BẢNG TAI LIEU
CREATE TABLE TaiLieu (
    TaiLieuID INT IDENTITY(1,1) PRIMARY KEY,
    KhoaHocID INT NOT NULL,
    TenTaiLieu NVARCHAR(150) NOT NULL,
    LoaiTaiLieu NVARCHAR(30) NULL,
    DuongDan NVARCHAR(500) NOT NULL,
    MoTa NVARCHAR(500) NULL,
    NgayDang DATE NOT NULL DEFAULT GETDATE(),
    TrangThai NVARCHAR(20) NOT NULL DEFAULT N'Đang dùng',
    CONSTRAINT FK_TaiLieu_KhoaHoc FOREIGN KEY (KhoaHocID) REFERENCES KhoaHoc(KhoaHocID)
);
GO

-- 16. BẢNG CAM KET
CREATE TABLE CamKet (
    CamKetID INT IDENTITY(1,1) PRIMARY KEY,
    HocVienID INT NOT NULL,
    KhoaHocID INT NOT NULL,
    DangKyID INT NULL,
    NgayLap DATE NOT NULL DEFAULT GETDATE(),
    TrangThai NVARCHAR(20) NOT NULL DEFAULT N'Chờ duyệt',
    DiemDauVao DECIMAL(5,2) NULL,
    DiemYeuCauCuoiKhoa DECIMAL(5,2) NULL,
    DiemCuoiKhoa DECIMAL(5,2) NULL,
    GiaoVienDeXuatID INT NULL,
    NguoiDuyetID INT NULL,
    NgayDuyet DATE NULL,
    GhiChu NVARCHAR(500) NULL,
    CONSTRAINT FK_CamKet_HocVien FOREIGN KEY(HocVienID) REFERENCES HocVien(HocVienID),
    CONSTRAINT FK_CamKet_KhoaHoc FOREIGN KEY(KhoaHocID) REFERENCES KhoaHoc(KhoaHocID),
    CONSTRAINT FK_CamKet_DangKy FOREIGN KEY(DangKyID) REFERENCES DangKy(DangKyID),
    CONSTRAINT FK_CamKet_GiaoVien FOREIGN KEY(GiaoVienDeXuatID) REFERENCES NhanSu(NhanSuID),
    CONSTRAINT FK_CamKet_NguoiDuyet FOREIGN KEY(NguoiDuyetID) REFERENCES NguoiDung(NguoiDungID),
    CONSTRAINT CK_CamKet_Diem CHECK((DiemDauVao IS NULL OR DiemDauVao BETWEEN 0 AND 100) AND (DiemYeuCauCuoiKhoa IS NULL OR DiemYeuCauCuoiKhoa BETWEEN 0 AND 100) AND (DiemCuoiKhoa IS NULL OR DiemCuoiKhoa BETWEEN 0 AND 100))
);
GO

-- 17. BẢNG HOA DON
CREATE TABLE HoaDon (
    HoaDonID INT IDENTITY(1,1) PRIMARY KEY,
    HocVienID INT NOT NULL,
    DangKyID INT NULL,
    NhanSuLapID INT NOT NULL,
    NgayLap DATE NOT NULL DEFAULT GETDATE(),
    TongTien DECIMAL(12,2) NOT NULL,
    GiamGia DECIMAL(12,2) NOT NULL DEFAULT 0,
    TongPhaiTra DECIMAL(12,2) NOT NULL,
    TrangThai NVARCHAR(20) NOT NULL DEFAULT N'Chưa đủ',
    GhiChu NVARCHAR(300) NULL,
    CONSTRAINT FK_HoaDon_HocVien FOREIGN KEY(HocVienID) REFERENCES HocVien(HocVienID),
    CONSTRAINT FK_HoaDon_DangKy FOREIGN KEY(DangKyID) REFERENCES DangKy(DangKyID),
    CONSTRAINT FK_HoaDon_NhanSu FOREIGN KEY(NhanSuLapID) REFERENCES NhanSu(NhanSuID),
    CONSTRAINT CK_HoaDon_Tien CHECK(TongTien >= 0 AND GiamGia >= 0 AND TongPhaiTra >= 0 AND GiamGia <= TongTien AND TongPhaiTra = TongTien - GiamGia)
);
GO

-- 18. BẢNG THANH TOAN
CREATE TABLE ThanhToan (
    ThanhToanID INT IDENTITY(1,1) PRIMARY KEY,
    HoaDonID INT NOT NULL,
    NhanSuID INT NOT NULL,
    NgayThanhToan DATETIME NOT NULL DEFAULT GETDATE(),
    SoTien DECIMAL(12,2) NOT NULL,
    PhuongThuc NVARCHAR(30) NOT NULL,
    GhiChu NVARCHAR(300) NULL,
    CONSTRAINT FK_ThanhToan_HoaDon FOREIGN KEY(HoaDonID) REFERENCES HoaDon(HoaDonID),
    CONSTRAINT FK_ThanhToan_NhanSu FOREIGN KEY(NhanSuID) REFERENCES NhanSu(NhanSuID),
    CONSTRAINT CK_ThanhToan_SoTien CHECK(SoTien > 0)
);
GO

-- 19. BẢNG BANG LUONG
CREATE TABLE BangLuong (
    BangLuongID INT IDENTITY(1,1) PRIMARY KEY,
    NhanSuID INT NOT NULL,
    ThangNam CHAR(7) NOT NULL,
    SoNgayCong DECIMAL(5,2) NULL,
    SoGioDay DECIMAL(7,2) NULL,
    LuongCoBan DECIMAL(12,2) NULL,
    PhuCap DECIMAL(12,2) NOT NULL DEFAULT 0,
    Thuong DECIMAL(12,2) NOT NULL DEFAULT 0,
    KhauTru DECIMAL(12,2) NOT NULL DEFAULT 0,
    ThucLinh DECIMAL(12,2) NOT NULL,
    TrangThai NVARCHAR(20) NOT NULL DEFAULT N'Tạm tính',
    CONSTRAINT FK_BangLuong_NhanSu FOREIGN KEY(NhanSuID) REFERENCES NhanSu(NhanSuID),
    CONSTRAINT UQ_BangLuong_NhanSu_Thang UNIQUE(NhanSuID, ThangNam),
    CONSTRAINT CK_BangLuong_Tien CHECK((SoNgayCong IS NULL OR SoNgayCong >= 0) AND (SoGioDay IS NULL OR SoGioDay >= 0) AND (LuongCoBan IS NULL OR LuongCoBan >= 0) AND PhuCap >= 0 AND Thuong >= 0 AND KhauTru >= 0 AND ThucLinh >= 0)
);
GO

-- 20. BẢNG LICH SU DIEU CHUYEN
CREATE TABLE LichSuDieuChuyen (
    DieuChuyenID INT IDENTITY(1,1) PRIMARY KEY,
    NhanSuID INT NOT NULL,
    CoSoCuID INT NOT NULL,
    CoSoMoiID INT NOT NULL,
    NgayDeXuat DATE NOT NULL DEFAULT GETDATE(),
    NgayHieuLuc DATE NULL,
    NguoiDeXuatID INT NULL,
    NguoiDuyetID INT NULL,
    TrangThai NVARCHAR(20) NOT NULL DEFAULT N'Chờ duyệt',
    LyDo NVARCHAR(300) NULL,
    CONSTRAINT FK_DieuChuyen_NhanSu FOREIGN KEY(NhanSuID) REFERENCES NhanSu(NhanSuID),
    CONSTRAINT FK_DieuChuyen_CoSoCu FOREIGN KEY(CoSoCuID) REFERENCES CoSo(CoSoID),
    CONSTRAINT FK_DieuChuyen_CoSoMoi FOREIGN KEY(CoSoMoiID) REFERENCES CoSo(CoSoID),
    CONSTRAINT FK_DieuChuyen_NguoiDeXuat FOREIGN KEY(NguoiDeXuatID) REFERENCES NhanSu(NhanSuID),
    CONSTRAINT FK_DieuChuyen_NguoiDuyet FOREIGN KEY(NguoiDuyetID) REFERENCES NguoiDung(NguoiDungID),
    CONSTRAINT CK_DieuChuyen_CoSo CHECK(CoSoCuID <> CoSoMoiID)
);
GO

/* ==========================================================================
   DU LIEU MAU DAY DU (20/20 BANG CO DU LIEU HOP LE)
   ========================================================================== */
INSERT INTO VaiTro(TenVaiTro, MoTa) VALUES
(N'Admin', N'Quản trị hệ thống'),
(N'Nhân viên', N'Giáo vụ / Tư vấn tuyển sinh'),
(N'Giáo viên', N'Giảng viên giảng dạy'),
(N'Học viên', N'Học viên trung tâm');

INSERT INTO CoSo(TenCoSo, DiaChi, SoDienThoai, TrangThai) VALUES
(N'Cơ sở Cầu Giấy', N'123 Đường Cầu Giấy, Hà Nội', '0900000001', 1),
(N'Cơ sở Đống Đa', N'456 Đường Tây Sơn, Đống Đa, Hà Nội', '0900000009', 1);

INSERT INTO PhongHoc(CoSoID, TenPhong, SucChua, TrangThai, GhiChu) VALUES
(1, N'P.101', 25, N'Đang sử dụng', N'Phòng học lý thuyết'),
(1, N'P.102', 30, N'Đang sử dụng', N'Phòng nghe nhìn Lab'),
(2, N'P.201', 20, N'Đang sử dụng', N'Phòng học cơ sở 2');

INSERT INTO NguoiDung(TenDangNhap, MatKhauHash, VaiTroID, TrangThai) VALUES
('admin_demo', N'HASH_DEMO_ONLY', 1, 1),
('gv_demo', N'HASH_DEMO_ONLY', 3, 1),
('hv_demo', N'HASH_DEMO_ONLY', 4, 1);

INSERT INTO NhanSu(NguoiDungID, CoSoID, HoTen, NgaySinh, GioiTinh, SoDienThoai, Email, LoaiNhanSu, TrinhDo, LuongCoBan, LuongTheoGio, NgayVaoLam, NhiemVu, TrangThai) VALUES
(1, 1, N'Nguyễn Văn Quản Lý', '1995-01-01', N'Nam', '0900000002', 'admin@example.com', N'Cả hai', N'Thạc sĩ', 12000000, 200000, '2024-01-01', N'Quản lý trung tâm kiêm giáo viên', N'Đang làm'),
(2, 1, N'Trần Thị Giảng Viên', '1998-05-15', N'Nữ', '0900000004', 'gv@example.com', N'Giáo viên', N'IELTS 8.0', NULL, 180000, '2024-06-01', N'Giáo viên chính', N'Đang làm');

INSERT INTO HocVien(NguoiDungID, HoTen, NgaySinh, GioiTinh, SoDienThoai, Email, TrinhDoDauVao, DiemDauVao, NhanXetDauVao, TrangThai) VALUES
(3, N'Lê Văn Học Viên', '2005-05-10', N'Nam', '0900000003', 'hocvien@example.com', N'A2', 65, N'Đạt yêu cầu đầu vào khóa TOEIC 500', N'Đang học');

INSERT INTO KhoaHoc(TenKhoaHoc, TrinhDo, MoTa, SoBuoi, HocPhi, TrangThai) VALUES
(N'TOEIC 500 Mục Tiêu', N'B1', N'Khóa đào tạo TOEIC chuẩn đầu ra 500+', 24, 4500000, N'Đang mở');

INSERT INTO LopHoc(TenLop, KhoaHocID, CoSoID, GiaoVienPhuTrachID, SiSoToiDa, NgayBatDau, NgayKetThuc, TrangThai) VALUES
(N'TOEIC500-K01', 1, 1, 2, 20, '2026-10-01', '2026-12-31', N'Đang học');

INSERT INTO DangKy(HocVienID, LopHocID, NgayDangKy, HocPhiApDung, TrangThai, DiemCuoiKhoa, KetQua, DuocHocLaiMienPhi, GhiChu) VALUES
(1, 1, '2026-10-01', 4500000, N'Đang học', NULL, NULL, 0, N'Học viên đăng ký đúng hạn');

INSERT INTO LichHoc(LopHocID, PhongHocID, NgayHoc, GioBatDau, GioKetThuc, TrangThai, GhiChu) VALUES
(1, 1, '2026-10-05', '18:30', '20:30', N'Đã hoàn thành', N'Buổi 1: Giới thiệu & Định hướng');

INSERT INTO PhanCongDay(LichHocID, NhanSuID, VaiTroDay, TrangThai, NguoiDuyetID, LyDo) VALUES
(1, 2, N'Chính', N'Đã duyệt', 1, N'Phân công giảng viên chính phụ trách');

INSERT INTO DiemDanh(LichHocID, HocVienID, TrangThai, GhiChu) VALUES
(1, 1, N'Có mặt', N'Tham gia đúng giờ');

INSERT INTO BaiTap(LopHocID, TieuDe, MoTa, NgayGiao, HanNop, TrangThai) VALUES
(1, N'Bài tập Reading Part 5 - Buổi 1', N'Hoàn thành 30 câu hỏi trắc nghiệm ngữ pháp', '2026-10-05 20:30:00', '2026-10-08 23:59:00', N'Đang mở');

INSERT INTO ChiTietNopBai(BaiTapID, HocVienID, LanNop, DuongDan, ThoiGianNop, Diem, NhanXet, NguoiChamID, NgayCham) VALUES
(1, 1, 1, N'/uploads/baitap/hv1_buoi1.pdf', '2026-10-07 19:30:00', 85, N'Nắm chắc ngữ pháp danh từ và đại từ', 2, '2026-10-08 10:00:00');

INSERT INTO TaiLieu(KhoaHocID, TenTaiLieu, LoaiTaiLieu, DuongDan, MoTa, NgayDang, TrangThai) VALUES
(1, N'Giáo trình TOEIC Starter PDF', N'PDF', N'/documents/toeic_starter.pdf', N'Tài liệu lưu hành nội bộ', '2026-10-01', N'Đang dùng');

INSERT INTO CamKet(HocVienID, KhoaHocID, DangKyID, NgayLap, TrangThai, DiemDauVao, DiemYeuCauCuoiKhoa, DiemCuoiKhoa, GiaoVienDeXuatID, NguoiDuyetID, NgayDuyet, GhiChu) VALUES
(1, 1, 1, '2026-10-01', N'Đã duyệt', 65, 75, NULL, 2, 1, '2026-10-02', N'Cam kết đạt 500 điểm TOEIC');

INSERT INTO HoaDon(HocVienID, DangKyID, NhanSuLapID, NgayLap, TongTien, GiamGia, TongPhaiTra, TrangThai, GhiChu) VALUES
(1, 1, 1, '2026-10-01', 4500000, 500000, 4000000, N'Chưa đủ', N'Học viên được áp dụng voucher 500k');

INSERT INTO ThanhToan(HoaDonID, NhanSuID, NgayThanhToan, SoTien, PhuongThuc, GhiChu) VALUES
(1, 1, '2026-10-01 09:30:00', 2000000, N'Chuyển khoản', N'Thanh toán đợt 1');

INSERT INTO BangLuong(NhanSuID, ThangNam, SoNgayCong, SoGioDay, LuongCoBan, PhuCap, Thuong, KhauTru, ThucLinh, TrangThai) VALUES
(1, '2026-10', 26, 20, 12000000, 500000, 1000000, 200000, 13300000, N'Đã duyệt');

INSERT INTO LichSuDieuChuyen(NhanSuID, CoSoCuID, CoSoMoiID, NgayDeXuat, NgayHieuLuc, NguoiDeXuatID, NguoiDuyetID, TrangThai, LyDo) VALUES
(2, 1, 2, '2026-10-01', '2026-11-01', 1, 1, N'Đã duyệt', N'Tăng cường giảng viên chất lượng cao cho cơ sở Đống Đa');
GO