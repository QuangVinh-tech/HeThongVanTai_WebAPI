using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

// Logger tạm để ghi được cả lỗi khởi động; sau đó thay bằng cấu hình trong appsettings (mục "Serilog")
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Ghi log ra console và file Logs/web-<ngày>.log
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Địa chỉ API đặt trong appsettings (ApiBaseUrl), không viết cứng trong controller
var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("Thiếu cấu hình ApiBaseUrl trong appsettings.json");

// Mọi lời gọi API đi qua ApiClient; BearerTokenHandler tự gắn token của người đang đăng nhập
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<BearerTokenHandler>();

// Client riêng (không gắn Bearer) để làm mới token trong cookie; xem TokenRefresher
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("api-refresh", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Endpoint /health: Web chạy và gọi được API thì báo Healthy
builder.Services.AddHealthChecks().AddCheck<ApiHealthCheck>("api");

// Lưu khóa mã hóa ra thư mục cố định khi triển khai (DataProtection:KeysPath), nếu không mỗi lần khởi động lại
// container thì cookie đăng nhập và mã chống giả mạo form cũ đều mất hiệu lực
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
    builder.Services.AddDataProtection().SetApplicationName("HeThongVanTai.Web").PersistKeysToFileSystem(new DirectoryInfo(keysPath));

// Service gọi API theo từng module
builder.Services.AddScoped<DanhMucApiService>();
builder.Services.AddScoped<DashboardApiService>();
builder.Services.AddScoped<UsersApiService>();
builder.Services.AddScoped<ReportsApiService>();
builder.Services.AddScoped<VeKhachApiService>();
builder.Services.AddScoped<XeTaiXeApiService>();   // Xe, Tài xế (Admin/Vehicles, Admin/Drivers)
builder.Services.AddScoped<VanChuyenApiService>();   // Vận chuyển hàng hóa
builder.Services.AddScoped<CustomersApiService>();     // Khách hàng (Admin/Customers)
builder.Services.AddScoped<SchedulesApiService>();     // Lịch chạy cố định (Admin/Schedules)
builder.Services.AddScoped<DriverTripsApiService>();   // Chuyến của tôi (Driver/Trips)
builder.Services.AddScoped<AuditLogsApiService>();     // Nhật ký thao tác (Admin/AuditLogs)
builder.Services.AddScoped<SettingsApiService>();       // Cài đặt hệ thống + thông tin công ty (chân trang)

// Tài khoản nhận chuyển khoản để tạo mã QR (mục "BankTransfer" trong appsettings.json)
builder.Services.Configure<BankQrOptions>(builder.Configuration.GetSection("BankTransfer"));

// Web đăng nhập bằng cookie; token JWT của API được cất trong cookie (HttpOnly)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "vantai.auth";
        options.Cookie.HttpOnly = true;
        options.SlidingExpiration = false;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);   // cùng hạn refresh token của API; hạn thật do API quyết định
        // Access token ngắn hạn: tự làm mới bằng refresh token, API từ chối thì đăng xuất
        options.Events.OnValidatePrincipal = TokenRefresher.ValidateAsync;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSerilogRequestLogging();   // mỗi request một dòng log (chỉ đường dẫn, không ghi form hay token)
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();   // phải đứng trước UseAuthorization
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponse.WriteAsync });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
