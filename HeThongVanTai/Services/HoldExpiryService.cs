namespace HeThongVanTai.Services;

public class HoldExpiryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<HoldExpiryService> _logger;

    public HoldExpiryService(IServiceScopeFactory scopes, ILogger<HoldExpiryService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<BookingService>();
                await svc.ReleaseExpiredAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "HoldExpiryService lỗi khi nhả giữ chỗ quá hạn");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(1), ct); }
            catch (OperationCanceledException) { }
        }
    }
}