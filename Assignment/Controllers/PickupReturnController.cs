using Assignment;
using Assignment.Models;
using Assignment.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers;

public class PickupReturnController : Controller
{
    private readonly ILogger<PickupReturnController> _logger;
    private readonly IWebHostEnvironment env;
    private readonly Helper _hp;
    private readonly DB _db;

    public PickupReturnController(ILogger<PickupReturnController> logger, IWebHostEnvironment en, Helper hp, DB db)
    {
        _logger = logger;
        env = en;
        _hp = hp;
        _db = db;
    }

    public IActionResult Index()
    {
        var today = DateTime.Today;

        // Today's Pickups (booked, not yet picked up)
        var pickups = _db.Rentals
            .Where(r => r.Status == "Booked" && r.RentalDate.Date == today)
            .Select(r => new {
                r.RentalId,
                CustomerName = r.Customer.Name,
                VehiclePlate = "",
                
            })
            .ToList();

        // Today's Returns (picked-up but not returned)
        var returns = _db.Rentals
            .Where(r => r.Status == "Pickup" && _db.PickupRecord
                    .Any(p => p.RentalId == r.RentalId && r.ReturnDate == today))
            .Select(r => new {
                r.RentalId,
                CustomerName = r.Customer.Name,
                VehiclePlate = _db.PickupRecord
                                  .Where(p => p.RentalId == r.RentalId)
                                  .Select(p => p.Vehicle.PlateNumber)
                                  .FirstOrDefault(),
                PickupDate = r.PickupDate,
            })
            .ToList();

        // Late Returns
        var lateReturns = _db.Rentals
            .Include(r => r.Customer)
            .Include(r => r.PickupRecord)
            .ThenInclude(p => p.Vehicle)
        .Where(r => r.Status == "Pickup"
             && r.PickupRecord != null
             && r.ReturnRecord == null
             && r.ReturnDate < today)
        .Select(r => new
        {
            r.RentalId,
            CustomerName = r.Customer.Name,
            VehiclePlate = r.PickupRecord.Vehicle.PlateNumber,
            PickupDate = r.PickupRecord.PickupDateTime, 
            DaysLate = EF.Functions.DateDiffDay(r.ReturnDate, today)
        })
        .ToList();




        ViewBag.Pickups = pickups;
        ViewBag.Returns = returns;
        ViewBag.LateReturns = lateReturns;

        return View();
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
        bool isValid = _db.Rentals
                          .Any(r => r.RentalId == rentalId
                           && r.Status == "Booked"
                           && !_db.PickupRecord.Any(p => p.RentalId == rentalId));

        if (!isValid)
        {
            return BadRequest("Invalid RentalId. Rental must exist, be booked, and not yet picked up.");
        }


        var rental = _db.Rentals
                       .Include(r => r.Customer)
                       .Include(r => r.Model)
                       .ThenInclude(m => m.Brand)
                       .FirstOrDefault(r => r.RentalId == rentalId);

        if (rental == null)
            return NotFound("Rental not found.");

        // get available vehicles
        var availableVehicles = _db.Vehicles
                                  .Where(v => v.ModelId == rental.ModelId && v.Available)
                                  .ToList();
        ViewBag.VehicleList = new SelectList(availableVehicles, "VehicleId", "PlateNumber");

        ViewBag.FuelList = new[] { "Full", "Half", "Low" };

        var vm = new PickupViewModel
        {
            RentalId = rentalId,
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

        _logger.LogInformation("POST Pickup called for VehicleId: {u}", vm.ExteriorPhoto);
        _logger.LogInformation("ExteriorPhoto: {file}", vm.ExteriorPhoto?.FileName ?? "null");
        _logger.LogInformation("ExteriorPhoto: {file}", _hp);

        // rental id exist in Rentals but not exist in PickupRecord
        // should also check the status of rental or not
        bool isValid = _db.Rentals
                           .Any(r => r.RentalId == vm.RentalId
                            && r.Status == "Booked"
                            && !_db.PickupRecord.Any(p => p.RentalId == vm.RentalId));

        if (!isValid)
        {
            return BadRequest("Invalid RentalId. Rental must exist, be booked, and not yet picked up.");
        }

        var v = _db.Vehicles.Find(vm.VehicleId);
        if (v == null)
        {
            ModelState.AddModelError("VehicleId", "Invalid VEHICLE ID");
        }

        var s = _db.Staffs.Find(vm.StaffId);
        if (s == null)
        {
            ModelState.AddModelError("VehicleId", "Invalid STAFF ID");
        }

        if (ModelState.IsValid("PickupDateTime"))
        {
            if(vm.PickupDateTime != DateTime.Today)
            {
                ModelState.AddModelError("PickupDateTime", "Only Today Date Allow");
            }
        }

        // Validate uploaded photos ModelState.isValid("")
        if (ModelState.IsValid("CustomerDrivingLicense"))
        {
            var e = _hp.ValidatePhoto(vm.CustomerDrivingLicense);
            if (e != "") ModelState.AddModelError("CustomerDrivingLicense", e);
        }
        if (ModelState.IsValid("ExteriorPhoto"))
        {
            var e = _hp.ValidatePhoto(vm.ExteriorPhoto);
            _logger.LogInformation("ExteriorPhoto: {file}", e);

            if (e != "") ModelState.AddModelError("ExteriorPhoto", e);
        }
        if (ModelState.IsValid("InteriorPhoto"))
        {
            var e = _hp.ValidatePhoto(vm.InteriorPhoto);
            if (e != "") ModelState.AddModelError("InteriorPhoto", e);
        }
        if (ModelState.IsValid("OdometerPhoto"))
        {
            var e = _hp.ValidatePhoto(vm.OdometerPhoto);
            if (e != "") ModelState.AddModelError("OdometerPhoto", e);
        }
        if (ModelState.IsValid("FuelPhoto"))
        {
            var e = _hp.ValidatePhoto(vm.FuelPhoto);
            if (e != "") ModelState.AddModelError("FuelPhoto", e);
        }

        // vehicle id, staff id , rental id
        // check rental id dun exist inside pickup
        // Save pickup record

        _logger.LogInformation("POST Redirect is valled: {RentalId}", vm.RentalId);
        _logger.LogInformation("POST Pickup called for RentalId: {RentalId}", vm.RentalId);
        _logger.LogInformation("POST Pickup called for ModelName: {RentalId}", ModelState.IsValid("ModelName"));
        _logger.LogInformation("POST Pickup called for CustomerName: {RentalId}", ModelState.IsValid("CustomerName"));
        _logger.LogInformation("POST Pickup called for FuelLevel: {RentalId}", ModelState.IsValid("FuelLevelPickup"));
        _logger.LogInformation("POST Pickup called for BodyCondition: {RentalId}", ModelState.IsValid("BodyCondition"));
        

        _logger.LogInformation("POST Pickup called for OdometerPickup: {RentalId}", ModelState.IsValid("InteriorCondition"));
        
       

        _logger.LogInformation("POST Pickup called for BodyCondition: {u}", ModelState.IsValid("BodyCondition"));
        _logger.LogInformation("POST Pickup called for StaffId: {u}", ModelState.IsValid("StaffId"));
        _logger.LogInformation("POST Pickup called for Model State: {u}", ModelState.IsValid);
        _logger.LogInformation("POST Pickup called for VehicleId: {u}", ModelState.IsValid("ExteriorPhoto"));
        
        _logger.LogInformation("POST Pickup called for rental id: {u}", ModelState.IsValid("RentalId"));
        _logger.LogInformation("POST Pickup called for VehicleId: {u}", ModelState.IsValid("VehicleId"));
        _logger.LogInformation("POST Pickup called for PickupDateTime: {u}", ModelState.IsValid("PickupDateTime"));
        _logger.LogInformation("POST Pickup called for rental id: {u}", ModelState.IsValid("CustomerDrivingLicense"));
        _logger.LogInformation("POST Pickup called for OdometerPickup: {RentalId}", ModelState.IsValid("OdometerPickup"));
        _logger.LogInformation("POST Pickup called for OdometerPickup: {RentalId}", ModelState.IsValid("FuelLevelPickup"));
        _logger.LogInformation("POST Pickup called for OdometerPickup: {RentalId}", ModelState.IsValid("BodyCondition"));
        _logger.LogInformation("POST Pickup called for OdometerPickup: {RentalId}", ModelState.IsValid("InteriorCondition"));
        _logger.LogInformation("POST Pickup called for OdometerPickup: {RentalId}", ModelState.IsValid("TyreCondition"));
        _logger.LogInformation("POST Pickup called for OdometerPickup: {RentalId}", ModelState.IsValid("LightsCondition"));
        _logger.LogInformation("POST Pickup called for OdometerPickup: {RentalId}", ModelState.IsValid("StaffId"));
        _logger.LogInformation("POST Pickup called for OdometerPickup: {RentalId}", ModelState.IsValid("ExteriorPhoto"));
        _logger.LogInformation("POST Pickup called for OdometerPickup: {RentalId}", ModelState.IsValid("InteriorPhoto"));
        _logger.LogInformation("POST Pickup called for OdometerPickup: {RentalId}", ModelState.IsValid("OdometerPhoto"));
        _logger.LogInformation("POST Pickup called for OdometerPickup: {RentalId}", ModelState.IsValid("FuelPhoto"));



        if (ModelState.IsValid("RentalId") && ModelState.IsValid("VehicleId") && ModelState.IsValid("PickupDateTime") && 
            ModelState.IsValid("CustomerDrivingLicense") && ModelState.IsValid("OdometerPickup") && ModelState.IsValid("FuelLevelPickup") && 
            ModelState.IsValid("BodyCondition") && ModelState.IsValid("InteriorCondition") && ModelState.IsValid("TyreCondition") &&
            ModelState.IsValid("LightsCondition") && ModelState.IsValid("StaffId") && ModelState.IsValid("ExteriorPhoto") &&
            ModelState.IsValid("InteriorPhoto") && ModelState.IsValid("OdometerPhoto") && ModelState.IsValid("FuelPhoto")
            )
        {
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

            // change rental status
            var rent = _db.Rentals.Find(vm.RentalId);
            if (rent != null) rent.Status = "Pickup";

            _db.SaveChanges();
            TempData["Info"] = "Pickup record saved successfully.";

        }


        // INFO : RESET VALUE AFTER POST / OPTIONAL HIDDEN VALUE ON UI
        var rental = _db.Rentals
                   .Include(r => r.Customer)
                   .Include(r => r.Model)
                   .ThenInclude(m => m.Brand)
                   .FirstOrDefault(r => r.RentalId == vm.RentalId);

        vm.RentalId = vm.RentalId;
        vm.CustomerName = rental.Customer.Name;
        vm.ModelName = rental.Model.ModelName;
        vm.StaffId = "STF0001";
        vm.StaffName = "John Staff";

        // selection list
        var availableVehicles = _db.Vehicles
                                  .Where(v => v.ModelId == rental.ModelId && v.Available)
                                  .ToList();
        ViewBag.VehicleList = new SelectList(availableVehicles, "VehicleId", "PlateNumber");
        ViewBag.FuelList = new[] { "Full", "Half", "Low" };
        
        return View(vm);

    }

    private string NextReturnId()
    {
        string max = _db.PickupRecord.Max(p => p.PickupId) ?? "RT0000";
        int n = int.Parse(max[2..]);
        return $"RT{(n + 1).ToString("0000")}";
    }

    // GET: Return
    public IActionResult Return(string rentalId)
    {
        bool isValid = _db.Rentals
                          .Any(r => r.RentalId == rentalId
                           && r.Status == "Pickup"
                           && !_db.ReturnRecord.Any(p => p.RentalId == rentalId));

        if (!isValid)
        {
            return BadRequest("Invalid RentalId. Rental must exist, be booked, and not yet picked up.");
        }

        var rental = _db.Rentals
            .Include(r => r.Customer)
            .Include(r => r.Model)
            .FirstOrDefault(r => r.RentalId == rentalId);

        if (rental == null)
            return NotFound("Rental not found.");

        var pickup = _db.PickupRecord
            .Include(p => p.Vehicle)
            .FirstOrDefault(p => p.RentalId == rentalId);

        if (pickup == null)
            return NotFound("Pickup record not found.");

        // Load vehicle details
        var vehicle = pickup.Vehicle;

        // Fill ViewModel
        var vm = new ReturnRecordVM
        {
            RentalId = rental.RentalId,
            CustomerName = rental.Customer.Name,
            ModelName = rental.Model.ModelName,
            PlateNumber = vehicle.PlateNumber,
            PickupDateTime = pickup.PickupDateTime,
            StaffId = "STF0001"
        };

        ViewBag.FuelList = new[] { "Full", "Half", "Low" };

        return View(vm);
    }

    [HttpPost]
    public IActionResult Return(ReturnRecordVM vm)
    {
        _logger.LogInformation("POST Return called for RentalId: {id}", vm.RentalId);

        // validate rental id and staff id
        bool isValidRental = _db.Rentals
            .Any(r => r.RentalId == vm.RentalId
                   && r.Status == "Pickup"
                   && !_db.ReturnRecord.Any(rr => rr.RentalId == vm.RentalId));

        if (!isValidRental)
        {
            return BadRequest("Invalid RentalId. Must be picked-up and not yet returned.");
        }

        var pickup = _db.PickupRecord
           .Include(p => p.Vehicle)
           .FirstOrDefault(p => p.RentalId == vm.RentalId);

        var vehicle = pickup.Vehicle;

        var s = _db.Staffs.Find(vm.StaffId);
        if (s == null)
            ModelState.AddModelError("StaffId", "Invalid Staff ID");

       // validate return date - only allow today
        if (ModelState.IsValid("ReturnDateTime"))
        {
            if (vm.ReturnDateTime.Date != DateTime.Today)
            {
                ModelState.AddModelError("ReturnDateTime", "Only today's date is allowed.");
            }
        }

        // validate odometer value - must greater than pickup
        if (vm.OdometerReturn <= pickup.OdometerPickup)
        {
            ModelState.AddModelError("OdometerReturn", "Return odometer must be greater than pickup odometer");
        }

        // validate late return day, fee, cleaning fee, 
        

        // photo validation
        if (ModelState.IsValid("ExteriorPhoto"))
        {
            var msg = _hp.ValidatePhoto(vm.ExteriorPhoto);
            if (msg != "") ModelState.AddModelError("ExteriorPhoto", msg);
        }

        if (ModelState.IsValid("InteriorPhoto"))
        {
            var msg = _hp.ValidatePhoto(vm.InteriorPhoto);
            if (msg != "") ModelState.AddModelError("InteriorPhoto", msg);
        }

        if (ModelState.IsValid("OdometerPhoto"))
        {
            var msg = _hp.ValidatePhoto(vm.OdometerPhoto);
            if (msg != "") ModelState.AddModelError("OdometerPhoto", msg);
        }

        if (ModelState.IsValid("FuelPhoto"))
        {
            var msg = _hp.ValidatePhoto(vm.FuelPhoto);
            if (msg != "") ModelState.AddModelError("FuelPhoto", msg);
        }

        if (vm.HasDamage && ModelState.IsValid("DamagePhoto"))
        {
            var msg = _hp.ValidatePhoto(vm.DamagePhoto);
            if (msg != "") ModelState.AddModelError("DamagePhoto", msg);
        }

        // save return record
        if (true)
        {
            var rec = new ReturnRecord
            {
                ReturnId = NextReturnId(),
                RentalId = vm.RentalId,
                ReturnDateTime = vm.ReturnDateTime,
                OdometerReturn = vm.OdometerReturn,
                FuelLevelReturn = vm.FuelLevelReturn,

                BodyCondition = vm.BodyCondition,
                InteriorCondition = vm.InteriorCondition,
                TyreCondition = vm.TyreCondition,
                LightsCondition = vm.LightsCondition,
                CleanlinessCondition = vm.CleanlinessCondition,

                HasDamage = vm.HasDamage,
                DamageDescription = vm.DamageDescription,
                DamageCost = vm.DamageCost,

                FuelCharge = vm.FuelCharge,
                LateReturnDay = vm.LateReturnDay,
                LateFee = vm.LateFee,
                CleaningFee = vm.CleaningFee,
                ExtraCharges = vm.ExtraCharges,
                TotalReturnCost = vm.TotalReturnCost,

                Remarks = vm.Remarks ?? "",

                StaffId = vm.StaffId,

                ExteriorPhotoPath = _hp.SavePhoto(vm.ExteriorPhoto, "PickupReturn"),
                InteriorPhotoPath = _hp.SavePhoto(vm.InteriorPhoto, "PickupReturn"),
                OdometerPhotoPath = _hp.SavePhoto(vm.OdometerPhoto, "PickupReturn"),
                FuelPhotoPath = _hp.SavePhoto(vm.FuelPhoto, "PickupReturn"),
                DamagePhotoPath = vm.HasDamage
                    ? _hp.SavePhoto(vm.DamagePhoto, "PickupReturn")
                    : null
            };

            _db.ReturnRecord.Add(rec);

            // Update Vehicle Availability
            var v = _db.Vehicles.FirstOrDefault(x => x.VehicleId == vehicle.VehicleId);
            if (v != null) vehicle.Available = true;

            // Update Rental Status
            var rental = _db.Rentals.Find(vm.RentalId);
            if (rental != null) rental.Status = "Returned";

            _db.SaveChanges();
            TempData["Info"] = "Return record saved successfully.";

        }

        // rebind info if error
        var rtn = _db.Rentals
            .Include(r => r.Customer)
            .Include(r => r.Model)
            .FirstOrDefault(r => r.RentalId == vm.RentalId);
       
        vm.RentalId = rtn.RentalId;
        vm.CustomerName = rtn.Customer.Name;
        vm.ModelName = rtn.Model.ModelName;
        vm.PlateNumber = vehicle.PlateNumber;
        vm.PickupDateTime = pickup.PickupDateTime;
        vm.StaffId = "STF0001";

        ViewBag.FuelList = new[] { "Full", "Half", "Low" };

        return View(vm);
    }

}
