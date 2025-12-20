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
    public string PickupId { get; set; }
    [Required]
    [StringLength(8)]
    [RegularExpression(@"RN\d{4}", ErrorMessage = "Invalid {0}.")]
    public string RentalId { get; set; }

    [Required]
    public DateTime PickupDateTime { get; set; } = DateTime.Now;

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
    [RegularExpression(@"STF\d{4}", ErrorMessage = "Invalid {0}.")]
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

    // ───────────────────────────────────────────
    // DB Saved Photo Paths (for display)
    // ───────────────────────────────────────────
    public string? ExteriorPhotoPath { get; set; }
    public string? InteriorPhotoPath { get; set; }
    public string? OdometerPhotoPath { get; set; }
    public string? FuelPhotoPath { get; set; }
}

public class ReturnRecordVM
{
    [Required]
    [MaxLength(8)]
    public string ReturnId { get; set; }

    [Required]
    [MaxLength(8)]
    [RegularExpression(@"VH\d{4}", ErrorMessage = "Invalid {0}.")]
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
    public decimal? FuelCharge { get; set; }
    public int? LateReturnDay { get; set; }
    public decimal? LateFee { get; set; }
    public decimal? CleaningFee { get; set; }
    public decimal? ExtraCharges { get; set; }
    public decimal? TotalReturnCost { get; set; }

    public string? Remarks { get; set; }

    // -----------------------------
    // STAFF
    // -----------------------------
    [Required]
    [RegularExpression(@"STF\d{4}", ErrorMessage = "Invalid {0}.")]
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

// -----------------------------
// Reservation 
// -----------------------------
public class ReserveVM
{
    public int ModelId { get; set; }

    [DataType(DataType.Date)]
    public DateTime RentalDate { get; set; }

    [DataType(DataType.Date)]

    public DateTime ReturnDate { get; set; }

    public decimal PricePerDay { get; set; }

    public decimal TotalPrice { get; set; }

    public decimal DepositAmount { get; set; }
}

// -----------------------------
// Payment
// -----------------------------
public class ReturnPaymentVM
{
    public string RentalId { get; set; }
    public string CustomerName { get; set; }

    public decimal Amount { get; set; }

    public string PaymentType { get; set; }   // ExtraCharge / Refund

    [Required(ErrorMessage = "Please select payment method")]
    public string PaymentMethod { get; set; } // Cash / TNG
}

public class PaymentVM
{
    [Required]
    public string RentalId { get; set; }

    public string CarModel { get; set; }

    [Display(Name = "Payment Date")]
    public DateTime PaymentDate { get; set; } = DateTime.Now;

    [Required]
    [Range(0.01, 100000, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required]
    [Display(Name = "Payment Type")]
    public string PaymentType { get; set; } // "Deposit", "Rental Fee", "Damage Fee"

    [Required]
    [Display(Name = "Payment Method")]
    public string PaymentMethod { get; set; } // "Credit Card", "Cash", "E-Wallet"
    public decimal TotalRentalPrice { get; set; }
    public decimal DepositRequired { get; set; }

    [Display(Name = "Card Number")]
    [RegularExpression(@"\d{16}", ErrorMessage = "Enter a valid 16-digit card number")]
    public string? CardNumber { get; set; }

    [Display(Name = "Expiry Date (MM/YY)")]
    [RegularExpression(@"^(0[1-9]|1[0-2])\/?([0-9]{2})$", ErrorMessage = "Invalid format MM/YY")]
    public string? ExpiryDate { get; set; }

    [Display(Name = "CVV")]
    [RegularExpression(@"\d{3}", ErrorMessage = "Invalid CVV")]
    public string? CVV { get; set; }

    public string CustomerName { get; set; }
}


// -----------------------------
// LOGIN 
// -----------------------------
public class LoginVM
    {
        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        public bool RememberMe { get; set; }
    }

// -----------------------------
// REGISTER
// -----------------------------
public class RegisterVM
{
    [Required, StringLength(100)]
    public string Name { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; }

    [Required, Phone]
    [StringLength(20)]
    [RegularExpression(@"^01\d-\d{7,8}$", ErrorMessage = "Invalid format (e.g. 012-3456789).")]
    public string PhoneNumber { get; set; }

    [Required, MinLength(5)]
    public string Password { get; set; }

    [Required, Compare("Password", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; }

    public IFormFile? Photo { get; set; }
}
// -----------------------------
// PROFILE
// -----------------------------
public class UpdateProfileVM
{
    public string? Email { get; set; }

    [Required]
    public string Name { get; set; }

    [Phone]
    [Display(Name = "Phone Number")]
    [StringLength(20)]
    [RegularExpression(@"^01\d-\d{7,8}$", ErrorMessage = "Invalid format (e.g. 012-3456789).")]
    public string? PhoneNumber { get; set; }

    public string? PhotoURL { get; set; }
    public IFormFile? Photo { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string? CurrentPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
    public string? ConfirmNewPassword { get; set; }
}

public class EditUserVM
{
    public string Id { get; set; } // Holds either StaffId or CustomerId
    [Required]
    public string Name { get; set; }
    [Required, EmailAddress]
    public string Email { get; set; }
    [Required]
    public string Phone { get; set; }
    public string UserType { get; set; } // "Staff" or "Customer"

    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
    public string? ConfirmNewPassword { get; set; }
}

// -----------------------------
// CarCatalog
// -----------------------------
public class VehicleCatalogViewModel
{
    public IEnumerable<CarModel> CarModels { get; set; }

    // === 改这里：把 int? 改成 string ===
    public string SelectedCategory { get; set; }
    public string SelectedBrandId { get; set; }
    // ===================================

    public string SearchTerm { get; set; }
    public SelectList CategoryList { get; set; }
    public SelectList BrandList { get; set; }
}
// -----------------------------
// Vehicle Category
// -----------------------------
public class VehicleCategoryViewModel
{
    // ID 在创建时不需要（自动生成），但在编辑时可能需要隐藏字段
    public string? CategoryId { get; set; }

    [Required(ErrorMessage = "Category Name is required")]
    [Display(Name = "Category Name")]
    [MaxLength(50)]
    public string? CategoryName { get; set; } // e.g., Sedan, SUV
}
