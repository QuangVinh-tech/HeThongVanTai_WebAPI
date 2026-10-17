using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HeThongVanTai.Tests;

/// <summary>Dữ liệu mẫu lấy từ BookingTestBase: tuyến Bến A - Bến B dài 100 km, 1 chuyến Open.</summary>
public class ShipmentServiceTests : BookingTestBase
{
    private ShipmentService Svc() =>
        new(Db, Options.Create(new ShippingOptions()), NullLogger<ShipmentService>.Instance);

    private CreateShipmentDto Dto() => new()
    {
        FromStationId = FromStation.Id,
        ToStationId = ToStation.Id,
        PickupMode = "Station",
        DeliveryMode = "Station",
        SenderName = "An",
        SenderPhone = "0901234567",
        ReceiverName = "Bình",
        ReceiverPhone = "0912345678",
        Parcels = { new ParcelDto { Name = "Quần áo", Weight = 2, Length = 10, Width = 10, Height = 10 } }
    };

    private async Task<int> ErrorStatus(Func<Task> act)
        => (await Assert.ThrowsAsync<ServiceException>(act)).Status;

    [Fact]
    public async Task Create_Ok_Calculates_Fee_And_Writes_First_Log()
    {
        var s = await Svc().CreateAsync(Dto(), "user-1", "khach@test.com", canDebt: false);

        Assert.StartsWith("VC", s.Code);
        Assert.Equal("Pending", s.Status);
        Assert.Equal("Unpaid", s.PaymentStatus);
        Assert.Equal(27000m, s.ShippingFee);   // 100km x 150 + 2kg x 6000
        Assert.Equal(27000m, s.TotalAmount);
        Assert.Single(s.Logs);
    }

    [Fact]
    public async Task Quote_Without_Route_Returns_400()
    {
        var c = new Station { Name = "Bến C" };
        Db.Add(c);
        Db.SaveChanges();
        var dto = Dto();
        dto.ToStationId = c.Id;

        Assert.Equal(400, await ErrorStatus(() => Svc().QuoteAsync(dto)));
    }

    [Fact]
    public async Task Same_Station_Returns_400()
    {
        var dto = Dto();
        dto.ToStationId = dto.FromStationId;
        Assert.Equal(400, await ErrorStatus(() => Svc().QuoteAsync(dto)));
    }

    [Fact]
    public async Task Door_Pickup_Without_Address_Returns_400()
    {
        var dto = Dto();
        dto.PickupMode = "Door";
        Assert.Equal(400, await ErrorStatus(() => Svc().CreateAsync(dto, "user-1", "a@b.c", false)));
    }

    [Fact]
    public async Task Debt_Not_Allowed_For_Customer_Returns_403()
    {
        var dto = Dto();
        dto.FeePayer = "Debt";
        Assert.Equal(403, await ErrorStatus(() => Svc().CreateAsync(dto, "user-1", "a@b.c", canDebt: false)));
        var s = await Svc().CreateAsync(dto, "user-1", "a@b.c", canDebt: true);
        Assert.Equal("Debt", s.FeePayer);
    }

    [Fact]
    public async Task Promo_Applies_Discount_And_Uses_Up_The_Limit()
    {
        Db.Add(new Promotion
        {
            Code = "GIAM10",
            DiscountType = "Percent",
            Value = 10,
            MinOrder = 0,
            StartAt = DateTime.Now.AddDays(-1),
            EndAt = DateTime.Now.AddDays(1),
            UsageLimit = 1
        });
        Db.SaveChanges();
        var dto = Dto();
        dto.PromoCode = "giam10";   // tự đổi thành chữ hoa

        var s = await Svc().CreateAsync(dto, "user-1", "a@b.c", false);

        Assert.Equal(2700m, s.DiscountAmount);
        Assert.Equal(24300m, s.TotalAmount);
        Assert.Equal(1, (await Db.Promotions.AsNoTracking().SingleAsync()).UsedCount);

        // lượt dùng đã hết nên đơn thứ hai bị từ chối
        Assert.Equal(400, await ErrorStatus(() => Svc().CreateAsync(dto, "user-1", "a@b.c", false)));
    }

    [Fact]
    public async Task ChangeStatus_Follows_Flow_And_Rejects_Skipping()
    {
        var s = await Svc().CreateAsync(Dto(), "user-1", "a@b.c", false);

        Assert.Equal(400, await ErrorStatus(() => Svc().ChangeStatusAsync(s.Id, "Delivered", null, "op@x.y")));

        await Svc().ChangeStatusAsync(s.Id, "PickedUp", "Đã lấy hàng", "op@x.y");
        var saved = await Db.Shipments.Include(x => x.Logs).AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.Equal("PickedUp", saved.Status);
        Assert.Equal(2, saved.Logs.Count);
    }

    [Fact]
    public async Task AssignTrip_Same_Route_Ok_Other_Route_400()
    {
        var s = await Svc().CreateAsync(Dto(), "user-1", "a@b.c", false);

        await Svc().AssignTripAsync(s.Id, MainTrip.Id, "op@x.y");
        Assert.Equal(MainTrip.Id, (await Db.Shipments.AsNoTracking().SingleAsync()).TripId);

        var c = new Station { Name = "Bến C" };
        var r2 = new BusRoute { Name = "B - C", FromStation = ToStation, ToStation = c, DistanceKm = 50, DurationMin = 60 };
        Db.AddRange(c, r2);
        Db.SaveChanges();
        var otherTrip = AddTrip(DateTime.Today.AddDays(4).AddHours(8), routeId: r2.Id);

        Assert.Equal(400, await ErrorStatus(() => Svc().AssignTripAsync(s.Id, otherTrip.Id, "op@x.y")));
    }

    [Fact]
    public async Task Cancel_Only_By_Owner_While_Pending()
    {
        var s = await Svc().CreateAsync(Dto(), "user-1", "a@b.c", false);

        Assert.Equal(404, await ErrorStatus(() => Svc().CancelAsync(s.Id, "user-khac", "x@y.z")));

        await Svc().CancelAsync(s.Id, "user-1", "a@b.c");
        Assert.Equal("Cancelled", (await Db.Shipments.AsNoTracking().SingleAsync()).Status);

        Assert.Equal(400, await ErrorStatus(() => Svc().CancelAsync(s.Id, "user-1", "a@b.c")));
    }

    [Fact]
    public async Task CollectFee_Twice_Returns_409()
    {
        var s = await Svc().CreateAsync(Dto(), "user-1", "a@b.c", false);

        await Svc().CollectFeeAsync(s.Id, "seller@x.y");
        Assert.Equal(409, await ErrorStatus(() => Svc().CollectFeeAsync(s.Id, "seller@x.y")));
    }
}