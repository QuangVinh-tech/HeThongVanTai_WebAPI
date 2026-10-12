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

## Trang Tổng quan (Admin, Operator)

- Đường dẫn `/Admin/Dashboard`, gọi `GET /api/Dashboard/get-summary` và `GET /api/Dashboard/get-alerts`.
- **Doanh thu** = tiền đã thu (Paid) trừ tiền đã hoàn (Refunded), tính theo ngày thu. Biểu đồ 7 ngày vẽ bằng Chart.js (CDN).
- **Chuyến đang chạy**: đã khởi hành, chưa đến nơi, chưa hủy/hoàn thành. **Lấp đầy**: ghế đã đặt / tổng ghế của các chuyến khởi hành hôm nay.
- **Cảnh báo**: đăng kiểm, bảo hiểm xe và GPLX đã hết hạn hoặc còn dưới 30 ngày; chuyến có trạng thái `Delayed`.
- Một API lỗi thì phần còn lại vẫn hiển thị; chưa có doanh thu thì biểu đồ báo "chưa có dữ liệu".

## Quản lý người dùng (chỉ Admin)

- Đường dẫn `/Admin/Users`; API `GET /api/Users` (tìm theo email, lọc vai trò, phân trang), `GET /api/Users/roles`, `GET /api/Users/{id}`, `PUT /api/Users/{id}/roles`, `PATCH /api/Users/{id}/lock`.
- Gán vai trò: chọn ít nhất một trong 5 vai trò. Vai trò nằm trong token nên **có hiệu lực ở lần đăng nhập sau** (token sống 60 phút).
- Khóa tài khoản: tài khoản bị khóa không đăng nhập được (API trả 403). Token đã cấp vẫn dùng được đến khi hết hạn.
- Bảo vệ: không tự khóa mình, không tự bỏ vai trò Admin của mình, không khóa hoặc bỏ vai trò Admin cuối cùng.

## Trang Báo cáo doanh thu (Admin, Accountant)

- Đường dẫn `/Admin/Reports`, gọi `GET /api/Reports/get-revenue-by-route`.
- Lọc theo khoảng ngày (mặc định 30 ngày gần nhất, tối đa 366 ngày) và theo tuyến; bấm tiêu đề cột để sắp xếp; phân trang 10 dòng.
- Doanh thu = tiền đã thu trừ tiền đã hoàn, tính theo ngày thu, gom theo tuyến của chuyến trong booking. Tổng ở các thẻ tính trên toàn bộ kết quả lọc, không chỉ trang hiện tại.
- **Xuất Excel**: nút "Xuất Excel" tải file `.xlsx` (ClosedXML) với đúng điều kiện lọc hiện tại, gồm toàn bộ dòng và dòng tổng cộng. Sau khi pull cần `dotnet restore` để tải ClosedXML.
- Kế toán đăng nhập xong vào thẳng trang này.
