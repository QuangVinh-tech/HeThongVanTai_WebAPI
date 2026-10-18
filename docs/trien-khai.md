# Hướng dẫn triển khai và vận hành

Tài liệu này dành cho người **ngoài nhóm** cần chạy hệ thống trên máy hoặc máy chủ. Có hai cách:

- **Cách 1 (khuyên dùng): Docker Compose.** Một lệnh dựng đủ SQL Server, API, Web và HTTPS.
- **Cách 2: IIS trên Windows Server.** Dùng khi cơ quan đã có máy chủ Windows và SQL Server.

## 1. Kiến trúc khi chạy bằng Docker

```
Internet ──443──> Caddy (HTTPS, tự cấp chứng chỉ)
                   ├── WEB_DOMAIN  ──> web  (Web MVC, cổng nội bộ 8080)
                   └── API_DOMAIN  ──> api  (Web API, cổng nội bộ 8080)
web ──mạng nội bộ──> api ──mạng nội bộ──> db (SQL Server 2022 Express)
```

- Chỉ Caddy mở cổng ra ngoài (80 và 443). SQL Server, API và Web không mở cổng trực tiếp.
- Web gọi API qua mạng nội bộ (`http://api:8080/`). Trình duyệt chỉ tải **ảnh** và nhận **webhook SePay** từ API qua `API_DOMAIN`, nên `API_DOMAIN` phải truy cập được từ Internet.
- Dữ liệu nằm trong các volume Docker: `sqldata` (CSDL), `api_images` (ảnh tải lên), `api_logs` và `web_logs` (log), `api_keys` và `web_keys` (khóa mã hóa), `caddy_data` (chứng chỉ HTTPS).

## 2. Chạy thử trên máy (Docker Desktop)

