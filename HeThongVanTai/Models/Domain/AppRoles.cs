namespace HeThongVanTai.Models.Domain
{
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string Operator = "Operator";
        public const string Seller = "Seller";
        public const string Accountant = "Accountant";
        public const string Customer = "Customer";

        // Tổ hợp vai trò dùng chung cho [Authorize(Roles = ...)]
        public const string AdminOperator = Admin + "," + Operator;
    }
}