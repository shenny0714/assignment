using Assignment.Models;
using Assignment.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using X.PagedList;
using X.PagedList.Extensions;

namespace Assignment.Controllers
{
    public class AccountController : Controller
    {
        private readonly DB _db;
        private readonly Helper _hp;

        public AccountController(DB db, Helper hp)
        {
            _db = db;
            _hp = hp;
        }

        // --- LOGIN ---
        public IActionResult Login()
        {
            if (User.Identity!.IsAuthenticated)
                return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginVM vm)
        {
            if (ModelState.IsValid)
            {
                var hash = HashPassword(vm.Password);
                //1. Check Staffs Table (Contains Admins and Staff)
                var staff = _db.Staffs.FirstOrDefault(s => s.Email == vm.Email && s.HashPassword == hash);
                if (staff != null)
                {
                    await SignInUser(staff.StaffId, staff.Name, staff.Email, staff.Type, null, vm.RememberMe);

                    if (staff.Type == "Admin") return RedirectToAction("Admin");
                    else return RedirectToAction("Staff");
                }

                // 2. Check Customer Table Second
                var cust = _db.Customers.FirstOrDefault(c => c.Email == vm.Email && c.HashPassword == hash);
                if (cust != null)
                {
                    await SignInUser(cust.CustomerId, cust.Name, cust.Email, "Customer", cust.PhotoURL, vm.RememberMe);
                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError("", "Invalid email or password.");
            }
            return View(vm);
        }

        // --- REGISTER (For Customers Only) ---

        public IActionResult Register() => View();

        [HttpPost]
        public IActionResult Register(RegisterVM vm)
        {
            if (_db.Customers.Any(x => x.Email == vm.Email) || _db.Staffs.Any(x => x.Email == vm.Email))
            {
                ModelState.AddModelError("Email", "Email already registered.");
            }

            if (ModelState.IsValid)
            {
                // Generate ID: CU001, CU002...
                string newId = "CU001";
                var lastCust = _db.Customers
                    .AsEnumerable()
                    .OrderByDescending(c => c.CustomerId.Length)
                    .ThenByDescending(c => c.CustomerId)
                    .FirstOrDefault();

                if (lastCust != null)
                {
                    string numPart = lastCust.CustomerId.Substring(2);
                    if (int.TryParse(numPart, out int lastNum))
                    {
                        newId = "CU" + (lastNum + 1).ToString("D3");
                    }
                }

                var c = new Customer
                {
                    CustomerId = newId,
                    Name = vm.Name,
                    Email = vm.Email,
                    Phone = vm.PhoneNumber,
                    HashPassword = HashPassword(vm.Password),
                    PhotoURL = _hp.SavePhoto(vm.Photo, "photos")
                };

                _db.Customers.Add(c);
                _db.SaveChanges();

                TempData["Success"] = "Registration successful! Please login.";
                return RedirectToAction("Login");
            }
            return View(vm);
        }

        // --- LOGOUT ---
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // --- PROFILE (GET) ---
        [Authorize]
        public IActionResult Profile()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            var role = User.FindFirstValue(ClaimTypes.Role);

            User u = GetUserByEmail(email, role);
            if (u == null) return RedirectToAction("Login");

            var vm = new UpdateProfileVM
            {
                Email = u.Email,
                Name = u.Name,
                PhoneNumber = u.Phone,
            };

            // If Customer, load photo
            if (u is Customer c) vm.PhotoURL = c.PhotoURL;

            return View(vm);
        }

        // --- PROFILE (POST) ---
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Profile(UpdateProfileVM vm)
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            var role = User.FindFirstValue(ClaimTypes.Role);

            // Fetch the correct user object (Staff or Customer)
            User u = GetUserByEmail(email, role);
            if (u == null) return RedirectToAction("Login");

            bool hasChanges = false;

            // 1. Handle Photo (Only for Customers)
            if (u is Customer cust && vm.Photo != null)
            {
                if (!string.IsNullOrEmpty(cust.PhotoURL)) _hp.DeletePhoto(cust.PhotoURL, "photos");
                cust.PhotoURL = _hp.SavePhoto(vm.Photo, "photos");
                hasChanges = true;
            }

