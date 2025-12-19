using Assignment.Models;
using Assignment.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

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

                // 1. Check Staff Table First
                var staff = _db.Staffs.FirstOrDefault(s => s.Email == vm.Email && s.HashPassword == hash);
                if (staff != null)
                {
                    await SignInUser(staff.StaffId, staff.Name, staff.Email, staff.Type, null, vm.RememberMe);
                    return RedirectToAction("Index", "Home");
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