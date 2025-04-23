using ECommerce.Data;
using ECommerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RestSharp;
using System.Reflection;
using System.Text;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace ECommerce.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly SmsService _smsService;

        public AccountController(ApplicationDbContext context,SmsService smsService)
        {
            _context = context;
            _smsService = smsService;
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

            // Send OTP via SMS
            bool otpSent =  _smsService.SendSmsOTP(Mobile, otp);

            if (!otpSent)
            {
                ViewBag.Error = "Failed to send OTP. Please try again.";
                return View("Login");
            }

            ViewBag.Mobile = Mobile;
            // Redirect to OTP verification page
            return View("Login");
        }

        [HttpGet]
        public IActionResult VerifyOTP(long Mobile)
        {
             ViewBag.Mobile = Mobile;
            return PartialView("_VerifyOTP"); //  Use a Partial View
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

            // Check if OTP has expired (5-minute validity)
            if (DateTime.Now > loginEntry.GeneratedAt.AddMinutes(5))
            {
                loginEntry.IsValid = false;
                _context.SaveChanges();
                ViewBag.Error = "OTP expired. Please request a new one.";
                return View("Login");
            }

            // OTP is valid → Log in the user
            loginEntry.IsValid = false; // Mark OTP as used
            _context.SaveChanges();

            CookieOptions options = new CookieOptions
            {
                Path = "/",
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Secure = false,
            };

            Response.Cookies.Append("IShopId", user.IShopId.ToString(), options);
            HttpContext.Session.SetInt32("IShopId", user.IShopId);

            //  Redirect based on role
            if (user.Role == 0)  // Admin
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            else if (user.Role == 1 && Helper.IsCheckout)  // Regular User & trying to checkout
            {
                Helper.IsCheckout = false;
                return RedirectToAction("Checkout", "Home");
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        public void ExpireOTP()
        {
            var expiredOtps = _context.Login
                .Where(l => l.IsValid && DateTime.Now > l.GeneratedAt.AddMinutes(5))
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
                TempData.Keep("IsCheckout"); //  Keep checkout intent in case of failure
                return View("Login");
            }

            if (user.Password != Password)
            {
                ViewBag.Error = "Invalid password.";
                TempData.Keep("IsCheckout"); //  Keep checkout intent in case of failure
                return View("Login");
            }

            CookieOptions options = new CookieOptions
            {
                Path = "/",
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Secure = false,
            };

            Response.Cookies.Append("IShopId", user.IShopId.ToString(), options);
            HttpContext.Session.SetInt32("IShopId", user.IShopId);

            //  Redirect based on role
            if (user.Role == 0)  // Admin
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            else if (user.Role == 1 && Helper.IsCheckout)  // Regular User & trying to checkout
            {
                Helper.IsCheckout = false;
                return RedirectToAction("Checkout", "Home");
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }
                
        [HttpGet]
        public ContentResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("IShopId");
            Response.Cookies.Delete("cartItems");

            string js = @"
            <script>
                localStorage.removeItem('cartItems');
                localStorage.removeItem('checkoutItems');
                localStorage.removeItem('selectedAddress');
                localStorage.removeItem('orderPlaced');
                window.location.href = '/Account/Login';
            </script>";

            return Content(js, "text/html");
        }
    }
}