            // 2. Password
            if (!string.IsNullOrEmpty(vm.NewPassword))
            {
                var currentHash = HashPassword(vm.CurrentPassword ?? "");
                if (u.HashPassword != currentHash)
                {
                    ModelState.AddModelError("CurrentPassword", "Incorrect current password.");
                    if (u is Customer c) vm.PhotoURL = c.PhotoURL;
                    return View(vm);
                }
                u.HashPassword = HashPassword(vm.NewPassword);
                hasChanges = true;
            }

            // 3. Basic Info
            if (u.Name != vm.Name) { u.Name = vm.Name; hasChanges = true; }
            if (u.Phone != vm.PhoneNumber) { u.Phone = vm.PhoneNumber; hasChanges = true; }

            if (hasChanges)
            {
                _db.SaveChanges();

                // Refresh Cookie
                string photo = (u is Customer c2) ? c2.PhotoURL : null;
                await SignInUser(
                    role == "Admin" || role == "Staff" ? ((Staff)u).StaffId : ((Customer)u).CustomerId,
                    u.Name, u.Email, u.Role, photo, false
                );

                TempData["Success"] = "Profile updated successfully!";
                return RedirectToAction("Profile");
            }

            return RedirectToAction("Profile");
        }

        // ──────────────────────────────────────
        // ADMIN PANEL: CUSTOMERS
        // ──────────────────────────────────────

        // 1. CUSTOMER LIST (Default Admin Page)


        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCustomer(string id)
        {
            var customer = _db.Customers.Find(id);
            if (customer != null)
            {
                if (!string.IsNullOrEmpty(customer.PhotoURL)) _hp.DeletePhoto(customer.PhotoURL, "photos");
                _db.Customers.Remove(customer);
                _db.SaveChanges();
                TempData["Success"] = "Customer deleted successfully.";
            }
            else
            {
                TempData["Error"] = "Customer not found.";
            }
            return RedirectToAction(nameof(Admin));
        }

        // ──────────────────────────────────────
        // ADMIN PANEL: STAFF MANAGEMENT
        // ──────────────────────────────────────

        // 1. STAFF LIST


        // 2. CREATE STAFF (GET)
        [Authorize(Roles = "Admin")]
        public IActionResult CreateStaff()
        {
            return View();
        }

        // 3. CREATE STAFF (POST)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult CreateStaff(RegisterVM vm)
        {
            ModelState.Remove("Photo"); // Not needed for staff

            if (_db.Staffs.Any(s => s.Email == vm.Email) || _db.Customers.Any(c => c.Email == vm.Email))
            {
                ModelState.AddModelError("Email", "Email already registered.");
            }

            if (ModelState.IsValid)
            {
                // Generate ID: S001, S002...
                string newId = "S001";
                var lastStaff = _db.Staffs
                    .AsEnumerable()
                    .OrderByDescending(s => s.StaffId.Length)
                    .ThenByDescending(s => s.StaffId)
                    .FirstOrDefault();

                if (lastStaff != null)
                {
                    string numPart = lastStaff.StaffId.Substring(1);
                    if (int.TryParse(numPart, out int lastNum))
                    {
                        newId = "S" + (lastNum + 1).ToString("D3");
                    }
                }

                var s = new Staff
                {
                    StaffId = newId,
                    Name = vm.Name,
                    Email = vm.Email,
                    Phone = vm.PhoneNumber,
                    HashPassword = HashPassword(vm.Password),
                    Type = "Staff" // Explicitly create as Staff
                };

                _db.Staffs.Add(s);
                _db.SaveChanges();

                TempData["Success"] = "New staff account created successfully!";
                return RedirectToAction(nameof(AdminStaff));
            }

            return View(vm);
        }

