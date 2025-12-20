using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Migrations
{
    /// <inheritdoc />
    public partial class AddfrontImageToVehicle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "Rentals");

            migrationBuilder.AddColumn<string>(
                name: "backImage",
                table: "Vehicles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "frontImage",
                table: "Vehicles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sideImage",
                table: "Vehicles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "backImage",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "frontImage",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "sideImage",
                table: "Vehicles");

            migrationBuilder.AddColumn<string>(
                name: "PaymentId",
                table: "Rentals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