Cần cài [Docker Desktop](https://www.docker.com/products/docker-desktop/) (hoặc Docker Engine kèm plugin Compose) và máy còn trống ít nhất 4 GB RAM cho SQL Server.

1. Lấy mã nguồn, vào thư mục gốc (nơi có `docker-compose.yml`).
2. Tạo file cấu hình:
   - Windows PowerShell: `Copy-Item .env.example .env`
   - Linux, macOS: `cp .env.example .env`
3. Mở `.env` sửa tối thiểu: `MSSQL_SA_PASSWORD`, `JWT_KEY` (dài từ 32 ký tự), `ADMIN_EMAIL`, `ADMIN_PASSWORD`. Ý nghĩa từng biến ở mục 4.
4. Dựng và chạy:
   ```
   docker compose up -d --build
   ```
   Lần đầu mất vài phút (tải image, biên dịch). API tự áp migration cho cả hai CSDL và tạo tài khoản Admin từ `ADMIN_EMAIL`, `ADMIN_PASSWORD`.
5. Xem trạng thái: `docker compose ps`. Cả bốn dịch vụ cần ở trạng thái `running`, riêng `db`, `api`, `web` có thêm `(healthy)`.
6. Mở trình duyệt:
   - Web: https://localhost
   - API (Swagger tắt ở môi trường thật): https://api.localhost/health
   - Đăng nhập bằng `ADMIN_EMAIL` và `ADMIN_PASSWORD`, rồi **đổi mật khẩu ngay** ở `/Account/Profile`.

**Cảnh báo chứng chỉ khi chạy thử:** với `localhost`, Caddy tự ký chứng chỉ nội bộ nên trình duyệt báo "không an toàn". Bấm tiếp tục, hoặc tin cậy chứng chỉ gốc của Caddy:

```
docker compose cp caddy:/data/caddy/pki/authorities/local/root.crt ./caddy-root.crt
```
rồi nhấp đôi file `caddy-root.crt` và cài vào "Trusted Root Certification Authorities" (Windows). Với tên miền thật không cần bước này.

Dừng hệ thống: `docker compose down` (dữ liệu vẫn còn). Xóa cả dữ liệu: `docker compose down -v` (**mất hết CSDL và ảnh**).

## 3. Lên máy chủ thật (Ubuntu, VPS)

1. Chuẩn bị máy chủ Ubuntu 22.04 trở lên, tối thiểu 2 vCPU và 4 GB RAM. Mở cổng 80 và 443 (tường lửa và nhà cung cấp VPS), không mở cổng 1433.
2. Cài Docker: làm theo https://docs.docker.com/engine/install/ubuntu/ (gồm `docker compose`).
3. Trỏ DNS: tạo hai bản ghi **A** về địa chỉ IP máy chủ, ví dụ `vantai.example.com` và `api.vantai.example.com`.
4. Lấy mã nguồn: `git clone <địa chỉ repo>` rồi `cd` vào thư mục.
5. `cp .env.example .env` và sửa:
   - `WEB_DOMAIN=vantai.example.com`, `API_DOMAIN=api.vantai.example.com`
   - mật khẩu mạnh cho `MSSQL_SA_PASSWORD`, `ADMIN_PASSWORD`; khóa ngẫu nhiên cho `JWT_KEY` (tạo bằng `openssl rand -base64 48`)
   - SMTP, SePay, tài khoản ngân hàng (mục 4)
6. `docker compose up -d --build`. Caddy tự xin chứng chỉ Let's Encrypt cho hai tên miền (cần DNS đã trỏ đúng và cổng 80, 443 mở). Theo dõi: `docker compose logs -f caddy`.
7. Kiểm tra: mở `https://vantai.example.com` và `https://api.vantai.example.com/health`.
8. Cấu hình **webhook SePay**: URL `https://api.vantai.example.com/api/Payments/webhook/sepay`, kiểu xác thực API Key, giá trị trùng `SEPAY_WEBHOOK_KEY` trong `.env`.
9. Đặt lịch sao lưu (mục 6).

### Cập nhật phiên bản mới

```
git pull
docker compose up -d --build
```
API tự áp migration mới khi khởi động. **Sao lưu trước khi cập nhật** (mục 6).

## 4. Biến môi trường (file `.env`)

| Biến | Bắt buộc | Ý nghĩa |
|---|---|---|
| `MSSQL_SA_PASSWORD` | Có | Mật khẩu tài khoản `sa` của SQL Server. Tối thiểu 8 ký tự, có chữ hoa, chữ thường, số, ký tự đặc biệt; không dùng `;` và `=`. |
| `WEB_DOMAIN`, `API_DOMAIN` | Có | Tên miền của Web và API. Chạy thử giữ `localhost` và `api.localhost`. |
| `JWT_KEY` | Có | Khóa ký token đăng nhập, từ 32 ký tự. Giữ bí mật; đổi khóa thì mọi người phải đăng nhập lại. |
| `ADMIN_EMAIL`, `ADMIN_PASSWORD` | Lần đầu | Tạo tài khoản Admin đầu tiên nếu chưa có. Đã có rồi thì không đổi mật khẩu. |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM` | Nên có | Gửi email quên mật khẩu và mã xác thực. Với Gmail dùng "mật khẩu ứng dụng". Bỏ trống thì không gửi được email. |
| `SEPAY_WEBHOOK_KEY` | Nếu dùng SePay | Khóa xác thực webhook thanh toán. |
| `BANK_ID`, `BANK_ACCOUNT_NO`, `BANK_ACCOUNT_NAME` | Nếu dùng QR | Tài khoản nhận tiền để tạo mã QR. |

Các cấu hình khác (thời gian giữ ghế, hoàn tiền, thông tin công ty) chỉnh trong trang **Cài đặt hệ thống** của Admin, không cần sửa file. Muốn đổi một khóa bất kỳ của `appsettings.json` bằng biến môi trường, dùng hai dấu gạch dưới thay cho dấu hai chấm, ví dụ `Auth__MaxFailedAttempts=3` rồi thêm vào mục `environment` của dịch vụ `api` trong `docker-compose.yml`.

**Không đưa `.env` lên Git** (đã thêm vào `.gitignore`; chỉ `.env.example` với giá trị mẫu được đưa lên). CI sẽ báo lỗi nếu có `password=` trong file `.json`.

## 5. Kiểm tra sức khỏe: `/health`

| Địa chỉ | Trả về |
|---|---|
| `https://<API_DOMAIN>/health` | `Healthy` khi kết nối được **cả hai** CSDL (`csdl-nghiep-vu`, `csdl-tai-khoan`). |
| `https://<WEB_DOMAIN>/health` | `Healthy` khi Web chạy và gọi được API. |

Mã HTTP 200 là khỏe, 503 là có thành phần hỏng. Nội dung JSON, ví dụ:

```json
{"status":"Healthy","totalMs":12,"checks":[{"name":"csdl-nghiep-vu","status":"Healthy","description":"Kết nối được","ms":8},{"name":"csdl-tai-khoan","status":"Healthy","description":"Kết nối được","ms":9}]}
```

Kết quả không chứa chuỗi kết nối hay nội dung lỗi. Dùng địa chỉ này cho dịch vụ giám sát (UptimeRobot, Better Stack...) để nhận cảnh báo khi hệ thống ngừng. Docker cũng tự kiểm tra `/health` mỗi 30 giây (`docker compose ps` hiện `healthy` hoặc `unhealthy`).

## 6. Sao lưu và phục hồi

### Cần sao lưu những gì

| Thành phần | Cách sao lưu |
|---|---|
| Hai CSDL `VanTaiDB` và `VanTaiAuthDB` | Script `deploy/backup.sh` (hoặc `.ps1`): `BACKUP DATABASE ... WITH COMPRESSION, CHECKSUM` và kiểm tra lại bằng `RESTORE VERIFYONLY`. |
| Ảnh tải lên (xe, tài xế, logo) | Script trên nén thư mục `wwwroot/Images` thành `images.tar.gz`. |
| File `.env` | Chép thủ công, cất ở nơi **riêng và an toàn** (có mật khẩu và khóa). Script không sao lưu file này. |

### Sao lưu

Linux, macOS (hoặc Git Bash trên Windows), chạy ở thư mục gốc, khi hệ thống đang chạy:

```
bash deploy/backup.sh
```

Windows PowerShell:

```
powershell -ExecutionPolicy Bypass -File deploy\backup.ps1
```

Kết quả nằm ở `backups/<yyyyMMdd_HHmmss>/` gồm `VanTaiDB.bak`, `VanTaiAuthDB.bak`, `images.tar.gz`. Mặc định giữ 14 ngày, đổi bằng biến `KEEP_DAYS`. **Hãy chép thư mục `backups` sang máy khác hoặc dịch vụ lưu trữ**: sao lưu chỉ nằm trên cùng máy chủ thì mất cả hai khi máy hỏng.

**Đặt lịch hằng ngày:**
- Linux, cron (`crontab -e`): `0 2 * * * cd /đường/dẫn/dự/án && bash deploy/backup.sh >> backups/backup.log 2>&1`
- Windows, Task Scheduler: tạo tác vụ chạy hằng ngày với chương trình `powershell.exe`, tham số `-ExecutionPolicy Bypass -File "C:\đường\dẫn\deploy\backup.ps1"`, thư mục bắt đầu là thư mục gốc dự án.

### Phục hồi

**Cảnh báo:** phục hồi **ghi đè** toàn bộ dữ liệu hiện tại.

```
bash deploy/restore.sh backups/20261018_020000
```

hoặc `powershell -ExecutionPolicy Bypass -File deploy\restore.ps1 backups\20261018_020000`.

Script hỏi xác nhận (gõ `CO`), rồi: dừng web, api, caddy → kiểm tra file bằng `RESTORE VERIFYONLY` → phục hồi hai CSDL (đưa về chế độ một người dùng, `WITH REPLACE`, rồi trả về nhiều người dùng) → khởi động lại → giải nén ảnh.

**Phục hồi lên máy mới:** cài Docker, lấy mã nguồn, chép lại `.env`, chạy `docker compose up -d db` và đợi `healthy`, chép thư mục `backups/<thời-điểm>` sang máy mới, rồi chạy script phục hồi ở trên (script tự khởi động các dịch vụ còn lại).

### Bài thực hành phục hồi (làm trước khi đưa vào dùng thật)

Phải thử phục hồi ít nhất một lần, một bản sao lưu chưa từng thử phục hồi thì chưa thể tin cậy.

1. Chạy hệ thống, đăng nhập Admin, tạo một dữ liệu dễ nhận biết (ví dụ một bến xe tên `KIEM-TRA-PHUC-HOI`) và tải một ảnh logo.
2. Chạy `deploy/backup.sh`. Ghi lại thư mục tạo ra.
3. Xóa bến xe `KIEM-TRA-PHUC-HOI` và đổi logo.
4. Chạy `deploy/restore.sh` với thư mục vừa sao lưu.
5. Mở lại Web: bến xe `KIEM-TRA-PHUC-HOI` và logo cũ phải xuất hiện; đăng nhập Admin được; `/health` của API và Web đều `Healthy`.
6. Ghi lại ngày thử và người thử vào mục "Nhật ký thử phục hồi" cuối tài liệu này.

## 7. Triển khai lên IIS (Windows Server)

Dùng khi không dùng Docker. Cần Windows Server 2019 trở lên và SQL Server (bản Express đủ dùng).

1. **Cài thành phần:** bật IIS (Web Server) và cài [.NET 8 **Hosting Bundle**](https://dotnet.microsoft.com/download/dotnet/8.0) (không phải chỉ Runtime), rồi chạy `iisreset`.
2. **Cơ sở dữ liệu:** tạo đăng nhập SQL (hoặc dùng tài khoản của application pool) có quyền `db_owner` trên hai CSDL `VanTaiDB` và `VanTaiAuthDB`. Có thể để API tự tạo bằng migration (bước 5).
3. **Xuất bản (publish)** trên máy có .NET 8 SDK, ở thư mục gốc dự án:
   ```
   dotnet publish HeThongVanTai/HeThongVanTai.csproj -c Release -o C:\publish\api
   dotnet publish HeThongVanTai.Web/HeThongVanTai.Web.csproj -c Release -o C:\publish\web
   ```
   Chép hai thư mục sang máy chủ, ví dụ `C:\inetpub\vantai\api` và `C:\inetpub\vantai\web`.
4. **Tạo hai site** trong IIS Manager, mỗi site một Application Pool riêng với `.NET CLR version = No Managed Code`:
   - `api` (ví dụ `api.vantai.example.com`) trỏ tới thư mục `api`
   - `web` (ví dụ `vantai.example.com`) trỏ tới thư mục `web`
   - Gắn binding **HTTPS** với chứng chỉ (mua hoặc dùng [win-acme](https://www.win-acme.com/) để lấy Let's Encrypt miễn phí). Bật chuyển hướng HTTP sang HTTPS (URL Rewrite) nếu cần.
5. **Cấu hình bằng biến môi trường** trong `web.config` của từng site (do `dotnet publish` tạo), thêm vào thẻ `<aspNetCore>`:
   ```xml
   <environmentVariables>
     <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
     <environmentVariable name="ConnectionStrings__DefaultConnection" value="Server=TEN-MAY\SQLEXPRESS;Database=VanTaiDB;Trusted_Connection=True;TrustServerCertificate=True;" />
     <environmentVariable name="ConnectionStrings__VanTaiAuthConnection" value="Server=TEN-MAY\SQLEXPRESS;Database=VanTaiAuthDB;Trusted_Connection=True;TrustServerCertificate=True;" />
     <environmentVariable name="Jwt__Key" value="chuoi-ngau-nhien-dai-it-nhat-32-ky-tu" />
     <environmentVariable name="Jwt__Issuer" value="https://api.vantai.example.com" />
     <environmentVariable name="Jwt__Audience" value="https://api.vantai.example.com" />
     <environmentVariable name="Web__BaseUrl" value="https://vantai.example.com" />
     <environmentVariable name="PublicUrls__ApiBaseUrl" value="https://api.vantai.example.com" />
     <environmentVariable name="Database__AutoMigrate" value="true" />
     <environmentVariable name="Seed__AdminEmail" value="admin@example.com" />
     <environmentVariable name="Seed__AdminPassword" value="Doi_Mat_Khau_Admin_123" />
     <environmentVariable name="DataProtection__KeysPath" value="C:\inetpub\vantai\keys" />
   </environmentVariables>
   ```
   Với site **web**, đặt `ApiBaseUrl` là địa chỉ HTTPS của API (`https://api.vantai.example.com/`) và `DataProtection__KeysPath` riêng (ví dụ `C:\inetpub\vantai\keys-web`). Thêm cấu hình email, SePay, ngân hàng như bảng ở mục 4 (`Email__Host`, `Email__User`, `Email__Password`, `Email__From`, `Payment__WebhookApiKey`, `BankTransfer__AccountNo`...). **Không để `web.config` có chứa mật khẩu trong kho Git**; chỉ sửa trên máy chủ. Dùng `Trusted_Connection=True` (đăng nhập bằng tài khoản của application pool) giúp không phải ghi mật khẩu SQL.
   - Nếu Web và API chạy trên cùng IIS và không đi qua proxy, thêm vào `Security__TrustedProxies__0` địa chỉ IP của máy chủ Web để API tính đúng IP khách khi giới hạn đăng nhập.
6. **Phân quyền thư mục:** cấp quyền **Modify** cho `IIS AppPool\<tên pool>` trên: `Logs`, `wwwroot\Images` (API) và thư mục `DataProtection__KeysPath`.
7. Mở `https://api.vantai.example.com/health` rồi `https://vantai.example.com/health`; cả hai phải trả `Healthy`. Lần đầu API có thể mất vài chục giây vì áp migration.
8. **Sao lưu trên IIS (không có Docker):** tạo tác vụ Task Scheduler chạy hằng ngày lệnh `sqlcmd` (cài kèm SQL Server):
   ```
   sqlcmd -S TEN-MAY\SQLEXPRESS -E -b -Q "BACKUP DATABASE [VanTaiDB] TO DISK = N'D:\backup\VanTaiDB.bak' WITH COMPRESSION, CHECKSUM, INIT"
   sqlcmd -S TEN-MAY\SQLEXPRESS -E -b -Q "BACKUP DATABASE [VanTaiAuthDB] TO DISK = N'D:\backup\VanTaiAuthDB.bak' WITH COMPRESSION, CHECKSUM, INIT"
   ```
   và chép thư mục ảnh `wwwroot\Images`. Phục hồi: `RESTORE DATABASE [VanTaiDB] FROM DISK = N'D:\backup\VanTaiDB.bak' WITH REPLACE` (dừng site trước, bật chế độ một người dùng như script `deploy/restore.sh`).

## 8. Sự cố thường gặp

| Hiện tượng | Nguyên nhân và cách xử lý |
|---|---|
| `docker compose up` báo "Hãy đặt MSSQL_SA_PASSWORD" hoặc "JWT_KEY" | Chưa có file `.env` hoặc thiếu biến. Làm lại bước tạo `.env`. |
| Dịch vụ `db` mãi `starting` hoặc `unhealthy` | Mật khẩu `sa` không đủ mạnh (SQL Server từ chối khởi động) hoặc máy thiếu RAM. Xem `docker compose logs db`. Sau khi đổi mật khẩu phải `docker compose down -v` (mất dữ liệu thử) vì SQL Server giữ mật khẩu cũ trong volume. |
| API `unhealthy`, log báo lỗi kết nối CSDL | `db` chưa sẵn sàng hoặc sai mật khẩu. API thử lại 10 lần mỗi 5 giây khi khởi động; nếu vẫn lỗi, `docker compose restart api` sau khi `db` đã `healthy`. |
| Caddy không lấy được chứng chỉ | DNS chưa trỏ đúng, cổng 80 hoặc 443 bị chặn, hoặc tên miền gõ sai trong `.env`. Xem `docker compose logs caddy`. |
| Ảnh xe, tài xế, logo không hiện | `API_DOMAIN` không truy cập được từ trình duyệt, hoặc ảnh cũ lưu địa chỉ `localhost` (dữ liệu chuyển từ máy dev). Tải lại ảnh trên hệ thống thật. |
| Email không gửi được | Chưa đặt `SMTP_*`. Gmail cần "mật khẩu ứng dụng" và bật xác minh 2 bước. |
| Thanh toán chuyển khoản không tự xác nhận | Webhook SePay sai địa chỉ hoặc khác `SEPAY_WEBHOOK_KEY`. Xem lịch sử gọi ở trang SePay và log API (`docker compose logs api`). |
| Khách bị chặn "thao tác quá nhiều lần" dù mới đăng nhập vài lần | API đang thấy mọi khách cùng một IP. Với Docker đã bật `ASPNETCORE_FORWARDEDHEADERS_ENABLED`; với IIS xem chú thích `Security__TrustedProxies` ở mục 7. |

## Nhật ký thử phục hồi

| Ngày thử | Người thử | Máy chủ | Kết quả | Ghi chú |
|---|---|---|---|---|
| | | | | |
