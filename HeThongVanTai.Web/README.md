# HeThongVanTai.Web

Ứng dụng web (ASP.NET Core MVC, .NET 8) gọi API của project `HeThongVanTai` qua HTTP.

## Chạy

1. Chạy API (`https://localhost:7200`) và Web (`https://localhost:7300`) cùng lúc:
   Chuột phải Solution → Properties → Startup Project → **Multiple startup projects** → chọn Start cho cả hai.
2. Địa chỉ API nằm ở `appsettings.json` (`ApiBaseUrl`). Máy khác cổng thì đổi tại đây, không sửa trong controller.
3. Tài khoản thử: `admin@vantai.com` / `Admin@123` (chỉ có ở môi trường Development).

## Quy ước

- Gọi API qua `ApiClient` (đã tự gắn token). Không tự tạo `HttpClient`, không viết cứng địa chỉ API.
- Trang quản trị: controller riêng, `[Authorize(Roles = ...)]`, route `Admin/<Trang>`, view dùng `Layout = "_AdminLayout"`.
- Xóa dữ liệu dùng POST kèm `[ValidateAntiForgeryToken]`, không dùng link GET.
- Màu và thành phần dùng chung (nút, thẻ, bảng, nhãn trạng thái, ghế) nằm trong `wwwroot/css/site.css`, chỉ Vinh sửa.
  CSS riêng của mỗi module đặt ở `wwwroot/css/<module>.css`.

## Ghi log (Serilog)

- API và Web đều ghi log ra console và file theo ngày: `Logs/api-<ngày>.log`, `Logs/web-<ngày>.log` (giữ 30 ngày), nằm trong thư mục của từng project.
- Mức log, đường dẫn file chỉnh ở mục `Serilog` trong `appsettings.json`, không sửa trong code.
- Đã ghi: đăng nhập/đăng ký/đăng xuất, đặt vé, hủy vé, đổi vé, thanh toán, xác nhận chuyển khoản, nhả giữ chỗ hết hạn, lỗi nghiệp vụ và lỗi gọi API.
- **Không** ghi mật khẩu, token, nội dung form. Chỉ ghi email và mã đặt vé. Khi thêm log mới, đừng đưa cả DTO/model vào log.
- Thư mục `Logs/` đã nằm trong `.gitignore`.
- Cách ghi log trong code: inject `ILogger<TênLớp>` rồi gọi `_logger.LogInformation("Nội dung {ThamSo}", giaTri)`; lỗi bắt được thì `_logger.LogError(ex, "...")`.

## Trang Danh mục (Admin, Operator)

| Trang | Đường dẫn | API |
|---|---|---|
| Bến xe | `/Admin/Stations` | `/api/stations` |
| Tuyến đường | `/Admin/Routes` | `/api/routes` |
| Loại xe và sơ đồ ghế | `/Admin/VehicleTypes` | `/api/vehicle-types` |

- Mã nằm ở `Controllers/{Stations,Routes,VehicleTypes}Controller.cs`, `Services/DanhMucApiService.cs`, `Models/ViewModels/DanhMuc/`.
- Xóa bến, tuyến, loại xe đang được dùng: API từ chối và trang hiện thông báo lỗi của API.
- Loại xe: thêm mới (API tự sinh ghế) và chỉnh sơ đồ ghế; API hiện chưa có chức năng đổi tên loại xe.
