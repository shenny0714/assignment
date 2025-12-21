using Assignment.Models;
using Assignment.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net.Mail;
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
        private readonly IMemoryCache _cache;

        public AccountController(DB db, Helper hp, IMemoryCache cache)
        {
            _db = db;
            _hp = hp;
            _cache = cache;
        }

        // GET: Account/Login
        public IActionResult Login(string? email)
        {
            // 1. If we have a specific error from the Redirect (POST), show it first.
            if (TempData["Error"] != null)
            {
                ModelState.AddModelError("", TempData["Error"]!.ToString());
            }
            // 2. If no TempData, check DB to see if we should show a persistent warning
            else if (!string.IsNullOrEmpty(email))
            {
                var c = _db.Customers.FirstOrDefault(x => x.Email == email);
                if (c != null)
                {
                    CheckUserStatus(c.LoginRetryCount, c.LockedUntil, $"FailTime_{c.Email}");
                }
            }

            // 3. Pre-fill email
            var vm = new LoginVM { Email = email };
            return View(vm);
        }

        private void CheckUserStatus(int retryCount, DateTime? lockedUntil, string cacheKey)
        {
            bool isReset = !_cache.TryGetValue(cacheKey, out DateTime _);

            if (isReset && retryCount > 0)
            {
                return;
            }

            // B. Check Lock
            if (lockedUntil.HasValue && lockedUntil.Value > DateTime.Now)
            {
                var remaining = Math.Ceiling((lockedUntil.Value - DateTime.Now).TotalMinutes);
                ModelState.AddModelError("", $"Account locked. Try again in {remaining} minute(s).");
            }
            // C. Check Attempts
            else if (retryCount > 0)
            {
                int attemptsLeft = 3 - retryCount;
                ModelState.AddModelError("", $"Warning: You have {attemptsLeft} attempt(s) remaining.");
            }
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

                // ──────────────────────────────────────────────
                // 2. CHECK CUSTOMER (Repeat Logic)
                // ──────────────────────────────────────────────
                var cust = _db.Customers.FirstOrDefault(c => c.Email == vm.Email);
                if (cust != null)
                {
                    string cacheKey = $"FailTime_{cust.Email}";

                    // ✅ AUTO-RESET Check
                    if (!_cache.TryGetValue(cacheKey, out DateTime _))
                    {
                        if (cust.LoginRetryCount > 0)
                        {
                            cust.LoginRetryCount = 0;
                            cust.LockedUntil = null;
                            _db.SaveChanges();
                        }
                    }

                    if (cust.LockedUntil.HasValue && cust.LockedUntil.Value > DateTime.Now)
                    {
                        var remaining = Math.Ceiling((cust.LockedUntil.Value - DateTime.Now).TotalMinutes);
                        TempData["Error"] = $"Account locked. Try again in {remaining} minute(s).";
                        return RedirectToAction("Login", new { email = vm.Email });
                    }

                    if (cust.HashPassword == hash)
                    {
                        cust.LoginRetryCount = 0;
                        cust.LockedUntil = null;
                        _db.SaveChanges();
                        _cache.Remove(cacheKey);


                        await SignInUser(cust.CustomerId, cust.Name, cust.Email, "Customer", cust.PhotoURL, vm.RememberMe);
                        TempData["Info"] = "Login successfully.";
                        return RedirectToAction("Index", "Home");
                    }
                    else
                    {
                        cust.LoginRetryCount++;
                        // SET CACHE: expire in 10 minutes
                        _cache.Set(cacheKey, DateTime.Now, TimeSpan.FromMinutes(1));

                        int attemptsLeft = 3 - cust.LoginRetryCount;

                        if (attemptsLeft <= 0)
                        {
                            cust.LockedUntil = DateTime.Now.AddMinutes(5);
                            _db.SaveChanges();
                            TempData["Error"] = "Account locked. Try again in 5 minutes.";
                        }
                        else
                        {
                            _db.SaveChanges();
                            TempData["Error"] = $"Invalid password. You have {attemptsLeft} attempt(s) remaining.";
                        }
                        return RedirectToAction("Login", new { email = vm.Email });
                    }
                }
                TempData["Info"] = "Invalid email or password.";
            }
            return RedirectToAction("Login", new { email = vm.Email });
        }


        // ──────────────────────────────────────
        // CUSTOMER REGISTRATION
        // ──────────────────────────────────────
        public IActionResult Register() => View();

        [HttpPost]
        public IActionResult Register(RegisterVM vm, string captchaInput)
        {
            /* =========================
             * 1️. BASIC INPUT VALIDATION
             * ========================= */
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            /* =========================
             *  2.CAPTCHA VALIDATION
             * ========================= */
            var sessionCaptcha = HttpContext.Session.GetString("CaptchaCode");
            if (string.IsNullOrEmpty(captchaInput) || captchaInput != sessionCaptcha)
            {
                ModelState.AddModelError("Captcha", "Invalid Captcha code.");
                return View(vm);
            }

            /* =========================
             * 3️. PASSWORD STRENGTH CHECK
             * ========================= */
            if (!_hp.IsStrongPassword(vm.Password))
            {
                ModelState.AddModelError(
                    "Password",
                    "Password must be at least 8 characters, contain 1 uppercase, 1 lowercase, 1 number, and 1 special character."
                );
                return View(vm);
            }

            /* =========================
             * 4️. DUPLICATE EMAIL CHECK
             * ========================= */
            if (_db.Customers.Any(x => x.Email == vm.Email) ||
                _db.Staffs.Any(x => x.Email == vm.Email))
            {
                ModelState.AddModelError("Email", "Email already registered.");
                return View(vm);
            }

            /* =========================
             * 5️. GENERATE CUSTOMER ID
             * ========================= */
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

            /* =========================
             * 6️. SAVE CUSTOMER
             * ========================= */
            var customer = new Customer
            {
                CustomerId = newId,
                Name = vm.Name,
                Email = vm.Email,
                Phone = vm.Phone,
                HashPassword = HashPassword(vm.Password),
                PhotoURL = _hp.SavePhoto(vm.Photo, "photos")
            };

            _db.Customers.Add(customer);
            _db.SaveChanges();

            TempData["Info"] = "Registration successful! Please login.";
            return RedirectToAction("Login");
        }


        // --- LOGOUT ---
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["Info"] = "Logout successfully.";
            return RedirectToAction("Login");
        }
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
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

                TempData["Info"] = "Profile updated successfully!";
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
                    Phone = vm.Phone,
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

        // ──────────────────────────────────────
        // FORGOT PASSWORD (Sends Email Link)
        // ──────────────────────────────────────
        public IActionResult ForgotPass() => View();

        [HttpPost]
        public IActionResult ForgotPass(string email)
        {
            // 1. Generate Token
            string token = Guid.NewGuid().ToString();
            string resetLink = Url.Action("ResetPass", "Account", new { token }, Request.Scheme);

            // 2. Find User

            var c = _db.Customers.FirstOrDefault(x => x.Email == email);
            if (c != null)
            {
                c.ResetToken = token;
                c.ResetTokenExpiry = DateTime.Now.AddMinutes(15);
                _db.SaveChanges();
            }

            // 3. Send Email if user exists
            if (c != null)
            {
                string name = c.Name;
                SendResetLinkEmail(email, name, resetLink);
                TempData["Info"] = "Reset link sent to your email.";
            }
            else
            {
                TempData["Info"] = "If the email exists, a link has been sent.";
            }

            return View();
        }

        // Helper to construct the email (Similar to Demo)
        private void SendResetLinkEmail(string toEmail, string name, string link)
        {
            var mail = new MailMessage();
            mail.To.Add(new MailAddress(toEmail, name));
            mail.Subject = "Reset Password Request";
            mail.IsBodyHtml = true;

            // Body with Link
            mail.Body = $@"
        <div style='font-family: Arial, sans-serif; padding: 20px;'>
            <h2 style='color: #5d5fef;'>Password Reset</h2>
            <p>Dear {name},</p>
            <p>We received a request to reset your password.</p>
            <p>Please click the button below to reset it (valid for 15 minutes):</p>
            <a href='{link}' style='background-color: #5d5fef; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block; margin-top: 10px;'>Reset Password</a>
            <p style='margin-top: 20px;'>If you did not request this, please ignore this email.</p>
            <p>From,<br>Car Rental Admin</p>
        </div>
    ";

            _hp.SendEmail(mail);
        }

        // ──────────────────────────────────────
        // RESET PASSWORD (Validate & Update)
        // ──────────────────────────────────────
        public IActionResult ResetPass(string token)
        {
            if (_db.Customers.Any(x => x.ResetToken == token && x.ResetTokenExpiry > DateTime.Now))
            {
                return View(new ResetPasswordVM { Token = token });
            }
            return Content("Invalid or expired token.");
        }

        [HttpPost]
        public IActionResult ResetPass(ResetPasswordVM vm)
        {
            if (!_hp.IsStrongPassword(vm.NewPassword))
            {
                ModelState.AddModelError("NewPassword", "Password must be at least 8 characters, contain 1 uppercase, 1 lowercase, 1 number, and 1 special character.");
                return View(vm);
            }


            var c = _db.Customers.FirstOrDefault(x => x.ResetToken == vm.Token);
            if (c != null)
            {
                c.HashPassword = HashPassword(vm.NewPassword);
                c.ResetToken = null;
                _db.SaveChanges();
                TempData["Info"] = "Password reset successful. Please login.";
                return RedirectToAction("Login");
            }

            return View(vm);
        }

        // ──────────────────────────────────────
        // Captcha Image for Register
        // ──────────────────────────────────────
        [AllowAnonymous]
        public IActionResult GetCaptchaImage()
        {
            // 1. Generate stronger random code (Alphanumeric, avoiding confusing chars)
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new Random();
            // Generate a 5-character string
            string code = new string(Enumerable.Repeat(chars, 5)
              .Select(s => s[random.Next(s.Length)]).ToArray());

            // Store in session for validation later
            HttpContext.Session.SetString("CaptchaCode", code);

            // Increase image size slightly to fit distorted text
            int width = 160;
            int height = 50;

            using (var bitmap = new Bitmap(width, height))
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.White);

                // 2. Add Noise: Random Lines across background
                Pen linePen = new Pen(Color.LightGray, 2);
                for (int i = 0; i < 4; i++)
                {
                    int x1 = random.Next(width);
                    int y1 = random.Next(height);
                    int x2 = random.Next(width);
                    int y2 = random.Next(height);
                    g.DrawBezier(linePen, x1, y1, x1 + 50, y1 - 30, x2 - 50, y2 + 30, x2, y2);
                }

                // 3. Draw Characters with Rotation and varying fonts
                // Define a list of distinct fonts (ensure these exist on the server)
                string[] fontNames = { "Arial", "Verdana", "Times New Roman", "Courier New" };

                // Starting X position
                float charX = 10;

                foreach (char c in code)
                {
                    // Pick random font and size
                    string fontName = fontNames[random.Next(fontNames.Length)];
                    int fontSize = random.Next(24, 30);
                    Font font = new Font(fontName, fontSize, FontStyle.Bold);

                    // Pick a random dark color for text
                    Brush brush = new SolidBrush(Color.FromArgb(
                        random.Next(0, 100),
                        random.Next(0, 100),
                        random.Next(0, 150)));

                    // Determine random rotation angle (between -30 and +30 degrees)
                    float angle = random.Next(-30, 30);

                    // Save current graphic state
                    GraphicsState state = g.Save();

                    // Move to the character's position and rotate the canvas
                    g.TranslateTransform(charX, height / 2);
                    g.RotateTransform(angle);

                    // Draw the character centered relative to its rotation point
                    SizeF charSize = g.MeasureString(c.ToString(), font);
                    g.DrawString(c.ToString(), font, brush, -(charSize.Width / 2), -(charSize.Height / 2));

                    // Restore graphic state for the next character
                    g.Restore(state);

                    // Advance X position for next character (spacing varies slightly)
                    charX += charSize.Width + random.Next(-5, 5);
                }

                // 4. Add Foreground Noise: Random Dots (Speckles)
                for (int i = 0; i < 200; i++)
                {
                    int x = random.Next(width);
                    int y = random.Next(height);
                    // Draw a 2x2 pixel dot
                    bitmap.SetPixel(x, y, Color.FromArgb(random.Next(150, 255), random.Next(150, 255), random.Next(150, 255)));
                    if (x < width - 1 && y < height - 1) bitmap.SetPixel(x + 1, y + 1, Color.Gray);
                }

                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Png);
                    return File(stream.ToArray(), "image/png");
                }
            }
        }

        //=====================================
        // --- HELPER METHODS ---
        //=====================================
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