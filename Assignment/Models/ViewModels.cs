using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;


namespace Assignment.ViewModels;

public class PickupViewModel
{
    // ───────────────────────────────────────────
    // Basic Pickup Info 
    // ───────────────────────────────────────────
    [MaxLength(8)]
    public string PickupId { get; set; }

    [Required]
    [MaxLength(8)]
    public string RentalId { get; set; }

    [Required]
    public DateTime PickupDateTime { get; set; } = DateTime.Now;

    // ───────────────────────────────────────────
    // Customer & Vehicle Info (for display in form)
    // ───────────────────────────────────────────
    public string CustomerName { get; set; }

    [Required]
    [Display(Name = "Driving Licence")]
    public string CustomerDrivingLicense { get; set; }

    public string VehicleId { get; set; }
    public string PlateNumber { get; set; }
    public string ModelName { get; set; }

    // ───────────────────────────────────────────
    // Pickup Details
    // ───────────────────────────────────────────

    [Required]
    [Range(0, int.MaxValue)]
    public int OdometerPickup { get; set; }

    [Required, MaxLength(20)]
    [Display(Name = "Fuel Level (Full / Half / Low)")]
    public string FuelLevelPickup { get; set; }

    [Required, MaxLength(6)]
    public string BodyCondition { get; set; }

    [Required, MaxLength(6)]
    public string InteriorCondition { get; set; }

    [Required, MaxLength(6)]
    public string TyreCondition { get; set; }

    [Required, MaxLength(6)]
    public string LightsCondition { get; set; }

    [MaxLength(100)]
    public string? Remarks { get; set; }

    // ───────────────────────────────────────────
    // Staff Handling Pickup
    // ───────────────────────────────────────────
    public string StaffId { get; set; }
    public string StaffName { get; set; }

    // ───────────────────────────────────────────
    // Photo Uploads (Form Only)
    // ───────────────────────────────────────────
    public IFormFile? ExteriorPhoto { get; set; }
    public IFormFile? InteriorPhoto { get; set; }
    public IFormFile? OdometerPhoto { get; set; }
    public IFormFile? FuelPhoto { get; set; }

    // ───────────────────────────────────────────
    // DB Saved Photo Paths (for display)
    // ───────────────────────────────────────────
    public string? ExteriorPhotoPath { get; set; }
    public string? InteriorPhotoPath { get; set; }
    public string? OdometerPhotoPath { get; set; }
    public string? FuelPhotoPath { get; set; }
}

