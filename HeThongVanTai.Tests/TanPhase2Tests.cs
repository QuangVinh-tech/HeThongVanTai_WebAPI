using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
using HeThongVanTai.Controllers;
using HeThongVanTai.Data;
using HeThongVanTai.Models.Domain;
using HeThongVanTai.Models.DTO;
using HeThongVanTai.Repositories;

namespace HeThongVanTai.Tests
{
    public class FakeImageRepository : IImageRepository
    {
        public Task<Image> Upload(Image image)
        {
            image.Id = 1;
            image.FilePath = $"https://localhost:7200/Images/{image.FileName}";
            return Task.FromResult(image);
        }

        public Task<System.Collections.Generic.IEnumerable<Image>> GetAll() =>
            Task.FromResult<System.Collections.Generic.IEnumerable<Image>>(Array.Empty<Image>());
    }

    public class TanPhase2Tests
    {
        private AppDbContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task CreateVehicle_DuplicatePlate_ReturnsBadRequest400()
        {
            var context = GetInMemoryContext();
            context.VehicleTypes.Add(new VehicleType { Id = 1, Name = "Limousine", SeatCount = 34 });
            context.Vehicles.Add(new Vehicle { Id = 1, Plate = "49B-123.45", VehicleTypeId = 1, Status = "Active" });
            await context.SaveChangesAsync();

            var controller = new VehiclesController(context);
            var result = await controller.Create(new VehicleDto { Plate = "49b-123.45", VehicleTypeId = 1 });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreateTrip_DriverExpiredLicense_ReturnsBadRequest400()
        {
            var context = GetInMemoryContext();
            context.Stations.AddRange(new Station { Id = 1, Name = "Da Lat" }, new Station { Id = 2, Name = "Sai Gon" });
            context.BusRoutes.Add(new BusRoute { Id = 1, Name = "DL-SG", FromStationId = 1, ToStationId = 2, DurationMin = 360 });
            context.VehicleTypes.Add(new VehicleType { Id = 1, Name = "Limousine", SeatCount = 34 });
            context.Vehicles.Add(new Vehicle { Id = 1, Plate = "49B-111.11", VehicleTypeId = 1, Status = "Active", RegistrationExpiry = DateTime.Today.AddMonths(3) });
            context.Drivers.Add(new Driver { Id = 1, FullName = "Tai xe A", Status = "Active", LicenseExpiry = DateTime.Today.AddDays(-5) });
            await context.SaveChangesAsync();

            var controller = new TripsController(context);
            var result = await controller.CreateTrip(new CreateTripDto
            {
                BusRouteId = 1,
                VehicleId = 1,
                DriverId = 1,
                DepartAt = DateTime.Today.AddDays(1),
                Price = 300000
            });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreateTrip_OverlappingVehicleSchedule_ReturnsBadRequest400()
        {
            var context = GetInMemoryContext();
            var departTime = DateTime.Today.AddDays(1).AddHours(8);

            context.Stations.AddRange(new Station { Id = 1, Name = "Da Lat" }, new Station { Id = 2, Name = "Sai Gon" });
            context.BusRoutes.Add(new BusRoute { Id = 1, Name = "DL-SG", FromStationId = 1, ToStationId = 2, DurationMin = 360 });
            context.VehicleTypes.Add(new VehicleType { Id = 1, Name = "Limousine", SeatCount = 34 });
            context.Vehicles.Add(new Vehicle { Id = 1, Plate = "49B-111.11", VehicleTypeId = 1, Status = "Active", RegistrationExpiry = DateTime.Today.AddMonths(6) });
            context.Drivers.AddRange(
                new Driver { Id = 1, FullName = "Tai xe 1", Status = "Active", LicenseExpiry = DateTime.Today.AddYears(2) },
                new Driver { Id = 2, FullName = "Tai xe 2", Status = "Active", LicenseExpiry = DateTime.Today.AddYears(2) }
            );
            context.Trips.Add(new Trip
            {
                Id = 1,
                BusRouteId = 1,
                VehicleId = 1,
                DriverId = 1,
                DepartAt = departTime,
                ArriveAt = departTime.AddHours(6),
                Status = "Open"
            });
            await context.SaveChangesAsync();

            var controller = new TripsController(context);
            var result = await controller.CreateTrip(new CreateTripDto
            {
                BusRouteId = 1,
                VehicleId = 1,
                DriverId = 2,
                DepartAt = departTime.AddHours(2),
                Price = 300000
            });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UploadImage_InvalidExtension_ReturnsBadRequest400()
        {
            var controller = new ImagesController(new FakeImageRepository());
            var bytes = Encoding.UTF8.GetBytes("fake-exe-content");
            var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", "malware.exe");

            var result = await controller.Upload(new ImageUploadRequestDto { File = file });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UploadImage_Exceeds5MB_ReturnsBadRequest400()
        {
            var controller = new ImagesController(new FakeImageRepository());
            var oversizedBytes = new byte[6 * 1024 * 1024];
            var file = new FormFile(new MemoryStream(oversizedBytes), 0, oversizedBytes.Length, "File", "bigphoto.jpg");

            var result = await controller.Upload(new ImageUploadRequestDto { File = file });

            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}