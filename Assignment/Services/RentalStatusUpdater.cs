using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Assignment.Models; 

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
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DB>();

                var now = DateTime.Now;
                var rentals = db.Rentals
                    .Where(r => r.Status != "Returned" && r.Status != "Cancelled")
                    .ToList();
                foreach (var r in rentals)
                {
                    if (r.Status == "Booked")
                    {
                        var pickupStart = r.PickupDate.Date.AddHours(12); // 12:00 PM
                        var pickupEnd = r.PickupDate.Date.AddDays(1).AddTicks(-1);  // 12:00 AM next day

                        if (r.PickupDate.Date < now.Date && now > pickupEnd)
                        {
                            r.Status = "Expired";
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
    }
}
