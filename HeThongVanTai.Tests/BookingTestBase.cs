using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HeThongVanTai.Tests;

/// <summary>
/// Lớp nền cho test đặt vé / đánh giá: tạo CSDL SQLite trong bộ nhớ và dữ liệu mẫu.
/// Dữ liệu mẫu: tuyến A - B, loại xe 3 ghế (A1, A2, A3), 1 chuyến mở bán sau 3 ngày giá 100.000, 1 khách (user-1).
/// </summary>
public abstract class BookingTestBase : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    protected readonly AppDbContext Db;

    protected Station FromStation = null!;
    protected Station ToStation = null!;
    protected Trip MainTrip = null!;
    protected Customer MainCustomer = null!;

    protected BookingTestBase()
    {
        _conn.Open();
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        Db.Database.EnsureCreated();
        Seed();
    }

    public void Dispose()
    {
        Db.Dispose();
        _conn.Dispose();
    }

    private void Seed()
    {
        FromStation = new Station { Name = "Bến A" };
        ToStation = new Station { Name = "Bến B" };
        var type = new VehicleType
        {
            Name = "Xe 3 ghế",
            SeatCount = 3,
            SeatTemplates =
            {
                new SeatTemplate { SeatCode = "A1" },
                new SeatTemplate { SeatCode = "A2" },
                new SeatTemplate { SeatCode = "A3" }
            }
        };
        var route = new BusRoute { Name = "A - B", FromStation = FromStation, ToStation = ToStation, DistanceKm = 100, DurationMin = 120 };
        var vehicle = new Vehicle { Plate = "51B-00001", VehicleType = type };
        var driver = new Driver { FullName = "Tài xế Test" };

        var depart = DateTime.Today.AddDays(3).AddHours(8);
        MainTrip = new Trip
        {
            BusRoute = route,
            Vehicle = vehicle,
            Driver = driver,
            DepartAt = depart,
            ArriveAt = depart.AddHours(2),
            Price = 100_000m,
            Status = "Open"
        };
        MainCustomer = new Customer { FullName = "Khách Test", UserId = "user-1", Email = "khach@test.com", Phone = "0901234567" };

        Db.AddRange(MainTrip, MainCustomer);
        Db.SaveChanges();
    }

    // ---------- Hàm tạo dữ liệu dùng chung ----------

    protected BookingService Svc(int holdMinutes = 15)
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Booking:HoldMinutes"] = holdMinutes.ToString() })
            .Build();
        return new BookingService(Db, cfg, NullLogger<BookingService>.Instance);
    }

    /// <summary>Thêm chuyến cùng xe/tài xế với chuyến chính. Mặc định cùng tuyến, mở bán, đến sau 2 giờ.</summary>
    protected Trip AddTrip(DateTime departAt, decimal price = 100_000m, string status = "Open",
                           int? routeId = null, DateTime? arriveAt = null)
    {
        var t = new Trip
        {
            BusRouteId = routeId ?? MainTrip.BusRouteId,
            VehicleId = MainTrip.VehicleId,
            DriverId = MainTrip.DriverId,
            DepartAt = departAt,
            ArriveAt = arriveAt ?? departAt.AddHours(2),
            Price = price,
            Status = status
        };
        Db.Trips.Add(t);
        Db.SaveChanges();
        return t;
    }

    /// <summary>Thêm một tuyến khác (B - A) để thử đổi vé khác tuyến.</summary>
    protected BusRoute AddOtherRoute()
    {
        var r = new BusRoute
        {
            Name = "B - A",
            FromStationId = ToStation.Id,
            ToStationId = FromStation.Id,
            DistanceKm = 100,
            DurationMin = 120
        };
        Db.BusRoutes.Add(r);
        Db.SaveChanges();
        return r;
    }

    /// <summary>
    /// Thêm sẵn một booking với các ghế cho trước. status = "Paid" thì có sẵn 1 khoản thanh toán đã trả đủ.
    /// Giá mỗi vé = tổng tiền / số ghế.
    /// </summary>
    protected Booking AddBooking(Trip trip, string status, decimal total, params string[] seats)
    {
        var b = new Booking
        {
            Code = Rules.NewBookingCode(),
            CustomerId = MainCustomer.Id,
            Status = status,
            TotalAmount = total,
            CreatedAt = DateTime.Now,
            HoldExpiresAt = status == "Holding" ? DateTime.Now.AddMinutes(10) : null,
            Tickets = seats.Select(s => new Ticket
            {
                TripId = trip.Id,
                SeatCode = s,
                TicketCode = Rules.NewTicketCode(),
                PassengerName = "Hành khách " + s,
                Price = total / seats.Length,
                Status = "Active"
            }).ToList()
        };
        if (status == "Paid")
            b.Payments.Add(new Payment { Amount = total, Method = "BankTransfer", Status = "Paid", PaidAt = DateTime.Now });

        Db.Bookings.Add(b);
        Db.SaveChanges();
        return b;
    }

    protected Promotion AddPromo(string code, decimal percent = 10, DateTime? start = null, DateTime? end = null,
                                 int? limit = null, int used = 0, bool active = true)
    {
        var p = new Promotion
        {
            Code = code,
            DiscountType = "Percent",
            Value = percent,
            MinOrder = 0,
            StartAt = start ?? DateTime.Now.AddDays(-1),
            EndAt = end ?? DateTime.Now.AddDays(10),
            UsageLimit = limit,
            UsedCount = used,
            IsActive = active
        };
        Db.Promotions.Add(p);
        Db.SaveChanges();
        return p;
    }

    protected Models.DTO.CreateBookingDto NewBookingDto(Trip trip, string? promo = null, params string[] seats) => new()
    {
        CustomerId = MainCustomer.Id,
        TripId = trip.Id,
        PromoCode = promo,
        ContactPhone = "0901234567",
        Seats = seats.Select(s => new Models.DTO.SeatDto { SeatCode = s, PassengerName = "Khách " + s }).ToList()
    };

    /// <summary>Đọc kết quả service trả về dạng object ẩn danh.</summary>
    protected static T Prop<T>(object o, string name) =>
        (T)o.GetType().GetProperty(name)!.GetValue(o)!;

    /// <summary>Bỏ cache của DbContext để đọc lại dữ liệu thật sau các lệnh ExecuteUpdate.</summary>
    protected void Refresh() => Db.ChangeTracker.Clear();
}