using System.Net.Http.Headers;
using HeThongVanTai.Web.Infrastructure;
using HeThongVanTai.Web.Models.ViewModels.DanhMuc;
using HeThongVanTai.Web.Models.ViewModels.XeTaiXe;

namespace HeThongVanTai.Web.Services;

public class XeTaiXeApiService
{
    public const int PageSize = 8;
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };

    private readonly ApiClient _api;

    public XeTaiXeApiService(ApiClient api)
    {
        _api = api;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildQuery(params (string Key, string? Value)[] pairs)
    {
        var parts = pairs
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => p.Key + "=" + Uri.EscapeDataString(p.Value!.Trim()));
        var query = string.Join("&", parts);
        return query.Length > 0 ? "?" + query : "";
    }

    public static string? CheckImage(IFormFile? file)
    {
        if (file == null || file.Length == 0) return null;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext)) return "Ảnh chỉ nhận định dạng .jpg, .jpeg, .png";
        if (file.Length > MaxImageBytes) return "Ảnh không được vượt quá 5MB";
        return null;
    }

    public async Task<ApiResult<string>> UploadImageAsync(IFormFile file, string prefix)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var name = prefix + "_" + Guid.NewGuid().ToString("N") + ext;

        await using var stream = file.OpenReadStream();
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(ext == ".png" ? "image/png" : "image/jpeg");
        form.Add(fileContent, "File", name);
        form.Add(new StringContent(name), "FileName");

        var result = await _api.PostFormAsync<ImageUploadResult>("api/Images/Upload", form);
        if (!result.Ok || string.IsNullOrWhiteSpace(result.Data?.FilePath))
        {
            return new ApiResult<string>
            {
                Ok = false,
                Status = result.Status,
                Error = result.Error ?? "Không tải được ảnh lên. Vui lòng thử lại."
            };
        }

        return new ApiResult<string> { Ok = true, Status = result.Status, Data = result.Data!.FilePath };
    }

    public Task<ApiResult<List<VehicleTypeItem>>> GetVehicleTypesAsync() =>
        _api.GetAsync<List<VehicleTypeItem>>("api/vehicle-types");

    public Task<ApiResult<PagedResult<VehicleItem>>> GetVehiclesAsync(VehicleFilter f)
    {
        var (sortBy, ascending) = f.Sort switch
        {
            "plate_desc" => ("Plate", false),
            "reg_asc" => ("RegistrationExpiry", true),
            "ins_asc" => ("InsuranceExpiry", true),
            _ => ("Plate", true)
        };

        var url = "api/vehicles" + BuildQuery(
            ("filterOn", Clean(f.Keyword) == null ? null : "Plate"),
            ("filterQuery", f.Keyword),
            ("status", f.Status),
            ("vehicleTypeId", f.VehicleTypeId?.ToString()),
            ("sortBy", sortBy),
            ("isAscending", ascending ? "true" : "false"),
            ("pageNumber", f.Page.ToString()),
            ("pageSize", PageSize.ToString()));

        return _api.GetAsync<PagedResult<VehicleItem>>(url);
    }

    public Task<ApiResult<VehicleItem>> GetVehicleAsync(int id) =>
        _api.GetAsync<VehicleItem>($"api/vehicles/{id}");

    private static object VehicleBody(VehicleForm f, string? imageUrl) => new
    {
        plate = f.Plate.Trim(),
        vehicleTypeId = f.VehicleTypeId,
        registrationExpiry = f.RegistrationExpiry,
        insuranceExpiry = f.InsuranceExpiry,
        status = f.Status,
        imageUrl
    };

    public Task<ApiResult<string>> CreateVehicleAsync(VehicleForm form, string? imageUrl) =>
        _api.PostAsync<string>("api/vehicles", VehicleBody(form, imageUrl));

    public Task<ApiResult<string>> UpdateVehicleAsync(int id, VehicleForm form, string? imageUrl) =>
        _api.PutAsync<string>($"api/vehicles/{id}", VehicleBody(form, imageUrl));

    public Task<ApiResult<string>> DeleteVehicleAsync(int id) =>
        _api.DeleteAsync<string>($"api/vehicles/{id}");

    public Task<ApiResult<PagedResult<DriverItem>>> GetDriversAsync(DriverFilter f)
    {
        var filterOn = f.FilterOn is "Phone" or "LicenseNo" ? f.FilterOn : "FullName";

        var (sortBy, ascending) = f.Sort switch
        {
            "name_desc" => ("FullName", false),
            "exp_asc" => ("LicenseExpiry", true),
            "exp_desc" => ("LicenseExpiry", false),
            _ => ("FullName", true)
        };

        var url = "api/drivers" + BuildQuery(
            ("filterOn", Clean(f.Keyword) == null ? null : filterOn),
            ("filterQuery", f.Keyword),
            ("status", f.Status),
            ("licenseClass", f.LicenseClass),
            ("sortBy", sortBy),
            ("isAscending", ascending ? "true" : "false"),
            ("pageNumber", f.Page.ToString()),
            ("pageSize", PageSize.ToString()));

        return _api.GetAsync<PagedResult<DriverItem>>(url);
    }

    public Task<ApiResult<DriverItem>> GetDriverAsync(int id) =>
        _api.GetAsync<DriverItem>($"api/drivers/{id}");

    private static object DriverBody(DriverForm f, string? avatarUrl) => new
    {
        fullName = f.FullName.Trim(),
        phone = Clean(f.Phone),
        licenseNo = Clean(f.LicenseNo),
        licenseClass = Clean(f.LicenseClass),
        licenseExpiry = f.LicenseExpiry,
        status = f.Status,
        avatarUrl
    };

    public Task<ApiResult<string>> CreateDriverAsync(DriverForm form, string? avatarUrl) =>
        _api.PostAsync<string>("api/drivers", DriverBody(form, avatarUrl));

    public Task<ApiResult<string>> UpdateDriverAsync(int id, DriverForm form, string? avatarUrl) =>
        _api.PutAsync<string>($"api/drivers/{id}", DriverBody(form, avatarUrl));

    public Task<ApiResult<string>> DeleteDriverAsync(int id) =>
        _api.DeleteAsync<string>($"api/drivers/{id}");
}