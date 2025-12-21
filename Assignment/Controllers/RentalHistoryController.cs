using Assignment;
using Assignment.Models;
using Assignment.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Security.Claims;

namespace Assignment.Controllers;

public class RentalHistoryController : Controller
{
    private readonly ILogger<RentalHistoryController> _logger;
    private readonly IWebHostEnvironment env;
    private readonly Helper _hp;
    private readonly DB _db;

    public RentalHistoryController(ILogger<RentalHistoryController> logger, IWebHostEnvironment en, Helper hp, DB db)
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

    // Index: RentalHistory
    [Authorize]
    public IActionResult Index(string tab, 
                               string? search = null,
                               DateTime? pickupFrom = null,
                               DateTime? pickupTo = null,
                               DateTime? returnFrom = null,
                               DateTime? returnTo = null)
    {
        ViewBag.ActiveTab = tab ?? "All";

        var today = DateTime.Today;
        IQueryable<Rental> q = BaseQuery();

        // filter by role (see whether customer)
        // User.IsInRole("Customer")
        if (User.IsInRole("Customer"))
        {
            string id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            q = q.Where(r => r.Customer.CustomerId == id);
        }

        // tab filtering
        switch (tab)
        {
            case "Booked":
                q = q.Where(r => r.Status == "Booked");
                break;

            case "Pickup":
                q = q.Where(r => r.Status == "Pick up" || r.Status == "Pickup");
                break;

            case "Returned":
                q = q.Where(r => r.Status == "Returned");
                break;

            case "LateReturned":
                q = q.Where(r => r.Status == "LateReturned");
                break;

            case "LateDue":
                q = q.Where(r => r.Status == "LateDue");
                break;

            case "Cancelled":
                q = q.Where(r => r.Status == "Cancelled");
                break;

            case "Expired":
                q = q.Where(r => r.Status == "Expired");
                break;

            default:
                // "All"
                break;
        }

        // search input
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search?.Trim() ?? ""; 
            if (User.IsInRole("Customer"))
                q = q.Where(r => r.RentalId.Contains(search) || r.Model.ModelName.Contains(search) || r.PickupRecord.Vehicle.PlateNumber.Contains(search));
            else
                q = q.Where(r => r.RentalId.Contains(search) || r.Model.ModelName.Contains(search) || r.PickupRecord.Vehicle.PlateNumber.Contains(search) || r.Customer.Name.Contains(search));
        }

        // Pickup date filter
        if (pickupFrom.HasValue)
            q = q.Where(r => r.PickupDate.Date >= pickupFrom.Value.Date);
        if (pickupTo.HasValue)
            q = q.Where(r => r.PickupDate.Date <= pickupTo.Value.Date);

        // Return date filter
        if (returnFrom.HasValue)
            q = q.Where(r => r.ReturnDate.Date >= returnFrom.Value.Date);
        if (returnTo.HasValue)
            q = q.Where(r => r.ReturnDate.Date <= returnTo.Value.Date);


        // TODO
        if (Request.IsAjax())
        {
            return PartialView("_RentalHistory", q.ToList());//if is ajax return partial view
        }

        ViewBag.ActiveTab = tab;
        ViewBag.IsAdmin = User.IsInRole("Staff");

