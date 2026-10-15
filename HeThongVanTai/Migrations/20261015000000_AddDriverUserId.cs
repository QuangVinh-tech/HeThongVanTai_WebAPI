using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeThongVanTai.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Drivers",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_UserId",
                table: "Drivers",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Drivers_UserId", table: "Drivers");
            migrationBuilder.DropColumn(name: "UserId", table: "Drivers");
        }
    }
}
