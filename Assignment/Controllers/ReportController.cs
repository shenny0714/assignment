using Assignment.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers;

public class ReportController : Controller
{
    private readonly DB _db;

    public ReportController(DB db) => _db = db;

    // -------------------------
    // Dashboard
    // -------------------------
    public IActionResult Index(DateTime? start, DateTime? end)
    {
        start ??= DateTime.Today.AddDays(-6); // last 7 days
        end ??= DateTime.Today;

        ViewBag.Start = start.Value.ToString("yyyy-MM-dd");
        ViewBag.End = end.Value.ToString("yyyy-MM-dd");

        return View();
    }

    // -------------------------
    // Partial Views for 2x2 layout
    // -------------------------
    public IActionResult RentalStatusChart() => PartialView("_RentalStatusChart");
    public IActionResult RevenueChart() => PartialView("_RevenueChart");
    public IActionResult ModelChart() => PartialView("_ModelChart");
    public IActionResult LateReturnChart() => PartialView("_LateReturnChart");
    // -------------------------
    // JSON endpoints for charts (EF Core compatible date filtering)
    // -------------------------

    [HttpGet]
    public IActionResult RentalStatusChartData(DateTime start, DateTime end)
    {
        var startDate = start.Date;
        var endDate = end.Date.AddDays(1); // exclusive end

        var data = _db.Rentals
            .Where(r => r.RentalDate >= startDate && r.RentalDate < endDate)
            .GroupBy(r => r.Status)
            .Select(g => new { Label = g.Key, Value = g.Count() })
            .ToList();

        if (!data.Any()) data.Add(new { Label = "No Data", Value = 0 });
        return Json(data);
    }

    [HttpGet]
    public IActionResult RevenueChartData(DateTime start, DateTime end)
    {
        var startDate = start.Date;
        var endDate = end.Date.AddDays(1);

        var data = _db.Payments
            .Where(p => p.Status == "Paid" && p.Date >= startDate && p.Date < endDate)
            .GroupBy(p => p.PaymentType)
            .Select(g => new { Label = g.Key, Value = g.Sum(x => x.Amount) })
            .ToList();

        if (!data.Any()) data.Add(new { Label = "No Data", Value = 0m });
        return Json(data);
    }

    [HttpGet]
    public IActionResult ModelChartData(DateTime start, DateTime end)
    {
        var startDate = start.Date;
        var endDate = end.Date.AddDays(1);

        var data = _db.Rentals
            .Include(r => r.Model)
            .Where(r => r.RentalDate >= startDate && r.RentalDate < endDate)
            .GroupBy(r => r.Model.ModelName)
            .Select(g => new { Label = g.Key, Value = g.Count() })
            .OrderByDescending(x => x.Value)
            .ToList();

        if (!data.Any()) data.Add(new { Label = "No Data", Value = 0 });
        return Json(data);
    }

    [HttpGet]
    public IActionResult LateReturnChartData(DateTime start, DateTime end)
    {
        var startDate = start.Date;
        var endDate = end.Date.AddDays(1);

        var data = _db.ReturnRecord
            .Where(r => r.LateReturnDay > 0 &&
                        r.ReturnDateTime >= startDate &&
                        r.ReturnDateTime < endDate)
            .OrderByDescending(r => r.LateReturnDay)
            .Select(r => new { Label = r.RentalId, Value = r.LateFee ?? 0 })
            .ToList();

        if (!data.Any()) data.Add(new { Label = "No Data", Value = 0m });
        return Json(data);
    }

}
