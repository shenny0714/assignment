using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment.Models;

#nullable disable warnings

public class DB(DbContextOptions<DB> options) : DbContext(options)
{
    public DbSet<Staff> Staffs { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<VehicleCategory> VehicleCategories { get; set; }
    public DbSet<Brand> Brands { get; set; }
    public DbSet<CarModel> CarModels { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Rental> Rentals { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<PickupRecord> PickupRecord { get; set; }
    public DbSet<ReturnRecord> ReturnRecord { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Prevent multiple cascade paths for PickupRecord → Vehicle
        modelBuilder.Entity<PickupRecord>()
            .HasOne(p => p.Vehicle)
            .WithMany()
            .HasForeignKey(p => p.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional: explicitly define cascade for Rental → PickupRecord
        modelBuilder.Entity<PickupRecord>()
            .HasOne(p => p.Rental)
            .WithOne(r => r.PickupRecord)
            .HasForeignKey<PickupRecord>(p => p.RentalId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional: ReturnRecord → Rental
        modelBuilder.Entity<ReturnRecord>()
            .HasOne(r => r.Rental)
            .WithOne(rn => rn.ReturnRecord)
            .HasForeignKey<ReturnRecord>(r => r.RentalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}



//
// ──────────────────────────────────────
// USER BASE CLASS
// ──────────────────────────────────────
//
public class User
{
    public string Email { get; set; }

    [MaxLength(100)]
    public string HashPassword { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    [MaxLength(11)]
    public string Phone { get; set; }
    public string Role => GetType().Name;
}

//
// ──────────────────────────────────────
// STAFF
// ──────────────────────────────────────
//
public class Staff : User
{
    [Key, MaxLength(8)]
    public string StaffId { get; set; }

    [MaxLength(6)]
    public string Type { get; set; }  // Admin / Staff

    public List<Rental> RentalsHandled { get; set; } = [];
}

//
// ──────────────────────────────────────
// CUSTOMER
// ──────────────────────────────────────
//
public class Customer : User
{
    [Key, MaxLength(8)]
    public string CustomerId { get; set; }

    public string? PhotoURL { get; set; }

    public List<Rental> Rentals { get; set; } = [];

    public int LoginRetryCount { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }
}

//
// ──────────────────────────────────────
// VEHICLE CATEGORY
// ──────────────────────────────────────
//
public class VehicleCategory
{
    [Key, MaxLength(8)]
    public string CategoryId { get; set; }

    [MaxLength(50)]
    public string CategoryName { get; set; }

    public List<CarModel> CarModels { get; set; } = [];
}

//
// ──────────────────────────────────────
//  BRAND TABLE
// ──────────────────────────────────────
//
public class Brand
{
    [Key]
    public int BrandId { get; set; }

    [MaxLength(50)]
    public string BrandName { get; set; }

    public List<CarModel> Models { get; set; } = [];
}

//
// ──────────────────────────────────────
//  CAR MODEL TABLE
//  Customer selects THIS first
// ──────────────────────────────────────
//
public class CarModel
{
    [Key]
    public int ModelId { get; set; }
    [Required]
    public int BrandId { get; set; }
    public Brand Brand { get; set; }

    [MaxLength(50)]
    public string ModelName { get; set; }
    [Precision(6,2)]
    public decimal Price { get; set; }
    public string Description { get; set; }
    public string? ImagePathFront { get; set; }
    public string? ImagePathSide { get; set; }
    public string? ImagePathBack { get; set; }
    public string CategoryId { get; set; }
    public VehicleCategory Category { get; set; }
    public List<Vehicle> Vehicles { get; set; } = [];
}

//
// ──────────────────────────────────────
// VEHICLE  (physical car)
// ──────────────────────────────────────
//
public class Vehicle
{
    [Key, MaxLength(8)]
    public string VehicleId { get; set; }

    [MaxLength(50)]
    public string PlateNumber { get; set; }
    public bool Available { get; set; }
    public int ModelId { get; set; }
    public CarModel? Model { get; set; }
    //add
    public string? frontImage { get; set; }
    public string? sideImage { get; set; }
    public string? backImage { get; set; }

}

//
// ──────────────────────────────────────
// RENTAL
//  Customer chooses Model first
//  Vehicle assigned later (nullable)
// ──────────────────────────────────────
//
public class Rental
{
    [Key, MaxLength(8)]
    public string RentalId { get; set; }
    public string CustomerId { get; set; }
    public Customer Customer { get; set; }

    // Customer selects this first
    public int ModelId { get; set; }
    public CarModel Model { get; set; }

    [DataType(DataType.Date)]
    public DateTime RentalDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime PickupDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime ReturnDate { get; set; }

    [Precision(10, 2)]
    public decimal DepositAmount { get; set; }

    [Precision(10, 2)]
    public decimal TotalPrice { get; set; }

    [MaxLength(15)]
    public string Status { get; set; }
    public List<Payment> Payment { get; set; }
    public PickupRecord PickupRecord { get; set; }
    public ReturnRecord ReturnRecord { get; set; }
}

//
// ──────────────────────────────────────
// PAYMENT
// ──────────────────────────────────────
//
public class Payment
{
    [Key, MaxLength(8)]
    public string PaymentId { get; set; }
    public string RentalId { get; set; }
    public Rental Rental { get; set; }

    [Precision(10, 2)]
    public decimal Amount { get; set; }
    public string PaymentType { get; set; }
    // Full Payment / ExtraCharge / Refund
    public string PaymentMethod { get; set; }
    public string Status { get; set; }

    public DateTime Date { get; set; }
}

//
// ──────────────────────────────────────
// PICKUP RECORD
// ──────────────────────────────────────
//
public class PickupRecord
{
    [Key, MaxLength(8)]
    public string PickupId { get; set; }

    public string RentalId { get; set; }
    public Rental Rental { get; set; }
    public DateTime PickupDateTime { get; set; }

    public string CustomerDrivingLisence { get; set; }
    public string VehicleId { get; set; }
    public Vehicle Vehicle { get; set; }

    [Range(1,100000)]
    public int OdometerPickup { get; set; }
    public string FuelLevelPickup { get; set; }

    public string BodyCondition { get; set; }
    public string InteriorCondition { get; set; }
    public string TyreCondition { get; set; }
    public string LightsCondition { get; set; }

    public string? Remarks { get; set; }

    public string StaffId { get; set; }
    public Staff Staff { get; set; }

    public string ExteriorPhotoPath { get; set; }
    public string InteriorPhotoPath { get; set; }
    public string OdometerPhotoPath { get; set; }
    public string FuelPhotoPath { get; set; }
}

//
// ──────────────────────────────────────
// RETURN RECORD
// ──────────────────────────────────────
//
public class ReturnRecord
{
    [Key, MaxLength(8)]
    public string ReturnId { get; set; }
    public string RentalId { get; set; }
    public Rental Rental { get; set; }
    public DateTime ReturnDateTime { get; set; }

    [Range(1, 100000)]
    public int OdometerReturn { get; set; }
    public string FuelLevelReturn { get; set; }

    public string BodyCondition { get; set; }
    public string InteriorCondition { get; set; }
    public string TyreCondition { get; set; }
    public string LightsCondition { get; set; }
    public string CleanlinessCondition { get; set; }

    public bool HasDamage { get; set; }
    public string? DamageDescription { get; set; }
    [Precision(10, 2)]
    public decimal? DamageCost { get; set; }
    [Precision(10, 2)]
    public decimal? FuelCharge { get; set; }

    public int? LateReturnDay { get; set; }
    [Precision(10, 2)]
    public decimal? LateFee { get; set; }
    [Precision(10, 2)]
    public decimal? CleaningFee { get; set; }
    public decimal? ExtraCharges { get; set; }
    [Precision(10, 2)]
    public decimal? TotalReturnCost { get; set; }

    public string? Remarks { get; set; }

    public string StaffId { get; set; }
    public Staff Staff { get; set; }

    public string ExteriorPhotoPath { get; set; }
    public string InteriorPhotoPath { get; set; }
    public string OdometerPhotoPath { get; set; }
    public string FuelPhotoPath { get; set; }
    public string? DamagePhotoPath { get; set; }
}
