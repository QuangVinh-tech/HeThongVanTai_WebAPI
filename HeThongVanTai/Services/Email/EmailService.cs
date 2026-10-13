using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace HeThongVanTai.Services.Email;

// Cấu hình SMTP (mục "Email" trong appsettings). Mật khẩu để trong User Secrets, không commit.
public class EmailOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
    public string FromName { get; set; } = "VậnTải HK";
}

public class EmailMessage
{
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Html { get; set; } = "";
    // Bản chữ thường, chỉ để ghi log khi chưa cấu hình SMTP ở môi trường Development
    public string Text { get; set; } = "";
}

// Ném ra khi chưa cấu hình SMTP (Email:Host trống) để nơi gọi biết là CHƯA gửi được email nào
public class EmailNotConfiguredException : Exception
{
    public EmailNotConfiguredException() : base("Chưa cấu hình SMTP (mục Email trong appsettings)") { }
}

public interface IEmailService
{
    Task SendAsync(EmailMessage message);
}

/// <summary>
/// Gửi email qua SMTP. Chưa cấu hình Host thì không gửi được: ở Development ghi nội dung vào log
/// (để thử mã OTP khi chưa có tài khoản SMTP), ở môi trường khác chỉ ghi lỗi, không ghi nội dung.
/// Cả hai trường hợp đều ném EmailNotConfiguredException để nơi gọi không báo nhầm là "đã gửi".
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailOptions _opt;
    private readonly IHostEnvironment _env;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailOptions> opt, IHostEnvironment env, ILogger<EmailService> logger)
    {
        _opt = opt.Value;
        _env = env;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage m)
    {
        if (string.IsNullOrWhiteSpace(_opt.Host))
        {
            if (_env.IsDevelopment())
                _logger.LogWarning("[EMAIL GIẢ LẬP - chưa cấu hình SMTP] Gửi tới {To}: {Subject}. Nội dung: {Text}",
                    LogText.Clean(m.To), m.Subject, m.Text);
            else
                _logger.LogError("Chưa cấu hình SMTP (mục Email trong appsettings) nên không gửi được email tới {To}",
                    LogText.Clean(m.To));
            throw new EmailNotConfiguredException();
        }

        var from = string.IsNullOrWhiteSpace(_opt.From) ? _opt.User : _opt.From;
        using var mail = new MailMessage
        {
            From = new MailAddress(from, _opt.FromName),
            Subject = m.Subject,
            Body = m.Html,
            IsBodyHtml = true
        };
        mail.To.Add(new MailAddress(m.To));

        using var client = new SmtpClient(_opt.Host, _opt.Port)
        {
            EnableSsl = _opt.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(_opt.User) ? null : new NetworkCredential(_opt.User, _opt.Password),
            Timeout = 15000
        };
        await client.SendMailAsync(mail);
        _logger.LogInformation("Đã gửi email '{Subject}' tới {To}", m.Subject, LogText.Clean(m.To));
    }
}
