using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeThongVanTai.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingSoldBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SoldBy",
                table: "Bookings",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SoldBy", table: "Bookings");
        }
    }
}
