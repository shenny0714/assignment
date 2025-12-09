using Assignment.Models;
using Assignment.ViewModels;
using Assignment;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers;

public class PickupReturnController(IWebHostEnvironment en,
                            Helper hp, DB db) : Controller
{
    public IActionResult Pickup(string rentalId)
    {
        if (string.IsNullOrEmpty(rentalId))
            return BadRequest("RentalId is required.");

        var rental = db.Rentals
                       .Include(r => r.Customer)
                       .Include(r => r.Vehicle)
                       .Include(r => r.Model)
                       .ThenInclude(m => m.Brand)
                       .FirstOrDefault(r => r.RentalId == rentalId);


        if (rental == null)
            return NotFound("Rental not found.");

        var vm = new PickupViewModel
        {
            RentalId = rental.RentalId,
            CustomerName = rental.Customer.Name,
            VehicleId = rental.VehicleId,
            PlateNumber = rental.Vehicle?.PlateNumber,
            ModelName = rental.Model.ModelName,
            StaffId = "ST0001",      // hardcode for testing
            StaffName = "John Staff" // replace with login session
        };

        return View(vm);
    }
}

//[HttpPost]
    //public IActionResult Create(PickupViewModel vm)
    //{
    //    if (!ModelState.IsValid)
    //        return View(vm);

    //    // 1. Generate Pickup ID
    //    vm.PickupId = "PK" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

    //    if (ModelState.IsValid("photo"))
    //    {
    //        var e = hp.ValidatePhoto(photo);
    //        if (e != "") ModelState.AddModelError("photo", e);
    //    }

    //    if (ModelState.IsValid)
    //    {
    //        // TODO
    //        //var path = Path.Combine(en.WebRootPath, "uploads", photo.FileName);
    //        //using var stream = System.IO.File.Create(path);
    //        //photo.CopyTo(stream);
    //        hp.SavePhoto(photo, "uploads");

    //        TempData["Info"] = "Photo uploaded.";
    //        return RedirectToAction();
    //    }

    //    var record = new PickupRecord
    //    {
    //        PickupId = vm.PickupId,
    //        RentalId = vm.RentalId,
    //        PickupDateTime = vm.PickupDateTime,
    //        CustomerDrivingLisence = vm.CustomerDrivingLicense,
    //        OdometerPickup = vm.OdometerPickup,
    //        FuelLevelPickup = vm.FuelLevelPickup,
    //        BodyCondition = vm.BodyCondition,
    //        InteriorCondition = vm.InteriorCondition,
    //        TyreCondition = vm.TyreCondition,
    //        LightsCondition = vm.LightsCondition,
    //        Remarks = vm.Remarks,
    //        StaffId = vm.StaffId,
    //        ExteriorPhotoPath = Upload(vm.ExteriorPhoto),
    //        InteriorPhotoPath = Upload(vm.InteriorPhoto),
    //        OdometerPhotoPath = Upload(vm.OdometerPhoto),
    //        FuelPhotoPath = Upload(vm.FuelPhoto)
    //    };

    //    db.PickupRecord.Add(record);

    //    // 3. Mark vehicle unavailable
    //    var rental = db.Rentals.Include(r => r.Vehicle)
    //                            .First(r => r.RentalId == vm.RentalId);

    //    rental.Vehicle.Available = false;

    //    // 4. Save all changes
    //    _db.SaveChanges();

    //    return RedirectToAction("Success");
    //}

