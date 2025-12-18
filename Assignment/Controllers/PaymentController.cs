using Assignment.Models;
using Assignment.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace Assignment.Controllers;

public class PaymentController : Controller
{
    private readonly DB _db;
    private readonly IConfiguration _configuration;

    public PaymentController(DB db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
    }

    // ============================================================
    // 1. GET: SHOW THE PAGE
    // ============================================================
    [HttpGet]
    public IActionResult MakePayment(int modelId, DateTime rentalDate, DateTime returnDate, decimal totalPrice, decimal deposit)
    {
        var model = _db.CarModels.Find(modelId);
        if (model == null) return RedirectToAction("Index", "Home");

        var vm = new PaymentVM
        {
            CarModel = model.ModelName,
            TotalRentalPrice = totalPrice,
            DepositRequired = deposit,
            Amount = totalPrice + deposit
        };

        ViewBag.ModelId = modelId;
        ViewBag.RentalDate = rentalDate;
        ViewBag.ReturnDate = returnDate;

        return View(vm);
    }

    // ============================================================
    // 2. POST: PROCESS THE BUTTON CLICK
    // ============================================================
    [HttpPost]
    public IActionResult MakePayment(
        int modelId,
        DateTime rentalDate,
        DateTime returnDate,
        decimal totalPrice,
        decimal deposit,
        string paymentMethod)
    {
        if (paymentMethod == "Stripe")
        {
            var model = _db.CarModels.Find(modelId);
            if (model == null) return RedirectToAction("Index", "Home");

            decimal grandTotal = totalPrice + deposit;
            long amountInCents = (long)(grandTotal * 100);

            // Correct Port: 7102
            var domain = "https://localhost:7102";

            // Build the URL to return to the selection page
            // We must pass the parameters back so the page loads correctly
            string cancelUrl = domain + $"/Payment/MakePayment?modelId={modelId}&rentalDate={rentalDate:yyyy-MM-dd}&returnDate={returnDate:yyyy-MM-dd}&totalPrice={totalPrice}&deposit={deposit}";

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = amountInCents,
                            Currency = "myr",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Car Rental: {model.ModelName}",
                                Description = "Deposit + Rental Fee",
                            },
                        },
                        Quantity = 1,
                    },
                },
                Mode = "payment",
                SuccessUrl = domain + $"/Payment/Success?modelId={modelId}&rentalDate={rentalDate:yyyy-MM-dd}&returnDate={returnDate:yyyy-MM-dd}&amount={grandTotal}",

                // FIXED: Now goes back to the 'MakePayment' page
                CancelUrl = cancelUrl,
            };

            var service = new SessionService();
            Session session = service.Create(options);
            return Redirect(session.Url);
        }
        else if (paymentMethod == "ToyyibPay")
        {
            TempData["Info"] = "Online Banking coming soon!";
            // Redirect back to MakePayment instead of Home so they can choose again
            return RedirectToAction("MakePayment", new { modelId, rentalDate, returnDate, totalPrice, deposit });
        }

        return RedirectToAction("MakePayment", new { modelId, rentalDate, returnDate, totalPrice, deposit });
    }

    // ============================================================
    // 3. SUCCESS HANDLER (Stripe sends user here after paying)
    // ============================================================
    public IActionResult Success(int modelId, DateTime rentalDate, DateTime returnDate, decimal amount)
    {
        // 1. Generate New IDs
        string newRentalId = GenerateRentalId();
        string newPaymentId = GeneratePaymentId();

        // 2. Create Rental Record
        var rental = new Rental
        {
            RentalId = newRentalId,
            CustomerId = "C001",           // Hardcoded for now (Replace with User.Identity.Name later)
            ModelId = modelId,
            RentalDate = DateTime.Now,     // The moment they booked
            PickupDate = rentalDate,
            ReturnDate = returnDate,

            // Financials
            DepositAmount = amount * 0.2m, // 20% Deposit
            TotalPrice = amount * 0.8m,    // 80% Rental Fee

            Status = "Booked"              // Confirmed Status
        };

        // 3. Create Payment Record
        var payment = new Payment
        {
            PaymentId = newPaymentId,
            RentalId = newRentalId,
            Amount = amount,               // Total Paid (Deposit + Fee)
            PaymentType = "Full Payment",
            PaymentMethod = "Stripe",      // Recorded as Card Payment
            Status = "Successful",
            Date = DateTime.Now
        };

        // 4. Save To Database
        _db.Rentals.Add(rental);
        _db.Payments.Add(payment);
        _db.SaveChanges();

        // 5. Show Success Message & Redirect
        TempData["Info"] = "Payment Successful! Your booking is confirmed.";

        // Redirect to the Receipt Page (Rental/Detail)
        return RedirectToAction("Detail", "Rental", new { id = newRentalId });
    }

    // ============================================================
    // HELPERS: Generate Unique IDs (RN0001, PM0001, etc.)
    // ============================================================
    private string GenerateRentalId()
    {
        // Find the highest ID currently in database (e.g., RN0004)
        string? max = _db.Rentals.Max(r => r.RentalId);

        if (max == null)
        {
            return "RN0001"; // First ever rental
        }

        // Extract number (RN0004 -> 4), add 1, format back to RN0005
        int n = int.Parse(max.Substring(2));
        return $"RN{(n + 1):D4}";
    }

    private string GeneratePaymentId()
    {
        string? max = _db.Payments.Max(p => p.PaymentId);

        if (max == null)
        {
            return "PM0001";
        }

        int n = int.Parse(max.Substring(2));
        return $"PM{(n + 1):D4}";
    }
}