        return View(q.ToList());
    }


    // GET: Details
    [Authorize]
    public IActionResult Details(string id)
    {
        if (string.IsNullOrEmpty(id))
            return RedirectToAction("Index");

        var rental = _db.Rentals.Include(r => r.Customer)
                                .Include(r => r.Model)
                                .Include(r => r.Payment)
                                .Include(r => r.PickupRecord)
                                .ThenInclude(p => p.Vehicle)
                                .Include(r => r.PickupRecord)
                                .ThenInclude(p => p.Staff)
                                .Include(r => r.ReturnRecord)
                                .ThenInclude(rp => rp.Staff)
                                .FirstOrDefault(r => r.RentalId == id);

        if (rental == null)
            return RedirectToAction("Index");

        bool isStaff = false;
        ViewBag.IsStaff = isStaff;

        if (!isStaff)
        {
            var customerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (rental.CustomerId != customerId)
                return RedirectToAction("Index");
        }

        return View(rental);
    }


    private string NextPaymentId()
    {
        string max = _db.Payments.Max(p => p.PaymentId) ?? "PA0000";
        int n = int.Parse(max[2..]);
        return $"PA{(n + 1).ToString("0000")}";
    }

    [HttpPost]
    [Authorize(Roles ="Customer")]
    public IActionResult Cancel(string id)
    {
        var rental = _db.Rentals
            .Include(r => r.Customer)
            .FirstOrDefault(r => r.RentalId == id);

        if (rental == null)
            return RedirectToAction("Index");

        // Only allow cancel before pickup
        if (rental.Status != "Booked")
            TempData["Info"] = "Booking cannot be cancel after pickup.";

        var hoursBeforePickup = (rental.PickupDate - DateTime.Now).TotalHours;

        decimal refundAmount = 0m;

        if (hoursBeforePickup > 48)
        {
            // Refund deposit + booking fee
            refundAmount = rental.DepositAmount + rental.TotalPrice;
        }
        else
        {
            // Refund booking fee only
            refundAmount = rental.TotalPrice;
        }

        // Insert refund payment record
        var refundPayment = new Payment
        {
            PaymentId = NextPaymentId(),
            RentalId = rental.RentalId,
            Amount = refundAmount,
            PaymentType = "Refund",
            PaymentMethod = "Online Banking",
            Status = "Completed",
            Date = DateTime.Now
        };
        _db.Payments.Add(refundPayment);

        rental.Status = "Cancelled";
        if(hoursBeforePickup > 48)
        {
            SendCancellationEmail(rental.Customer.Email, rental, true);
        }
        else
        {
            SendCancellationEmail(rental.Customer.Email, rental, false);
        }


        _db.SaveChanges();
        return RedirectToAction("Details", new { id });
    }

    public void SendCancellationEmail(string email, Assignment.Models.Rental rental, Boolean refundable)
    {
        string customerName = rental.Customer?.Name ?? "Customer";
        string body = "";
        if (refundable)
        {
            body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{
            font-family: Helvetica, Arial, sans-serif;
            background-color: #f8f9fa;
            padding: 20px;
        }}
        .container {{
            max-width: 600px;
            margin: auto;
            background: #ffffff;
            padding: 25px;
            border-radius: 8px;
            border: 1px solid #e0e0e0;
        }}
        .title {{
            font-size: 18px;
            font-weight: bold;
            color: #212529;
            margin-bottom: 15px;
        }}
        .text {{
            font-size: 14px;
            color: #555;
            line-height: 1.6;
        }}
        .footer {{
            margin-top: 20px;
            font-size: 12px;
            color: #888;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='title'>Booking Cancellation Confirmation</div>

        <div class='text'>
            Dear {customerName},<br><br>

            Your booking (<strong>Ref: {rental.RentalId}</strong>) has been cancelled.<br><br>

            The <strong>booking fee and deposit will be refunded</strong> accordance with our policy.<br><br>

            Refund processing may take <strong>3–5 working days</strong>.
        </div>

        <div class='footer'>
            Regards,<br>
            Car Rental Admin
        </div>
    </div>
</body>
</html>";
        }
        else
        {
             body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{
            font-family: Helvetica, Arial, sans-serif;
            background-color: #f8f9fa;
            padding: 20px;
        }}
        .container {{
            max-width: 600px;
            margin: auto;
            background: #ffffff;
            padding: 25px;
            border-radius: 8px;
            border: 1px solid #e0e0e0;
        }}
        .title {{
            font-size: 18px;
            font-weight: bold;
            color: #212529;
            margin-bottom: 15px;
        }}
        .text {{
            font-size: 14px;
            color: #555;
            line-height: 1.6;
        }}
        .footer {{
            margin-top: 20px;
            font-size: 12px;
            color: #888;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='title'>Booking Cancellation Confirmation</div>

        <div class='text'>
            Dear {customerName},<br><br>

            Your booking (<strong>Ref: {rental.RentalId}</strong>) has been cancelled.<br><br>

            The <strong>booking fee will be refunded</strong>, while the
            <strong>deposit is non-refundable</strong> in accordance with our policy.<br><br>

            Refund processing may take <strong>3–5 working days</strong>.
        </div>

        <div class='footer'>
            Regards,<br>
            Car Rental Admin
        </div>
    </div>
</body>
</html>";
        }


            var mail = new MailMessage
            {
                Subject = "Booking Cancellation Confirmation",
                Body = body,
                IsBodyHtml = true
            };

        mail.To.Add(new MailAddress(email, customerName));

        // Send using your helper
        _hp.SendEmail(mail);
    }

}
