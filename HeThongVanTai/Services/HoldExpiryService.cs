namespace HeThongVanTai.Services;

public class HoldExpiryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    public HoldExpiryService(IServiceScopeFactory scopes) => _scopes = scopes;

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
                Console.WriteLine("HoldExpiryService lỗi: " + ex.Message);
            }

            try { await Task.Delay(TimeSpan.FromMinutes(1), ct); }
            catch (OperationCanceledException) { }
        }
    }
}