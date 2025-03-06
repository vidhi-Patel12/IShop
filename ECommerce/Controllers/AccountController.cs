using ECommerce.Data;
using ECommerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestSharp;

namespace ECommerce.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(Register model)
        {
            if (ModelState.IsValid)
            {
                // Check if the mobile number already exists
                var existingUser = _context.Register.FirstOrDefault(u => u.Mobile == model.Mobile);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Mobile", "This mobile number is already registered.");
                    return View(model);
                }
                model.Role = 1;      
                model.IsActive = true;
                _context.Register.Add(model);
                _context.SaveChanges();

                return RedirectToAction("Login");
            }

            return View(model);
        }


        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult LoginWithOTP(long Mobile)
        {
            var user = _context.Register.FirstOrDefault(u => u.Mobile == Mobile);

            if (user == null)
            {
                ViewBag.Error = "Mobile number not registered.";
                return View("Login");
            }

            // Generate a random 6-digit OTP
            Random random = new Random();
            int otp = random.Next(100000, 999999);

            // Save OTP in the Login table
            var loginEntry = new Login
            {
                IShopId = user.IShopId,
                OTP = otp,
                IsValid = true,
                GeneratedAt = DateTime.Now
            };

            _context.Login.Add(loginEntry);
            _context.SaveChanges();

            //bool smsSent = SendOTPViaSMS(Mobile, otp);
            //if (!smsSent)
            //{
            //    ViewBag.Error = "Failed to send OTP. Please try again.";
            //    return View("Login");
            //}


            // Redirect to OTP verification page
            return RedirectToAction("VerifyOTP", new { Mobile });
        }

        //private bool SendOTPViaSMS(string mobile, int otp)
        //{
        //    var client = new RestClient("https://www.fast2sms.com/dev/bulkV2");
        //    var request = new RestRequest(Method.POST);
        //    request.AddHeader("authorization", "YOUR_FAST2SMS_API_KEY");
        //    request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

        //    request.AddParameter("variables_values", otp);
        //    request.AddParameter("route", "otp");
        //    request.AddParameter("numbers", mobile);

        //    IRestResponse response = client.Execute(request);

        //    // Parse response
        //    var jsonResponse = JObject.Parse(response.Content);
        //    return jsonResponse["return"] != null && jsonResponse["return"].Value<bool>();
        //}

        [HttpPost]
        public IActionResult GenerateOTP(long Mobile)
        {
            var user = _context.Register.FirstOrDefault(u => u.Mobile == Mobile);

            if (user == null)
            {
                ViewBag.Error = "User does not exist.";
                return View();
            }

            // Generate a random 6-digit OTP
            Random random = new Random();
            int otp = random.Next(100000, 999999);

            // Save OTP in Login table
            var loginEntry = new Login
            {
                IShopId = user.IShopId,                
                OTP = otp,
                IsValid = true,
                GeneratedAt = DateTime.Now
            };

            _context.Login.Add(loginEntry);
            _context.SaveChanges();

            // Send OTP via SMS/Email (Integrate Twilio, SendGrid, etc.)
            Console.WriteLine($"OTP for {Mobile}: {otp}");

            return RedirectToAction("VerifyOTP", new { Mobile });
        }

        [HttpGet]
        public IActionResult VerifyOTP(long Mobile)
        {
            ViewBag.Mobile = Mobile; // Store mobile in ViewBag for the form
            return View();
        }

        [HttpPost]
        public IActionResult VerifyOTP(long Mobile, int OTP)
        {
            var user = _context.Register.FirstOrDefault(u => u.Mobile == Mobile);
            if (user == null)
            {
                ViewBag.Error = "User not found.";
                return View("Login");
            }

            var loginEntry = _context.Login
                .Where(l => l.IShopId == user.IShopId && l.IsValid)
                .OrderByDescending(l => l.GeneratedAt)
                .FirstOrDefault();

            if (loginEntry == null || loginEntry.OTP != OTP)
            {
                ViewBag.Error = "Invalid or expired OTP.";
                return View("Login");
            }

            // Check if OTP has expired (30 seconds limit)
            if (DateTime.Now > loginEntry.GeneratedAt.AddSeconds(30))
            {
                loginEntry.IsValid = false;
                _context.SaveChanges();
                ViewBag.Error = "OTP expired. Please request a new one.";
                return View("Login");
            }

            // OTP is valid → Log in the user
            HttpContext.Session.SetInt32("IShopId", user.IShopId);
            return RedirectToAction("Index", "Home");
        }


        public void ExpireOTP()
        {
            var expiredOtps = _context.Login
                .Where(l => l.IsValid && DateTime.Now > l.GeneratedAt.AddSeconds(30))
                .ToList();

            foreach (var entry in expiredOtps)
            {
                entry.IsValid = false;
            }

            _context.SaveChanges();
        }

        [HttpPost]
        public IActionResult LoginWithPassword(long Mobile, string Password)
        {
            var user = _context.Register.FirstOrDefault(u => u.Mobile == Mobile);

            if (user == null)
            {
                ViewBag.Error = "Mobile number not registered.";
                return View("Login");
            }

            // Verify password (assuming passwords are stored as plain text - NOT recommended)
            if (user.Password != Password)
            {
                ViewBag.Error = "Invalid password.";
                return View("Login");
            }

            CookieOptions options = new CookieOptions
            {      
                HttpOnly = true,               
                SameSite = SameSiteMode.Strict,
                Secure = true,
            };

            Response.Cookies.Append("IShopId", user.IShopId.ToString(), options);

            // Store session and redirect
            HttpContext.Session.SetInt32("IShopId", user.IShopId);

            if (user.Role == 0)  // Admin
            {
                return RedirectToAction("Index", "Admin");
            }
            else if (user.Role == 1)  // Regular User
            {
                return RedirectToAction("Index", "Home");
            }


            return RedirectToAction("Index", "Home");
        }


        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("IShopId");
            return RedirectToAction("Login");
        }

        //[HttpPost]
        //public IActionResult Login(long Mobile, string Password)
        //{
        //    var user = _context.Register.FirstOrDefault(u => u.Mobile == Mobile);

        //    if (user == null)
        //    {
        //        ViewBag.Error = "User does not exist.";
        //        return View();
        //    }

        //    if (user.Password != Password) 
        //    {
        //        ViewBag.Error = "Invalid mobile number or password.";
        //        return View();
        //    }

        //    // Check if the user is active
        //    if (!user.IsActive)
        //    {
        //        ViewBag.Error = "Your account is inactive. Contact support.";
        //        return View();
        //    }

        //    // Store user session
        //    HttpContext.Session.SetInt32("IShopId", user.IShopId);
        //    HttpContext.Session.SetString("UserRole", user.Role.ToString());

        //    // Redirect based on role
        //    if (user.Role == 0)  // Admin
        //    {
        //        return RedirectToAction("Index", "Home");
        //    }
        //    else if (user.Role == 1)  // Regular User
        //    {
        //        return RedirectToAction("Index", "Home");
        //    }

        //    return RedirectToAction("Index", "Home");
        //}

    }
}
