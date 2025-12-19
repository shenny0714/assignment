using Assignment.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

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
        start ??= DateTime.Today.AddDays(-6);
        end ??= DateTime.Today;

        ViewBag.Start = start.Value.ToString("yyyy-MM-dd");
        ViewBag.End = end.Value.ToString("yyyy-MM-dd");

        return View();
    }

    // -------------------------
    // Chart Data (summary)
    // -------------------------
    [HttpGet]
    public IActionResult RentalStatusChartData(DateTime start, DateTime end)
    {
        var data = _db.Rentals
            .Where(r => r.RentalDate >= start.Date && r.RentalDate < end.Date.AddDays(1))
            .GroupBy(r => r.Status)
            .Select(g => new { Label = g.Key, Value = g.Count() })
            .ToList();

        if (!data.Any()) data.Add(new { Label = "No Data", Value = 0 });
        return Json(data);
    }

    [HttpGet]
    public IActionResult RevenueChartData(DateTime start, DateTime end)
    {
        var data = _db.Payments
            .Where(p => p.Status == "Paid" && p.Date >= start.Date && p.Date < end.Date.AddDays(1))
            .GroupBy(p => p.PaymentType)
            .Select(g => new { Label = g.Key, Value = g.Sum(x => x.Amount) })
            .ToList();

        if (!data.Any()) data.Add(new { Label = "No Data", Value = 0m });
        return Json(data);
    }

    [HttpGet]
    public IActionResult ModelChartData(DateTime start, DateTime end)
    {
        var data = _db.Rentals
            .Include(r => r.Model)
            .Where(r => r.RentalDate >= start.Date && r.RentalDate < end.Date.AddDays(1))
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
        var data = _db.ReturnRecord
            .Where(r => r.LateReturnDay > 0 &&
                        r.ReturnDateTime >= start.Date &&
                        r.ReturnDateTime < end.Date.AddDays(1))
            .Select(r => new { RentalId = r.RentalId, Value = r.LateFee ?? 0m })
            .ToList();

        if (!data.Any()) data.Add(new { RentalId = "-", Value = 0m });
        return Json(data);
    }

    // -------------------------
    // Table Data (detailed)
    // -------------------------
    [HttpGet]
    public IActionResult RentalStatusTableData(DateTime start, DateTime end)
    {
        var data = _db.Rentals
            .Where(r => r.RentalDate >= start.Date && r.RentalDate < end.Date.AddDays(1))
            .Select(r => new { r.RentalId, r.CustomerId, r.Status, r.RentalDate })
            .OrderByDescending(r => r.RentalDate)
            .ToList();
        return Json(data);
    }

    [HttpGet]
    public IActionResult RevenueTableData(DateTime start, DateTime end)
    {
        var data = _db.Payments
            .Where(p => p.Status == "Paid" && p.Date >= start.Date && p.Date < end.Date.AddDays(1))
            .Select(p => new { p.PaymentId, p.Date, p.Rental.Customer.CustomerId, p.PaymentType, p.Amount })
            .OrderByDescending(p => p.Date)
            .ToList();
        return Json(data);
    }

    [HttpGet]
    public IActionResult ModelTableData(DateTime start, DateTime end)
    {
        var data = _db.Rentals
            .Include(r => r.Model)
            .Where(r => r.RentalDate >= start.Date && r.RentalDate < end.Date.AddDays(1))
            .Select(r => new { r.RentalId, r.CustomerId, ModelName = r.Model.ModelName, r.RentalDate })
            .OrderByDescending(r => r.RentalDate)
            .ToList();
        return Json(data);
    }

    [HttpGet]
    public IActionResult LateReturnTableData(DateTime start, DateTime end)
    {
        var data = _db.ReturnRecord
            .Where(r => r.LateReturnDay > 0 &&
                        r.ReturnDateTime >= start.Date &&
                        r.ReturnDateTime < end.Date.AddDays(1))
            .Select(r => new { r.RentalId, r.ReturnDateTime, r.LateReturnDay, LateFee = r.LateFee ?? 0m })
            .OrderByDescending(r => r.ReturnDateTime)
            .ToList();
        return Json(data);
    }

    // -------------------------
    // CSV Downloads
    // -------------------------
    private IActionResult JsonToCsv(IEnumerable<dynamic> data, string filename)
    {
        var sb = new StringBuilder();
        var dict = ((IDictionary<string, object>)data.FirstOrDefault() ?? new Dictionary<string, object>());
        sb.AppendLine(string.Join(",", dict.Keys));
        foreach (var row in data)
        {
            var values = dict.Keys.Select(k => row[k]?.ToString()?.Replace(",", " ") ?? "");
            sb.AppendLine(string.Join(",", values));
        }
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", filename);
    }

    [HttpGet]
    public IActionResult DownloadRevenueCsv(DateTime start, DateTime end)
    {
        var data = _db.Payments
            .Include(p => p.Rental)
            .ThenInclude(r => r.Customer)
            .Where(p => p.Date >= start.Date && p.Date < end.Date.AddDays(1))
            .Select(p => new {
                p.PaymentId,
                PaymentDate = p.Date,
                CustomerId = p.Rental.Customer.CustomerId,
                CustomerName = p.Rental.Customer.Name,
                p.RentalId,
                p.PaymentType,
                p.PaymentMethod,
                p.Amount,
                p.Status
            }).ToList();

        var csv = new StringBuilder();
        csv.AppendLine("Payment ID,Payment Date,Customer ID,Customer Name,Rental ID,Payment Type,Payment Method,Amount,Status");

        foreach (var row in data)
        {
            csv.AppendLine($"{row.PaymentId},{row.PaymentDate:yyyy-MM-dd},{row.CustomerId},{row.CustomerName},{row.RentalId},{row.PaymentType},{row.PaymentMethod},{row.Amount},{row.Status}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"Revenue_{start:yyyyMMdd}_{end:yyyyMMdd}.csv");
    }

    // -------------------------
    // Rental Status CSV (with customer & model details)
    // -------------------------
    [HttpGet]
    public IActionResult DownloadRentalStatusCsv(DateTime start, DateTime end)
    {
        var data = _db.Rentals
            .Include(r => r.Customer)
            .Include(r => r.Model)
            .ThenInclude(m => m.Brand)
            .Where(r => r.RentalDate >= start.Date && r.RentalDate < end.Date.AddDays(1))
            .Select(r => new {
                r.RentalId,
                r.CustomerId,
                CustomerName = r.Customer.Name,
                r.ModelId,
                ModelName = r.Model.ModelName,
                BrandName = r.Model.Brand.BrandName,
                r.RentalDate,
                r.PickupDate,
                r.ReturnDate,
                r.Status,
                r.TotalPrice
            }).ToList();

        var csv = new StringBuilder();
        csv.AppendLine("Rental ID,Customer ID,Customer Name,Model ID,Model Name,Brand Name,Rental Date,Pickup Date,Return Date,Status,Total Price");

        foreach (var row in data)
        {
            csv.AppendLine($"{row.RentalId},{row.CustomerId},{row.CustomerName},{row.ModelId},{row.ModelName},{row.BrandName},{row.RentalDate:yyyy-MM-dd},{row.PickupDate:yyyy-MM-dd},{row.ReturnDate:yyyy-MM-dd},{row.Status},{row.TotalPrice}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"RentalStatus_{start:yyyyMMdd}_{end:yyyyMMdd}.csv");
    }

    // -------------------------
    // Model Popularity CSV (each rental with customer info)
    // -------------------------
    [HttpGet]
    public IActionResult DownloadModelCsv(DateTime start, DateTime end)
    {
        var data = _db.Rentals
            .Include(r => r.Customer)
            .Include(r => r.Model)
            .ThenInclude(m => m.Brand)
            .Where(r => r.RentalDate >= start.Date && r.RentalDate < end.Date.AddDays(1))
            .Select(r => new {
                r.RentalId,
                r.CustomerId,
                CustomerName = r.Customer.Name,
                ModelName = r.Model.ModelName,
                BrandName = r.Model.Brand.BrandName,
                r.RentalDate,
                r.Status
            }).ToList();

        var csv = new StringBuilder();
        csv.AppendLine("Rental ID,Customer ID,Customer Name,Model Name,Brand Name,Rental Date,Status");

        foreach (var row in data)
        {
            csv.AppendLine($"{row.RentalId},{row.CustomerId},{row.CustomerName},{row.ModelName},{row.BrandName},{row.RentalDate:yyyy-MM-dd},{row.Status}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"ModelPopularity_{start:yyyyMMdd}_{end:yyyyMMdd}.csv");
    }

    // -------------------------
    // Late Returns CSV (with rental, customer, staff info)
    // -------------------------
    [HttpGet]
    public IActionResult DownloadLateCsv(DateTime start, DateTime end)
    {
        var data = _db.ReturnRecord
            .Include(r => r.Rental)
            .ThenInclude(r => r.Customer)
            .Include(r => r.Staff)
            .Where(r => r.ReturnDateTime >= start.Date && r.ReturnDateTime < end.Date.AddDays(1) && r.LateReturnDay > 0)
            .Select(r => new {
                r.RentalId,
                CustomerId = r.Rental.Customer.CustomerId,
                CustomerName = r.Rental.Customer.Name,
                r.ReturnDateTime,
                r.LateReturnDay,
                r.LateFee,
                StaffId = r.StaffId,
                StaffName = r.Staff.Name
            }).ToList();

        var csv = new StringBuilder();
        csv.AppendLine("Rental ID,Customer ID,Customer Name,Return Date,Late Days,Late Fee,Handled By Staff ID,Staff Name");

        foreach (var row in data)
        {
            csv.AppendLine($"{row.RentalId},{row.CustomerId},{row.CustomerName},{row.ReturnDateTime:yyyy-MM-dd},{row.LateReturnDay},{row.LateFee},{row.StaffId},{row.StaffName}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"LateReturns_{start:yyyyMMdd}_{end:yyyyMMdd}.csv");
    }
}
