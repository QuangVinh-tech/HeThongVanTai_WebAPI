using System.ComponentModel.DataAnnotations;

namespace HeThongVanTai.Web.Models.ViewModels.DatVe;

// Khớp JSON của GET /api/promotions
public class PromotionItem
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string DiscountType { get; set; } = "Percent";   // Percent | Amount
    public decimal Value { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal MinOrder { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; }
}

public class PromotionForm
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã giảm giá.")]
    [StringLength(30, ErrorMessage = "Mã tối đa 30 ký tự.")]
    public string Code { get; set; } = "";

    public string DiscountType { get; set; } = "Percent";

    [Range(1, 1000000000, ErrorMessage = "Giá trị giảm phải lớn hơn 0.")]
    public decimal Value { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Mức giảm tối đa không được âm.")]
    public decimal? MaxDiscount { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Đơn tối thiểu không được âm.")]
    public decimal MinOrder { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời điểm bắt đầu.")]
    public DateTime? StartAt { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời điểm kết thúc.")]
    public DateTime? EndAt { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Giới hạn lượt dùng phải từ 1 trở lên.")]
    public int? UsageLimit { get; set; }
}

public class PromotionsViewModel
{
    public List<PromotionItem> Items { get; set; } = new();
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; }
    public int Total { get; set; }
    public string? Error { get; set; }
}