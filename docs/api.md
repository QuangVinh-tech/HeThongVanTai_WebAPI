# Tài liệu API

Địa chỉ gốc: `https://localhost:7200`. Có Swagger tại `/swagger` (chỉ môi trường Development), dùng nút **Authorize** để dán token.

## Quy ước chung

- **Xác thực:** JWT. Đăng nhập tại `POST /api/Auth/Login`, lấy `jwtToken` và `refreshToken`, gửi kèm mọi lời gọi: `Authorization: Bearer <jwtToken>`. `jwtToken` sống ngắn (mặc định 15 phút, `Jwt:AccessTokenMinutes`); hết hạn thì gọi `POST /api/Auth/refresh` để lấy cặp token mới (refresh token sống 7 ngày, `Jwt:RefreshTokenDays`, mỗi lần dùng được đổi lấy token mới). Tài khoản bị khóa hoặc bị xóa thì `jwtToken` cũ bị từ chối ngay (401).
- **Mặc định phải đăng nhập.** API công khai được ghi rõ `[AllowAnonymous]` (cột Quyền ghi "Mọi người").
- **Vai trò:** `Admin`, `Operator`, `Seller`, `Accountant`, `Driver` (tài xế), `Customer`. Vai trò nằm trong token, nên đổi vai trò có hiệu lực khi đăng nhập hoặc làm mới token.
- **Lỗi nghiệp vụ** trả JSON `{ "message": "..." }` kèm mã 400, 404 hoặc 409. Lỗi dữ liệu nhập trả 400 theo chuẩn ProblemDetails. Chưa đăng nhập: 401. Không đủ quyền: 403.
- **Ngày giờ** theo giờ máy chủ. Tham số ngày dạng `yyyy-MM-dd`.
- **Phân trang:** tên tham số và dạng kết quả khác nhau giữa các API. Người dùng, báo cáo, đặt vé dùng `page`, `pageSize`; xe, tài xế, chuyến xe dùng `pageNumber`, `pageSize`, `sortBy`, `isAscending`. Xem Swagger để biết chính xác từng API.

> Cột "Quyền" lấy từ các thuộc tính `[Authorize]` trong mã nguồn. Khi sửa quyền ở controller, cập nhật lại bảng này.

## Tài khoản – `/api/Auth`

