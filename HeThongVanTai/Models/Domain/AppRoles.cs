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

        // Tài xế: xem và đổi trạng thái chuyến của mình; xem danh sách hành khách
        public const string AdminOperatorDriver = Admin + "," + Operator + "," + Driver;
        public const string AdminOperatorSellerDriver = Admin + "," + Operator + "," + Seller + "," + Driver;

        // Đặt vé: Customer đặt cho mình; Seller và Admin đặt hộ khách (theo sheet Phân quyền)
        public const string AdminSellerCustomer = Admin + "," + Seller + "," + Customer;
        // Xem danh sách đặt vé của mọi khách: Kế toán chỉ xem để đối soát
        public const string AdminSellerAccountant = Admin + "," + Seller + "," + Accountant;

        // Soát vé tại bến hoặc trên xe
        public const string CheckInStaff = Admin + "," + Operator + "," + Seller + "," + Driver;
    }
}