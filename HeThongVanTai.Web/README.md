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