| Phương thức | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/Auth/Register` | Mọi người | Đăng ký (giới hạn 10 lần mỗi phút theo IP). Người lạ luôn là `Customer`; chỉ Admin đang đăng nhập mới gán được vai trò khác. Trả `{ message, requiresVerification, emailSent }`; tự đăng ký thì phải xác thực email bằng mã OTP. Đăng ký lại email chưa xác thực sẽ đặt lại mật khẩu theo lần mới và gửi lại mã. |
| POST | `/api/Auth/verify-email` | Mọi người | Body `{ email, code }` (mã 6 số, hiệu lực khoảng 9 phút). Sai 5 lần thì tạm khóa 5 phút (429). |
| POST | `/api/Auth/resend-otp` | Mọi người | Body `{ email }`. Gửi lại mã, tối thiểu 60 giây một lần (429 nếu gửi dồn). Luôn trả cùng một thông báo. |
| POST | `/api/Auth/Login` | Mọi người | Đăng nhập, trả `{ jwtToken, refreshToken, refreshExpiresAt }`. Sai mật khẩu 5 lần (`Auth:MaxFailedAttempts`) thì tạm khóa 5 phút (`Auth:LockoutMinutes`), trả 403 kèm số phút còn lại. Tài khoản bị admin khóa hoặc chưa xác thực email cũng trả 403. Giới hạn 10 lần mỗi phút theo IP (429, header `Retry-After`). |
| POST | `/api/Auth/refresh` | Mọi người | Body `{ refreshToken }`. Trả cặp token mới, nạp lại vai trò; token cũ bị thu hồi. Token hết hạn, đã thu hồi hoặc tài khoản bị khóa trả 401. Dùng lại token đã đổi (ngoài 20 giây ân hạn) bị coi là nghi lộ và thu hồi mọi phiên của tài khoản. |
| POST | `/api/Auth/forgot-password` | Mọi người | Body `{ email }`. Gửi email chứa liên kết đặt lại mật khẩu (hết hạn sau `Auth:ResetLinkMinutes` = 30 phút). Luôn trả cùng một thông báo dù email có tài khoản hay không; mỗi email chỉ yêu cầu lại sau 60 giây (429). Tài khoản bị admin khóa không được gửi. |
| POST | `/api/Auth/reset-password` | Mọi người | Body `{ email, token, newPassword }`. Liên kết dùng một lần; sai, hết hạn hoặc đã dùng đều trả 400 cùng một thông báo. Thành công thì mở khóa tạm, thu hồi mọi phiên đăng nhập. |
| POST | `/api/Auth/logout` | Mọi người | Body `{ refreshToken }`. Thu hồi phiên; luôn trả 200. |
| GET | `/api/Auth/me` | Đăng nhập | Email và vai trò của token hiện tại. |
| POST | `/api/Auth/change-password` | Đăng nhập (mọi vai trò) | Đổi mật khẩu của chính mình. Body `{ currentPassword, newPassword, refreshToken? }` (gửi `refreshToken` của phiên đang dùng để giữ phiên này, các phiên khác bị thu hồi). Sai mật khẩu hiện tại, mật khẩu mới trùng mật khẩu cũ hoặc không đạt quy tắc thì trả 400 kèm `message`. Mật khẩu không được ghi vào log. |

## Người dùng – `/api/Users` (chỉ Admin)

| Phương thức | Đường dẫn | Mô tả |
|---|---|---|
| GET | `/api/Users/roles` | Danh sách vai trò trong hệ thống. |
| GET | `/api/Users?keyword=&role=&page=1&pageSize=10` | Danh sách, tìm theo email, lọc theo vai trò, phân trang (tối đa 50/trang). |
| GET | `/api/Users/driver-profiles` | Danh sách hồ sơ tài xế (kèm `linkedUserId`) để chọn khi gán vai trò Driver. |
| GET | `/api/Users/{id}` | Một người dùng (kèm `driverId`, `driverName` nếu đã liên kết hồ sơ tài xế). |
| PUT | `/api/Users/{id}/roles` | Đổi vai trò. Body `{ "roles": ["Seller"] }`; vai trò `Driver` cần thêm `"driverId"` (hồ sơ tài xế chưa liên kết tài khoản khác), bỏ vai trò Driver thì gỡ liên kết. Ít nhất 1 vai trò; không bỏ Admin của chính mình hoặc Admin cuối cùng. |
| PATCH | `/api/Users/{id}/lock` | Khóa hoặc mở khóa. Body `{ "locked": true }`. Không tự khóa mình, không khóa Admin cuối cùng. |

## Cài đặt hệ thống – `/api/settings`

Thời gian giữ ghế, chính sách hoàn tiền và thông tin công ty lưu trong bảng `SystemSettings`. Chưa lưu thì dùng mặc định (giữ ghế lấy từ `Booking:HoldMinutes`, hoàn tiền 100% từ 24 giờ, 70% từ 12 giờ, 50% từ 2 giờ). Lưu xong có hiệu lực ngay với lần đặt vé và hủy vé tiếp theo.

| Phương thức | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/settings/public` | Mọi người | Tên công ty, logo, hotline, email, thời gian giữ ghế, các mốc hoàn tiền (cho chân trang và trang Liên hệ, Chính sách). |
| GET | `/api/settings` | Admin | Cài đặt hiện tại. |
| PUT | `/api/settings` | Admin | Lưu cài đặt. Body `{ holdMinutes (1-120), refundTiers: [{ hoursBefore, ratePercent }] (1-10 mốc), companyName, logoUrl, hotline, email }`. Mốc không được trùng giờ; hủy càng sát giờ chạy thì tỉ lệ hoàn không được cao hơn. Sai dữ liệu trả 400 kèm `message`. |

## Tổng quan và báo cáo

