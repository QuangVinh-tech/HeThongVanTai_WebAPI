using System.Security.Cryptography;
using HeThongVanTai.Models.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HeThongVanTai.Services;

// Service ném lỗi này, filter đổi thành HTTP 400/404/409 + {"message": "..."}
public class ServiceException : Exception
{
    public int Status { get; }
    public ServiceException(int status, string message) : base(message) => Status = status;
}

public class ServiceExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is ServiceException ex)
        {
            context.Result = new ObjectResult(new { message = ex.Message }) { StatusCode = ex.Status };
            context.ExceptionHandled = true;
        }
    }
}

public static class Rules
{
    // Chính sách hoàn tiền: >=24h: 100%, 12-24h: 70%, 2-12h: 50%, <2h: 0%
    public static decimal RefundRate(DateTime departAt, DateTime now)
    {
        var h = (departAt - now).TotalHours;
        return h >= 24 ? 1m : h >= 12 ? 0.7m : h >= 2 ? 0.5m : 0m;
    }

    // Trả về số tiền giảm; nếu mã không dùng được thì error != null
    public static decimal Discount(Promotion p, decimal subtotal, DateTime now, out string? error)
    {
        error = null;
        if (!p.IsActive) { error = "Mã giảm giá đã bị vô hiệu hóa."; return 0; }
        if (now < p.StartAt || now > p.EndAt) { error = "Mã giảm giá chưa có hiệu lực hoặc đã hết hạn."; return 0; }
        if (subtotal < p.MinOrder) { error = $"Đơn tối thiểu {p.MinOrder:N0}đ mới dùng được mã này."; return 0; }
        if (p.UsageLimit.HasValue && p.UsedCount >= p.UsageLimit.Value) { error = "Mã giảm giá đã hết lượt sử dụng."; return 0; }

        var d = p.DiscountType == "Percent" ? Math.Round(subtotal * p.Value / 100m, 0) : p.Value;
        if (p.MaxDiscount.HasValue) d = Math.Min(d, p.MaxDiscount.Value);
        return Math.Min(d, subtotal);
    }

    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   
    public static string NewTicketCode() => "VT" + RandomNumberGenerator.GetString(Chars, 8);
    public static string NewBookingCode() => "BK" + DateTime.Now.ToString("yyMMdd") + RandomNumberGenerator.GetString(Chars, 5);

    
    public static bool IsDuplicate(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}