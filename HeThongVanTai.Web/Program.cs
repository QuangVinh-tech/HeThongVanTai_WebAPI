using HeThongVanTai.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
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

// Web đăng nhập bằng cookie; token JWT của API được cất trong cookie (HttpOnly)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "vantai.auth";
        options.Cookie.HttpOnly = true;
        options.SlidingExpiration = false;
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
