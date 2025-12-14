using Microsoft.AspNetCore.Mvc;
using Assignment;
using Assignment.Models;
using Assignment.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

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

    // Central base query (includes common navigation props)
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
    public IActionResult Index(string tab, 
                               string? search = null,
                               DateTime? pickupFrom = null,
                               DateTime? pickupTo = null,
                               DateTime? returnFrom = null,
                               DateTime? returnTo = null)
    {
        ViewBag.ActiveTab = tab ?? "All";

        var today = DateTime.Today;

        // 1. Start with base
        IQueryable<Rental> q = BaseQuery();

        // filter by role (see whether customer)
        // User.IsInRole("Customer")
        if (true)
        {
            string id = "CU0001";
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
            // User.IsInRole("Customer")
            if (true)
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
        // User.IsInRole("Admin")
        // ViewBag.IsAdmin = null;

        return View(q.ToList());
    }


    // GET: Details
    public IActionResult Details(string id)
    {
        if (string.IsNullOrEmpty(id))
            return RedirectToAction("Index");

        var rental = _db.Rentals
                        .Include(r => r.Customer)
                        .Include(r => r.Model)
                        .Include(r => r.PickupRecord)
                        .ThenInclude(p => p.Vehicle)
                        .Include(r => r.PickupRecord)
                        .ThenInclude(p => p.Staff)
                        .Include(r => r.ReturnRecord)
                        .ThenInclude(rp => rp.Staff)
                        .FirstOrDefault(r => r.RentalId == id);

        if (rental == null)
            return RedirectToAction("Index");

        //User.IsInRole("Admin") || User.IsInRole("Staff")
        bool isStaff = false;
        ViewBag.IsStaff = isStaff;

        if (!isStaff)
        {
            // check iflogin user id == rental customer id?
            //  User.FindFirst("CustomerId")?.Value
            var customerId = "CU0001";
            if (rental.CustomerId != customerId)
                return RedirectToAction("Index");
        }

        return View(rental);
    }

    [HttpPost]
    public IActionResult Cancel(string id)
    {
        var rental = _db.Rentals.FirstOrDefault(r => r.RentalId == id);

        if (rental == null)
            return NotFound();

        // Only allow cancel before pickup
        if (rental.Status != "Booked")
            TempData["Info"] = "Booking cannot be cancel after pickup.";

        rental.Status = "Cancelled";
        _db.SaveChanges();

        return RedirectToAction("Details", new { id });
    }

}
