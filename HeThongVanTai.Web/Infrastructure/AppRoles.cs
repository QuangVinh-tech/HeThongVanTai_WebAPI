namespace HeThongVanTai.Web.Infrastructure;

// Phải trùng với tên vai trò bên API (AppRoles trong project HeThongVanTai)
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Seller = "Seller";
    public const string Accountant = "Accountant";
    public const string Customer = "Customer";
    public const string Driver = "Driver";

    public const string AdminOperator = Admin + "," + Operator;
    public const string AdminAccountant = Admin + "," + Accountant;
    public const string AdminSeller = Admin + "," + Seller;
    public const string AdminSellerCustomer = Admin + "," + Seller + "," + Customer;
}
