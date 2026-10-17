# HeThongVanTai – Hệ thống bán vé xe khách

Hệ thống quản lý và bán vé vận tải hành khách gồm hai project trong một solution:

| Project | Vai trò | Địa chỉ mặc định |
|---|---|---|
| `HeThongVanTai` | Web API (ASP.NET Core .NET 8): đặt vé, thanh toán, danh mục, báo cáo, tài khoản | https://localhost:7200 (Swagger ở `/swagger`) |
| `HeThongVanTai.Web` | Giao diện Web MVC (.NET 8), gọi API qua HTTP | https://localhost:7300 |

Tài liệu API: [docs/api.md](docs/api.md). Quy ước riêng của project Web: [HeThongVanTai.Web/README.md](HeThongVanTai.Web/README.md).

## Chức năng chính

- **Khách hàng:** đăng ký, đăng nhập, tìm chuyến, chọn ghế, đặt vé, thanh toán, vé điện tử QR, tra cứu vé, hủy hoặc đổi vé, đánh giá chuyến đi.
- **Quản trị:** Tổng quan (chỉ số, biểu đồ doanh thu, cảnh báo), Bến xe, Tuyến đường, Loại xe và sơ đồ ghế, Người dùng và vai trò, Báo cáo doanh thu (xuất Excel), quản lý xe, tài xế, chuyến xe, khuyến mãi.
- **Vai trò:** `Admin`, `Operator` (điều hành), `Seller` (bán vé), `Accountant` (kế toán), `Driver` (tài xế, liên kết với hồ sơ tài xế qua `Driver.UserId`), `Customer` (khách hàng).

## Yêu cầu

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server hoặc SQL Server LocalDB (đi kèm Visual Studio)
- Visual Studio 2022, hoặc dùng dòng lệnh `dotnet`
- Công cụ migration: `dotnet tool install --global dotnet-ef`

## Cài đặt và chạy

### 1. Lấy mã nguồn

```
git clone https://github.com/QuangVinh-tech/HeThongVanTai_WebAPI.git
cd HeThongVanTai_WebAPI
```

### 2. Cấu hình bí mật (User Secrets)

Khóa ký token **không** nằm trong `appsettings.json`. Đặt bằng User Secrets, chạy trong thư mục project API:

```
cd HeThongVanTai
dotnet user-secrets set "Jwt:Key" "chuoi-bi-mat-dai-it-nhat-32-ky-tu-xxxxxxxx"
```

Khóa cần dài ít nhất 32 ký tự. Mỗi máy tự đặt khóa của mình, không đưa lên GitHub. Chuỗi kết nối mặc định dùng LocalDB (xem `ConnectionStrings` trong `appsettings.json`); nếu dùng SQL Server khác, đặt lại bằng User Secrets:

```
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<chuỗi kết nối CSDL nghiệp vụ>"
dotnet user-secrets set "ConnectionStrings:VanTaiAuthConnection" "<chuỗi kết nối CSDL tài khoản>"
```

Không ghi mật khẩu vào file `.json`: CI sẽ báo lỗi nếu tìm thấy `password=` trong file json.

**Bảo mật đăng nhập** (mục `Auth`, `Jwt`, `Security` trong `appsettings.json` của API): sai mật khẩu 5 lần thì tạm khóa 5 phút; access token sống 15 phút và Web tự làm mới bằng refresh token (7 ngày); đăng nhập và đăng ký bị giới hạn 10 lần mỗi phút theo IP. API tính IP khách từ header `X-Forwarded-For` do Web gửi, chỉ tin proxy loopback. Khi Web và API chạy trên máy khác nhau (Docker, IIS...), thêm địa chỉ IP của máy chủ Web vào `Security:TrustedProxies`, nếu không mọi khách sẽ bị tính chung một IP.

### 3. Tạo cơ sở dữ liệu (migration)

Hệ thống dùng **hai** cơ sở dữ liệu, mỗi cái một DbContext:

```
cd HeThongVanTai
dotnet ef database update --context AppDbContext
dotnet ef database update --context VanTaiAuthDbContext
```

Trong Visual Studio có thể dùng Package Manager Console: `Update-Database -Context AppDbContext` và `Update-Database -Context VanTaiAuthDbContext`.

