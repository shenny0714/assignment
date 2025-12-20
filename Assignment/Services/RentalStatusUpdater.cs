using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Assignment.Models;
using System.Net.Mail;

namespace Assignment.Services
{
    public class RentalStatusUpdater : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;


        public RentalStatusUpdater(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DB>();

            while (!stoppingToken.IsCancellationRequested)
            {

                var now = DateTime.Now;
                var rentals = db.Rentals
                     .Include(r => r.Customer)  // <-- load customer data
                     .Where(r => r.Status != "Returned" && r.Status != "Cancelled")
                     .ToList();

                foreach (var r in rentals)
                {
                    if (r.Status == "Booked")
                    {
                        var pickupEnd = r.PickupDate.Date.AddDays(1);  // 12:00 AM next day

                        if (now >= pickupEnd)
                        {
                            r.Status = "Expired";

                            // Refund calculation
                            decimal refundAmount = 0m;
                            decimal bookingFee = r.TotalPrice - r.DepositAmount;
                            var hoursBeforePickup = (r.PickupDate - DateTime.Now).TotalHours;

                            if (hoursBeforePickup > 48)
                            {
                                // Refund deposit + booking fee
                                refundAmount = r.DepositAmount + bookingFee;
                            }
                            else
                            {
                                // Refund booking fee only
                                refundAmount = bookingFee;
                            }

                            // Insert refund payment record
                            var refundPayment = new Payment
                            {
                                PaymentId = NextPaymentId(),
                                RentalId = r.RentalId,
                                Amount = refundAmount,
                                PaymentType = "Refund",
                                PaymentMethod = "Online Banking",
                                Status = "Completed",
                                Date = DateTime.Now
                            };
                            db.Payments.Add(refundPayment);

                            // Send email
                            if (r.Customer != null && !string.IsNullOrEmpty(r.Customer.Email))
                            {
                                SendExpiredEmail(r.Customer.Email, r);
                            }
                        }
                    }

                    if (r.Status == "Pickup")
                    {
                        var lateThreshold = r.ReturnDate.Date
                                            .AddDays(1)
                                            .AddHours(12); // next day 12 PM

                        if (now > lateThreshold)
                            r.Status = "LateDue";
                    }
                }


                db.SaveChanges();
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private string NextPaymentId()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DB>();
            
            string max = db.Payments.Max(p => p.PaymentId) ?? "PA0000";
            int n = int.Parse(max[2..]);
            return $"PA{(n + 1).ToString("0000")}";
        }


        private void SendExpiredEmail(string email, Rental rental)
        {
            using var scope = _scopeFactory.CreateScope();
            var hp = scope.ServiceProvider.GetRequiredService<Assignment.Helper>(); // resolve scoped service here

            string customerName = rental.Customer?.Name ?? "Customer";

            string body = $@"
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
        <div class='title'>Booking Expired</div>

        <div class='text'>
            Dear {customerName},<br><br>

            Your booking (<strong>Ref: {rental.RentalId}</strong>) has expired because you did not pick up the car.<br><br>

            The <strong>booking fee will be refunded</strong>, while the <strong>deposit is non-refundable</strong> according to our policy.<br><br>

            Refund processing may take <strong>3–5 working days</strong>.
        </div>

        <div class='footer'>
            Regards,<br>
            Car Rental Admin
        </div>
    </div>
</body>
</html>";

            var mail = new MailMessage
            {
                Subject = "Booking Expired Notification",
                Body = body,
                IsBodyHtml = true
            };

            mail.To.Add(new MailAddress(email, customerName));

            try
            {
                hp.SendEmail(mail);
            }
            catch (Exception ex)
            {
                // Optionally log failure
                Console.WriteLine($"Failed to send expired email to {email}: {ex.Message}");
            }
        }

    }
}