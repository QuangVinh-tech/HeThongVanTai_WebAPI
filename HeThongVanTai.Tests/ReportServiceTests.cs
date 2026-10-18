using HeThongVanTai.Models.Domain;
using HeThongVanTai.Services;
using Xunit;

namespace HeThongVanTai.Tests;

/// <summary>Báo cáo mở rộng: doanh thu theo ngày, tháng, tuyến, xe, tài xế, nhân viên; công nợ; đối soát.</summary>
public class ReportServiceTests : BookingTestBase
{
    private static readonly DateTime D1 = new(2026, 10, 5, 10, 0, 0);
    private static readonly DateTime D2 = new(2026, 10, 7, 15, 0, 0);
    private static readonly DateTime NextMonth = new(2026, 11, 2, 9, 0, 0);

    private ReportService Reports() => new(Db);

    private Payment Pay(Booking b, decimal amount, DateTime paidAt, string status = "Paid",
                        string method = "BankTransfer", string? txn = "TX-1")
    {
        var p = new Payment
        {
            BookingId = b.Id, Amount = amount, Method = method, Status = status, PaidAt = paidAt,
            SePayTransactionId = txn, BankReference = txn == null ? null : "REF-" + txn
        };
        Db.Payments.Add(p);
        Db.SaveChanges();
        return p;
    }

    private Booking Sold(Trip trip, string status, decimal total, string? soldBy, params string[] seats)
    {
        // AddBooking("Paid") tự thêm một khoản thanh toán; tạo ở trạng thái khác rồi đổi để tự kiểm soát các khoản thanh toán
        var b = AddBooking(trip, "Draft", total, seats);
        b.Status = status;
        b.SoldBy = soldBy;
        Db.SaveChanges();
        return b;
    }

    // Dữ liệu mẫu: 3 đặt vé đã trả tiền, 1 khoản hoàn
    private void Seed()
    {
        var tripA = MainTrip;
        var other = AddOtherRoute();
        var tripB = AddTrip(DateTime.Now.AddDays(5), routeId: other.Id);

        var b1 = Sold(tripA, "Paid", 200_000m, "seller1@test.com", "A1", "A2");
        var b2 = Sold(tripB, "Paid", 100_000m, null, "A1");
        var b3 = Sold(tripA, "Paid", 100_000m, "seller1@test.com", "A3");
        Pay(b1, 200_000m, D1, txn: "TX-1");
        Pay(b2, 100_000m, D2, method: "Cash", txn: null);
        Pay(b3, 100_000m, NextMonth, txn: "TX-3");
        // Hoàn 50.000 cho đặt vé thứ nhất vào ngày D2
        Pay(b1, 50_000m, D2, status: "Refunded", txn: null);
    }

    [Fact]
    public async Task MoiLoaiBaoCao_CungMotTong_VaKhopTheoTuyen()
    {
        Seed();
        var start = new DateTime(2026, 10, 1);
        var end = new DateTime(2026, 11, 30);

        var route = (await Reports().RevenueByAsync("route", start, end, null, null, true, 1, 10, true)).Summary.TotalRevenue;
        Assert.Equal(350_000m, route);   // 200k + 100k + 100k - 50k
        foreach (var by in new[] { "day", "month", "vehicle", "driver", "seller" })
        {
            var total = (await Reports().RevenueByAsync(by, start, end, null, null, true, 1, 10, true)).Summary.TotalRevenue;
            Assert.Equal(route, total);
        }
    }

    [Fact]
    public async Task TheoNgay_DuMoiNgayKeCaNgayKhongCoDoanhThu_VaTruTienHoan()
    {
        Seed();

        var r = await Reports().RevenueByAsync("day", new DateTime(2026, 10, 4), new DateTime(2026, 10, 8),
            null, null, desc: false, 1, 10, all: true);

        Assert.Equal(5, r.Items.Count);   // 4,5,6,7,8/10 đủ năm ngày
        Assert.Equal(new[] { "2026-10-04", "2026-10-05", "2026-10-06", "2026-10-07", "2026-10-08" }, r.Items.Select(i => i.Key));
        Assert.Equal(new[] { 0m, 200_000m, 0m, 50_000m, 0m }, r.Items.Select(i => i.Revenue));   // ngày 7: +100k -50k
        Assert.Equal(250_000m, r.Summary.TotalRevenue);
    }

    [Fact]
    public async Task TheoThang_GomDungVaDuThang()
    {
        Seed();

        var r = await Reports().RevenueByAsync("month", new DateTime(2026, 9, 15), new DateTime(2026, 11, 20),
            null, null, desc: false, 1, 10, all: true);

        Assert.Equal(new[] { "2026-09", "2026-10", "2026-11" }, r.Items.Select(i => i.Key));
        Assert.Equal(new[] { 0m, 250_000m, 100_000m }, r.Items.Select(i => i.Revenue));
    }

