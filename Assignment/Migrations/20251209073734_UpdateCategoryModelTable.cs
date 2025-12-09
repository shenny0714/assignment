using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCategoryModelTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_VehicleCategories_CategoryId",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_CategoryId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Vehicles");

            migrationBuilder.AddColumn<string>(
                name: "CategoryId",
                table: "CarModels",
                type: "nvarchar(8)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarModels_CategoryId",
                table: "CarModels",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_CarModels_VehicleCategories_CategoryId",
                table: "CarModels",
                column: "CategoryId",
                principalTable: "VehicleCategories",
                principalColumn: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CarModels_VehicleCategories_CategoryId",
                table: "CarModels");

            migrationBuilder.DropIndex(
                name: "IX_CarModels_CategoryId",
                table: "CarModels");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "CarModels");

            migrationBuilder.AddColumn<string>(
                name: "CategoryId",
                table: "Vehicles",
                type: "nvarchar(8)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CategoryId",
                table: "Vehicles",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_VehicleCategories_CategoryId",
                table: "Vehicles",
                column: "CategoryId",
                principalTable: "VehicleCategories",
                principalColumn: "CategoryId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