| Phương thức | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/Dashboard/get-summary?days=7` | Admin, Operator | Doanh thu hôm nay và N ngày, chuyến đang chạy, vé bán hôm nay, tỉ lệ lấp đầy, doanh thu từng ngày. |
| GET | `/api/Dashboard/get-alerts?days=30` | Admin, Operator | Cảnh báo đăng kiểm, bảo hiểm, GPLX đã hết hoặc sắp hết hạn trong N ngày; chuyến trạng thái `Delayed`. |
| GET | `/api/Reports/get-revenue-by-route` | Admin, Accountant | Doanh thu theo tuyến. Tham số: `from`, `to`, `routeId`, `sortBy` (`revenue`, `tickets`, `payments`, `route`), `desc`, `page`, `pageSize`, `all` (true: trả toàn bộ để xuất Excel). Trả thêm `summary` với tổng trên toàn bộ kết quả lọc. |

Cách tính doanh thu: tiền đã thu (`Paid`) trừ tiền đã hoàn (`Refunded`), tính theo ngày thu.

## Báo cáo mở rộng – `/api/Reports` (Admin, Accountant)

Mọi báo cáo doanh thu dùng chung một cách tính (`ReportService.LoadFactsAsync`): doanh thu = tiền đã thu (Paid) trừ tiền đã hoàn (Refunded), tính theo ngày thu, gắn với tuyến, xe, tài xế của vé đầu tiên trong đặt vé và nhân viên bán (`Booking.SoldBy`, chỉ có khi bán tại quầy). Vì vậy tổng của các báo cáo theo ngày, tháng, tuyến, xe, tài xế, nhân viên luôn bằng nhau. Đặt vé bán trước khi có cột `SoldBy` được gom vào dòng "Khách tự đặt hoặc chưa ghi nhận".

| Phương thức | Đường dẫn | Mô tả |
|---|---|---|
| GET | `/api/Reports/get-revenue-by-route` | Doanh thu theo tuyến (đã có từ trước). |
| GET | `/api/Reports/revenue-by` | `by=day\|month\|route\|vehicle\|driver\|seller`, `from`, `to` (tối đa 366 ngày), `routeId`, `sortBy=revenue\|tickets\|payments\|name`, `desc`, `page`, `pageSize`, `all`. Theo ngày và tháng trả đủ mọi ngày, tháng trong khoảng (kỳ không có doanh thu là 0). |
| GET | `/api/Reports/debt` | Công nợ vận chuyển: đơn chưa thu tiền (không tính đơn đã hủy) gom theo người gửi, chia tuổi nợ 0-30, 31-60, trên 60 ngày. `from`, `to` (lọc theo ngày tạo đơn, bỏ trống là không giới hạn), `feePayer=Sender\|Receiver\|Debt`, `all=true` trả thêm `details` từng đơn. |
| GET | `/api/Reports/reconciliation` | Đối soát chuyển khoản: giao dịch `BankTransfer` đã thanh toán trong khoảng ngày, kèm cảnh báo (lệch số tiền, nhiều giao dịch cho một đặt vé, thiếu mã giao dịch ngân hàng, đặt vé hết hạn mà có tiền) và đặt vé đã Paid nhưng không có giao dịch. `onlyIssues=true` chỉ lấy dòng bất thường; phần tổng hợp gồm cả tiền mặt. |

Giới hạn: giao dịch SePay không tự khớp được (sai số tiền, đặt vé hết hạn hoặc đã thanh toán) hiện chỉ ghi log, chưa lưu CSDL nên chưa lên báo cáo đối soát.

## Danh mục

| Phương thức | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/stations`, `/api/stations/{id}` | Mọi người | Bến xe; lọc `city`, `keyword`. |
| POST, PUT, DELETE | `/api/stations`, `/api/stations/{id}` | Admin, Operator | Thêm, sửa, xóa. Xóa bến đang dùng trong tuyến: 400. |
| GET | `/api/routes`, `/api/routes/{id}` | Mọi người | Tuyến đường. |
| POST, PUT, DELETE | `/api/routes`, `/api/routes/{id}` | Admin, Operator | Thêm, sửa, xóa. Xóa tuyến đã có chuyến: 400. |
| GET, POST | `/api/vehicle-types` | Admin, Operator | Loại xe và sơ đồ ghế; thêm loại xe (tự sinh ghế theo số ghế, số tầng). |
| GET, DELETE | `/api/vehicle-types/{id}` | Admin, Operator | Chi tiết; xóa (400 nếu đang có xe dùng). |
| PUT | `/api/vehicle-types/{id}/seats` | Admin, Operator | Thay toàn bộ sơ đồ ghế. Body là mảng `{ seatCode, floor }`. |

## Xe, tài xế, ảnh (quyền: Admin, Operator)

| Phương thức | Đường dẫn | Mô tả |
|---|---|---|
| GET, POST | `/api/vehicles` | Danh sách xe (tham số `filterOn`, `filterQuery`, `status`, `vehicleTypeId`, `sortBy`, `isAscending`, `pageNumber`, `pageSize`); thêm xe. |
| GET, PUT, DELETE | `/api/vehicles/{id}` | Chi tiết, sửa, xóa xe. |
| PATCH | `/api/vehicles/{id}/image` | Gắn ảnh xe. |
| GET | `/api/vehicles/alerts` | Xe sắp hết hạn đăng kiểm, bảo hiểm. |
| GET, POST | `/api/drivers` | Danh sách tài xế; thêm tài xế. |
| GET, PUT, DELETE | `/api/drivers/{id}` | Chi tiết, sửa, xóa tài xế. |
| PATCH | `/api/drivers/{id}/avatar` | Gắn ảnh đại diện. |
| GET | `/api/Images` | Danh sách ảnh đã upload. |
| POST | `/api/Images/Upload` | Upload ảnh (`.jpg`, `.jpeg`, `.png`, tối đa 5 MB). |

## Chuyến xe – `/api/trips`

