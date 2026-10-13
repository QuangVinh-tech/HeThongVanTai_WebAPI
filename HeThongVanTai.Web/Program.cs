using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("Thiếu cấu hình ApiBaseUrl trong appsettings.json");

builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<BearerTokenHandler>();

// Đăng ký các HttpClient cho từng Service chuyên biệt
builder.Services.AddHttpClient<TannApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<BearerTokenHandler>();

// Đăng ký các Scoped Services
builder.Services.AddScoped<DanhMucApiService>();
builder.Services.AddScoped<XeTaiXeApiService>();
builder.Services.AddScoped<DashboardApiService>();
builder.Services.AddScoped<UsersApiService>();
builder.Services.AddScoped<ReportsApiService>();

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

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();