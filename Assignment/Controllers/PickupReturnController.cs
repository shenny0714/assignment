using Assignment;
using Assignment.Models;
using Assignment.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers;

public class PickupReturnController : Controller
{
    private readonly ILogger<PickupReturnController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly Helper _hp;
    private readonly DB _db;

    public PickupReturnController(ILogger<PickupReturnController> logger, IWebHostEnvironment en, Helper hp, DB db)
    {
        _logger = logger;
        _env = en;
        _hp = hp;
        _db = db;
    }

    // Generate next PickupId
    private string NextPickupId()
    {
        string max = _db.PickupRecord.Max(p => p.PickupId) ?? "PK0000";
        int n = int.Parse(max[2..]);
        return $"PK{(n + 1).ToString("0000")}";
    }

    // GET: Pickup page
    public IActionResult Pickup(string rentalId)
    {
        if (string.IsNullOrEmpty(rentalId))
            return BadRequest("RentalId is required.");

        var rental = _db.Rentals
            .Include(r => r.Customer)
            .Include(r => r.Model)
                .ThenInclude(m => m.Brand)
            .FirstOrDefault(r => r.RentalId == rentalId);

        if (rental == null)
            return NotFound("Rental not found.");

        // Populate available vehicles
        var availableVehicles = _db.Vehicles
            .Where(v => v.ModelId == rental.ModelId && v.Available)
            .ToList();
        ViewBag.VehicleList = new SelectList(availableVehicles, "VehicleId", "PlateNumber");

        // Prepare ViewModel
        var vm = new PickupViewModel
        {
            RentalId = rental.RentalId,
            CustomerName = rental.Customer.Name,
            ModelName = rental.Model.ModelName,
            StaffId = "STF0001",      // for testing; replace with session user
            StaffName = "John Staff"
        };

        return View(vm);
    }

    // POST: Pickup
    [HttpPost]
    public IActionResult Pickup(PickupViewModel vm)
    {
        _logger.LogInformation("POST Pickup called for RentalId: {RentalId}", vm.RentalId);
        _logger.LogInformation("POST Pickup called for PickupDateTime: {u}", ModelState.IsValid("PickupDateTime"));
        _logger.LogInformation("POST Pickup called for VehicleId: {u}", ModelState.IsValid("VehicleId"));
        _logger.LogInformation("POST Pickup called for CustomerDrivingLicense: {u}", _hp.ValidatePhoto(vm.CustomerDrivingLicense));
        _logger.LogInformation("POST Pickup called for ExteriorPhoto: {u}", _hp.ValidatePhoto(vm.ExteriorPhoto));
        _logger.LogInformation("POST Pickup called for InteriorPhoto: {u}", _hp.ValidatePhoto(vm.InteriorPhoto));
        _logger.LogInformation("POST Pickup called for OdometerPhoto: {u}", _hp.ValidatePhoto(vm.OdometerPhoto));
        _logger.LogInformation("POST Pickup called for FuelPhoto: {u}", _hp.ValidatePhoto(vm.FuelPhoto));
        _logger.LogInformation("POST Pickup called for BodyCondition: {u}", ModelState.IsValid("BodyCondition"));
        _logger.LogInformation("POST Pickup called for StaffId: {u}", ModelState.IsValid("StaffId"));
        _logger.LogInformation("POST Pickup called for VehicleId: {u}", ModelState.IsValid(""));

        // Validate uploaded photos
        if (vm.CustomerDrivingLicense != null)
        {
            var e = _hp.ValidatePhoto(vm.CustomerDrivingLicense);
            if (e != "") ModelState.AddModelError("CustomerDrivingLicense", e);
        }
        if (vm.ExteriorPhoto != null)
        {
            var e = _hp.ValidatePhoto(vm.ExteriorPhoto);
            if (e != "") ModelState.AddModelError("ExteriorPhoto", e);
        }
        if (vm.InteriorPhoto != null)
        {
            var e = _hp.ValidatePhoto(vm.InteriorPhoto);
            if (e != "") ModelState.AddModelError("InteriorPhoto", e);
        }
        if (vm.OdometerPhoto != null)
        {
            var e = _hp.ValidatePhoto(vm.OdometerPhoto);
            if (e != "") ModelState.AddModelError("OdometerPhoto", e);
        }
        if (vm.FuelPhoto != null)
        {
            var e = _hp.ValidatePhoto(vm.FuelPhoto);
            if (e != "") ModelState.AddModelError("FuelPhoto", e);
        }

        // Save pickup record
        var record = new PickupRecord
        {
            PickupId = NextPickupId(),
            RentalId = vm.RentalId,
            VehicleId = vm.VehicleId,
            PickupDateTime = vm.PickupDateTime,
            CustomerDrivingLisence = _hp.SavePhoto(vm.CustomerDrivingLicense, "PickupReturn"),
            OdometerPickup = vm.OdometerPickup,
            FuelLevelPickup = vm.FuelLevelPickup,
            BodyCondition = vm.BodyCondition,
            InteriorCondition = vm.InteriorCondition,
            TyreCondition = vm.TyreCondition,
            LightsCondition = vm.LightsCondition,
            Remarks = vm.Remarks ?? "",
            StaffId = vm.StaffId,
            ExteriorPhotoPath = _hp.SavePhoto(vm.ExteriorPhoto, "PickupReturn"),
            InteriorPhotoPath = _hp.SavePhoto(vm.InteriorPhoto, "PickupReturn"),
            OdometerPhotoPath = _hp.SavePhoto(vm.OdometerPhoto, "PickupReturn"),
            FuelPhotoPath = _hp.SavePhoto(vm.FuelPhoto, "PickupReturn")
        };

        _db.PickupRecord.Add(record);

        // Set vehicle availability to false
        var vehicle = _db.Vehicles.FirstOrDefault(v => v.VehicleId == vm.VehicleId);
        if (vehicle != null) vehicle.Available = false;

        _db.SaveChanges();

        TempData["Info"] = "Pickup record saved successfully.";
        _logger.LogInformation("POST Redirect is valled: {RentalId}", vm.RentalId);
        // Redirect to avoid duplicate POST
        return RedirectToAction("Pickup", new { rentalId = vm.RentalId});
    }
}
