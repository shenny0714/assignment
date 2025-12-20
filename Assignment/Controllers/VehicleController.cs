using Assignment.Models;
using Assignment.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers
{
    public class VehicleController : Controller
    {
        private readonly DB _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public VehicleController(DB context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // ==========================================
        // 1. Index 
        // ==========================================
        // GET: Admin/Vehicle/Index
        public async Task<IActionResult> Index()
        {
            
            var carModels = _context.CarModels
                .Include(c => c.Brand)
                .Include(c => c.Category)
                .Include(c => c.Vehicles) 
                .AsQueryable();

            return View(await carModels.ToListAsync());
        }

        // ==========================================
        // 2. Insert (Create)
        // ==========================================
        // GET: /Vehicle/Insert
        public async Task<IActionResult> Insert()
        {
            var viewModel = new VehicleViewModel
            {
                AvailableBrands = await _context.Brands.ToListAsync(),
                AvailableCategories = await _context.VehicleCategories.ToListAsync()
            };
            return View(viewModel);
        }

        // POST: /Vehicle/Insert ()
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VehicleViewModel vm) 
        {
            if (vm.Price <= 0)
            {
                ModelState.AddModelError(nameof(vm.Price), "Price must be greater than 0.");
            }
            if (ModelState.IsValid)
            {
                var carModel = new CarModel
                {
                    ModelName = vm.ModelName,
                    BrandId = vm.SelectedBrandId.Value,
                    CategoryId = vm.SelectedCategoryId,
                    Description = vm.Description,
                    Price = vm.Price
                };

                // UPLOAD
                carModel.ImagePathFront = await SaveImage(vm.PhotoFront);
                carModel.ImagePathSide = await SaveImage(vm.PhotoSide);
                carModel.ImagePathBack = await SaveImage(vm.PhotoBack);

                _context.Add(carModel);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            
            vm.AvailableBrands = await _context.Brands.ToListAsync();
            vm.AvailableCategories = await _context.VehicleCategories.ToListAsync();
            return View("Insert", vm); 
        }

        // ==========================================
        // 3. Edit (Update) 
        // ==========================================
        // GET: /Vehicle/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();


            var carModel = await _context.CarModels.FindAsync(id);
            if (carModel == null) return NotFound();

            var viewModel = new VehicleViewModel
            {
                ModelName = carModel.ModelName,
                Description = carModel.Description,
                Price = carModel.Price,
                SelectedBrandId = carModel.BrandId,
                SelectedCategoryId = carModel.CategoryId,
            };


            ViewBag.CurrentFront = carModel.ImagePathFront;
            ViewBag.CurrentSide = carModel.ImagePathSide;
            ViewBag.CurrentBack = carModel.ImagePathBack;
            ViewBag.ModelId = carModel.ModelId;

            viewModel.AvailableBrands = await _context.Brands.ToListAsync();
            viewModel.AvailableCategories = await _context.VehicleCategories.ToListAsync();

            return View(viewModel);
        }

        // POST: /Vehicle/Edit/5
        //[HttpPost]
        public async Task<IActionResult> EditFunction(int id, string ModelName,int? SelectedBrandId,string? SelectedCategoryId,decimal Price,string Description,IFormFile PhotoFront,IFormFile PhotoSide,IFormFile PhotoBack)
        {
            var carModelToUpdate = await _context.CarModels.FindAsync(id);
            if (carModelToUpdate == null)
                return NotFound();

            carModelToUpdate.ModelName = ModelName;
            carModelToUpdate.BrandId = SelectedBrandId.Value;
            carModelToUpdate.CategoryId = SelectedCategoryId;
            carModelToUpdate.Price = Price;
            carModelToUpdate.Description = Description == null ? "No Sescription" : Description;



            if (PhotoFront != null)
                carModelToUpdate.ImagePathFront = await SaveImage(PhotoFront);

            if (PhotoSide != null)
                carModelToUpdate.ImagePathSide = await SaveImage(PhotoSide);

            if (PhotoBack != null)
                carModelToUpdate.ImagePathBack = await SaveImage(PhotoBack);

            _context.Update(carModelToUpdate);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }



        // ==============================================
        // (Add Inventory)
        // ==============================================

        // GET: Vehicle/AddInventory
        public async Task<IActionResult> Inventory()
        {
            // 使用 Include 抓取关联的 CarModel 和 Brand 信息，这样页面才能显示名字而不只是 ID
            var vehicles = await _context.Vehicles
                .Include(v => v.Model)
                    .ThenInclude(m => m.Brand)
                .ToListAsync();

            return View(vehicles);
        }

        // GET: /Vehicle/AddInventory
        public IActionResult AddInventory()
        {
            // 必须在这里查询数据库，否则下拉框就是空的
            var models = _context.CarModels
                .Include(m => m.Brand) // 包含品牌信息，让显示更清晰
                .Select(m => new
                {
                    ModelId = m.ModelId,
                    // 拼接品牌和型号，例如 "BMW iX"
                    FullName = m.Brand.BrandName + " " + m.ModelName
                })
                .ToList();

            // 将数据放入 ViewBag，注意这里的 Key 必须和 View 里的 asp-items 一致
            ViewData["ModelId"] = new SelectList(models, "ModelId", "FullName");

            return View();
        }
        //POST Vehicle/AddInventory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddInventory(Vehicle vehicle)
        {
            // 移除不需要验证的项
            ModelState.Remove("VehicleId");
            ModelState.Remove("Model");

            if (ModelState.IsValid)
            {
                // 1. check VehicleId
                var lastVehicle = await _context.Vehicles
                    .OrderByDescending(v => v.VehicleId)
                    .FirstOrDefaultAsync();

                int nextNumber = 1; // start from 1 

                if (lastVehicle != null && !string.IsNullOrEmpty(lastVehicle.VehicleId))
                {
                    // 2. start from  "VH" + last 4 digit  = "VH0005"
                    string lastIdNumber = lastVehicle.VehicleId.Substring(2);
                    if (int.TryParse(lastIdNumber, out int lastId))
                    {
                        nextNumber = lastId + 1; 
                    }
                }

                // 3. 格式化为 VH + 4位数字 (D4 会自动补齐 0，如 1 变成 0001)
                vehicle.VehicleId = "VH" + nextNumber.ToString("D4");

                vehicle.Available = true;

                _context.Add(vehicle);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Inventory));
            }

            
            ViewData["ModelId"] = new SelectList(_context.CarModels.Include(m => m.Brand)
                .Select(m => new {
                    ModelId = m.ModelId,
                    FullName = m.Brand.BrandName + " " + m.ModelName
                }), "ModelId", "FullName", vehicle.ModelId);

            return View(vehicle);
        }

        //////////////////
        // EDIT INVENTORY 
        // GET: /Vehicle/EditInventory/VH0001
        public async Task<IActionResult> EditInventory(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle == null) return NotFound();

            // 填充车型下拉框（模仿 AddInventory 的逻辑）
            var models = _context.CarModels
                .Include(m => m.Brand)
                .Select(m => new {
                    ModelId = m.ModelId,
                    FullName = m.Brand.BrandName + " " + m.ModelName
                }).ToList();

            ViewData["ModelId"] = new SelectList(models, "ModelId", "FullName", vehicle.ModelId);

            return View(vehicle);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditInventory(Vehicle vehicle)
        {
            
            ModelState.Remove("Model");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(vehicle);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Inventory));
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "Unable to save changes. Try again.");
                }
            }

            // 失败时重新填充下拉框
            var models = _context.CarModels.Include(m => m.Brand)
                .Select(m => new { ModelId = m.ModelId, FullName = m.Brand.BrandName + " " + m.ModelName })
                .ToList();
            ViewData["ModelId"] = new SelectList(models, "ModelId", "FullName", vehicle.ModelId);

            return View(vehicle);
        }


        // POST: /Vehicle/DeleteInventory/VH12345
        [HttpPost] // 对应表单的 POST 方法
        public async Task<IActionResult> DeleteInventory(string id)
        {
            // 1. 查找要删除的具体车辆
            var vehicle = await _context.Vehicles.FindAsync(id);

            if (vehicle == null)
            {
                TempData["Error"] = "Vehicle not found.";
                return RedirectToAction(nameof(Inventory));
            }

            // 2. 执行删除
            try
            {
                _context.Vehicles.Remove(vehicle);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Vehicle (Plate: " + vehicle.PlateNumber + ") deleted successfully!";
            }
            catch (Exception ex)
            {
                // 如果这台车已经有订单关联，可能会删除失败
                TempData["Error"] = "Cannot delete this vehicle because it might be linked to existing rentals.";
            }

            // 3. 返回库存列表页面
            return RedirectToAction(nameof(Inventory));
        }


        


        // ==========================================
        // 5. Delete Model
        // ==========================================
        // GET: /Vehicle/DeleteModel/5
        public async Task<IActionResult> DeleteModel(int id)
        {
            // 1. 查找要删除的模型
            var carModel = await _context.CarModels
                .Include(m => m.Vehicles) // 包含车辆信息以检查库存
                .FirstOrDefaultAsync(m => m.ModelId == id);

            if (carModel == null)
            {
                return NotFound();
            }

            // 2. 安全检查：如果该模型下还有具体车辆（库存），禁止删除
            // 这与你前端按钮的 disabled 逻辑呼应，属于后端二次校验
            if (carModel.Vehicles.Any())
            {
                // 这里可以添加 TempData 提示错误
                TempData["Error"] = "Cannot delete model because it still has vehicles in inventory.";
                return RedirectToAction(nameof(Index));
            }

            // 3. 执行删除
            try
            {
                _context.CarModels.Remove(carModel);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Model deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while deleting: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // status ( rented or avaialable)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string id, bool status)
        {
            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle == null) return NotFound();

            vehicle.Available = status;

            // 必须移除 Model 导航属性的验证
            ModelState.Remove("Model");

            if (ModelState.IsValid)
            {
                _context.Update(vehicle);
                await _context.SaveChangesAsync();
            }

            // 修改完刷新页面，下拉框就会显示新的状态
            return RedirectToAction(nameof(Inventory));
        }
        // ==========================================
        // 4. Helper Method
        // ==========================================
        private async Task<string?> SaveImage(IFormFile? file)
        {
            if (file != null && file.Length > 0)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                // ENSURE wwwroot/uploads/carmodels
                var folder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads/carmodels");

                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                var filePath = Path.Combine(folder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                return "/uploads/carmodels/" + fileName;
            }
            return null;
        }
    }
}