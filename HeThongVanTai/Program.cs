using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Repositories;
using HeThongVanTai.Services;
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

// Dich vu dat ve / thanh toan (phan cua Tan)
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddHostedService<HoldExpiryService>();

// Auth (phan cua Vinh)
builder.Services.AddMemoryCache();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITokenRepository, TokenRepository>();

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
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(option =>
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
    });
// Mọi API mặc định phải đăng nhập; API công khai thì ghi [AllowAnonymous] rõ ràng
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IImageRepository, LocalImageRepository>();

var app = builder.Build();

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

app.UseSerilogRequestLogging();   // mỗi request một dòng log (chỉ đường dẫn, không ghi body, không ghi token)
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();   // phải đứng trước UseAuthorization
app.UseAuthorization();
app.MapControllers();
app.Run();