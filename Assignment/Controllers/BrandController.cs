using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers
{
    public class BrandController : Controller
    {
        private readonly DB _context;

        public BrandController(DB context)
        {
            _context = context;
        }

        // Index
        public async Task<IActionResult> Index()
        {
            return View(await _context.Brands.ToListAsync());
        }

        // Create (GET)
        public IActionResult Create()
        {
            return View();
        }

        // Create (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Brand brand)
        {
            // auto generate
            if (ModelState.IsValid)
            {
                _context.Add(brand);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Create successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(brand);
        }

        // Edit (GET) 
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var brand = await _context.Brands.FindAsync(id);
            if (brand == null) return NotFound();
            return View(brand);
        }

        // Edit (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Brand brand)
        {
            if (id != brand.BrandId) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(brand);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Edit successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(brand);
        }

        // Delete (GET)
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var brand = await _context.Brands.FirstOrDefaultAsync(m => m.BrandId == id);
            if (brand == null) return NotFound();
            return View(brand);
        }

        // Delete (POST)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var brand = await _context.Brands.FindAsync(id);
            if (brand != null)
            {
                _context.Brands.Remove(brand);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Delete successfully!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
