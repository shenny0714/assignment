using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;




namespace Assignment.ViewModels;

public class PickupViewModel
{
    // ───────────────────────────────────────────
    // Basic Pickup Info 
    // ───────────────────────────────────────────
    [Required]
    [StringLength(8)]
    [RegularExpression(@"RN\d{4}", ErrorMessage = "Invalid {0}.")]
    public string RentalId { get; set; }
    [Required]
    public DateTime PickupDateTime { get; set; } = DateTime.Today;

    // ───────────────────────────────────────────
    // Customer & Vehicle Info (for display in form)
    // ───────────────────────────────────────────
    public string CustomerName { get; set; }
    [Required]

    public string ModelName { get; set; }

    // ───────────────────────────────────────────
    // Pickup Details
    // ───────────────────────────────────────────

    [Display(Name = "Driving Licence")]
    public IFormFile CustomerDrivingLicense { get; set; }

    [Required]
    [RegularExpression(@"VH\d{4}", ErrorMessage = "Invalid {0}.")]
    public string VehicleId { get; set; }
    
    [Required]

    [Range(1, int.MaxValue, ErrorMessage = "Odometer must be positive.")]
    public int OdometerPickup { get; set; }
    [Required]
    [StringLength(20)]
    [Display(Name = "Fuel Level (Full / Half / Low)")]
    public string FuelLevelPickup { get; set; }
    [Required]
    [StringLength(6)]
    public string BodyCondition { get; set; }
    [Required]
    [StringLength(6)]
    public string InteriorCondition { get; set; }

    [Required]
    [StringLength(6)]
    public string TyreCondition { get; set; }

    [Required]
    [StringLength(6)]
    public string LightsCondition { get; set; }
    [StringLength(100)]
    public string? Remarks { get; set; }

    // ───────────────────────────────────────────
    // Staff Handling Pickup
    // ───────────────────────────────────────────
    [RegularExpression(@"ST\d{4}", ErrorMessage = "Invalid {0}.")]
    public string? StaffId { get; set; }
    public string? StaffName { get; set; }

    // ───────────────────────────────────────────
    // Photo Uploads (Form Only)
    // ───────────────────────────────────────────
    [Required]
    public IFormFile ExteriorPhoto { get; set; }
    [Required]
    public IFormFile InteriorPhoto { get; set; }
    [Required]
    public IFormFile OdometerPhoto { get; set; }
    [Required]
    public IFormFile FuelPhoto { get; set; }
}

public class ReturnRecordVM
{
    [Required]
    [StringLength(8)]
    [RegularExpression(@"RN\d{4}", ErrorMessage = "Invalid {0}.")]
    public string RentalId { get; set; }
    // ───────────────────────────────────────────
    // Customer & Vehicle Info (for display in form)
    // ───────────────────────────────────────────
    public string CustomerName { get; set; }
    public string ModelName { get; set; }
    public string PlateNumber { get; set; }
    public DateTime PickupDateTime { get; set; }

    // Display return date/time
    [Required]
    public DateTime ReturnDateTime { get; set; } = DateTime.Now;

    // -----------------------------
    // VEHICLE CONDITION
    // -----------------------------
    [Range(1, 100000, ErrorMessage = "Odometer must be between 0 and 100000.")]
    public int OdometerReturn { get; set; }

    [Required]
    public string FuelLevelReturn { get; set; }

    [Required]
    public string BodyCondition { get; set; }

    [Required]
    public string InteriorCondition { get; set; }

    [Required]
    public string TyreCondition { get; set; }

    [Required]
    public string LightsCondition { get; set; }

    [Required]
    public string CleanlinessCondition { get; set; }

    // -----------------------------
    // DAMAGE DETAILS
    // -----------------------------
    public bool HasDamage { get; set; }

    public string? DamageDescription { get; set; }

    [Range(1, 999999, ErrorMessage = "Damage cost must be a positive number.")]
    public decimal? DamageCost { get; set; }

    // -----------------------------
    // EXTRA CHARGES
    // -----------------------------
    //public decimal? FuelCharge { get; set; }
    //public int? LateReturnDay { get; set; }
    //public decimal? LateFee { get; set; }
    //public decimal? CleaningFee { get; set; }
    public decimal? ExtraCharges { get; set; }
    //public decimal? TotalReturnCost { get; set; }

    public string? Remarks { get; set; }

    // -----------------------------
    // STAFF
    // -----------------------------
    [Required]
    [RegularExpression(@"ST\d{4}", ErrorMessage = "Invalid {0}.")]
    public string StaffId { get; set; }

    // -----------------------------
    // PHOTO UPLOADS
    // -----------------------------
    [Required]
    public IFormFile ExteriorPhoto { get; set; }

    [Required]
    public IFormFile InteriorPhoto { get; set; }

    [Required]
    public IFormFile OdometerPhoto { get; set; }

    [Required]
    public IFormFile FuelPhoto { get; set; }

    public IFormFile? DamagePhoto { get; set; }
}

public class LateReturnVM
{
    [Display(Name = "Rental ID")]
    public string RentalId { get; set; }

    [Display(Name = "Late Days")]
    public int LateDays { get; set; }

    [Display(Name = "Late Fee (RM)")]
    [DataType(DataType.Currency)]
    public decimal LateFee { get; set; }
}

public class RentalStatusVM
{
    public string Status { get; set; }
    public int Count { get; set; }
}

public class ModelSummaryVM
{
    public string ModelName { get; set; }
    public int RentalCount { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class RevenueVM
{
    public string PaymentType { get; set; }
    public decimal Total { get; set; }
}