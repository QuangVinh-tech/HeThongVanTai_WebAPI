using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongVanTai.Tests;

public class PaymentServiceTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly AppDbContext _db;

    public PaymentServiceTests()
    {
        _conn.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    // Tạo một booking đang giữ chỗ (chưa cần chuyến/vé vì thanh toán không dùng tới)
    private Booking SeedBooking(DateTime holdUntil)
    {
        var b = new Booking
        {
            Code = "BKTEST",
            Customer = new Customer { FullName = "Khach Test" },
            TotalAmount = 200_000m,
            Status = "Holding",
            HoldExpiresAt = holdUntil
        };
        _db.Bookings.Add(b);
        _db.SaveChanges();
        return b;
    }

    [Fact]
    public async Task TienMat_DuocPaidNgay()
    {
        var b = SeedBooking(DateTime.Now.AddMinutes(10));

        await new PaymentService(_db, Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentService>.Instance).CreateAsync(new PayDto { BookingId = b.Id, Method = "Cash" });

        var booking = await _db.Bookings.FindAsync(b.Id);
        Assert.Equal("Paid", booking!.Status);
        Assert.Equal("Paid", _db.Payments.Single().Status);
        Assert.Equal(200_000m, _db.Payments.Single().Amount);
    }

    [Fact]
    public async Task ChuyenKhoan_ChoXacNhan_RoiMoiPaid()
    {
        var b = SeedBooking(DateTime.Now.AddMinutes(10));
        var svc = new PaymentService(_db, Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentService>.Instance);

        await svc.CreateAsync(new PayDto { BookingId = b.Id, Method = "BankTransfer" });
        Assert.Equal("Holding", (await _db.Bookings.FindAsync(b.Id))!.Status);
        Assert.Equal("Pending", _db.Payments.Single().Status);

        await svc.ConfirmAsync(_db.Payments.Single().Id);
        Assert.Equal("Paid", (await _db.Bookings.FindAsync(b.Id))!.Status);
        Assert.Equal("Paid", _db.Payments.Single().Status);
    }

    [Fact]
    public async Task HetHanGiuCho_BaoLoi400()
    {
        var b = SeedBooking(DateTime.Now.AddMinutes(-1));

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            new PaymentService(_db, Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentService>.Instance).CreateAsync(new PayDto { BookingId = b.Id, Method = "Cash" }));

        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task ThanhToanHaiLan_BaoLoi409()
    {
        var b = SeedBooking(DateTime.Now.AddMinutes(10));
        var svc = new PaymentService(_db, Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentService>.Instance);
        await svc.CreateAsync(new PayDto { BookingId = b.Id, Method = "BankTransfer" });

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            svc.CreateAsync(new PayDto { BookingId = b.Id, Method = "BankTransfer" }));

        Assert.Equal(409, ex.Status);
    }
}