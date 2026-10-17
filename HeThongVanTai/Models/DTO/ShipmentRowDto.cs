namespace HeThongVanTai.Models.DTO
{
    public class ShipmentRowDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string FromStation { get; set; } = "";
        public string ToStation { get; set; } = "";
        public string ReceiverName { get; set; } = "";
        public string ReceiverPhone { get; set; } = "";
        public string ServiceType { get; set; } = "";
        public string Status { get; set; } = "";
        public string PaymentStatus { get; set; } = "";
        public string FeePayer { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public int ParcelCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}