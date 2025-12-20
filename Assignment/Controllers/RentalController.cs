using Assignment.Models;
using Assignment.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers;

[Authorize(Roles = "Customer")]
public class RentalController(DB db) : Controller
{
    // ============================================================
    // RESERVE PAGE (GET)
    // ============================================================
    [HttpGet]
    public IActionResult Reserve(int? modelId, DateTime? pickedDate)
    {
        if (modelId == null) return RedirectToAction("Index", "CarCatalog");

        var model = db.CarModels
                      .Include(m => m.Brand)
                      .FirstOrDefault(m => m.ModelId == modelId);

        if (model == null) return RedirectToAction("Index", "CarCatalog");

        DateTime start = pickedDate ?? DateTime.Today;

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
    // RESERVE ACTION (POST)
    // ============================================================
    [HttpPost]
    public IActionResult Reserve(ReserveVM vm, int ModelId)
    {
        var model = db.CarModels
                      .Include(m => m.Brand)
                      .FirstOrDefault(m => m.ModelId == ModelId);

        if (model == null) return RedirectToAction("Index", "CarCatalog");


        if (vm.RentalDate.Date < DateTime.Today)
        {
            ModelState.AddModelError("RentalDate", "Date cannot be in the past.");
        }

        if (ModelState.IsValid)
        {
            int totalStock = db.Vehicles.Count(v => v.ModelId == ModelId && v.Available == true);

            int busyCount = db.Rentals
                .Where(r => r.ModelId == ModelId)
                .Where(r => r.Status != "Completed" && r.Status != "Cancelled")
                // Strict Check (<=) to match your requirement
                .Where(r => vm.RentalDate <= r.ReturnDate && r.PickupDate <= vm.ReturnDate)
                .Count();

            if ((totalStock - busyCount) <= 0)
            {
                ModelState.AddModelError("", "Sorry! This car became fully booked just now.");
            }
        }

        // SUCCESS -> REDIRECT TO PAYMENT
        if (ModelState.IsValid)
        {
            int days = (vm.ReturnDate - vm.RentalDate).Days;

            // Allow same-day return (0 days diff = 1 day charge)
            if (days < 0) days = 1;
            if (days == 0) days = 1;

            decimal finalTotal = days * model.Price;
            decimal finalDeposit = finalTotal * 0.2m;

            return RedirectToAction("MakePayment", "Payment", new
            {
                modelId = ModelId,
                rentalDate = vm.RentalDate.ToString("yyyy-MM-dd"),
                returnDate = vm.ReturnDate.ToString("yyyy-MM-dd"),
                totalPrice = finalTotal,
                deposit = finalDeposit
            });
        }

        // FAILURE -> RELOAD PAGE
        ViewBag.SelectedModel = model;
        ViewBag.ModelId = ModelId;
        vm.PricePerDay = model.Price;

        return View(vm);
    }

    // ============================================================
    // CHECK AVAILABILITY (Strict Logic)
    // ============================================================
    [HttpGet]
    public IActionResult CheckAvailability(int modelId, DateTime start, DateTime end)
    {
        // 1. Get total stock
        int totalStock = db.Vehicles.Count(v => v.ModelId == modelId && v.Available == true);

        // 2. Count busy cars
        int busyCount = db.Rentals
            .Where(r => r.ModelId == modelId)
            .Where(r => r.Status != "Completed" && r.Status != "Cancelled")
            // Strict Overlap (<=) blocks if dates even touch
            .Where(r => start <= r.ReturnDate && r.PickupDate <= end)
            .Count();

        // 3. Compare
        if (busyCount >= totalStock)
        {
            return Json(new { available = false, message = "This car is fully booked for the selected dates." });
        }

        return Json(new { available = true });
    }

    // ============================================================
    // CALENDAR DATA (Synced with Strict Logic)
    // ============================================================
    [HttpGet]
    public IActionResult GetUnavailableDates(int modelId, int month, int year)
    {
        int totalStock = db.Vehicles.Count(v => v.ModelId == modelId && v.Available == true);

        DateTime min = new DateTime(year, month, 1);
        DateTime max = min.AddMonths(1);

        var rentals = db.Rentals
            .Where(r => r.ModelId == modelId)
            .Where(r => r.Status != "Completed" && r.Status != "Cancelled")
            .Where(r => min <= r.ReturnDate && r.PickupDate <= max)
            .ToList();

        List<int> fullBookedDays = new List<int>();

        // Check everyday in the month
        for (var day = min; day < max; day = day.AddDays(1))
        {
            // Strict Count: If a rental touches this day, it counts.
            int busyCount = rentals.Count(r => day >= r.PickupDate && day <= r.ReturnDate);

            // Only turn Red if ALL cars are taken
            if (busyCount >= totalStock)
            {
                fullBookedDays.Add(day.Day);
            }
        }

        return Json(fullBookedDays.Distinct());
    }

    // ============================================================
    // DETAIL PAGE
    // ============================================================
    public IActionResult Detail(string id)
    {
        string username = User.Identity.Name;
        var currentCustomer = db.Customers.FirstOrDefault(c => c.Email == username || c.Name == username);

        if (currentCustomer == null) return RedirectToAction("Login", "Account");

        var rental = db.Rentals
                        .Include(r => r.Model)
                            .ThenInclude(m => m.Brand)
                        .Include(r => r.Model)
                            .ThenInclude(m => m.Category)
                        .Include(r => r.Customer)
                        .FirstOrDefault(r => r.RentalId == id);

        if (rental == null)
        {
            TempData["Info"] = "Receipt not found.";
            return RedirectToAction("Index", "Home");
        }

        if (rental.CustomerId != currentCustomer.CustomerId)
        {
            TempData["Info"] = "You are not authorized to view this receipt.";
            return RedirectToAction("Index", "Home");
        }

        return View(rental);
    }
}