namespace HeThongVanTai.Models.Domain
{
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string Operator = "Operator";
        public const string Seller = "Seller";
        public const string Accountant = "Accountant";
        public const string Customer = "Customer";
        public const string Driver = "Driver";

        // Tổ hợp vai trò dùng chung cho [Authorize(Roles = ...)]
        public const string AdminOperator = Admin + "," + Operator;
        public const string AdminAccountant = Admin + "," + Accountant;

        // Đặt vé: Customer đặt cho mình; Seller và Admin đặt hộ khách (theo sheet Phân quyền)
        public const string AdminSellerCustomer = Admin + "," + Seller + "," + Customer;
        // Xem danh sách đặt vé của mọi khách: Kế toán chỉ xem để đối soát
        public const string AdminSellerAccountant = Admin + "," + Seller + "," + Accountant;
    }
}