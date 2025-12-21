using Assignment;
using Assignment.Models;
using Assignment.PDF;
using Assignment.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.Net.Mail;
using System.Reflection.Metadata;
using System.Security.Claims;



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

    private IQueryable<Rental> BaseQuery()
    {
        return _db.Rentals
                  .Include(r => r.Customer)
                  .Include(r => r.Model)
                  .Include(r => r.PickupRecord)
                  .ThenInclude(p => p.Vehicle)
                  .Include(r => r.ReturnRecord)
                  .OrderByDescending(r => r.PickupDate);
    }

    [Authorize(Roles = "Staff,Admin")]
    public IActionResult Index(string tab, bool todayOnly, string? search = null)
    {
        ViewBag.ActiveTab = tab ?? "All";
        ViewBag.TodayOnly = todayOnly; // pass to view to show toggle state

        var today = DateTime.Today;

        IQueryable<Rental> q = BaseQuery();

        switch (tab)
        {
            case "Pickup":
                q = q.Where(r => r.Status == "Booked");
                break;

            case "Return":
                q = q.Where(r => r.Status == "Pickup");
                break;

            case "LateDue":
                q = q.Where(r => r.Status == "LateDue");
                break;
            case "All":
            default:
                break;
        }

        if (todayOnly)
        {
            q = q.Where(r =>
                (r.Status == "Booked" && r.PickupDate == today) ||
                (r.Status == "Pickup" && r.ReturnDate.AddDays(1) == today)
            );
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(r => r.RentalId.Contains(search) ||
                             r.Model.ModelName.Contains(search) ||
                             r.PickupRecord.Vehicle.PlateNumber.Contains(search) ||
                             r.Customer.Name.Contains(search));
        }

        bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        if (isAjaxRequest)
        {
            return PartialView("_PickupReturn", q.ToList());
        }
        else
        {
            return View(q.ToList());
        }

    }


    // Generate next PickupId
    private string NextPickupId()
    {
        string max = _db.PickupRecord.Max(p => p.PickupId) ?? "PK0000";
        int n = int.Parse(max[2..]);
        return $"PK{(n + 1).ToString("0000")}";
    }

    // GET: Pickup page
    [Authorize(Roles = "Staff,Admin")]
    public IActionResult Pickup(string rentalId)
    {

        var rental = _db.Rentals
                       .Include(r => r.Customer)
                       .Include(r => r.Model)
                       .ThenInclude(m => m.Brand)
                       .FirstOrDefault(r => r.RentalId == rentalId);

        if (rental == null || rental.Status != "Booked" || _db.PickupRecord.Any(p => p.RentalId == rentalId))
            return RedirectToAction("Index");

        

        // get available vehicles
        var sessionDate = rental.PickupDate.Date;

        var occupiedVehicleIds = _db.PickupRecord
            .Where(p => p.Rental.Status != "Cancelled"
                        && p.Rental.PickupDate <= sessionDate
                        && p.Rental.ReturnDate >= sessionDate)
            .Select(p => p.VehicleId)
            .ToList();

        // Get available vehicles of this model
        var availableVehicles = _db.Vehicles
            .Where(v => v.ModelId == rental.ModelId
                        && v.Available
                        && !occupiedVehicleIds.Contains(v.VehicleId));
            
        ViewBag.VehicleList = new SelectList(availableVehicles, "VehicleId", "PlateNumber");

        ViewBag.FuelList = new[] { "Full", "Half", "Low" };

        var vm = new PickupViewModel
        {
            RentalId = rentalId,
            CustomerName = rental.Customer.Name,
            ModelName = rental.Model.ModelName,
            StaffId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            StaffName = User.Identity?.Name
        };

        return View(vm);
    }

    // POST: Pickup
    [HttpPost]
    [Authorize(Roles = "Staff,Admin")]
    public IActionResult Pickup(PickupViewModel vm)
    {
        // rental id exist in Rentals but not exist in PickupRecord
        // should also check the status of rental or not
        bool isValid = _db.Rentals
                           .Any(r => r.RentalId == vm.RentalId
                            && r.Status == "Booked"
                            && !_db.PickupRecord.Any(p => p.RentalId == vm.RentalId));

        if (!isValid)
        {
            return RedirectToAction("Index");
        }

        var v = _db.Vehicles.Find(vm.VehicleId);
        if (v == null)
        {
            ModelState.AddModelError("VehicleId", "Invalid VEHICLE ID");
        }

        var s = _db.Staffs.Find(vm.StaffId);
        if (s == null)
        {
            ModelState.AddModelError("ModelOnly", "Invalid STAFF ID");
        }

        if (ModelState.IsValid("PickupDateTime"))
        {
            if (vm.PickupDateTime.Date != DateTime.Today)
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

        if (ModelState.IsValid)
        {
            var record = new PickupRecord
            {
                PickupId = NextPickupId(),
                RentalId = vm.RentalId,
                VehicleId = vm.VehicleId,
                PickupDateTime = DateTime.Now,
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
            
            var rent = _db.Rentals.Find(vm.RentalId);
            if (rent != null) rent.Status = "Pickup";

            _db.SaveChanges();
            TempData["Success"] = "Pickup record saved successfully.";
            return RedirectToAction("Index"); 
            
        }


        // RESET VALUE AFTER POST / OPTIONAL HIDDEN VALUE ON UI
        var rental = _db.Rentals
                   .Include(r => r.Customer)
                   .Include(r => r.Model)
                   .ThenInclude(m => m.Brand)
                   .FirstOrDefault(r => r.RentalId == vm.RentalId);

        vm.RentalId = vm.RentalId;
        vm.CustomerName = rental.Customer.Name;
        vm.ModelName = rental.Model.ModelName;
        vm.StaffId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        vm.StaffName = User.Identity?.Name;

        // selection list
        // get available vehicles
        var sessionDate = rental.PickupDate.Date;

        var occupiedVehicleIds = _db.PickupRecord
            .Where(p => p.Rental.Status != "Cancelled"
                        && p.Rental.PickupDate <= sessionDate
                        && p.Rental.ReturnDate >= sessionDate)
            .Select(p => p.VehicleId)
            .ToList();

        // Get available vehicles of this model
        var availableVehicles = _db.Vehicles
            .Where(v => v.ModelId == rental.ModelId
                        && v.Available
                        && !occupiedVehicleIds.Contains(v.VehicleId));
        ViewBag.VehicleList = new SelectList(availableVehicles, "VehicleId", "PlateNumber");
        ViewBag.FuelList = new[] { "Full", "Half", "Low" };
        
        return View(vm);

    }

    private string NextReturnId()
    {
        string max = _db.ReturnRecord.Max(p => p.ReturnId) ?? "RR0000";
        int n = int.Parse(max[2..]);
        return $"RR{(n + 1).ToString("0000")}";
    }

    // GET: Return
    [Authorize(Roles = "Staff,Admin")]
    public IActionResult Return(string rentalId)
    {
        bool isValid = _db.Rentals
                          .Any(r => r.RentalId == rentalId
                           && (r.Status == "Pickup" || r.Status == "LateDue")
                           && !_db.ReturnRecord.Any(p => p.RentalId == rentalId));

        if (!isValid)
        {
            return RedirectToAction("Index");
        }

        var rental = _db.Rentals
            .Include(r => r.Customer)
            .Include(r => r.Model)
            .FirstOrDefault(r => r.RentalId == rentalId);

        if (rental == null)
            return RedirectToAction("Index");

        var pickup = _db.PickupRecord
            .Include(p => p.Vehicle)
            .FirstOrDefault(p => p.RentalId == rentalId);

        if (pickup == null)
            return RedirectToAction("Index");

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
            StaffId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        };

        ViewBag.FuelList = new[] { "Full", "Half", "Low" };

        return View(vm);
    }

    // POST: Return
    [HttpPost]
    [Authorize(Roles = "Staff,Admin")]
    public IActionResult Return(ReturnRecordVM vm)
    {
        foreach (var entry in ModelState)
        {
            bool loopValid = entry.Value.Errors.Count == 0;
            bool apiValid = ModelState.IsValid(entry.Key);

            _logger.LogWarning(
                "Field={Field}, LoopValid={LoopValid}, ApiValid={ApiValid}",
                entry.Key,
                loopValid,
                apiValid
            );
        }


        // validate rental id and staff id
        bool isValidRental = _db.Rentals
            .Any(r => r.RentalId == vm.RentalId
                   && (r.Status == "Pickup" || r.Status == "LateDue")
                   && !_db.ReturnRecord.Any(rr => rr.RentalId == vm.RentalId));

        if (!isValidRental)
        {
            return RedirectToAction("Index");
        }

        var pickup = _db.PickupRecord
           .Include(p => p.Vehicle)
           .FirstOrDefault(p => p.RentalId == vm.RentalId);

        if (pickup == null)
        {
            return RedirectToAction("Index");
        }
        var vehicle = pickup.Vehicle;

        var s = _db.Staffs.Find(vm.StaffId);
        if (s == null)
            return RedirectToAction("Index");

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


        var rental = _db.Rentals
                        .Include(r => r.Model)   
                        .FirstOrDefault(r => r.RentalId == vm.RentalId);


        // calc late fee
        var dueTime = rental.ReturnDate.AddDays(1).Date.AddHours(12);
        decimal lateFee = 0;
        int lateDays = 0;
        if (vm.ReturnDateTime > dueTime)
        {
            lateDays = (vm.ReturnDateTime.Date - dueTime.Date).Days + 1;
            lateFee = 50 + (lateDays - 1) * rental.Model.Price;
        }

        // calc fuel charge
        int FuelValue(string level) => level switch
        {
            "Full" => 2,
            "Half" => 1,
            "Low" => 0,
            _ => 0
        };

        var pickupFuel = FuelValue(pickup.FuelLevelPickup);
        var returnFuel = FuelValue(vm.FuelLevelReturn);

        decimal fuelCharge = 0;

        if (returnFuel < pickupFuel)
        {
            int diff = pickupFuel - returnFuel;

            // RM25 per level drop (test)
            fuelCharge = diff * 25;
            fuelCharge = (fuelCharge > 0) ? fuelCharge : 0;
        }

        // calc cleaming fee
        decimal cleaningFee = 0;
        if (vm.CleanlinessCondition == "Dirty")
            cleaningFee = 30;

        // damage cost n totalReturnCost
        decimal damageCost = vm.HasDamage ? vm.DamageCost ?? 0 : 0;
        decimal totalReturnCost =
            lateFee +
            fuelCharge +
            cleaningFee +
            damageCost +
            (vm.ExtraCharges ?? 0);

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

        if (vm.HasDamage && vm.DamagePhoto != null)
        {
            var msg = _hp.ValidatePhoto(vm.DamagePhoto);
            if (msg != "") ModelState.AddModelError("DamagePhoto", msg);
        }

        // save return record
        if (ModelState.IsValid)
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

                FuelCharge = fuelCharge,
                LateReturnDay = lateDays,
                LateFee = lateFee,
                CleaningFee = cleaningFee,
                ExtraCharges = vm.ExtraCharges,
                TotalReturnCost = totalReturnCost,

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

            // Update Rental Status
            
            if (rental != null)
            {
                if (rental.Status == "LateDue")
                    rental.Status = "LateReturned";
                else
                    rental.Status = "Returned";
            }

            _db.SaveChanges();
            TempData["Success"] = "Return record saved successfully.";
            return RedirectToAction("Invoice", new { rentalId = vm.RentalId });
        }

        // rebind info if got error in form
        var rtn = _db.Rentals
            .Include(r => r.Customer)
            .Include(r => r.Model)
            .FirstOrDefault(r => r.RentalId == vm.RentalId);
       
        vm.RentalId = rtn.RentalId;
        vm.CustomerName = rtn.Customer.Name;
        vm.ModelName = rtn.Model.ModelName;
        vm.PlateNumber = vehicle.PlateNumber;
        vm.PickupDateTime = pickup.PickupDateTime;
        vm.StaffId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        ViewBag.FuelList = new[] { "Full", "Half", "Low" };

        return View(vm);
    }

    // GET: Invoice
    [Authorize(Roles = "Staff,Admin")]
    public IActionResult Invoice(string rentalId)
    {
        if (string.IsNullOrEmpty(rentalId))
            return RedirectToAction("Index");

        var returnRec = _db.ReturnRecord.Include(r => r.Rental)
                                        .ThenInclude(r => r.Customer)
                                        .Include(r => r.Rental)
                                        .ThenInclude(r => r.Model)
                                        .FirstOrDefault(r => r.RentalId == rentalId);

        if (returnRec == null)
            return RedirectToAction("Index");

        var pickup = _db.PickupRecord
            .Include(p => p.Vehicle)
            .FirstOrDefault(p => p.RentalId == rentalId);

        decimal depositPaid = returnRec.Rental.DepositAmount;

        decimal amountDue = 0;
        decimal refund = 0;
        decimal totalExtra = returnRec.TotalReturnCost ?? 0m;

        if (totalExtra > depositPaid)
            amountDue = totalExtra - depositPaid;
        else
            refund = depositPaid - totalExtra;

        bool isReturnPaymentSettled = _db.Payments.Any(p =>
                                        p.RentalId == rentalId &&
                                        p.Status == "Paid" &&
                                        (p.PaymentType == "ExtraCharge" || p.PaymentType == "Refund")
                                    );

        ViewBag.IsPaid = isReturnPaymentSettled;
        ViewBag.DepositPaid = depositPaid;
        ViewBag.TotalExtra = totalExtra;
        ViewBag.AmountDue = amountDue;
        ViewBag.Refund = refund;

        return View(returnRec);
    }

    private string NextPaymentId()
    {
        string max = _db.Payments.Max(p => p.PaymentId) ?? "PA0000";
        int n = int.Parse(max[2..]);
        return $"PA{(n + 1).ToString("0000")}";
    }

    // GET: Payment
    [Authorize(Roles = "Staff,Admin")]
    public IActionResult ProceedPayment(string rentalId)
    {
        if (string.IsNullOrEmpty(rentalId))
            return RedirectToAction("Index");

        // check no exist payment for this rental id
        var existingReturnPayment = _db.Payments.FirstOrDefault(p =>
                                                p.RentalId == rentalId &&
                                                p.Status == "Paid" &&
                                                (p.PaymentType == "ExtraCharge" || p.PaymentType == "Refund")
                                            );

        if (existingReturnPayment != null)
        {
            TempData["Info"] = "Return payment has already been settled.";
            return RedirectToAction("Receipt", new { paymentId = existingReturnPayment.PaymentId });
        }


        // Load return record including rental and customer
        var returnRec = _db.ReturnRecord
            .Include(r => r.Rental)
            .ThenInclude(r => r.Customer)
            .FirstOrDefault(r => r.RentalId == rentalId);

        if (returnRec == null)
            return RedirectToAction("Index");

        decimal deposit = returnRec.Rental.DepositAmount;
        decimal totalExtra = returnRec.TotalReturnCost ?? 0m;

        decimal amountDue = 0;
        decimal refund = 0;

        if (totalExtra > deposit)
            amountDue = totalExtra - deposit;
        else
            refund = deposit - totalExtra;

        // If nothing to pay/refund, redirect to invoice
        if (amountDue == 0 && refund == 0)
            return RedirectToAction("Invoice", new { rentalId });

        // Create view model
        var vm = new ReturnPaymentVM
        {
            RentalId = rentalId,
            CustomerName = returnRec.Rental.Customer.Name ?? "Unknown",
            Amount = amountDue > 0 ? amountDue : refund,
            PaymentType = amountDue > 0 ? "ExtraCharge" : "Refund"
        };

        return View(vm);
    }

    // POST: Payment
    [HttpPost]
    [Authorize(Roles = "Staff,Admin")]
    public IActionResult ProceedPayment(ReturnPaymentVM vm)
    {
        if (!ModelState.IsValid)
            return View(vm);
        

        bool alreadySettled = _db.Payments.Any(p =>
                                    p.RentalId == vm.RentalId &&
                                    p.Status == "Paid" &&
                                    (p.PaymentType == "ExtraCharge" || p.PaymentType == "Refund")
                                );

        if (alreadySettled)
        {
            TempData["Error"] = "Return payment already processed.";
            return RedirectToAction("Index");
        }


        var payment = new Payment
        {
            PaymentId = NextPaymentId(),
            RentalId = vm.RentalId,
            Amount = vm.Amount,
            PaymentType = vm.PaymentType,     // ExtraCharge OR Refund
            PaymentMethod = vm.PaymentMethod, // Cash / TNG
            Status = "Paid",
            Date = DateTime.Now
        };

        _db.Payments.Add(payment);
        _db.SaveChanges();
        
        TempData["Success"] =
            vm.PaymentType == "Refund"
            ? $"Refund issued via {vm.PaymentMethod}."
            : $"Payment received via {vm.PaymentMethod}.";

        return RedirectToAction("Receipt", new { paymentId = payment.PaymentId });
    }

    // GET: Receipt
    [Authorize(Roles = "Staff,Admin")]
    public IActionResult Receipt(string paymentId)
    {
        var payment = _db.Payments
            .Include(p => p.Rental)
            .ThenInclude(r => r.Customer)
            .FirstOrDefault(p => p.PaymentId == paymentId);

        if (payment == null)
            return RedirectToAction("Index");

        return View(payment); // send model to Receipt.cshtml
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    // POST: EmailReceipt
    [HttpPost]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> EmailReceipt(string paymentId)
    {
        var payment = _db.Payments
            .Include(p => p.Rental)
            .ThenInclude(r => r.Customer)
            .FirstOrDefault(p => p.PaymentId == paymentId);

        if (payment == null)
        {
            return RedirectToAction("Index");
        }
        // generate PDF in memory
        var document = new ReceiptPdf(payment);
        byte[] pdfBytes;
        using (var ms = new MemoryStream())
        {
            document.GeneratePdf(ms);
            pdfBytes = ms.ToArray();
        }

        // validate customer email
        var email = payment.Rental.Customer.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
        {
            TempData["Error"] = "Customer email is invalid.";
            return RedirectToAction("Receipt", new { paymentId });
        }
        try
        {
            // create email
            var mail = new MailMessage
            {
                Subject = "Your Rental Receipt",
                Body = "Dear customer, please find attached your payment receipt.",
                IsBodyHtml = true
            };
            mail.To.Add(new MailAddress(email, payment.Rental.Customer.Name));

            // attach PDF
            mail.Attachments.Add(new Attachment(
                new MemoryStream(pdfBytes),
                $"Receipt_{payment.PaymentId}.pdf",
                "application/pdf"));

            // send email
            _hp.SendEmail(mail);

            TempData["Success"] = "Receipt sent to customer via email.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Failed to send receipt email. Please check logs.";
        }

        return RedirectToAction("Index");
    }
    [Authorize(Roles = "Staff,Admin")]
    public IActionResult DownloadReceiptPDF(string paymentId)
    {
        var payment = _db.Payments.Include(p => p.Rental).ThenInclude(r => r.Customer)
                                  .FirstOrDefault(p => p.PaymentId == paymentId);
        if (payment == null) return RedirectToAction("Index");

        var pdf = new ReceiptPdf(payment);
        var stream = new MemoryStream();
        pdf.GeneratePdf(stream);
        stream.Position = 0;
        return File(stream, "application/pdf", $"Receipt_{paymentId}.pdf");
    }


}
