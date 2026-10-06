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
            modelBuilder.Entity<BusRoute>().HasOne(r => r.FromStation).WithMany()
                .HasForeignKey(r => r.FromStationId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<BusRoute>().HasOne(r => r.ToStation).WithMany()
                .HasForeignKey(r => r.ToStationId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ticket>().HasIndex(t => new { t.TripId, t.SeatCode })
                .IsUnique().HasFilter("[Status] <> 'Cancelled'");

         
            modelBuilder.Entity<Ticket>().HasIndex(t => t.TicketCode)
                .IsUnique().HasFilter("[TicketCode] <> ''");
            modelBuilder.Entity<Promotion>().HasIndex(p => p.Code).IsUnique();
           
        }

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
    }
}