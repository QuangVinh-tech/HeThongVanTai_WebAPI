using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using HeThongVanTai.Controllers;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HeThongVanTai.Tests;

/// <summary>Test giai đoạn 2 phần của Tân: lịch chạy cố định, chuyến của tài xế, nhật ký thao tác, API công khai.</summary>
public class TanPhase3Tests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void SetUser(ControllerBase controller, string userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, userId + "@test.com")
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
    }

    private static JsonElement ToJson(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return JsonSerializer.SerializeToElement(ok.Value);
    }

    // Tuyến 360 phút, 1 xe, 2 tài xế (driver-1, driver-2 có tài khoản)
    private static AppDbContext SeedFleet()
    {
        var db = NewDb();
        db.Stations.AddRange(new Station { Id = 1, Name = "Đà Lạt" }, new Station { Id = 2, Name = "Sài Gòn" });
        db.BusRoutes.Add(new BusRoute { Id = 1, Name = "DL-SG", FromStationId = 1, ToStationId = 2, DurationMin = 360 });
        db.VehicleTypes.Add(new VehicleType { Id = 1, Name = "Limousine", SeatCount = 3 });
        db.Vehicles.AddRange(
            new Vehicle { Id = 1, Plate = "49B-111.11", VehicleTypeId = 1, Status = "Active", RegistrationExpiry = DateTime.Today.AddYears(1) },
            new Vehicle { Id = 2, Plate = "49B-222.22", VehicleTypeId = 1, Status = "Active", RegistrationExpiry = DateTime.Today.AddYears(1) });
        db.Drivers.AddRange(
            new Driver { Id = 1, FullName = "Tài xế 1", Status = "Active", LicenseExpiry = DateTime.Today.AddYears(2), UserId = "driver-1" },
            new Driver { Id = 2, FullName = "Tài xế 2", Status = "Active", LicenseExpiry = DateTime.Today.AddYears(2), UserId = "driver-2" });
        db.SaveChanges();
        return db;
    }

    private static Trip AddTrip(AppDbContext db, int vehicleId, int driverId, DateTime departAt, string status = "Open")
    {
        var t = new Trip
        {
            BusRouteId = 1,
            VehicleId = vehicleId,
            DriverId = driverId,
            DepartAt = departAt,
            ArriveAt = departAt.AddHours(6),
            Price = 100000,
            Status = status
        };
        db.Trips.Add(t);
        db.SaveChanges();
        return t;
    }

    private static GenerateScheduleDto Schedule(int vehicleId, int driverId, int fromOffset, int toOffset, params DayOfWeek[] days) => new()
    {
        BusRouteId = 1,
        VehicleId = vehicleId,
        DriverId = driverId,
        FromDate = DateTime.Today.AddDays(fromOffset),
        ToDate = DateTime.Today.AddDays(toOffset),
        DepartureTime = "08:00",
        DaysOfWeek = days.ToList(),
        Price = 200000
    };

    // ---------- Lịch chạy cố định ----------

    [Fact]
    public async Task Preview_VehicleConflict_FlagsOnlyThatDay()
    {
        var db = SeedFleet();
        AddTrip(db, vehicleId: 1, driverId: 2, DateTime.Today.AddDays(3).AddHours(9));   // trùng giờ với chuyến 08:00 ngày +3
        var c = new TripsController(db);

        var json = ToJson(await c.PreviewSchedule(Schedule(1, 1, 1, 5)));

        Assert.Equal(5, json.GetProperty("TotalItems").GetInt32());
        Assert.Equal(1, json.GetProperty("ConflictCount").GetInt32());
        var bad = json.GetProperty("Items").EnumerateArray().Single(i => !i.GetProperty("Ok").GetBoolean());
        Assert.Contains("Xe đã có chuyến khác", bad.GetProperty("Problem").GetString()!);
    }

    [Fact]
    public async Task Generate_DriverConflict_RejectsWholeSchedule()
    {
        var db = SeedFleet();
        AddTrip(db, vehicleId: 2, driverId: 1, DateTime.Today.AddDays(2).AddHours(10));  // tài xế 1 đã có chuyến
        var c = new TripsController(db);

        var result = await c.GenerateFixedSchedule(Schedule(1, 1, 1, 4));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(1, await db.Trips.CountAsync());   // không tạo chuyến nào
    }

    [Fact]
    public async Task Generate_SelectedWeekdays_CreatesOnlyThoseDays()
    {
        var db = SeedFleet();
        var c = new TripsController(db);
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Friday };
        var expected = Enumerable.Range(1, 14).Count(i => days.Contains(DateTime.Today.AddDays(i).DayOfWeek));

        var result = await c.GenerateFixedSchedule(Schedule(1, 1, 1, 14, days));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expected, await db.Trips.CountAsync());
        Assert.All(await db.Trips.ToListAsync(), t => Assert.Contains(t.DepartAt.DayOfWeek, days));
    }

    [Fact]
    public async Task Generate_ToDateBeforeFromDate_ReturnsBadRequest()
    {
        var c = new TripsController(SeedFleet());
        Assert.IsType<BadRequestObjectResult>(await c.GenerateFixedSchedule(Schedule(1, 1, 5, 2)));
    }

    // ---------- Chuyến của tài xế ----------

    [Fact]
    public async Task Mine_ReturnsOnlyOwnTrips_WithoutPrice()
    {
        var db = SeedFleet();
        AddTrip(db, 1, 1, DateTime.Today.AddDays(1).AddHours(8));
        AddTrip(db, 2, 2, DateTime.Today.AddDays(1).AddHours(8));
        var c = new TripsController(db);
        SetUser(c, "driver-1", AppRoles.Driver);

        var json = ToJson(await c.GetMine());

        var items = json.GetProperty("Items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.False(items[0].TryGetProperty("Price", out _));
    }

    [Fact]
    public async Task Mine_AccountNotLinkedToDriver_ReturnsNotFound()
    {
        var c = new TripsController(SeedFleet());
        SetUser(c, "driver-unknown", AppRoles.Driver);
        Assert.IsType<NotFoundObjectResult>(await c.GetMine());
    }

    [Fact]
    public async Task Driver_CannotSeeOrChangeOtherDriversTrip()
    {
        var db = SeedFleet();
        var other = AddTrip(db, 2, 2, DateTime.Today.AddHours(DateTime.Now.Hour));
        var c = new TripsController(db);
        SetUser(c, "driver-1", AppRoles.Driver);

        var detail = await c.GetMineDetail(other.Id);
        var status = await c.UpdateTripStatus(other.Id, new UpdateTripStatusDto { Status = "Running" });
        var passengers = await c.GetPassengers(other.Id);

        Assert.Equal(403, Assert.IsType<ObjectResult>(detail).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(status).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(passengers).StatusCode);
        Assert.Equal("Open", (await db.Trips.FindAsync(other.Id))!.Status);
    }

    [Fact]
    public async Task Driver_StartThenComplete_FollowsStatusOrder()
    {
        var db = SeedFleet();
        var trip = AddTrip(db, 1, 1, DateTime.Now.AddMinutes(30));
        var c = new TripsController(db);
        SetUser(c, "driver-1", AppRoles.Driver);

        // Chưa chạy thì chưa hoàn thành được
        Assert.IsType<BadRequestObjectResult>(await c.UpdateTripStatus(trip.Id, new UpdateTripStatusDto { Status = "Completed" }));

        Assert.IsType<OkObjectResult>(await c.UpdateTripStatus(trip.Id, new UpdateTripStatusDto { Status = "Running" }));
        Assert.Equal("Running", (await db.Trips.FindAsync(trip.Id))!.Status);

        Assert.IsType<OkObjectResult>(await c.UpdateTripStatus(trip.Id, new UpdateTripStatusDto { Status = "Completed" }));
        Assert.Equal("Completed", (await db.Trips.FindAsync(trip.Id))!.Status);
    }

    [Fact]
    public async Task Driver_CannotStartTooEarly_OrCancel()
    {
        var db = SeedFleet();
        var trip = AddTrip(db, 1, 1, DateTime.Now.AddDays(2));
        var c = new TripsController(db);
        SetUser(c, "driver-1", AppRoles.Driver);

        Assert.IsType<BadRequestObjectResult>(await c.UpdateTripStatus(trip.Id, new UpdateTripStatusDto { Status = "Running" }));
        Assert.IsType<BadRequestObjectResult>(await c.UpdateTripStatus(trip.Id, new UpdateTripStatusDto { Status = "Cancelled" }));
        Assert.Equal("Open", (await db.Trips.FindAsync(trip.Id))!.Status);
    }

    [Fact]
    public async Task Driver_Delay_RequiresLaterDepartureTime()
    {
        var db = SeedFleet();
        var depart = DateTime.Now.AddHours(3);
        var trip = AddTrip(db, 1, 1, depart);
        var c = new TripsController(db);
        SetUser(c, "driver-1", AppRoles.Driver);

        Assert.IsType<BadRequestObjectResult>(await c.UpdateTripStatus(trip.Id, new UpdateTripStatusDto { Status = "Delayed" }));
        Assert.IsType<OkObjectResult>(await c.UpdateTripStatus(trip.Id,
            new UpdateTripStatusDto { Status = "Delayed", NewDepartAt = depart.AddMinutes(45) }));
        Assert.Equal("Delayed", (await db.Trips.FindAsync(trip.Id))!.Status);
    }

    [Fact]
    public async Task Operator_CannotReopenCompletedTrip()
    {
        var db = SeedFleet();
        var trip = AddTrip(db, 1, 1, DateTime.Now.AddHours(-8), "Completed");
        var c = new TripsController(db);
        SetUser(c, "op-1", AppRoles.Operator);

        Assert.IsType<BadRequestObjectResult>(await c.UpdateTripStatus(trip.Id, new UpdateTripStatusDto { Status = "Open" }));
    }

    [Fact]
    public async Task Passengers_ListsActiveTicketsOnly()
    {
        var db = SeedFleet();
        var trip = AddTrip(db, 1, 1, DateTime.Today.AddDays(1).AddHours(8));
        db.Customers.Add(new Customer { Id = 1, FullName = "Khách" });
        db.Bookings.Add(new Booking { Id = 1, Code = "BK1", CustomerId = 1, Status = "Paid" });
        db.Tickets.AddRange(
            new Ticket { BookingId = 1, TripId = trip.Id, SeatCode = "A2", TicketCode = "VT2", PassengerName = "Hai", Price = 1, Status = "Active" },
            new Ticket { BookingId = 1, TripId = trip.Id, SeatCode = "A1", TicketCode = "VT1", PassengerName = "Một", Price = 1, Status = "Active" },
            new Ticket { BookingId = 1, TripId = trip.Id, SeatCode = "A3", TicketCode = "VT3", PassengerName = "Ba", Price = 1, Status = "Cancelled" });
        db.SaveChanges();
        var c = new TripsController(db);
        SetUser(c, "driver-1", AppRoles.Driver);

        var json = ToJson(await c.GetPassengers(trip.Id));

        var list = json.GetProperty("Passengers");
        Assert.Equal(2, list.GetArrayLength());
        Assert.Equal("A1", list[0].GetProperty("SeatCode").GetString());
        Assert.False(list[0].TryGetProperty("Price", out _));
    }

    // ---------- Nhật ký thao tác ----------

    private static AuditService NewAudit(AppDbContext db, string userId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId), new(ClaimTypes.Email, userId + "@test.com") };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return new AuditService(db, http, NullLogger<AuditService>.Instance);
    }

    [Fact]
    public async Task Audit_WritesWhoWhenWhat()
    {
        var db = NewDb();
        var audit = NewAudit(db, "admin-1", AppRoles.Admin);

        await audit.LogAsync("Update", "Vehicle", 7, "Sửa xe 49B-111.11");

        var row = await db.AuditLogs.SingleAsync();
        Assert.Equal("admin-1@test.com", row.UserName);
        Assert.Equal("admin-1", row.UserId);
        Assert.Equal("Vehicle", row.EntityType);
        Assert.Equal("7", row.EntityId);
        Assert.True((DateTime.Now - row.CreatedAt).TotalMinutes < 1);
    }

    [Fact]
    public async Task Audit_FailureDoesNotThrow()
    {
        var db = NewDb();
        var audit = NewAudit(db, "admin-1", AppRoles.Admin);
        await db.DisposeAsync();   // ghi vào context đã hủy sẽ lỗi, nhưng không được làm hỏng thao tác chính

        await audit.LogAsync("Create", "Vehicle", 1, "x");
    }

    [Fact]
    public async Task CreateVehicle_WritesAuditRow()
    {
        var db = NewDb();
        db.VehicleTypes.Add(new VehicleType { Id = 1, Name = "Limousine", SeatCount = 34 });
        await db.SaveChangesAsync();
        var c = new VehiclesController(db, NewAudit(db, "op-1", AppRoles.Operator));

        var result = await c.Create(new VehicleDto { Plate = "49b-999.99", VehicleTypeId = 1 });

        Assert.IsType<CreatedAtActionResult>(result);
        var row = await db.AuditLogs.SingleAsync();
        Assert.Equal("Create", row.Action);
        Assert.Equal("Vehicle", row.EntityType);
        Assert.Contains("49B-999.99", row.Detail);
    }

    [Fact]
    public async Task AuditLogs_FilterByEntityTypeAndPaging()
    {
        var db = NewDb();
        for (var i = 0; i < 5; i++)
            db.AuditLogs.Add(new AuditLog { CreatedAt = DateTime.Now.AddMinutes(-i), UserName = "a@x", Action = "Create", EntityType = "Vehicle", EntityId = i.ToString() });
        db.AuditLogs.Add(new AuditLog { CreatedAt = DateTime.Now, UserName = "b@x", Action = "Delete", EntityType = "Driver", EntityId = "1" });
        await db.SaveChangesAsync();
        var c = new AuditLogsController(db);

        var json = ToJson(await c.GetAll(null, "Vehicle", null, null, null, pageNumber: 1, pageSize: 2));

        Assert.Equal(5, json.GetProperty("TotalItems").GetInt32());
        Assert.Equal(3, json.GetProperty("TotalPages").GetInt32());
        Assert.Equal(2, json.GetProperty("Items").GetArrayLength());
    }

    [Fact]
    public void AuditLogs_OnlyAdminCanRead()
    {
        var attr = typeof(AuditLogsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(AppRoles.Admin, attr!.Roles);
    }

    // ---------- API công khai ----------

    [Fact]
    public void TemporaryAnonymousPages_AreRemoved()
    {
        var asm = typeof(TripsController).Assembly;
        Assert.Null(asm.GetType("HeThongVanTai.Controllers.QuanLyXeController"));
        Assert.Null(asm.GetType("HeThongVanTai.Controllers.QuanLyTaiXeController"));
    }

    [Fact]
    public void AnonymousEndpoints_MatchApprovedList()
    {
        var approved = new SortedSet<string>
        {
            "AuthController.Register", "AuthController.VerifyEmail", "AuthController.ResendOtp", "AuthController.Login",
            "BookingsController.Search", "BookingsController.GetByCode", "BookingsController.Lookup",
            "TripsController.GetTripSeats", "TripsController.GetTrips",
            "DanhMucController.GetStations", "DanhMucController.GetStationById",
            "DanhMucController.GetBusRoutes", "DanhMucController.GetBusRouteById",
            "TicketsController.Lookup", "TicketsController.Qr",
            "ReviewsController.ByTrip",
            "PaymentsController.SePayWebhook",
            "ShipmentsController.Quote", "ShipmentsController.Track"   // báo giá và tra cứu vận đơn (phần Vận chuyển)
        };

        var found = new SortedSet<string>();
        foreach (var type in typeof(TripsController).Assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            var classAnon = type.GetCustomAttribute<AllowAnonymousAttribute>() != null;
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var isAction = m.GetCustomAttributes().Any(a => a is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute);
                if (isAction && (classAnon || m.GetCustomAttribute<AllowAnonymousAttribute>() != null))
                    found.Add(type.Name + "." + m.Name);
            }
        }

        Assert.Equal(approved, found);
    }
}
