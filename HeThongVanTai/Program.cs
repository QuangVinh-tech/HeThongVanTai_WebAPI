using System.Text;
using System.Text.Json.Serialization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Repositories;
using HeThongVanTai.Services;
using HeThongVanTai.Services.Audit;
using HeThongVanTai.Services.Email;
using Serilog;

// Logger tạm để ghi được cả lỗi khởi động; sau khi host dựng xong sẽ thay bằng cấu hình trong appsettings (mục "Serilog")
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Ghi log ra console và file Logs/api-<ngày>.log (cấu hình ở appsettings.json, mục "Serilog")
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddControllers(o => o.Filters.Add<ServiceExceptionFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

builder.Services.AddEndpointsApiExplorer();

// Swagger có nút Authorize để dán token
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Dán chuỗi token JWT vào đây (không cần gõ chữ Bearer)"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// 2 database: nghiệp vụ và tài khoản
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContext<VanTaiAuthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("VanTaiAuthConnection")));

// Kiểm tra sức khỏe (endpoint /health): kết nối được cả hai CSDL thì báo Healthy
builder.Services.AddHealthChecks()
    .AddCheck<DbHealthCheck<AppDbContext>>("csdl-nghiep-vu")
    .AddCheck<DbHealthCheck<VanTaiAuthDbContext>>("csdl-tai-khoan");

// Lưu khóa mã hóa ra thư mục cố định khi triển khai (DataProtection:KeysPath), nếu không mỗi lần khởi động lại
// container thì liên kết đặt lại mật khẩu đã gửi sẽ không dùng được nữa
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
    builder.Services.AddDataProtection().SetApplicationName("HeThongVanTai.Api").PersistKeysToFileSystem(new DirectoryInfo(keysPath));

// Dich vu dat ve / thanh toan (phan cua Tan)
builder.Services.AddScoped<ReportService>();   // báo cáo mở rộng (việc 34)
builder.Services.AddScoped<SettingsService>();   // cài đặt hệ thống (việc 33)
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<SeatChangeService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.Configure<ShippingOptions>(builder.Configuration.GetSection("Shipping"));
builder.Services.AddScoped<ShipmentService>();
builder.Services.AddHostedService<HoldExpiryService>();
builder.Services.AddScoped<IAuditService, AuditService>();   // nhật ký thao tác (việc 42)

// Auth (phan cua Vinh)
builder.Services.AddMemoryCache();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITokenRepository, TokenRepository>();
builder.Services.AddScoped<ConsentService>();         // lưu ngày đồng ý điều khoản (việc 36)
builder.Services.AddScoped<RefreshTokenService>();

builder.Services.AddIdentityCore<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddTokenProvider<DataProtectorTokenProvider<IdentityUser>>("VanTai")
    .AddEntityFrameworkStores<VanTaiAuthDbContext>()
    .AddDefaultTokenProviders();


builder.Services.Configure<IdentityOptions>(option =>
{
    option.Password.RequireDigit = false;
    option.Password.RequireLowercase = false;
    option.Password.RequireNonAlphanumeric = false;
    option.Password.RequireUppercase = false;
    option.Password.RequiredLength = 6;
    option.Password.RequiredUniqueChars = 1;

    // Khóa tạm sau nhiều lần sai mật khẩu (cấu hình ở mục "Auth" trong appsettings)
    option.Lockout.AllowedForNewUsers = true;
    option.Lockout.MaxFailedAccessAttempts = builder.Configuration.GetValue("Auth:MaxFailedAttempts", 5);
    option.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(builder.Configuration.GetValue("Auth:LockoutMinutes", 5));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(option =>
{
    option.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ClockSkew = TimeSpan.Zero,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };

    // Kiểm tra ở mỗi request: tài khoản đã bị khóa hoặc bị xóa thì token cũ bị từ chối ngay (401),
    // không phải chờ token hết hạn. Vai trò thì vẫn theo token (đổi vai trò có hiệu lực khi đăng nhập hoặc làm mới token).
    option.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<IdentityUser>>();
            var user = userId == null ? null : await users.FindByIdAsync(userId);
            if (user == null || await users.IsLockedOutAsync(user))
                context.Fail("Tài khoản đã bị khóa hoặc không còn tồn tại.");
        }
    };
});

// Liên kết đặt lại mật khẩu (token của Identity) hết hạn sau Auth:ResetLinkMinutes phút, mặc định 30.
// Mã OTP xác thực email dùng bộ tạo mã riêng nên không bị ảnh hưởng.
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromMinutes(Math.Max(1, builder.Configuration.GetValue("Auth:ResetLinkMinutes", 30))));

// Giới hạn số lần gọi API đăng nhập và đăng ký theo địa chỉ IP (429 khi vượt). Cấu hình ở mục "Security:AuthRateLimit".
builder.Services.AddRateLimiter(options =>
{
    var permit = Math.Max(1, builder.Configuration.GetValue("Security:AuthRateLimit:PermitLimit", 10));
    var window = Math.Max(1, builder.Configuration.GetValue("Security:AuthRateLimit:WindowSeconds", 60));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permit,
            Window = TimeSpan.FromSeconds(window),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Bạn thao tác quá nhiều lần. Vui lòng thử lại sau ít phút." }, cancellationToken);
    };
});

// Web gọi API qua proxy/localhost: đọc địa chỉ khách thật từ X-Forwarded-For, chỉ tin các proxy loopback
// và địa chỉ khai báo ở "Security:TrustedProxies" (để giới hạn theo IP khách, không theo IP của máy chủ Web)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var ip in builder.Configuration.GetSection("Security:TrustedProxies").Get<string[]>() ?? Array.Empty<string>())
        if (IPAddress.TryParse(ip, out var address)) options.KnownProxies.Add(address);
});
// Mọi API mặc định phải đăng nhập; API công khai thì ghi [AllowAnonymous] rõ ràng
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IImageRepository, LocalImageRepository>();

var app = builder.Build();

// Triển khai: tự áp migration và tạo Admin đầu tiên theo cấu hình (xem docs/trien-khai.md)
await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration, app.Logger);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Tao tai khoan Admin dau tien de test (chi chay o moi truong Development)
    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    const string adminEmail = "admin@vantai.com";
    var seededAdmin = await userManager.FindByEmailAsync(adminEmail);
    if (seededAdmin == null)
    {
        // EmailConfirmed = true: tài khoản test không cần nhập OTP
        var admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        if ((await userManager.CreateAsync(admin, "Admin@123")).Succeeded)
            await userManager.AddToRoleAsync(admin, AppRoles.Admin);
    }
    else if (!seededAdmin.EmailConfirmed)
    {
        seededAdmin.EmailConfirmed = true;
        await userManager.UpdateAsync(seededAdmin);
    }
}

app.UseForwardedHeaders();   // phải đứng đầu để các middleware sau thấy IP khách thật
app.UseSerilogRequestLogging();   // mỗi request một dòng log (chỉ đường dẫn, không ghi body, không ghi token)
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();   // phải đứng trước UseAuthorization
app.UseAuthorization();
app.UseRateLimiter();
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponse.WriteAsync }).AllowAnonymous();
app.MapControllers();
app.Run();