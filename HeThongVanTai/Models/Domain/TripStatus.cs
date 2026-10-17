namespace HeThongVanTai.Models.Domain
{
    // Trạng thái chuyến xe (cột Trip.Status là chuỗi)
    public static class TripStatus
    {
        public const string Open = "Open";            // mở bán
        public const string Delayed = "Delayed";      // hoãn
        public const string Cancelled = "Cancelled";  // đã hủy
        public const string Running = "Running";      // đang chạy (tài xế bấm bắt đầu)
        public const string Completed = "Completed";  // đã hoàn thành

        public static readonly string[] All = { Open, Delayed, Cancelled, Running, Completed };
    }
}
