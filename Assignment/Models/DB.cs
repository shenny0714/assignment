using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Assignment.Models;

#nullable disable warnings


public class DB(DbContextOptions options) : DbContext(options)
{
    // DB SETS ----------------------------------------------------

    public DbSet<Staff> Staffs { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<VehicleCategory> VehicleCategories { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Rental> Rentals { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<PickupRecord> PickupRecord { get; set; }
    public DbSet<ReturnRecord> ReturnRecord { get; set; }
}



// MODEL CLASSES ==================================================

public class User
{
    [Key, MaxLength(100)]
    public string Email { get; set; }
    [MaxLength(100)]
    public string HashPassword { get; set; }
    [MaxLength(100)]
    public string Name { get; set; }
    [MaxLength(11)]
    public string Phone { get; set; }
    [MaxLength(100)]
    public string Role => GetType().Name;
}

// 1. STAFF (Admin + Staff Combined)

public class Staff : User
{
    [Key, MaxLength(8)]
    public string StaffId { get; set; }

    [MaxLength(6)] // "Admin" or "Staff"
    public string Type { get; set; }

    public List<Rental> RentalsHandled { get; set; } = [];

}

// 2. CUSTOMER -----------------------------------------------------

public class Customer : User
{
    [Key, MaxLength(8)]
    public string CustomerId { get; set; }

    [MaxLength(100)]
    public string PhotoURL { get; set; }

    public List<Rental> Rentals { get; set; } = [];
}



// 3. VEHICLE CATEGORY ---------------------------------------------

public class VehicleCategory
{
    [Key, MaxLength(8)]
    public string CategoryId { get; set; }

    [MaxLength(50)]
    public string CategoryName { get; set; }

    public List<Vehicle> Vehicles { get; set; } = [];
}



// 4. VEHICLE -------------------------------------------------------

public class Vehicle
{
    [Key, MaxLength(8)]
    public string VehicleId { get; set; }

    [MaxLength(50)]
    public string PlateNumber { get; set; }

    public string Brand { get; set; }
    public string Model { get; set; }

    public string CategoryId { get; set; }
    public VehicleCategory Category { get; set; }

    public bool Available { get; set; }

    public List<Rental> Rentals { get; set; } = [];
}



// 5. RENTAL --------------------------------------------------------

public class Rental
{
    [Key, MaxLength(8)]
    public string RentalId { get; set; }

    public string CustomerId { get; set; }
    public Customer Customer { get; set; }

    public string VehicleId { get; set; }
    public Vehicle Vehicle { get; set; }

    public DateTime RentalDate { get; set; }
    public DateTime? ReturnDate { get; set; }

    [Precision(10, 2)]
    public decimal DepositAmount { get; set; }


    [Precision(10, 2)]
    public decimal TotalPrice { get; set; }

    [MaxLength(15)]
    public string Status { get; set; }

    public Payment Payment { get; set; }
    public PickupRecord PickupRecord { get; set; }
    public ReturnRecord ReturnRecord { get; set; }
    public Review Review { get; set; }
}



// 6. PAYMENT -------------------------------------------------------

public class Payment
{
    [Key, MaxLength(8)]
    public string PaymentId { get; set; }

    public string RentalId { get; set; }
    public Rental Rental { get; set; }

    [Precision(10, 2)]
    public decimal Amount { get; set; }

    public DateTime Date { get; set; }
}



// 7. PICKUP & RETURN ----------------------------------------------

public class PickupRecord
{
    [Key, MaxLength(8)]
    public string PickupId { get; set; }

    public string RentalId { get; set; }
    public Rental Rental { get; set; }

    public DateTime PickupDateTime { get; set; }


    public int OdometerPickup { get; set; }

    [MaxLength(20)]
    public string FuelLevelPickup { get; set; } = string.Empty;

    // Condition checklist
    [MaxLength(2)]
    public string BodyCondition { get; set; } = string.Empty;

    [MaxLength(2)]
    public string InteriorCondition { get; set; } = string.Empty;

    [MaxLength(2)]
    public string TyreCondition { get; set; } = string.Empty;

    [MaxLength(2)]
    public string LightsCondition { get; set; } = string.Empty;

    // Remarks
    [MaxLength(100)]
    public string? Remarks { get; set; } = string.Empty;

    // Staff
    public string StaffId { get; set; }
    public Staff Staff { get; set; }

    // Photo file paths stored in DB
    public string? ExteriorPhotoPath { get; set; }
    public string? InteriorPhotoPath { get; set; }
    public string? OdometerPhotoPath { get; set; }
    public string? FuelPhotoPath { get; set; }
}


public class ReturnRecord
{
    [Key, MaxLength(8)]
    public string ReturnId { get; set; }

    public string RentalId { get; set; }
    public Rental Rental { get; set; }

    public DateTime ReturnDateTime { get; set; }

    public int OdometerReturn { get; set; }

    [MaxLength(20)]
    public string FuelLevelReturn { get; set; } = string.Empty;


    [MaxLength(2)]
    public string BodyCondition { get; set; } = string.Empty;

    [MaxLength(2)]
    public string InteriorCondition { get; set; } = string.Empty;

    [MaxLength(2)]
    public string TyreCondition { get; set; } = string.Empty;

    [MaxLength(2)]
    public string LightsCondition { get; set; } = string.Empty;

    [MaxLength(2)]
    public string CleanlinessCondition { get; set; } = string.Empty;


    // TRUE if any damage detected
    public bool HasDamage { get; set; }

    // Staff description of damage
    [MaxLength(500)]
    public string? DamageDescription { get; set; }

    // Cost of repairing vehicle body
    [Precision(10, 2)]
    public decimal? DamageCost { get; set; }

    // Fuel penalty
    [Precision(10, 2)]
    public decimal? FuelCharge { get; set; }

    // use to calc late fee
    public int? LateReturnDay { get; set; }

    // Late return fee
    [Precision(10, 2)]
    public decimal? LateFee { get; set; }

    // Cleaning fee if vehicle is dirty
    [Precision(10, 2)]
    public decimal? CleaningFee { get; set; }

    // Any custom charges added manually
    [Precision(10, 2)]
    public decimal? ExtraCharges { get; set; }

    // Total charge for return
    [Precision(10, 2)]
    public decimal? TotalReturnCost { get; set; }

    [MaxLength(500)]
    public string Remarks { get; set; } = string.Empty;


    public string StaffId { get; set; }
    public Staff staff { get; set; }

    public string? ExteriorPhotoPath { get; set; }
    public string? InteriorPhotoPath { get; set; }
    public string? OdometerPhotoPath { get; set; }
    public string? FuelPhotoPath { get; set; }
    public string? DamagePhotoPath { get; set; }  // extra for damage evidence
}
