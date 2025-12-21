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

        // Modified: SelectedBrandId is now int? (nullable integer) to match your search page
        public async Task<IActionResult> Index(string SelectedCategory, int? SelectedBrandId, string SearchTerm, string pickedDate)
        {
            bool isStaff = false;
            ViewBag.IsStaff = isStaff;

            // 1. HANDLE DATE LOGIC
            if (string.IsNullOrEmpty(pickedDate))
            {
                pickedDate = DateTime.Today.ToString("yyyy-MM-dd");
            }
            // We still keep this for ViewBag, but we also add it to the ViewModel below
            ViewBag.UserSearchDate = pickedDate;
            DateTime searchDate = DateTime.Parse(pickedDate);

            // 2. QUERY DATABASE
            if (!isStaff)
            {
                var carsQuery = _context.CarModels
                .Include(c => c.Brand)
                .Include(c => c.Category)
                .Include(c => c.Vehicles)
                .AsQueryable();

                // [FIXED] Category: Compare 'CategoryName' (string) instead of 'CategoryId' (int)
                // This matches the "City", "Sedan" links from your home page.
                if (!string.IsNullOrEmpty(SelectedCategory))
                {
                    carsQuery = carsQuery.Where(c => c.Category.CategoryName == SelectedCategory);
                }

                // [MODIFIED] Brand: Checks if the integer has a value
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

                // 3. CHECK AVAILABILITY LOGIC
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
                        int totalFleet = car.Vehicles.Count;
                        int bookedCount = bookedModelIds.Count(id => id == car.ModelId);
                        int availableCount = totalFleet - bookedCount;

                        if (availableCount > 0)
                        {
                            car.Vehicles = car.Vehicles.Take(availableCount).ToList();
                        }
                        else
                        {
                            car.Vehicles = new List<Assignment.Models.Vehicle>();
                        }
                    }
                }

                // 4. PREPARE VIEW MODEL
                var viewModel = new VehicleCatalogViewModel
                {
                    CarModels = filteredCars,
                    // [ADDED] Pass the date to the ViewModel to fix the "UserSearchDate" error
                    UserSearchDate = pickedDate,

                    SelectedCategory = SelectedCategory,
                    // [MODIFIED] Convert the int back to string if your ViewModel property is a string
                    SelectedBrandId = SelectedBrandId?.ToString(),
                    SearchTerm = SearchTerm,

                    // [FIXED] Ensure dropdown uses CategoryName for value to match the Home Page links
                    CategoryList = new SelectList(_context.VehicleCategories, "CategoryName", "CategoryName"),
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