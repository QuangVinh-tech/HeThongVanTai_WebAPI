using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HeThongVanTai.Models.Domain;

namespace HeThongVanTai.Data
{
    public class VanTaiAuthDbContext : IdentityDbContext
    {
        public VanTaiAuthDbContext(DbContextOptions<VanTaiAuthDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<IdentityRole>().HasData(
                NewRole("a0000000-0000-4000-8000-000000000001", AppRoles.Admin),
                NewRole("a0000000-0000-4000-8000-000000000002", AppRoles.Operator),
                NewRole("a0000000-0000-4000-8000-000000000003", AppRoles.Seller),
                NewRole("a0000000-0000-4000-8000-000000000004", AppRoles.Accountant),
                NewRole("a0000000-0000-4000-8000-000000000005", AppRoles.Customer));
        }

        private static IdentityRole NewRole(string id, string name) => new IdentityRole
        {
            Id = id,
            ConcurrencyStamp = id,
            Name = name,
            NormalizedName = name.ToUpper()
        };
    }
}