using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

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

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_opt.FromName, from));
        message.To.Add(MailboxAddress.Parse(m.To));
        message.Subject = m.Subject;
        message.Body = new BodyBuilder { HtmlBody = m.Html, TextBody = m.Text }.ToMessageBody();

        // Cổng 465 dùng SSL ngay khi kết nối, cổng 587 dùng STARTTLS (Gmail dùng 587)
        var secure = _opt.Port == 465 ? SecureSocketOptions.SslOnConnect
                   : _opt.EnableSsl ? SecureSocketOptions.StartTls
                   : SecureSocketOptions.None;

        using var client = new SmtpClient { Timeout = 15000 };
        await client.ConnectAsync(_opt.Host, _opt.Port, secure);

        if (!string.IsNullOrWhiteSpace(_opt.User))
        {
            // Mật khẩu ứng dụng Gmail hiển thị thành 4 nhóm cách nhau bởi dấu cách; bỏ dấu cách nếu lỡ dán cả vào
            var password = (_opt.Password ?? "").Replace(" ", "");
            if (password.Length == 0)
            {
                _logger.LogError("Chưa đặt mật khẩu gửi mail (Email:Password, đặt bằng dotnet user-secrets) nên không gửi được email tới {To}",
                    LogText.Clean(m.To));
                throw new EmailNotConfiguredException();
            }
            await client.AuthenticateAsync(_opt.User, password);
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
        _logger.LogInformation("Đã gửi email '{Subject}' tới {To}", m.Subject, LogText.Clean(m.To));
    }
}
