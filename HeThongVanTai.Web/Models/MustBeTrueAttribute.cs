using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HeThongVanTai.Web.Models;

/// <summary>
/// Bắt buộc ô tích (bool) phải được tích, dùng cho ô "đồng ý điều khoản".
/// Phía trình duyệt cần đăng ký quy tắc "mustbetrue" (xem Views/Account/Register.cshtml).
/// Không dùng [Range(typeof(bool), "true", "true")]: jQuery Validate không hiểu quy tắc Range trên ô tích nên luôn báo lỗi.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class MustBeTrueAttribute : ValidationAttribute, IClientModelValidator
{
    public override bool IsValid(object? value) => value is true;

    public void AddValidation(ClientModelValidationContext context)
    {
        Merge(context.Attributes, "data-val", "true");
        Merge(context.Attributes, "data-val-mustbetrue",
            ErrorMessage ?? FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
    }

    private static void Merge(IDictionary<string, string> attributes, string key, string value)
    {
        if (!attributes.ContainsKey(key)) attributes.Add(key, value);
    }
}
