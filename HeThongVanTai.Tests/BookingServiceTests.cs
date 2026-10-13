using HeThongVanTai.Models.DTO;
using HeThongVanTai.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongVanTai.Tests;

/// <summary>#28 Test đặt vé: giữ ghế, mã giảm giá, hết hạn giữ chỗ, hủy và hoàn tiền, đổi vé, tìm chuyến.</summary>
public class BookingServiceTests : BookingTestBase
{
    // ===================== Đặt vé và giữ ghế =====================

    [Fact]
    public async Task DatVe_GiuGhe_TaoBookingHolding_TinhDungTien_VaHanGiuCho()
    {
        var (id, _) = await Svc(holdMinutes: 15).CreateAsync(NewBookingDto(MainTrip, null, "A1", "A2"));

        Refresh();
        var b = Db.Bookings.Include(x => x.Tickets).Single(x => x.Id == id);
        Assert.Equal("Holding", b.Status);
        Assert.Equal(200_000m, b.TotalAmount);
        Assert.Equal(0m, b.DiscountAmount);
        Assert.Equal(2, b.Tickets.Count);
        Assert.All(b.Tickets, t => Assert.Equal("Active", t.Status));
        Assert.All(b.Tickets, t => Assert.StartsWith("VT", t.TicketCode));
        // Hạn giữ chỗ theo cấu hình 15 phút
        Assert.InRange((b.HoldExpiresAt!.Value - DateTime.Now).TotalMinutes, 14, 15.1);
    }

    [Fact]
    public async Task DatVe_GheDaCoNguoiDat_Tra409()
    {
        AddBooking(MainTrip, "Holding", 100_000m, "A1");

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            Svc().CreateAsync(NewBookingDto(MainTrip, null, "A1")));

