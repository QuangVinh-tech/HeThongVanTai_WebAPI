using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HeThongVanTai.Tests;

/// <summary>Yêu cầu đổi ghế: khách gửi, Admin xác nhận (ghế mới phải còn trống) hoặc từ chối.</summary>
public class SeatChangeServiceTests : BookingTestBase
{
    private const string Owner = "user-1";   // MainCustomer
    private const string Admin = "admin@test.com";

    private SeatChangeService Service() => new(Db, Svc(), NullLogger<SeatChangeService>.Instance);

    private Ticket PaidTicket(string seat = "A1", Trip? trip = null) =>
        AddBooking(trip ?? MainTrip, "Paid", 100_000m, seat).Tickets.Single();

    private static CreateSeatChangeDto Dto(int ticketId, string seat, string? reason = null) =>
        new() { TicketId = ticketId, NewSeatCode = seat, Reason = reason };

    private static async Task<int> StatusOf(Func<Task> act) => (await Assert.ThrowsAsync<ServiceException>(act)).Status;

    // ---------------- Khách gửi yêu cầu ----------------

    [Fact]
    public async Task Gui_yeu_cau_luu_o_trang_thai_cho_va_chua_doi_ghe()
    {
        var ticket = PaidTicket("A1");

        var item = await Service().CreateAsync(Dto(ticket.Id, "a3", "  muốn ngồi gần cửa sổ  "), Owner);

        Assert.Equal("Pending", item.Status);
        Assert.Equal("A1", item.FromSeat);
        Assert.Equal("A3", item.ToSeat);
        Assert.Equal("muốn ngồi gần cửa sổ", item.Reason);
        Refresh();
        Assert.Equal("A1", Db.Tickets.Single(t => t.Id == ticket.Id).SeatCode);   // chưa đổi
    }

    [Fact]
    public async Task Khach_khac_khong_gui_duoc_yeu_cau_cho_ve_cua_nguoi_khac()
    {
        var ticket = PaidTicket("A1");
        Assert.Equal(403, await StatusOf(() => Service().CreateAsync(Dto(ticket.Id, "A3"), "user-2")));
        Assert.Empty(Db.SeatChangeRequests);
    }

    [Fact]
    public async Task Ghe_trung_ghe_hien_tai_hoac_khong_thuoc_xe_bi_tu_choi()
    {
        var ticket = PaidTicket("A1");
        Assert.Equal(400, await StatusOf(() => Service().CreateAsync(Dto(ticket.Id, "A1"), Owner)));
        Assert.Equal(400, await StatusOf(() => Service().CreateAsync(Dto(ticket.Id, "Z9"), Owner)));
        Assert.Equal(400, await StatusOf(() => Service().CreateAsync(Dto(ticket.Id, "  "), Owner)));
    }

    [Fact]
    public async Task Ghe_da_co_nguoi_dat_bi_tu_choi_409()
    {
        var ticket = PaidTicket("A1");
        AddBooking(MainTrip, "Holding", 100_000m, "A2");

        Assert.Equal(409, await StatusOf(() => Service().CreateAsync(Dto(ticket.Id, "A2"), Owner)));
    }

    [Fact]
    public async Task Moi_ve_chi_co_mot_yeu_cau_cho()
    {
        var ticket = PaidTicket("A1");
        await Service().CreateAsync(Dto(ticket.Id, "A2"), Owner);

        Assert.Equal(409, await StatusOf(() => Service().CreateAsync(Dto(ticket.Id, "A3"), Owner)));
    }

    [Fact]
    public async Task Sat_gio_khoi_hanh_khong_gui_duoc()
    {
        var soon = AddTrip(DateTime.Now.AddHours(1));
        var ticket = PaidTicket("A1", soon);

        Assert.Equal(400, await StatusOf(() => Service().CreateAsync(Dto(ticket.Id, "A2"), Owner)));
    }

    [Fact]
    public async Task Ve_da_huy_khong_gui_duoc()
    {
        var ticket = PaidTicket("A1");
        ticket.Status = "Cancelled";
        Db.SaveChanges();

        Assert.Equal(400, await StatusOf(() => Service().CreateAsync(Dto(ticket.Id, "A2"), Owner)));
    }

    // ---------------- Admin xác nhận ----------------

    [Fact]
    public async Task Admin_xac_nhan_thi_ve_doi_ghe_that_va_ghi_nguoi_duyet()
    {
        var ticket = PaidTicket("A1");
        var req = await Service().CreateAsync(Dto(ticket.Id, "A3"), Owner);

        var done = await Service().ApproveAsync(req.Id, Admin, "ok");

        Assert.Equal("Approved", done.Status);
        Assert.Equal(Admin, done.ReviewedBy);
        Assert.NotNull(done.ReviewedAt);
        Refresh();
        Assert.Equal("A3", Db.Tickets.Single(t => t.Id == ticket.Id).SeatCode);
    }