Thêm migration mới (khi sửa model):

```
dotnet ef migrations add TenMigration --context AppDbContext
```

### 4. Chạy

Trong Visual Studio: chuột phải Solution → Properties → Startup Project → **Multiple startup projects** → chọn *Start* cho cả `HeThongVanTai` và `HeThongVanTai.Web`, rồi nhấn F5.

Hoặc dùng hai cửa sổ dòng lệnh:

```
dotnet run --project HeThongVanTai --launch-profile https
dotnet run --project HeThongVanTai.Web
```

Địa chỉ API mà Web gọi nằm ở `ApiBaseUrl` trong `HeThongVanTai.Web/appsettings.json`. Máy khác cổng thì sửa tại đây.

### 5. Tài khoản thử

Ở môi trường Development, API tự tạo tài khoản quản trị đầu tiên:

| Email | Mật khẩu | Vai trò |
|---|---|---|
| `admin@vantai.com` | `Admin@123` | Admin |

Đăng nhập bằng tài khoản này rồi vào **Người dùng** để gán vai trò cho các tài khoản khác. Người tự đăng ký luôn là `Customer`. Đổi mật khẩu này trước khi triển khai thật.

### 6. Xác thực email (OTP) và gửi email

Người tự đăng ký phải nhập mã 6 số gửi qua email mới đăng nhập được. API gửi bằng SMTP, cấu hình trong mục `Email` của `appsettings.json`; **mật khẩu SMTP đặt bằng User Secrets**, không commit:

```
cd HeThongVanTai
dotnet user-secrets set "Email:Host" "smtp.gmail.com"
dotnet user-secrets set "Email:User" "<email gửi>"
dotnet user-secrets set "Email:Password" "<mật khẩu ứng dụng>"
dotnet user-secrets set "Email:From" "<email gửi>"
```

Với Gmail, bật xác minh 2 bước rồi tạo "Mật khẩu ứng dụng" (App Password) để dùng làm `Email:Password`.

Chưa cấu hình `Email:Host` thì API không gửi email; ở môi trường Development, mã OTP được ghi vào log (tìm dòng `EMAIL GIẢ LẬP`) để bạn vẫn thử được. Tài khoản `admin@vantai.com` tự được đánh dấu đã xác thực.

Tài khoản đã có sẵn trong CSDL từ trước khi có tính năng này sẽ bị chặn đăng nhập cho tới khi xác thực. Muốn cho phép toàn bộ tài khoản cũ, chạy một lần trên CSDL tài khoản (`VanTaiAuthDB`): `UPDATE AspNetUsers SET EmailConfirmed = 1;`. Muốn tắt hẳn việc bắt buộc xác thực (ví dụ lúc phát triển), đặt `Auth:RequireEmailConfirmation` thành `false`.

## Ghi log

API và Web ghi log (Serilog) ra console và file `Logs/api-<ngày>.log`, `Logs/web-<ngày>.log` trong thư mục từng project, giữ 30 ngày. Log không chứa mật khẩu hay token. Thư mục `Logs/` đã nằm trong `.gitignore`.

## Kiểm thử và CI

```
dotnet build HeThongVanTai.sln
dotnet test HeThongVanTai.sln
```

Mỗi PR vào `main` chạy GitHub Actions (`.github/workflows/ci.yml`): restore, build, test và kiểm tra không lộ mật khẩu trong file json. PR chỉ được merge khi CI xanh.

## Làm việc nhóm

- Mỗi việc một nhánh `feature/<tên>-<nội dung>`, một PR một việc nhỏ, Vinh duyệt.
- File dùng chung của project Web (`_Layout.cshtml`, `site.css`, `Program.cs`) chỉ Vinh sửa; CSS riêng của từng module đặt ở `wwwroot/css/<module>.css`.
- Thư mục ảnh upload `wwwroot/Images/` không đưa ảnh thật lên GitHub.

## Ảnh chụp màn hình

Đặt ảnh vào `docs/images/` và gắn vào đây khi chuẩn bị demo (đăng nhập, Tổng quan, Danh mục, Người dùng, Báo cáo, đặt vé).
