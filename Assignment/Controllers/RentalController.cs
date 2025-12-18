using Assignment.Models;
using Assignment.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers;

public class RentalController(DB db) : Controller
{
    // ============================================================
    // 1. SEARCH PAGE (GET)
    // ============================================================
    public IActionResult Test(DateTime? pickedDate)
    {
        // 1. Determine Search Date (Default to Today)
        DateTime searchDate = pickedDate ?? DateTime.Today;

        // 2. 12 PM RULE: If searching Today & it's past 12pm -> Force Tomorrow
        if (searchDate.Date == DateTime.Today && DateTime.Now.Hour >= 12)
        {
            searchDate = DateTime.Today.AddDays(1);
            TempData["Info"] = "It is past 12:00 PM. Same-day bookings are closed. Showing cars for Tomorrow.";
        }
        
        // Prevent searching in the past
        if (searchDate < DateTime.Today) searchDate = DateTime.Today;

        ViewBag.UserSearchDate = searchDate.ToString("yyyy-MM-dd");

        // 3. Get Fleet Data
        var models = db.CarModels
            .Include(m => m.Brand)
            .Include(m => m.Vehicles)
            .OrderBy(m => m.Price)
            .ToList();

        // 4. FILTER LOGIC: Total Stock - Busy Cars = Available Stock
        foreach (var model in models)
        {
            int totalCars = db.Vehicles.Count(v => v.ModelId == model.ModelId && v.Available == true);

            // Busy if: Status is active AND Date overlaps
            int busyCount = db.Rentals
                .Where(r => r.ModelId == model.ModelId)
                .Where(r => r.Status == "Reserved" || r.Status == "Booked" || r.Status == "Ongoing")
                .Where(r => searchDate >= r.PickupDate && searchDate < r.ReturnDate)
                .Count();

            int availableStock = totalCars - busyCount;
            if (availableStock < 0) availableStock = 0;

            // Update List for View
            model.Vehicles.Clear();
            for (int i = 0; i < availableStock; i++)
            {
                model.Vehicles.Add(new Vehicle()); 
            }
        }

        return View(models);
    }

    // ============================================================
    // 2. RESERVE PAGE (GET)
    // ============================================================
    public IActionResult Reserve(int? modelId, DateTime? pickedDate)
    {
        if (modelId == null) return RedirectToAction("Test");

        var model = db.CarModels.Find(modelId);
        if (model == null) return RedirectToAction("Test");

        // 1. Default to Today or Picked Date
        DateTime start = pickedDate ?? DateTime.Today;

        // 2. Enforce 12 PM Rule
        if (start.Date <= DateTime.Today && DateTime.Now.Hour >= 12)
        {
            start = DateTime.Today.AddDays(1);
        }

        var vm = new ReserveVM
        {
            ModelId = model.ModelId,
            RentalDate = start,
            ReturnDate = start.AddDays(1),
            PricePerDay = model.Price,
            TotalPrice = model.Price,
            DepositAmount = model.Price * 0.2m
        };

        ViewBag.SelectedModel = model;
        ViewBag.ModelId = model.ModelId;

        return View(vm);
    }

    // ============================================================
    // 3. RESERVE ACTION (POST)
    // ============================================================
    [HttpPost]
    public IActionResult Reserve(ReserveVM vm, int ModelId)
    {
        // 1. Validate Model Exists
        var model = db.CarModels.Find(ModelId);
        if (model == null) return RedirectToAction("Test");

        // 2. SERVER-SIDE VALIDATION: 12 PM RULE
        if (vm.RentalDate.Date == DateTime.Today && DateTime.Now.Hour >= 12)
        {
            ModelState.AddModelError("RentalDate", "It is past 12:00 PM. Please select a date starting from Tomorrow.");
        }

        // 3. SERVER-SIDE VALIDATION: Past Dates
        if (vm.RentalDate.Date < DateTime.Today)
        {
             ModelState.AddModelError("RentalDate", "Date cannot be in the past.");
        }

        // 4. STOCK CHECK (Concurrency)
        // Only run if dates are valid so far
        if (ModelState.IsValid)
        {
            int totalStock = db.Vehicles.Count(v => v.ModelId == ModelId && v.Available == true);

            int busyCount = db.Rentals
                .Where(r => r.ModelId == ModelId)
                .Where(r => r.Status == "Reserved" || r.Status == "Booked" || r.Status == "Ongoing")
                .Where(r => vm.RentalDate < r.ReturnDate && r.PickupDate < vm.ReturnDate) // Overlap Formula
                .Count();

            if ((totalStock - busyCount) <= 0)
            {
                ModelState.AddModelError("", "Sorry! This car became fully booked just now.");
            }
        }

        // 5. IF SUCCESS -> REDIRECT TO PAYMENT
        if (ModelState.IsValid)
        {
            int days = (vm.ReturnDate - vm.RentalDate).Days;
            if (days < 1) days = 1;

            decimal finalTotal = days * model.Price;
            decimal finalDeposit = finalTotal * 0.2m;

            // REDIRECT TO "Payment/MakePayment" (GET)
            // We pass the data in the URL.
            return RedirectToAction("MakePayment", "Payment", new
            {
                modelId = ModelId,
                rentalDate = vm.RentalDate.ToString("yyyy-MM-dd"), // Pass as String
                returnDate = vm.ReturnDate.ToString("yyyy-MM-dd"), // Pass as String
                totalPrice = finalTotal,
                deposit = finalDeposit
                // NOTE: We do NOT pass 'paymentMethod' here.
                // This ensures the Payment Controller shows the selection screen first.
            });
        }

        // 6. IF FAILURE -> RELOAD PAGE WITH ERRORS
        ViewBag.SelectedModel = model;
        ViewBag.ModelId = ModelId;
        vm.PricePerDay = model.Price;
        
        return View(vm);
    }
    // ============================================================
    // 4. RENTAL RECEIPT / DETAIL PAGE
    // ============================================================
    public IActionResult Detail(string id)
    {
        // Find the rental by ID (e.g., "RN0005")
        var rental = db.Rentals
            .Include(r => r.Model)         
            .ThenInclude(m => m.Brand)     
            .FirstOrDefault(r => r.RentalId == id);

        if (rental == null)
        {
            TempData["Error"] = "Rental record not found.";
            return RedirectToAction("Test");
        }

        return View(rental);
    }
}