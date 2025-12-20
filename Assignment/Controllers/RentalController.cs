using Assignment.Models;
using Assignment.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace Assignment.Controllers;

public class RentalController(DB db) : Controller
{
    // ============================================================
    // 2. RESERVE PAGE (GET)
    // ============================================================
    public IActionResult Reserve(int? modelId, DateTime? pickedDate)
    {
        if (modelId == null) return RedirectToAction("Index", "CarCatalog");

        var model = db.CarModels.Find(modelId);
        if (model == null) return RedirectToAction("Index", "CarCatalog");

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
        if (model == null) return RedirectToAction("Index", "CarCatalog");

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

            return RedirectToAction("MakePayment", "Payment", new
            {
                modelId = ModelId,
                rentalDate = vm.RentalDate.ToString("yyyy-MM-dd"), // Pass as String
                returnDate = vm.ReturnDate.ToString("yyyy-MM-dd"), // Pass as String
                totalPrice = finalTotal,
                deposit = finalDeposit

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

        return View(rental);
    }
}