    [Fact]
    public async Task TheoNhanVien_KhachTuDatGomVaoMotDong()
    {
        Seed();

        var r = await Reports().RevenueByAsync("seller", new DateTime(2026, 10, 1), new DateTime(2026, 11, 30),
            null, "revenue", true, 1, 10, true);

        var seller = r.Items.Single(i => i.Key == "seller1@test.com");
        Assert.Equal(200_000m - 50_000m + 100_000m, seller.Revenue);
        var unknown = r.Items.Single(i => i.Key == "");
        Assert.Equal(ReportService.UnknownSeller, unknown.Label);
        Assert.Equal(100_000m, unknown.Revenue);
    }

    [Fact]
    public async Task TheoXeVaTaiXe_CoTenBienSoVaTaiXe()
    {
        Seed();

        var xe = await Reports().RevenueByAsync("vehicle", new DateTime(2026, 10, 1), new DateTime(2026, 11, 30),
            null, null, true, 1, 10, true);
        var tx = await Reports().RevenueByAsync("driver", new DateTime(2026, 10, 1), new DateTime(2026, 11, 30),
            null, null, true, 1, 10, true);

        Assert.Single(xe.Items);   // mọi chuyến mẫu dùng cùng một xe
        Assert.Equal(350_000m, xe.Items[0].Revenue);
        Assert.Equal("Tài xế Test", tx.Items.Single().Label);
    }

    [Fact]
    public async Task LocTheoTuyen_ChiTinhTuyenDo()
    {
        Seed();
        var routeId = MainTrip.BusRouteId;

        var r = await Reports().RevenueByAsync("day", new DateTime(2026, 10, 1), new DateTime(2026, 11, 30),
            routeId, null, false, 1, 10, true);

        Assert.Equal(200_000m - 50_000m + 100_000m, r.Summary.TotalRevenue);   // tuyến A: đặt vé 1 (có hoàn) và 3
    }

    [Fact]
    public async Task Phan_Trang_VaLoaiKhongHopLe()
    {
        Seed();
        var page = await Reports().RevenueByAsync("seller", new DateTime(2026, 10, 1), new DateTime(2026, 11, 30),
            null, null, true, page: 1, pageSize: 1, all: false);
        Assert.Single(page.Items);
        Assert.Equal(2, page.Total);

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            Reports().RevenueByAsync("abc", new DateTime(2026, 10, 1), new DateTime(2026, 10, 2), null, null, true, 1, 10, false));
        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task KhoangNgaySai_Tra400()
    {
        var ex1 = await Assert.ThrowsAsync<ServiceException>(() =>
            Reports().RevenueByAsync("day", new DateTime(2026, 10, 10), new DateTime(2026, 10, 1), null, null, true, 1, 10, false));
        var ex2 = await Assert.ThrowsAsync<ServiceException>(() =>
            Reports().RevenueByAsync("day", new DateTime(2024, 1, 1), new DateTime(2026, 1, 1), null, null, true, 1, 10, false));
        Assert.Equal(400, ex1.Status);
        Assert.Equal(400, ex2.Status);
    }

    // ===================== Công nợ =====================
    private Shipment Ship(string code, string phone, decimal total, DateTime createdAt,
                          string status = "Pending", string payment = "Unpaid", string feePayer = "Debt") => new()
    {
        Code = code, FromStationId = FromStation.Id, ToStationId = ToStation.Id,
        SenderName = "Người gửi " + phone, SenderPhone = phone, ReceiverName = "Người nhận", ReceiverPhone = "0900000000",
        FeePayer = feePayer, TotalAmount = total, ShippingFee = total, Status = status, PaymentStatus = payment,
        CreatedAt = createdAt
    };

    [Fact]
    public async Task CongNo_ChiTinhDonChuaThuVaKhongHuy_ChiaTuoiNo()
    {
        var today = new DateTime(2026, 10, 20);
        Db.Shipments.AddRange(
            Ship("DH01", "0911111111", 100_000m, today.AddDays(-5)),                       // 0-30
            Ship("DH02", "0911111111", 200_000m, today.AddDays(-45)),                      // 31-60
            Ship("DH03", "0922222222", 300_000m, today.AddDays(-90)),                      // trên 60
            Ship("DH04", "0933333333", 999_000m, today.AddDays(-3), payment: "Paid"),      // đã thu: bỏ
            Ship("DH05", "0944444444", 888_000m, today.AddDays(-3), status: "Cancelled"),  // đã hủy: bỏ
            Ship("DH06", "0955555555", 50_000m, today.AddDays(-2), feePayer: "Sender"));
        Db.SaveChanges();

        var r = await Reports().DebtAsync(null, null, null, 1, 10, all: true, today: today);

        Assert.Equal(650_000m, r.Summary.TotalDebt);
        Assert.Equal(3, r.Summary.Customers);
        Assert.Equal(4, r.Summary.Shipments);
        Assert.Equal(150_000m, r.Summary.Within30);
        Assert.Equal(200_000m, r.Summary.Days31To60);
        Assert.Equal(300_000m, r.Summary.Over60);
        var a = r.Items.Single(c => c.SenderPhone == "0911111111");
        Assert.Equal(300_000m, a.TotalAmount);
        Assert.Equal(2, a.Shipments);
        Assert.Equal(45, a.OldestDays);
        Assert.Equal(4, r.Details!.Count);
        Assert.Equal(300_000m, r.Items[0].TotalAmount);   // nợ nhiều nhất xếp đầu
    }