| Phương thức | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/trips` | Mọi người | Danh sách chuyến. |
| GET | `/api/trips/{id}/seats` | Mọi người | Sơ đồ ghế của chuyến (ghế trống, đã bán). |
| GET | `/api/trips/search` | Mọi người | Tìm chuyến theo `fromStationId`, `toStationId`, `date`; lọc thêm `fromHour`, `toHour`, `vehicleTypeId`, `minPrice`, `maxPrice`, `tickets` (số ghế trống tối thiểu); `sort` (`time`, `time_desc`, `price`, `price_desc`); phân trang `page`, `pageSize` (tối đa 50). Trả `{ items, total, page, pageSize }`. |
| POST | `/api/trips` | Admin, Operator | Tạo chuyến. |
| POST | `/api/trips/generate-schedule` | Admin, Operator | Sinh lịch chạy. |
| PUT | `/api/trips/{id}/change-vehicle` | Admin, Operator | Đổi xe, tài xế. |
| PATCH | `/api/trips/{id}/status` | Admin, Operator | Đổi trạng thái (`Open`, `Delayed`, `Cancelled`…). |

## Đặt vé, thanh toán, vé

| Phương thức | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/bookings` | Customer, Seller, Admin | Đặt vé, giữ ghế (hết hạn sau `Booking:HoldMinutes`), áp mã khuyến mãi. Body cần thêm `contactPhone` (bắt buộc, 10 số), `needTransfer` (true/false) và `transferAddress` (bắt buộc khi `needTransfer` = true, tối đa 300 ký tự). |
| GET | `/api/bookings` | Admin, Seller, Accountant | Danh sách đặt vé (lọc, sắp xếp, phân trang). |
| GET | `/api/bookings/{id}` | Đăng nhập | Chi tiết booking. |
| GET | `/api/bookings/by-code/{code}` | Mọi người | Tra cứu booking theo mã. |
| GET | `/api/bookings/mine` | Đăng nhập | Booking của chính mình; tham số `page`, `pageSize`, `status` (Holding, Paid, Cancelled, Expired). |
| GET | `/api/bookings/mine/{code}` | Đăng nhập | Chi tiết một đặt vé của chính mình: vé, tuyến, giờ đi, các giao dịch thanh toán. |
| POST | `/api/bookings/lookup` | Mọi người | Tra cứu không cần đăng nhập. Body `{ code, contact }`, `contact` là SĐT hoặc email đã dùng khi đặt. Sai mã hay sai thông tin đều trả 404. |
| GET | `/api/bookings/{id}/refund-preview` | Đăng nhập | Xem trước số tiền hoàn nếu hủy. |
| PUT | `/api/bookings/{id}/cancel` | Đăng nhập | Hủy booking, hoàn tiền theo chính sách (≥24 giờ: 100%, 12–24 giờ: 70%, 2–12 giờ: 50%, dưới 2 giờ: 0%). |
| POST | `/api/payments` | Đăng nhập | Thanh toán (`Cash`: thu ngay; `BankTransfer`: chờ xác nhận). |
| PUT | `/api/payments/{id}/confirm` | Admin, Seller, Accountant | Xác nhận đã nhận chuyển khoản. |
| GET | `/api/payments/booking/{bookingId}` | Đăng nhập | Các giao dịch của một booking. |
| GET | `/api/tickets/lookup/{code}` | Mọi người | Tra cứu vé theo mã. |
| GET | `/api/tickets/{code}/qr` | Mọi người | Ảnh QR của vé. |
| PUT | `/api/tickets/{id}/change` | Đăng nhập | Đổi ghế hoặc đổi sang chuyến cùng tuyến (trước giờ đi ít nhất 2 giờ). |

## Khách hàng, khuyến mãi, đánh giá

| Phương thức | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| GET, POST | `/api/customers` | Admin, Seller | Danh sách, thêm khách hàng. |
| GET, PUT, DELETE | `/api/customers/{id}` | Admin, Seller | Chi tiết, sửa, xóa. |
| GET | `/api/customers/{id}/history` | Admin, Seller | Lịch sử đặt vé. |
| GET, POST | `/api/promotions` | Admin | Danh sách, thêm mã khuyến mãi. |
| PUT, DELETE | `/api/promotions/{id}` | Admin | Sửa, xóa. |
| PATCH | `/api/promotions/{id}/active` | Admin | Bật hoặc tắt mã. |
| POST | `/api/promotions/validate` | Đăng nhập | Kiểm tra mã dùng được không và số tiền giảm. |
| POST | `/api/reviews` | Đăng nhập | Đánh giá chuyến đã đi (1–5 sao); mỗi vé đánh giá một lần. |
| GET | `/api/trips/{tripId}/reviews` | Mọi người | Đánh giá của một chuyến. |
| GET | `/api/reviews/mine` | Đăng nhập | Đánh giá của chính mình. |

## Ghi chú còn tồn đọng

- `QuanLyXe` và `QuanLyTaiXe` là trang MVC tạm nằm trong project API, đang mở `[AllowAnonymous]`. Khi trang Xe và Tài xế chuyển sang project Web thì xóa hai trang này.
- Chưa có API đổi tên loại xe (`PUT /api/vehicle-types/{id}`).
