using Microsoft.EntityFrameworkCore;
using HeThongVanTai.Models.Domain;

namespace HeThongVanTai.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> dbContextOptions) : base(dbContextOptions)
        {
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BusRoute>()
                .HasOne(r => r.FromStation)
                .WithMany()
                .HasForeignKey(r => r.FromStationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BusRoute>()
                .HasOne(r => r.ToStation)
                .WithMany()
                .HasForeignKey(r => r.ToStationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ticket>()
                .HasIndex(t => new { t.TripId, t.SeatCode })
                .IsUnique()
                .HasFilter("[Status] <> 'Cancelled'");

            modelBuilder.Entity<Ticket>()
                .HasIndex(t => t.TicketCode)
                .IsUnique()
                .HasFilter("[TicketCode] <> ''");

            modelBuilder.Entity<Promotion>()
                .HasIndex(p => p.Code)
                .IsUnique();

            // Mỗi vé chỉ được đánh giá một lần.
            modelBuilder.Entity<Review>()
                .HasIndex(r => r.TicketId)
                .IsUnique();

            modelBuilder.Entity<Review>()
                .HasIndex(r => r.TripId);

            // Một tài khoản chỉ liên kết với một hồ sơ tài xế
            modelBuilder.Entity<Driver>()
                .HasIndex(d => d.UserId)
                .IsUnique()
                .HasFilter("[UserId] IS NOT NULL");

            // Mỗi mã giao dịch SePay chỉ được gắn với một thanh toán.
            // Cho phép nhiều bản ghi Payment có giá trị null.
            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.SePayTransactionId)
                .IsUnique()
                .HasFilter("[SePayTransactionId] IS NOT NULL");
            modelBuilder.Entity<Shipment>().HasIndex(s => s.Code).IsUnique();

            // Restrict để SQL Server không báo lỗi "multiple cascade paths"
            modelBuilder.Entity<Shipment>().HasOne(s => s.FromStation).WithMany()
                .HasForeignKey(s => s.FromStationId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Shipment>().HasOne(s => s.ToStation).WithMany()
                .HasForeignKey(s => s.ToStationId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Shipment>().HasOne(s => s.Trip).WithMany()
                .HasForeignKey(s => s.TripId).OnDelete(DeleteBehavior.Restrict);
        }
        public DbSet<Shipment> Shipments { get; set; }
        public DbSet<ShipmentItem> ShipmentItems { get; set; }
        public DbSet<ShipmentLog> ShipmentLogs { get; set; }

        public DbSet<Station> Stations { get; set; }
        public DbSet<BusRoute> BusRoutes { get; set; }
        public DbSet<VehicleType> VehicleTypes { get; set; }
        public DbSet<SeatTemplate> SeatTemplates { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<Trip> Trips { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<Image> Images { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public string? SePayTransactionId { get; set; }
    }
}