    [Fact]
    public async Task CongNo_LocTheoNguoiTraCuoc_VaNgayTao()
    {
        var today = new DateTime(2026, 10, 20);
        Db.Shipments.AddRange(
            Ship("DH01", "0911111111", 100_000m, today.AddDays(-5), feePayer: "Debt"),
            Ship("DH02", "0922222222", 200_000m, today.AddDays(-5), feePayer: "Receiver"),
            Ship("DH03", "0933333333", 300_000m, today.AddDays(-60), feePayer: "Debt"));
        Db.SaveChanges();

        var debt = await Reports().DebtAsync(null, null, "Debt", 1, 10, true, today);
        Assert.Equal(400_000m, debt.Summary.TotalDebt);

        var recent = await Reports().DebtAsync(today.AddDays(-10), today, null, 1, 10, true, today);
        Assert.Equal(300_000m, recent.Summary.TotalDebt);

        var ex = await Assert.ThrowsAsync<ServiceException>(() => Reports().DebtAsync(null, null, "Khac", 1, 10, false, today));
        Assert.Equal(400, ex.Status);
    }

    // ===================== Đối soát chuyển khoản =====================
    [Fact]
    public async Task DoiSoat_TongHopVaCanhBaoBatThuong()
    {
        var b1 = Sold(MainTrip, "Paid", 200_000m, null, "A1", "A2");   // khớp hoàn toàn
        var b2 = Sold(MainTrip, "Paid", 100_000m, null, "A3");         // lệch số tiền
        var b3 = Sold(MainTrip, "Paid", 100_000m, null);               // thiếu mã giao dịch
        var b4 = Sold(MainTrip, "Paid", 100_000m, null);               // đã Paid nhưng không có giao dịch
        Pay(b1, 200_000m, D1, txn: "TX-OK");
        Pay(b2, 90_000m, D1, txn: "TX-LECH");
        Pay(b3, 100_000m, D1, txn: null);
        Pay(b1, 30_000m, D2, status: "Refunded", txn: null);
        var cash = Sold(MainTrip, "Paid", 70_000m, null);
        Pay(cash, 70_000m, D1, method: "Cash", txn: null);
        // b4 không có giao dịch; đặt CreatedAt vào khoảng báo cáo
        b4.CreatedAt = D1; Db.SaveChanges();

        var r = await Reports().ReconciliationAsync(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), false, 1, 50, true);

        Assert.Equal(3, r.Summary.BankCount);
        Assert.Equal(390_000m, r.Summary.BankReceived);
        Assert.Equal(30_000m, r.Summary.BankRefunded);
        Assert.Equal(360_000m, r.Summary.BankNet);
        Assert.Equal(70_000m, r.Summary.CashReceived);
        Assert.Equal(3, r.Summary.IssueCount);   // lệch tiền, thiếu mã, thiếu giao dịch

        var ok = r.Items.Single(i => i.SePayTransactionId == "TX-OK");
        Assert.Empty(ok.Issues);
        Assert.Equal(30_000m, ok.RefundedAmount);
        Assert.Contains("Lệch số tiền", r.Items.Single(i => i.SePayTransactionId == "TX-LECH").IssueText);
        Assert.Contains("mã giao dịch", r.Items.Single(i => i.BookingCode == b3.Code).IssueText);
        Assert.Contains("không có giao dịch", r.Items.Single(i => i.BookingCode == b4.Code).IssueText);
    }

    [Fact]
    public async Task DoiSoat_ChiHienBatThuong_VaGiaoDichTrung()
    {
        var b1 = Sold(MainTrip, "Paid", 100_000m, null, "A1");
        var b2 = Sold(MainTrip, "Paid", 100_000m, null, "A2");
        Pay(b1, 100_000m, D1, txn: "TX-A");
        Pay(b1, 100_000m, D1, txn: "TX-B");     // hai giao dịch cho một đặt vé
        Pay(b2, 100_000m, D1, txn: "TX-C");

        var r = await Reports().ReconciliationAsync(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), true, 1, 50, true);

        Assert.Equal(2, r.Items.Count);
        Assert.All(r.Items, i => Assert.Contains("nhiều hơn một giao dịch", i.IssueText));
        Assert.Equal(3, r.Summary.BankCount);   // tổng hợp vẫn tính đủ, chỉ danh sách lọc
    }
}
