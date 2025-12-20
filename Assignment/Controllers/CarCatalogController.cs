using Assignment.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
namespace Assignment.Controllers
{
    public class CarCatalogController : Controller
    {
        private readonly DB _context;

        public CarCatalogController(DB context)
        {
            _context = context;
        }


        public async Task<IActionResult> Index(string SelectedCategory, int? SelectedBrandId, string SearchTerm, string pickedDate)
        {
            bool isStaff = false;
            ViewBag.IsStaff = isStaff;
            // -----------------------------------------------------------
            // 1. HANDLE DATE LOGIC
            // -----------------------------------------------------------
            if (string.IsNullOrEmpty(pickedDate))
            {
                pickedDate = DateTime.Today.ToString("yyyy-MM-dd");
            }
            ViewBag.UserSearchDate = pickedDate;
            DateTime searchDate = DateTime.Parse(pickedDate);

            // -----------------------------------------------------------
            // 2. QUERY DATABASE
            // -----------------------------------------------------------
            if (!isStaff)
            {
                var carsQuery = _context.CarModels
                .Include(c => c.Brand)
                .Include(c => c.Category)
                .Include(c => c.Vehicles) // Important: Needed to count Total Fleet
                .AsQueryable();

                // Category 
                if (!string.IsNullOrEmpty(SelectedCategory))
                {
                    carsQuery = carsQuery.Where(c => c.CategoryId == SelectedCategory);
                }


                if (SelectedBrandId.HasValue && SelectedBrandId > 0)
                {

                    carsQuery = carsQuery.Where(c => c.BrandId == SelectedBrandId);
                }

                if (!string.IsNullOrEmpty(SearchTerm))
                {
                    carsQuery = carsQuery.Where(c => c.ModelName.Contains(SearchTerm) ||
                                                     c.Brand.BrandName.Contains(SearchTerm));
                }

                var filteredCars = await carsQuery.ToListAsync();

                // -----------------------------------------------------------
                // 3. [UPDATED] CHECK AVAILABILITY LOGIC (Count by Model)
                // -----------------------------------------------------------
                // Step A: Get a list of ALL Model IDs that are booked on this date
                // We ignore Cancelled/Rejected bookings.
                var bookedModelIds = _context.Rentals
                    .Where(r => r.Status != "Cancelled" &&
                                r.Status != "Rejected" &&
                                r.PickupDate <= searchDate &&
                                r.ReturnDate >= searchDate)
                    .Select(r => r.ModelId)
                    .ToList();

                foreach (var car in filteredCars)
                {
                    if (car.Vehicles != null)
                    {
                        // Total cars you own of this model
                        int totalFleet = car.Vehicles.Count;

                        // Total active bookings for this model
                        int bookedCount = bookedModelIds.Count(id => id == car.ModelId);

                        // Available = Fleet - Booked
                        int availableCount = totalFleet - bookedCount;

                        // Step B: Update the list to reflect availability
                        if (availableCount > 0)
                        {
                            // We strictly limit the list to the available number
                            // So @item.Vehicles.Count in the View shows the correct number
                            car.Vehicles = car.Vehicles.Take(availableCount).ToList();
                        }
                        else
                        {
                            // If 0 or negative, it means Fully Booked
                            car.Vehicles = new List<Assignment.Models.Vehicle>();
                        }
                    }
                }

                // -----------------------------------------------------------
                // 4. PREPARE VIEW MODEL
                // -----------------------------------------------------------
                var viewModel = new VehicleCatalogViewModel
                {
                    CarModels = filteredCars,
                    SelectedCategory = SelectedCategory,
                    SelectedBrandId = SelectedBrandId?.ToString(), // Safety check
                    SearchTerm = SearchTerm,
                    CategoryList = new SelectList(_context.VehicleCategories, "CategoryId", "CategoryName"),
                    BrandList = new SelectList(_context.Brands, "BrandId", "BrandName")
                };

                return View("Index", viewModel);
            }
            else
            {
                var carModels = _context.CarModels
                    .Include(c => c.Brand)
                    .Include(c => c.Category)
                    .Include(c => c.Vehicles)
                    .AsQueryable();

                return View("Views/Vehicle/Index.cshtml", carModels);
            }
        }

        // Details 
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var carModel = await _context.CarModels
                .Include(c => c.Category)
                .Include(c => c.Brand)
                .FirstOrDefaultAsync(m => m.ModelId == id);

            if (carModel == null) return NotFound();

            return View(carModel);
        }
    }
}