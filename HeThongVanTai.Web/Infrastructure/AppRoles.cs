namespace HeThongVanTai.Web.Infrastructure;

// Phải trùng với tên vai trò bên API (AppRoles trong project HeThongVanTai)
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Seller = "Seller";
    public const string Accountant = "Accountant";
    public const string Customer = "Customer";

    public const string AdminOperator = Admin + "," + Operator;
}
