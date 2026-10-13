using System.Security.Claims;
using HeThongVanTai.Controllers;
using HeThongVanTai.Models.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HeThongVanTai.Tests;

/// <summary>#28 Test đánh giá chuyến: chỉ vé đã thanh toán và chuyến đã kết thúc, mỗi vé một lần, đúng chủ vé.</summary>
public class ReviewsControllerTests : BookingTestBase
{
    private ReviewsController As(string userId)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "test");
        return new ReviewsController(Db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static int? Status(IActionResult r) => (r as ObjectResult)?.StatusCode;

    // Chuyến đã chạy xong 2 ngày trước
    private Models.Domain.Trip EndedTrip() => AddTrip(DateTime.Now.AddDays(-2));

    [Fact]
    public async Task DanhGia_VeHopLe_Tra201_VaLuuDiem()
    {
        var b = AddBooking(EndedTrip(), "Paid", 100_000m, "A1");
        var ticketId = b.Tickets.Single().Id;

        var r = await As("user-1").Create(new CreateReviewDto { TicketId = ticketId, Rating = 5, Comment = "  Xe sạch, đúng giờ  " });

        Assert.Equal(201, Status(r));
        Refresh();
        var review = Db.Reviews.Single();
        Assert.Equal(ticketId, review.TicketId);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Xe sạch, đúng giờ", review.Comment);   // đã cắt khoảng trắng
        Assert.Equal(MainCustomer.Id, review.CustomerId);
    }

    [Fact]
    public async Task DanhGia_LanThuHai_Tra409()
    {
        var b = AddBooking(EndedTrip(), "Paid", 100_000m, "A1");
        var ticketId = b.Tickets.Single().Id;
        await As("user-1").Create(new CreateReviewDto { TicketId = ticketId, Rating = 4 });

        var r = await As("user-1").Create(new CreateReviewDto { TicketId = ticketId, Rating = 1 });

        Assert.Equal(409, Status(r));
        Refresh();
        Assert.Single(Db.Reviews.ToList());
    }

    [Fact]
    public async Task DanhGia_ChuyenChuaKetThuc_Tra400()
    {
        var b = AddBooking(MainTrip, "Paid", 100_000m, "A1");   // chuyến chính khởi hành sau 3 ngày

        var r = await As("user-1").Create(new CreateReviewDto { TicketId = b.Tickets.Single().Id, Rating = 5 });

        Assert.Equal(400, Status(r));
    }

    [Fact]
    public async Task DanhGia_VeChuaThanhToan_Tra400()
    {
        var b = AddBooking(EndedTrip(), "Holding", 100_000m, "A1");

        var r = await As("user-1").Create(new CreateReviewDto { TicketId = b.Tickets.Single().Id, Rating = 5 });

        Assert.Equal(400, Status(r));
    }

    [Fact]
    public async Task DanhGia_VeDaHuy_Tra400()
    {
        var b = AddBooking(EndedTrip(), "Paid", 100_000m, "A1");
        var ticket = b.Tickets.Single();
        ticket.Status = "Cancelled";
        Db.SaveChanges();

        var r = await As("user-1").Create(new CreateReviewDto { TicketId = ticket.Id, Rating = 5 });

        Assert.Equal(400, Status(r));
    }

    [Fact]
    public async Task DanhGia_VeCuaNguoiKhac_Tra403()
    {
        var b = AddBooking(EndedTrip(), "Paid", 100_000m, "A1");

        var r = await As("user-2").Create(new CreateReviewDto { TicketId = b.Tickets.Single().Id, Rating = 5 });

        Assert.Equal(403, Status(r));
        Refresh();
        Assert.Empty(Db.Reviews.ToList());
    }

    [Fact]
    public async Task DanhGia_VeKhongTonTai_Tra404()
    {
        var r = await As("user-1").Create(new CreateReviewDto { TicketId = 99999, Rating = 5 });

        Assert.Equal(404, Status(r));
    }

    [Fact]
    public async Task DanhGia_ChuaDangNhap_Tra401()
    {
        var ctrl = new ReviewsController(Db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }   // không có người dùng
        };

        var r = await ctrl.Create(new CreateReviewDto { TicketId = 1, Rating = 5 });

        Assert.IsType<UnauthorizedResult>(r);
    }

    [Fact]
    public async Task XemDanhGiaChuyen_TinhDiemTrungBinh()
    {
        var trip = EndedTrip();
        var b = AddBooking(trip, "Paid", 200_000m, "A1", "A2");
        var ids = b.Tickets.Select(t => t.Id).ToList();
        await As("user-1").Create(new CreateReviewDto { TicketId = ids[0], Rating = 4, Comment = "Tốt" });
        await As("user-1").Create(new CreateReviewDto { TicketId = ids[1], Rating = 5 });

        var r = await As("user-1").ByTrip(trip.Id);

        var ok = Assert.IsType<OkObjectResult>(r);
        Assert.Equal(2, Prop<int>(ok.Value!, "count"));
        Assert.Equal(4.5, Prop<double>(ok.Value!, "average"));
        Assert.Equal(2, Prop<System.Collections.IEnumerable>(ok.Value!, "items").Cast<object>().Count());
    }

    [Fact]
    public async Task XemDanhGiaChuyen_ChuaCoDanhGia_DiemBang0()
    {
        var r = await As("user-1").ByTrip(MainTrip.Id);

        var ok = Assert.IsType<OkObjectResult>(r);
        Assert.Equal(0, Prop<int>(ok.Value!, "count"));
        Assert.Equal(0.0, Prop<double>(ok.Value!, "average"));
    }

    [Fact]
    public async Task VeDaDanhGia_TraDungDanhSachCuaToi()
    {
        var b = AddBooking(EndedTrip(), "Paid", 200_000m, "A1", "A2");
        var reviewedId = b.Tickets.First().Id;
        await As("user-1").Create(new CreateReviewDto { TicketId = reviewedId, Rating = 5 });

        var mine = Assert.IsType<OkObjectResult>(await As("user-1").Mine());
        var other = Assert.IsType<OkObjectResult>(await As("user-2").Mine());

        Assert.Equal(new List<int> { reviewedId }, Assert.IsType<List<int>>(mine.Value));
        Assert.Empty(Assert.IsType<List<int>>(other.Value));
    }
}