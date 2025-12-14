using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Assignment.Models; // Make sure to include your DbContext namespace

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
                    if (r.Status == "Booked" && r.PickupDate < now)
                        r.Status = "Expired";

                    if (r.Status == "Pickup" && r.ReturnDate < now)
                        r.Status = "LateDue";
                }

                db.SaveChanges();

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
