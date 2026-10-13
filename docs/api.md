# Tài liệu API

Địa chỉ gốc: `https://localhost:7200`. Có Swagger tại `/swagger` (chỉ môi trường Development), dùng nút **Authorize** để dán token.

## Quy ước chung

- **Xác thực:** JWT. Đăng nhập tại `POST /api/Auth/Login`, lấy `jwtToken`, gửi kèm mọi lời gọi: `Authorization: Bearer <token>`. Token sống 60 phút.
- **Mặc định phải đăng nhập.** API công khai được ghi rõ `[AllowAnonymous]` (cột Quyền ghi "Mọi người").
- **Vai trò:** `Admin`, `Operator`, `Seller`, `Accountant`, `Customer`. Vai trò nằm trong token, nên đổi vai trò có hiệu lực ở lần đăng nhập sau.
- **Lỗi nghiệp vụ** trả JSON `{ "message": "..." }` kèm mã 400, 404 hoặc 409. Lỗi dữ liệu nhập trả 400 theo chuẩn ProblemDetails. Chưa đăng nhập: 401. Không đủ quyền: 403.
- **Ngày giờ** theo giờ máy chủ. Tham số ngày dạng `yyyy-MM-dd`.
- **Phân trang:** tên tham số và dạng kết quả khác nhau giữa các API. Người dùng, báo cáo, đặt vé dùng `page`, `pageSize`; xe, tài xế, chuyến xe dùng `pageNumber`, `pageSize`, `sortBy`, `isAscending`. Xem Swagger để biết chính xác từng API.

> Cột "Quyền" lấy từ các thuộc tính `[Authorize]` trong mã nguồn. Khi sửa quyền ở controller, cập nhật lại bảng này.

## Tài khoản – `/api/Auth`

| Phương thức | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/Auth/Register` | Mọi người | Đăng ký. Người lạ luôn là `Customer`; chỉ Admin đang đăng nhập mới gán được vai trò khác. Trả `{ message, requiresVerification, emailSent }`; tự đăng ký thì phải xác thực email bằng mã OTP. Đăng ký lại email chưa xác thực sẽ đặt lại mật khẩu theo lần mới và gửi lại mã. |
| POST | `/api/Auth/verify-email` | Mọi người | Body `{ email, code }` (mã 6 số, hiệu lực khoảng 9 phút). Sai 5 lần thì tạm khóa 5 phút (429). |
| POST | `/api/Auth/resend-otp` | Mọi người | Body `{ email }`. Gửi lại mã, tối thiểu 60 giây một lần (429 nếu gửi dồn). Luôn trả cùng một thông báo. |
| POST | `/api/Auth/Login` | Mọi người | Đăng nhập, trả `{ jwtToken }`. Tài khoản bị khóa hoặc chưa xác thực email trả 403. |
| GET | `/api/Auth/me` | Đăng nhập | Email và vai trò của token hiện tại. |

## Người dùng – `/api/Users` (chỉ Admin)

| Phương thức | Đường dẫn | Mô tả |
|---|---|---|
| GET | `/api/Users/roles` | Danh sách vai trò trong hệ thống. |
| GET | `/api/Users?keyword=&role=&page=1&pageSize=10` | Danh sách, tìm theo email, lọc theo vai trò, phân trang (tối đa 50/trang). |
| GET | `/api/Users/{id}` | Một người dùng. |
| PUT | `/api/Users/{id}/roles` | Đổi vai trò. Body `{ "roles": ["Seller"] }`. Ít nhất 1 vai trò; không bỏ Admin của chính mình hoặc Admin cuối cùng. |
| PATCH | `/api/Users/{id}/lock` | Khóa hoặc mở khóa. Body `{ "locked": true }`. Không tự khóa mình, không khóa Admin cuối cùng. |

## Tổng quan và báo cáo

| Phương thức | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/Dashboard/get-summary?days=7` | Admin, Operator | Doanh thu hôm nay và N ngày, chuyến đang chạy, vé bán hôm nay, tỉ lệ lấp đầy, doanh thu từng ngày. |
| GET | `/api/Dashboard/get-alerts?days=30` | Admin, Operator | Cảnh báo đăng kiểm, bảo hiểm, GPLX đã hết hoặc sắp hết hạn trong N ngày; chuyến trạng thái `Delayed`. |
| GET | `/api/Reports/get-revenue-by-route` | Admin, Accountant | Doanh thu theo tuyến. Tham số: `from`, `to`, `routeId`, `sortBy` (`revenue`, `tickets`, `payments`, `route`), `desc`, `page`, `pageSize`, `all` (true: trả toàn bộ để xuất Excel). Trả thêm `summary` với tổng trên toàn bộ kết quả lọc. |

Cách tính doanh thu: tiền đã thu (`Paid`) trừ tiền đã hoàn (`Refunded`), tính theo ngày thu.

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
| POST | `/api/bookings` | Đăng nhập | Đặt vé, giữ ghế (hết hạn sau `Booking:HoldMinutes`), áp mã khuyến mãi. |
| GET | `/api/bookings` | Admin, Seller | Danh sách đặt vé (lọc, sắp xếp, phân trang). |
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
