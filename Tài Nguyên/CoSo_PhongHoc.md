# Cơ sở, phòng học và điều chuyển nhân sự

## Áp dụng bản vá vào bản dự án trên máy

Nếu nhận file `co-so-phong-hoc.patch`, mở Terminal tại thư mục repository trong Visual Studio. Lưu hoặc commit công việc đang làm trước, rồi chạy:

```sh
git apply --check duong-dan-den-file/co-so-phong-hoc.patch
git apply duong-dan-den-file/co-so-phong-hoc.patch
```

Thay đường dẫn bằng vị trí file đã tải. Nếu lệnh kiểm tra báo xung đột, giữ nguyên code đang có và đối chiếu thay đổi; không dùng lệnh reset để ghi đè công việc. Bản vá dựa trên commit `d220b04` của repository. Sau khi áp dụng, thực hiện bước nâng cấp SQL bên dưới trước khi chạy chức năng.

## Chạy trong Visual Studio

1. Mở `QLTrungTamTiengAnh_Web.slnx` bằng Visual Studio hỗ trợ .NET 10.
2. Nếu chưa có database, chạy `Database/QLTT_TiengAnh_Update.sql` một lần trong SQL Server Management Studio. **Không chạy lại script tạo database trên dữ liệu đang sử dụng.**
3. Sao lưu database hiện có, rồi chạy `Database/CoSo_PhongHoc_Upgrade.sql` trong SSMS. Script nâng cấp giữ dữ liệu cũ, bổ sung mã cơ sở và các cột mà model hiện tại cần. Có thể chạy lại script nâng cấp; mọi thay đổi nằm trong giao dịch.
4. Kiểm tra `ConnectionStrings:DefaultConnection` phù hợp SQL Server của máy. Có thể dùng **Manage User Secrets** trên project để cấu hình cục bộ thay vì sửa cấu hình dùng chung. Ví dụ với Windows Authentication:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=.\\SQLEXPRESS;Database=QLTrungTamTiengAnh;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
     }
   }
   ```

5. Chạy project web, đăng nhập bằng tài khoản có vai trò `Admin` hoặc `QuanLy`, rồi mở **Cơ sở & Phòng học** (`/CoSo`). Tài khoản mẫu trong script SQL gốc dùng `HASH_DEMO_ONLY`, không phải mật khẩu đăng nhập hợp lệ; hãy dùng tài khoản đang hoạt động của bạn.

## Các thao tác

- **Cơ sở:** thêm, sửa mã/tên/địa chỉ/hotline; ngừng hoạt động và mở lại. Mã cơ sở không được trùng. Mã tự bổ sung cho dữ liệu cũ có dạng `CS-1`, `CS-2`; có thể sửa lại trên giao diện.
- **Phòng học:** lọc theo cơ sở, thêm/sửa tên/sức chứa/trạng thái/ghi chú và xóa mềm. Tên phòng duy nhất trong từng cơ sở; sức chứa phải là số nguyên dương. Tên phòng đã xóa có thể dùng lại.
- **Điều chuyển:** chọn nhân sự, xem cơ sở hiện tại, chọn cơ sở đang hoạt động khác và nhập lý do. Nút phê duyệt đổi cơ sở làm việc **ngay lập tức**, đồng thời ghi lịch sử, thời gian và người duyệt từ tài khoản đăng nhập trong cùng một giao dịch. Phần này không có bước chờ duyệt riêng. Lớp và phân công giảng dạy hiện có không tự chuyển theo nhân sự.
- Không thể ngừng cơ sở còn nhân sự đang làm, lớp chưa kết thúc hoặc lịch học chưa kết thúc. Có thể điều chuyển nhân sự ra khỏi cơ sở đã ngừng hoạt động.
- Phòng còn lịch học tương lai hoặc đang diễn ra không thể bị xóa, chuyển cơ sở hay chuyển sang bảo trì/ngừng sử dụng. Giảm sức chứa phải đáp ứng sĩ số tối đa của lớp đã xếp lịch.

Dữ liệu được tải lại từ SQL Server sau khi lưu; tải lại trang vẫn giữ các thay đổi. Nếu phiên đăng nhập hết hạn hoặc lưu thất bại, giao diện hiển thị lỗi và không giả lập thành công.

## Kiểm thử

```sh
dotnet build QLTrungTamTiengAnh_Web.slnx
dotnet test QLTrungTamTiengAnh_Web.Tests/QLTrungTamTiengAnh_Web.Tests.csproj
```

Bộ kiểm thử sử dụng SQLite trong bộ nhớ cho lưu dữ liệu và ràng buộc nghiệp vụ, cùng ASP.NET TestServer để kiểm tra phân quyền và CSRF. Kiểm tra tương tác với SQL Server và trình duyệt được thực hiện riêng trong môi trường phát triển; bộ test SQLite không thay thế kiểm thử SQL Server.