    [Fact]
    public async Task Ghe_moi_bi_nguoi_khac_dat_truoc_khi_xac_nhan_thi_bao_409_va_yeu_cau_van_cho()
    {
        var ticket = PaidTicket("A1");
        var req = await Service().CreateAsync(Dto(ticket.Id, "A3"), Owner);
        AddBooking(MainTrip, "Holding", 100_000m, "A3");   // người khác đặt A3

        Assert.Equal(409, await StatusOf(() => Service().ApproveAsync(req.Id, Admin, null)));

        Refresh();
        Assert.Equal("Pending", Db.SeatChangeRequests.Single().Status);
        Assert.Equal("A1", Db.Tickets.Single(t => t.Id == ticket.Id).SeatCode);
    }

    [Fact]
    public async Task Yeu_cau_da_xu_ly_khong_xu_ly_lai_duoc()
    {
        var ticket = PaidTicket("A1");
        var req = await Service().CreateAsync(Dto(ticket.Id, "A3"), Owner);
        await Service().ApproveAsync(req.Id, Admin, null);

        Assert.Equal(409, await StatusOf(() => Service().ApproveAsync(req.Id, Admin, null)));
        Assert.Equal(409, await StatusOf(() => Service().RejectAsync(req.Id, Admin, "đã xử lý")));
    }

    [Fact]
    public async Task Nhan_vien_da_doi_ghe_khac_sau_khi_khach_gui_thi_khong_xac_nhan_duoc()
    {
        var ticket = PaidTicket("A1");
        var req = await Service().CreateAsync(Dto(ticket.Id, "A3"), Owner);
        await Svc().ChangeTicketAsync(ticket.Id, new ChangeTicketDto { NewSeatCode = "A2" });   // nhân viên đổi ngay

        Assert.Equal(409, await StatusOf(() => Service().ApproveAsync(req.Id, Admin, null)));
    }

    // ---------------- Admin từ chối ----------------

    [Fact]
    public async Task Tu_choi_can_ly_do_va_khong_doi_ghe()
    {
        var ticket = PaidTicket("A1");
        var req = await Service().CreateAsync(Dto(ticket.Id, "A3"), Owner);

        Assert.Equal(400, await StatusOf(() => Service().RejectAsync(req.Id, Admin, "   ")));

        var done = await Service().RejectAsync(req.Id, Admin, "Ghế dành cho khách đoàn");
        Assert.Equal("Rejected", done.Status);
        Assert.Equal("Ghế dành cho khách đoàn", done.ReviewNote);
        Refresh();
        Assert.Equal("A1", Db.Tickets.Single(t => t.Id == ticket.Id).SeatCode);
    }

    // ---------------- Khách hủy yêu cầu ----------------

    [Fact]
    public async Task Khach_huy_yeu_cau_dang_cho_va_co_the_gui_lai()
    {
        var ticket = PaidTicket("A1");
        var req = await Service().CreateAsync(Dto(ticket.Id, "A3"), Owner);

        var cancelled = await Service().CancelAsync(req.Id, Owner);

        Assert.Equal("Cancelled", cancelled.Status);
        var again = await Service().CreateAsync(Dto(ticket.Id, "A2"), Owner);
        Assert.Equal("Pending", again.Status);
    }

    [Fact]
    public async Task Chi_chu_ve_huy_duoc_va_chi_huy_yeu_cau_dang_cho()
    {
        var ticket = PaidTicket("A1");
        var req = await Service().CreateAsync(Dto(ticket.Id, "A3"), Owner);

        Assert.Equal(403, await StatusOf(() => Service().CancelAsync(req.Id, "user-2")));

        await Service().ApproveAsync(req.Id, Admin, null);
        Assert.Equal(409, await StatusOf(() => Service().CancelAsync(req.Id, Owner)));
    }

    // ---------------- Danh sách ----------------

    [Fact]
    public async Task Danh_sach_cua_toi_chi_co_yeu_cau_cua_chinh_khach()
    {
        var ticket = PaidTicket("A1");
        await Service().CreateAsync(Dto(ticket.Id, "A3"), Owner);

        Assert.Single(await Service().MineAsync(Owner));
        Assert.Empty(await Service().MineAsync("user-2"));
    }

    [Fact]
    public async Task Danh_sach_admin_cho_biet_ghe_moi_con_trong_hay_khong_va_dem_so_yeu_cau_cho()
    {
        var ticket = PaidTicket("A1");
        await Service().CreateAsync(Dto(ticket.Id, "A3"), Owner);

        var before = await Service().ListAsync("Pending", 1, 20);
        Assert.Equal(1, before.PendingCount);
        Assert.True(Assert.Single(before.Items).ToSeatAvailable);

        AddBooking(MainTrip, "Holding", 100_000m, "A3");   // ghế mới bị người khác đặt
        var after = await Service().ListAsync("Pending", 1, 20);
        Assert.False(Assert.Single(after.Items).ToSeatAvailable);
    }

    [Fact]
    public async Task Loc_theo_trang_thai()
    {
        var t1 = PaidTicket("A1");
        var t2 = PaidTicket("A2");
        var r1 = await Service().CreateAsync(Dto(t1.Id, "A3"), Owner);
        await Service().CreateAsync(Dto(t2.Id, "A3"), Owner);
        await Service().RejectAsync(r1.Id, Admin, "không được");

        Assert.Single((await Service().ListAsync("Pending", 1, 20)).Items);
        Assert.Single((await Service().ListAsync("Rejected", 1, 20)).Items);
        Assert.Equal(2, (await Service().ListAsync(null, 1, 20)).Total);
    }
}
