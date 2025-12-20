using Assignment.Models;
using Assignment.ViewModels; 
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers
{
    public class VehicleCategoriesController : Controller
    {
        private readonly DB _context;

        public VehicleCategoriesController(DB context)
        {
            _context = context;
        }

        // ============================================
        //  Category ID (VC0001)
        // ============================================
        private string NextCategoryId()
        {
            // check max ID
            string max = _context.VehicleCategories.Max(c => c.CategoryId) ?? "VC0000";

            // ID +1
            int n = int.Parse(max[2..]);
            return $"VC{(n + 1).ToString("0000")}";
        }

        // GET: VehicleCategories
        public async Task<IActionResult> Index()
        {
            return View(await _context.VehicleCategories.ToListAsync());
        }

        // GET: VehicleCategories/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: VehicleCategories/Create
        [HttpPost]
        public async Task<IActionResult> Create(VehicleCategoryViewModel vm)
        {
            if (ModelState.IsValid)
            {
                var category = new VehicleCategory
                {
                    CategoryId = NextCategoryId(),
                    CategoryName = vm.CategoryName
                };
                _context.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(vm);
        }

        // GET: VehicleCategories/Edit/VC0001
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var vehicleCategory = await _context.VehicleCategories.FindAsync(id);
            if (vehicleCategory == null) return NotFound();
            return View(vehicleCategory);
        }

        // POST: VehicleCategories/Edit/VC0001
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, VehicleCategory vehicleCategory)
        {
            if (id != vehicleCategory.CategoryId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(vehicleCategory);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.VehicleCategories.Any(e => e.CategoryId == id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(vehicleCategory);
        }

        // POST: VehicleCategories/Delete/VC0001
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var vehicleCategory = await _context.VehicleCategories.FindAsync(id);
            if (vehicleCategory != null)
            {
                _context.VehicleCategories.Remove(vehicleCategory);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}