        Assert.Equal(409, ex.Status);
    }

    [Fact]
    public async Task DatVe_GheDaHuy_DatLaiDuoc()
    {
        var old = AddBooking(MainTrip, "Holding", 100_000m, "A1");
        await Svc().CancelAsync(old.Id);

        var (id, _) = await Svc().CreateAsync(NewBookingDto(MainTrip, null, "A1"));

        Refresh();
        Assert.Equal("Holding", Db.Bookings.Single(x => x.Id == id).Status);
    }

    [Fact]
    public async Task DatVe_MaGheKhongThuocLoaiXe_Tra400()
    {
        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            Svc().CreateAsync(NewBookingDto(MainTrip, null, "Z9")));

        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task DatVe_TrungMaGheTrongYeuCau_Tra400()
    {
        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            Svc().CreateAsync(NewBookingDto(MainTrip, null, "A1", "a1")));

        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task DatVe_ChuyenDaKhoiHanh_Tra400()
    {
        var past = AddTrip(DateTime.Now.AddHours(-1));

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            Svc().CreateAsync(NewBookingDto(past, null, "A1")));

        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task DatVe_ChuyenKhongTonTai_Tra404()
    {
        var dto = NewBookingDto(MainTrip, null, "A1");
        dto.TripId = 99999;

        var ex = await Assert.ThrowsAsync<ServiceException>(() => Svc().CreateAsync(dto));

        Assert.Equal(404, ex.Status);
    }

    // ===================== Mã giảm giá =====================

    [Fact]
    public async Task DatVe_MaGiamGiaHopLe_TinhDungTong_VaTangLuotDung()
    {
        var promo = AddPromo("GIAM10", percent: 10, limit: 5);

        var (id, _) = await Svc().CreateAsync(NewBookingDto(MainTrip, "giam10", "A1", "A2"));   // gõ chữ thường vẫn nhận

        Refresh();
        var b = Db.Bookings.Single(x => x.Id == id);
        Assert.Equal(20_000m, b.DiscountAmount);
        Assert.Equal(180_000m, b.TotalAmount);
        Assert.Equal(promo.Id, b.PromotionId);
        Assert.Equal(1, Db.Promotions.Single(p => p.Id == promo.Id).UsedCount);
    }

    [Fact]
    public async Task DatVe_MaHetHan_Tra400()
    {
        AddPromo("CU", start: DateTime.Now.AddDays(-20), end: DateTime.Now.AddDays(-1));

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            Svc().CreateAsync(NewBookingDto(MainTrip, "CU", "A1")));

        Assert.Equal(400, ex.Status);
        Refresh();
        Assert.Empty(Db.Bookings);   // không tạo booking khi mã sai
    }

    [Fact]
    public async Task DatVe_MaHetLuotDung_Tra400()
    {
        AddPromo("HET", limit: 1, used: 1);

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            Svc().CreateAsync(NewBookingDto(MainTrip, "HET", "A1")));

        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task DatVe_MaBiTat_Tra400()
    {
        AddPromo("TAT", active: false);

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            Svc().CreateAsync(NewBookingDto(MainTrip, "TAT", "A1")));

        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task DatVe_MaKhongTonTai_Tra400()
    {
        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            Svc().CreateAsync(NewBookingDto(MainTrip, "KHONGCO", "A1")));

        Assert.Equal(400, ex.Status);
    }

    // ===================== Hết hạn giữ chỗ =====================

    [Fact]
    public async Task HetHanGiuCho_NhaGhe_HoanLuotMa_VaDatLaiDuoc()
    {
        var promo = AddPromo("GIAM10", limit: 5);
        var (id, _) = await Svc().CreateAsync(NewBookingDto(MainTrip, "GIAM10", "A1"));

        // Cho booking quá hạn giữ chỗ
        Refresh();
        var b = Db.Bookings.Single(x => x.Id == id);
        b.HoldExpiresAt = DateTime.Now.AddMinutes(-1);
        Db.SaveChanges();

        var released = await Svc().ReleaseExpiredAsync();

        Assert.Equal(1, released);
        Refresh();
        Assert.Equal("Expired", Db.Bookings.Single(x => x.Id == id).Status);
        Assert.All(Db.Tickets.Where(t => t.BookingId == id).ToList(), t => Assert.Equal("Cancelled", t.Status));
        Assert.Equal(0, Db.Promotions.Single(p => p.Id == promo.Id).UsedCount);

        // Người khác đặt lại đúng ghế đó được
        var (id2, _) = await Svc().CreateAsync(NewBookingDto(MainTrip, null, "A1"));
        Refresh();
        Assert.Equal("Holding", Db.Bookings.Single(x => x.Id == id2).Status);
    }

    [Fact]
    public async Task HetHanGiuCho_KhongDungVaoBookingConHan()
    {
        AddBooking(MainTrip, "Holding", 100_000m, "A1");   // còn hạn 10 phút

        var released = await Svc().ReleaseExpiredAsync();

        Assert.Equal(0, released);
    }

    [Fact]
    public async Task HetHanGiuCho_KhongDungVaoBookingDaThanhToan()
    {
        var b = AddBooking(MainTrip, "Paid", 100_000m, "A1");
        b.HoldExpiresAt = DateTime.Now.AddMinutes(-30);
        Db.SaveChanges();

        var released = await Svc().ReleaseExpiredAsync();

        Assert.Equal(0, released);
        Refresh();
        Assert.Equal("Paid", Db.Bookings.Single(x => x.Id == b.Id).Status);
    }

    // ===================== Hủy và hoàn tiền =====================

    [Fact]
    public async Task Huy_DangGiuCho_NhaGhe_KhongHoanTien()
    {
        var promo = AddPromo("GIAM10", limit: 5);
        var (id, _) = await Svc().CreateAsync(NewBookingDto(MainTrip, "GIAM10", "A1", "A2"));

        await Svc().CancelAsync(id);

        Refresh();
        Assert.Equal("Cancelled", Db.Bookings.Single(x => x.Id == id).Status);
        Assert.All(Db.Tickets.Where(t => t.BookingId == id).ToList(), t => Assert.Equal("Cancelled", t.Status));
        Assert.Empty(Db.Payments.Where(p => p.Status == "Refunded").ToList());
        Assert.Equal(0, Db.Promotions.Single(p => p.Id == promo.Id).UsedCount);   // trả lại lượt dùng mã
    }

    // Chính sách: từ 24 giờ 100%, 12 đến 24 giờ 70%, 2 đến 12 giờ 50%, dưới 2 giờ 0%. Đã trả 200.000
    [Theory]
    [InlineData(30, 200_000)]
    [InlineData(18, 140_000)]
    [InlineData(5, 100_000)]
    [InlineData(1, 0)]
    public async Task Huy_DaThanhToan_HoanTheoChinhSach(int hoursBeforeDepart, int expectedRefund)
    {
        var trip = AddTrip(DateTime.Now.AddHours(hoursBeforeDepart));
        var b = AddBooking(trip, "Paid", 200_000m, "A1", "A2");

        var result = await Svc().CancelAsync(b.Id);

        Assert.Equal((decimal)expectedRefund, Prop<decimal>(result, "RefundAmount"));
        Refresh();
        Assert.Equal("Cancelled", Db.Bookings.Single(x => x.Id == b.Id).Status);
        Assert.All(Db.Tickets.Where(t => t.BookingId == b.Id).ToList(), t => Assert.Equal("Cancelled", t.Status));

        var refunds = Db.Payments.Where(p => p.BookingId == b.Id && p.Status == "Refunded").ToList();
        Assert.Equal((decimal)expectedRefund, refunds.Sum(p => p.Amount));
        Assert.Equal(expectedRefund > 0 ? 1 : 0, refunds.Count);
    }

    [Fact]
    public async Task Huy_DaThanhToan_ChuyenDaKhoiHanh_Tra400()
    {
        var trip = AddTrip(DateTime.Now.AddHours(-1));
        var b = AddBooking(trip, "Paid", 100_000m, "A1");

        var ex = await Assert.ThrowsAsync<ServiceException>(() => Svc().CancelAsync(b.Id));

        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task Huy_BookingDaHuy_Tra400()
    {
        var b = AddBooking(MainTrip, "Cancelled", 100_000m, "A1");

        var ex = await Assert.ThrowsAsync<ServiceException>(() => Svc().CancelAsync(b.Id));

        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task Huy_BookingKhongTonTai_Tra404()
    {
        var ex = await Assert.ThrowsAsync<ServiceException>(() => Svc().CancelAsync(99999));

        Assert.Equal(404, ex.Status);
    }

    [Fact]
    public async Task XemTruocHoanTien_KhopVoiSoTienThucHoan()
    {
        var trip = AddTrip(DateTime.Now.AddHours(18));   // 70%
        var b = AddBooking(trip, "Paid", 200_000m, "A1", "A2");

        var preview = await Svc().RefundPreviewAsync(b.Id);

        Assert.True(Prop<bool>(preview, "CanCancel"));
        Assert.Equal(0.7m, Prop<decimal>(preview, "RefundRate"));
        Assert.Equal(140_000m, Prop<decimal>(preview, "RefundAmount"));
    }

    [Fact]
    public async Task XemTruocHoanTien_ChuyenDaDi_KhongChoHuy()
    {
        var trip = AddTrip(DateTime.Now.AddHours(-3));
        var b = AddBooking(trip, "Paid", 100_000m, "A1");

        var preview = await Svc().RefundPreviewAsync(b.Id);

        Assert.False(Prop<bool>(preview, "CanCancel"));
    }

    // ===================== Đổi vé =====================

    [Fact]
    public async Task DoiVe_DoiGhe_ThanhCong()
    {
        var b = AddBooking(MainTrip, "Holding", 100_000m, "A1");
        var ticketId = b.Tickets.Single().Id;

        await Svc().ChangeTicketAsync(ticketId, new ChangeTicketDto { NewSeatCode = "a3" });

        Refresh();
        Assert.Equal("A3", Db.Tickets.Single(t => t.Id == ticketId).SeatCode);
    }

    [Fact]
    public async Task DoiVe_GheMoiDaCoNguoiDat_Tra409()
    {
        var b1 = AddBooking(MainTrip, "Holding", 100_000m, "A1");
        AddBooking(MainTrip, "Holding", 100_000m, "A2");

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            Svc().ChangeTicketAsync(b1.Tickets.Single().Id, new ChangeTicketDto { NewSeatCode = "A2" }));

        Assert.Equal(409, ex.Status);
    }

    [Fact]
    public async Task DoiVe_GheMoiTrungGheHienTai_Tra400()
    {
        var b = AddBooking(MainTrip, "Holding", 100_000m, "A1");

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            Svc().ChangeTicketAsync(b.Tickets.Single().Id, new ChangeTicketDto { NewSeatCode = "A1" }));

        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task DoiVe_GheMoiKhongThuocLoaiXe_Tra400()
    {
        var b = AddBooking(MainTrip, "Holding", 100_000m, "A1");

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            Svc().ChangeTicketAsync(b.Tickets.Single().Id, new ChangeTicketDto { NewSeatCode = "Z9" }));

        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task DoiVe_SapKhoiHanhDuoi2Gio_Tra400()
    {
        var soon = AddTrip(DateTime.Now.AddMinutes(90));
        var b = AddBooking(soon, "Holding", 100_000m, "A1");

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            Svc().ChangeTicketAsync(b.Tickets.Single().Id, new ChangeTicketDto { NewSeatCode = "A2" }));

        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task DoiVe_BookingDaHuy_Tra400()
    {
        var b = AddBooking(MainTrip, "Cancelled", 100_000m, "A1");

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            Svc().ChangeTicketAsync(b.Tickets.Single().Id, new ChangeTicketDto { NewSeatCode = "A2" }));

        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task DoiVe_SangChuyenCungTuyen_ThanhCong()
    {
        var other = AddTrip(DateTime.Today.AddDays(5).AddHours(9), price: 100_000m);
        var b = AddBooking(MainTrip, "Paid", 100_000m, "A1");
        var ticketId = b.Tickets.Single().Id;

        await Svc().ChangeTicketAsync(ticketId, new ChangeTicketDto { NewSeatCode = "A2", NewTripId = other.Id });

        Refresh();
        var t = Db.Tickets.Single(x => x.Id == ticketId);
        Assert.Equal(other.Id, t.TripId);
        Assert.Equal("A2", t.SeatCode);
    }

    [Fact]
    public async Task DoiVe_DaThanhToan_SangChuyenGiaCaoHon_Tra400()
    {
        var dearer = AddTrip(DateTime.Today.AddDays(5).AddHours(9), price: 150_000m);
        var b = AddBooking(MainTrip, "Paid", 100_000m, "A1");

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            Svc().ChangeTicketAsync(b.Tickets.Single().Id, new ChangeTicketDto { NewSeatCode = "A1", NewTripId = dearer.Id }));

        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task DoiVe_DangGiuCho_SangChuyenGiaCaoHon_TangTongTien()
    {
        var dearer = AddTrip(DateTime.Today.AddDays(5).AddHours(9), price: 150_000m);
        var b = AddBooking(MainTrip, "Holding", 100_000m, "A1");
        var ticketId = b.Tickets.Single().Id;

        await Svc().ChangeTicketAsync(ticketId, new ChangeTicketDto { NewSeatCode = "A1", NewTripId = dearer.Id });

        Refresh();
        Assert.Equal(150_000m, Db.Tickets.Single(t => t.Id == ticketId).Price);
        Assert.Equal(150_000m, Db.Bookings.Single(x => x.Id == b.Id).TotalAmount);
    }

    [Fact]
    public async Task DoiVe_SangChuyenKhacTuyen_Tra400()
    {
        var route2 = AddOtherRoute();
        var other = AddTrip(DateTime.Today.AddDays(5).AddHours(9), routeId: route2.Id);
        var b = AddBooking(MainTrip, "Holding", 100_000m, "A1");

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            Svc().ChangeTicketAsync(b.Tickets.Single().Id, new ChangeTicketDto { NewSeatCode = "A1", NewTripId = other.Id }));

        Assert.Equal(400, ex.Status);
    }

    // ===================== Tìm chuyến =====================

    [Fact]
    public async Task TimChuyen_ChiTraChuyenMoBan_VaTinhDungGheTrong()
    {
        AddTrip(MainTrip.DepartAt.AddHours(3), status: "Cancelled");   // không được hiện
        AddTrip(MainTrip.DepartAt.AddHours(4), status: "Delayed");     // không được hiện
        AddBooking(MainTrip, "Holding", 100_000m, "A1");               // chiếm 1 ghế của chuyến chính

        var res = await Svc().SearchAsync(FromStation.Id, ToStation.Id, MainTrip.DepartAt.Date,
            null, null, null, null, null, null, 1, 10);

        Assert.Equal(1, Prop<int>(res, "total"));
        var first = Prop<System.Collections.IEnumerable>(res, "items").Cast<object>().Single();
        Assert.Equal(MainTrip.Id, Prop<int>(first, "TripId"));
        Assert.Equal(3, Prop<int>(first, "TotalSeats"));
        Assert.Equal(2, Prop<int>(first, "AvailableSeats"));
    }

    [Fact]
    public async Task TimChuyen_SaiNgayHoacSaiBen_KhongCoKetQua()
    {
        var wrongDay = await Svc().SearchAsync(FromStation.Id, ToStation.Id, MainTrip.DepartAt.Date.AddDays(1),
            null, null, null, null, null, null, 1, 10);
        var wrongWay = await Svc().SearchAsync(ToStation.Id, FromStation.Id, MainTrip.DepartAt.Date,
            null, null, null, null, null, null, 1, 10);

        Assert.Equal(0, Prop<int>(wrongDay, "total"));
        Assert.Equal(0, Prop<int>(wrongWay, "total"));
    }

    // ===================== Danh sách đặt vé (quản trị) và khách hàng =====================

    [Fact]
    public async Task DanhSachDatVe_LocTheoTrangThai()
    {
        AddBooking(MainTrip, "Paid", 100_000m, "A1");
        AddBooking(MainTrip, "Cancelled", 100_000m, "A2");

        var res = await Svc().ListAsync("Paid", null, null, null, null, true, 1, 10);

        Assert.Equal(1, Prop<int>(res, "total"));
    }

    [Fact]
    public async Task DanhSachDatVe_TimTheoSoDienThoaiKhach()
    {
        AddBooking(MainTrip, "Paid", 100_000m, "A1");

        var found = await Svc().ListAsync(null, "0901234567", null, null, null, true, 1, 10);
        var missing = await Svc().ListAsync(null, "0999999999", null, null, null, true, 1, 10);

        Assert.Equal(1, Prop<int>(found, "total"));
        Assert.Equal(0, Prop<int>(missing, "total"));
    }

    [Fact]
    public async Task HoSoKhachHang_TaoMoi_VaLienKetHoSoCuTheoEmail()
    {
        // Tài khoản mới chưa có hồ sơ: tạo mới, gọi lần hai vẫn ra cùng hồ sơ
        var id1 = await Svc().GetOrCreateCustomerIdAsync("user-new", "new@test.com");
        var id2 = await Svc().GetOrCreateCustomerIdAsync("user-new", "new@test.com");
        Assert.Equal(id1, id2);

        // Hồ sơ cũ do nhân viên tạo (chưa gắn tài khoản) được gắn khi khách đăng nhập bằng đúng email
        var old = new HeThongVanTai.Models.Domain.Customer { FullName = "Khách tại quầy", Email = "old@test.com" };
        Db.Customers.Add(old);
        Db.SaveChanges();

        var linked = await Svc().GetOrCreateCustomerIdAsync("user-old", "old@test.com");

        Assert.Equal(old.Id, linked);
        Refresh();
        Assert.Equal("user-old", Db.Customers.Single(c => c.Id == old.Id).UserId);
    }
}