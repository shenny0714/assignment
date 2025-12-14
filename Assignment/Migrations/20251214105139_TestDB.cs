using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Migrations
{
    /// <inheritdoc />
    public partial class TestDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    BrandId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BrandName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.BrandId);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    CustomerId = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    PhotoURL = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HashPassword = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.CustomerId);
                });

            migrationBuilder.CreateTable(
                name: "Staffs",
                columns: table => new
                {
                    StaffId = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HashPassword = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staffs", x => x.StaffId);
                });

            migrationBuilder.CreateTable(
                name: "VehicleCategories",
                columns: table => new
                {
                    CategoryId = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleCategories", x => x.CategoryId);
                });

            migrationBuilder.CreateTable(
                name: "CarModels",
                columns: table => new
                {
                    ModelId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BrandId = table.Column<int>(type: "int", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    CategoryId = table.Column<string>(type: "nvarchar(8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarModels", x => x.ModelId);
                    table.ForeignKey(
                        name: "FK_CarModels_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "BrandId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CarModels_VehicleCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "VehicleCategories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rentals",
                columns: table => new
                {
                    RentalId = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(8)", nullable: false),
                    ModelId = table.Column<int>(type: "int", nullable: false),
                    RentalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PickupDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    IsDepositRefunded = table.Column<bool>(type: "bit", nullable: false),
                    StaffId = table.Column<string>(type: "nvarchar(8)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rentals", x => x.RentalId);
                    table.ForeignKey(
                        name: "FK_Rentals_CarModels_ModelId",
                        column: x => x.ModelId,
                        principalTable: "CarModels",
                        principalColumn: "ModelId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rentals_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rentals_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId");
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    VehicleId = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    PlateNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ModelId = table.Column<int>(type: "int", nullable: false),
                    Available = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.VehicleId);
                    table.ForeignKey(
                        name: "FK_Vehicles_CarModels_ModelId",
                        column: x => x.ModelId,
                        principalTable: "CarModels",
                        principalColumn: "ModelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    PaymentId = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    RentalId = table.Column<string>(type: "nvarchar(8)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PaymentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.PaymentId);
                    table.ForeignKey(
                        name: "FK_Payments_Rentals_RentalId",
                        column: x => x.RentalId,
                        principalTable: "Rentals",
                        principalColumn: "RentalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReturnRecord",
                columns: table => new
                {
                    ReturnId = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    RentalId = table.Column<string>(type: "nvarchar(8)", nullable: false),
                    ReturnDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OdometerReturn = table.Column<int>(type: "int", nullable: false),
                    FuelLevelReturn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyCondition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InteriorCondition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TyreCondition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LightsCondition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CleanlinessCondition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HasDamage = table.Column<bool>(type: "bit", nullable: false),
                    DamageDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DamageCost = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    FuelCharge = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    LateReturnDay = table.Column<int>(type: "int", nullable: true),
                    LateFee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    CleaningFee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    ExtraCharges = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalReturnCost = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StaffId = table.Column<string>(type: "nvarchar(8)", nullable: false),
                    ExteriorPhotoPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InteriorPhotoPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OdometerPhotoPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FuelPhotoPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DamagePhotoPath = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnRecord", x => x.ReturnId);
                    table.ForeignKey(
                        name: "FK_ReturnRecord_Rentals_RentalId",
                        column: x => x.RentalId,
                        principalTable: "Rentals",
                        principalColumn: "RentalId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReturnRecord_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PickupRecord",
                columns: table => new
                {
                    PickupId = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    RentalId = table.Column<string>(type: "nvarchar(8)", nullable: false),
                    PickupDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerDrivingLisence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VehicleId = table.Column<string>(type: "nvarchar(8)", nullable: false),
                    OdometerPickup = table.Column<int>(type: "int", nullable: false),
                    FuelLevelPickup = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyCondition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InteriorCondition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TyreCondition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LightsCondition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StaffId = table.Column<string>(type: "nvarchar(8)", nullable: false),
                    ExteriorPhotoPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InteriorPhotoPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OdometerPhotoPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FuelPhotoPath = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickupRecord", x => x.PickupId);
                    table.ForeignKey(
                        name: "FK_PickupRecord_Rentals_RentalId",
                        column: x => x.RentalId,
                        principalTable: "Rentals",
                        principalColumn: "RentalId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PickupRecord_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PickupRecord_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CarModels_BrandId",
                table: "CarModels",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_CarModels_CategoryId",
                table: "CarModels",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RentalId",
                table: "Payments",
                column: "RentalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickupRecord_RentalId",
                table: "PickupRecord",
                column: "RentalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickupRecord_StaffId",
                table: "PickupRecord",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PickupRecord_VehicleId",
                table: "PickupRecord",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Rentals_CustomerId",
                table: "Rentals",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Rentals_ModelId",
                table: "Rentals",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Rentals_StaffId",
                table: "Rentals",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRecord_RentalId",
                table: "ReturnRecord",
                column: "RentalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRecord_StaffId",
                table: "ReturnRecord",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ModelId",
                table: "Vehicles",
                column: "ModelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PickupRecord");

            migrationBuilder.DropTable(
                name: "ReturnRecord");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "Rentals");

            migrationBuilder.DropTable(
                name: "CarModels");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "Staffs");

            migrationBuilder.DropTable(
                name: "Brands");

            migrationBuilder.DropTable(
                name: "VehicleCategories");
        }
    }
}