        // 4. DELETE STAFF
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteStaff(string id)
        {
            var staff = _db.Staffs.Find(id);
            if (staff != null)
            {
                if (staff.Type == "Admin" || User.Identity.Name == staff.Email)
                {
                    TempData["Error"] = "Cannot delete Admin accounts.";
                    return RedirectToAction(nameof(AdminStaff));
                }

                _db.Staffs.Remove(staff);
                _db.SaveChanges();
                TempData["Success"] = "Staff account deleted.";
            }
            else
            {
                TempData["Error"] = "Staff not found.";
            }
            return RedirectToAction(nameof(AdminStaff));
        }

        // ──────────────────────────────────────
        // ADMIN: SHARED EDIT (Customer & Staff)
        // ──────────────────────────────────────
        [Authorize(Roles = "Admin")]
        public IActionResult EditUser(string id)
        {
            EditUserVM vm = new EditUserVM();

            // 1. Try finding in Staff
            var staff = _db.Staffs.Find(id);
            if (staff != null)
            {
                vm.Id = staff.StaffId;
                vm.Name = staff.Name;
                vm.Email = staff.Email;
                vm.Phone = staff.Phone;
                vm.UserType = "Staff";
                return View(vm);
            }

            // 2. Try finding in Customer
            var cust = _db.Customers.Find(id);
            if (cust != null)
            {
                vm.Id = cust.CustomerId;
                vm.Name = cust.Name;
                vm.Email = cust.Email;
                vm.Phone = cust.Phone;
                vm.UserType = "Customer";
                return View(vm);
            }

            TempData["Error"] = "User not found.";
            return RedirectToAction("Admin");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult EditUser(EditUserVM vm)
        {
            // Check for Email Duplicates (Global check across both tables)
            bool emailExistsInStaff = _db.Staffs.Any(s => s.Email == vm.Email && s.StaffId != vm.Id);
            bool emailExistsInCust = _db.Customers.Any(c => c.Email == vm.Email && c.CustomerId != vm.Id);

            if (emailExistsInStaff || emailExistsInCust)
            {
                ModelState.AddModelError("Email", "Email is already taken.");
            }

            if (ModelState.IsValid)
            {
                if (vm.UserType == "Staff")
                {
                    var s = _db.Staffs.Find(vm.Id);
                    if (s != null)
                    {
                        s.Name = vm.Name;
                        s.Email = vm.Email;
                        s.Phone = vm.Phone;
                        _db.SaveChanges();
                        TempData["Success"] = "Staff details updated.";
                        return RedirectToAction("AdminStaff");
                    }
                }
                else if (vm.UserType == "Customer")
                {
                    var c = _db.Customers.Find(vm.Id);
                    if (c != null)
                    {
                        c.Name = vm.Name;
                        c.Email = vm.Email;
                        c.Phone = vm.Phone;
                        _db.SaveChanges();
                        TempData["Success"] = "Customer details updated.";
                        return RedirectToAction("Admin");
                    }
                }
            }

            // If validation fails, return the view with errors
            return View(vm);
        }
        // ──────────────────────────────────────
        // STAFF PANEL (View Customers)
        // ──────────────────────────────────────

        [Authorize(Roles = "Staff")]
        public IActionResult Staff(string? name, string? sort, string? dir, int page = 1)
        {
            // 1. Searching ------------------------
            // Logic from Demo5: Trim input and save to ViewBag
            ViewBag.Name = name = name?.Trim() ?? "";

            // Search by Name (Expanded to include Email/ID for better UX)
            var query = _db.Customers.AsQueryable();
            if (!string.IsNullOrEmpty(name))
            {
                query = query.Where(s => s.Name.Contains(name)
                                      || s.Email.Contains(name)
                                      || s.CustomerId.Contains(name));
            }

            // 2. Sorting --------------------------
            ViewBag.Sort = sort;
            ViewBag.Dir = dir;

            // Logic from Demo5: Switch expression for sorting
            Func<Customer, object> fn = sort switch
            {
                "Id" => s => s.CustomerId,
                "Name" => s => s.Name,
                "Email" => s => s.Email,
                _ => s => s.CustomerId // Default sort
            };

            // Logic from Demo5: Apply Ascending or Descending
            var sorted = dir == "des" ?
                         query.OrderByDescending(fn) :
                         query.OrderBy(fn);

            // 3. Paging ---------------------------
            // Logic from Demo5: Redirect if page < 1
            if (page < 1)
            {
                return RedirectToAction(null, new { name, sort, dir, page = 1 });
            }

            // Page Size = 10 records per page
            var model = sorted.ToPagedList(page, 10);

            // Logic from Demo5: Redirect if page > total pages
            if (page > model.PageCount && model.PageCount > 0)
            {
                return RedirectToAction(null, new { name, sort, dir, page = model.PageCount });
            }

            return View(model);
        }
        // ──────────────────────────────────────
        // ADMIN PANEL: CUSTOMERS (Search, Sort, Paging)
        // ──────────────────────────────────────
        [Authorize(Roles = "Admin")]
        public IActionResult Admin(string? name, string? sort, string? dir, int page = 1)
        {
            ViewBag.Name = name = name?.Trim() ?? "";
            ViewBag.Sort = sort;
            ViewBag.Dir = dir;

            var query = _db.Customers.AsQueryable();
            if (!string.IsNullOrEmpty(name))
            {
                query = query.Where(s => s.Name.Contains(name) || s.Email.Contains(name) || s.CustomerId.Contains(name));
            }

            Func<Customer, object> fn = sort switch
            {
                "Id" => s => s.CustomerId,
                "Name" => s => s.Name,
                "Email" => s => s.Email,
                _ => s => s.CustomerId
            };

            var sorted = dir == "des" ? query.OrderByDescending(fn) : query.OrderBy(fn);

            if (page < 1) return RedirectToAction(null, new { name, sort, dir, page = 1 });
            var model = sorted.ToPagedList(page, 8);
            if (page > model.PageCount && model.PageCount > 0) return RedirectToAction(null, new { name, sort, dir, page = model.PageCount });

            return View(model);
        }

        // ──────────────────────────────────────
        // ADMIN PANEL: STAFF (Search, Sort, Paging)
        // ──────────────────────────────────────
        [Authorize(Roles = "Admin")]
        public IActionResult AdminStaff(string? name, string? sort, string? dir, int page = 1)
        {
            ViewBag.Name = name = name?.Trim() ?? "";
            ViewBag.Sort = sort;
            ViewBag.Dir = dir;

            var query = _db.Staffs.AsQueryable();
            if (!string.IsNullOrEmpty(name))
            {
                query = query.Where(s => s.Name.Contains(name) || s.Email.Contains(name) || s.StaffId.Contains(name));
            }

            Func<Staff, object> fn = sort switch
            {
                "Id" => s => s.StaffId,
                "Name" => s => s.Name,
                "Email" => s => s.Email,
                _ => s => s.StaffId
            };

            var sorted = dir == "des" ? query.OrderByDescending(fn) : query.OrderBy(fn);

            if (page < 1) return RedirectToAction(null, new { name, sort, dir, page = 1 });
            var model = sorted.ToPagedList(page, 8);
            if (page > model.PageCount && model.PageCount > 0) return RedirectToAction(null, new { name, sort, dir, page = model.PageCount });

            return View(model);
        }


        // --- HELPER METHODS ---

        private User GetUserByEmail(string email, string role)
        {
            if (role == "Customer")
                return _db.Customers.FirstOrDefault(x => x.Email == email);
            else
                return _db.Staffs.FirstOrDefault(x => x.Email == email);
        }

        private async Task SignInUser(string id, string name, string email, string role, string photoUrl, bool isPersistent)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim(ClaimTypes.Name, name),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role), // Admin, Staff, or Customer
                new Claim("PhotoURL", photoUrl ?? "")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = isPersistent,
                ExpiresUtc = isPersistent ? DateTime.UtcNow.AddDays(7) : DateTime.UtcNow.AddMinutes(20)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal, authProperties);
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                var builder = new StringBuilder();
                foreach (var b in bytes) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }
    }
}