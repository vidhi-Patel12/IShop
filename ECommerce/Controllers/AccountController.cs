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

        private readonly HttpClient _httpClient;

        public AccountController(ApplicationDbContext context, IConfiguration configuration, SmsService smsService)
        {
            _context = context;
            _configuration = configuration;
            _smsService = smsService;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            _httpClient = new HttpClient(handler);
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(Register model)
        {
            if (ModelState.IsValid)
            {
                string baseUrl = _configuration["APIURL"];
                string apiUrl = $"{baseUrl}/account/v1/register";

                var jsonContent = new StringContent(
                    JsonConvert.SerializeObject(model),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(apiUrl, jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction("Login");
                }

                // Read errors from API
                var responseContent = await response.Content.ReadAsStringAsync();
                var errors = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(responseContent);

                foreach (var error in errors)
                {
                    foreach (var msg in error.Value)
                    {
                        ModelState.AddModelError(error.Key, msg);
                    }
                }
            }

            return View(model);
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> LoginWithOTP(long Mobile)
        {
            string baseUrl = _configuration["APIURL"]; // e.g., https://api.yoursite.com/api
            string apiUrl = $"{baseUrl}/account/v1/loginwithotp";

            var jsonContent = new StringContent(
                JsonConvert.SerializeObject(Mobile),
                Encoding.UTF8,
                "application/json"
            );

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync(apiUrl, jsonContent);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "API call failed: " + ex.Message;
                return View("Login");
            }

            if (response.IsSuccessStatusCode)
            {
                ViewBag.Mobile = Mobile;
                return View("Login"); // Redirect to OTP input or verification page
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            try
            {
                var errorResponse = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseContent);
                ViewBag.Error = errorResponse["message"];
            }
            catch
            {
                ViewBag.Error = "Unexpected error: " + responseContent;
            }

            return View("Login");
        }

        [HttpGet]
        public IActionResult VerifyOTP(long Mobile)
        {
            ViewBag.Mobile = Mobile;
            return PartialView("_VerifyOTP"); //  Use a Partial View
        }

        [HttpPost]
        public async Task<IActionResult> VerifyOTP(long Mobile, int OTP)
        {
            string baseUrl = _configuration["APIURL"];
            string apiUrl = $"{baseUrl}/account/v1/verifyotp";

            var requestBody = new
            {
                Mobile = Mobile,
                OTP = OTP
            };

            var jsonContent = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(apiUrl, jsonContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Deserialize dynamically
                dynamic result = JsonConvert.DeserializeObject<dynamic>(responseContent);

                int shopId = result.shopId;
                int role = result.role;

                CookieOptions options = new CookieOptions
                {
                    Path = "/",
                    HttpOnly = false,
                    SameSite = SameSiteMode.Lax,
                    Secure = false,
                };

                Response.Cookies.Append("IShopId", shopId.ToString(), options);
                HttpContext.Session.SetInt32("IShopId", shopId);

                string redirectUrl = (role == 0)
            ? "/Admin/Dashboard"
            : (role == 1 && Helper.IsCheckout)
                ? "/Home/Checkout"
                : "/Home/Index";

                Helper.IsCheckout = false;

                // Build JS with conditional localStorage set
                string js = "<script>\n";
                if (redirectUrl == "/Home/Index")
                {
                    js += "localStorage.setItem('cartSynced', 'true');\n";
                }
                js += $"window.location.href = '{redirectUrl}';\n";
                js += "</script>";

                return Content(js, "text/html");
            }
            else
            {
                dynamic error = JsonConvert.DeserializeObject<dynamic>(responseContent);
                ViewBag.Error = error?.message ?? "OTP verification failed.";
                return View("Login");
            }
        }

        public async Task ExpireOTP()
        {
            string baseUrl = _configuration["APIURL"]; // Your API base URL
            string apiUrl = $"{baseUrl}/account/v1/expireotp";

            var response = await _httpClient.PostAsync(apiUrl, null);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Expire OTP API called successfully: " + content);
            }
            else
            {
                Console.WriteLine("Failed to call Expire OTP API. Status: " + response.StatusCode);
            }
        }

        [HttpPost]
        public async Task<IActionResult> LoginWithPassword(long Mobile, string Password)
        {
            var baseUrl = _configuration["APIURL"]; // Your API base URL from config
            var apiUrl = $"{baseUrl}/account/v1/loginwithpassword";

            var loginRequest = new
            {
                Mobile = Mobile,
                Password = Password
            };

            var jsonContent = new StringContent(
                JsonConvert.SerializeObject(loginRequest),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(apiUrl, jsonContent);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<dynamic>(responseBody);

                int shopId = result.shopId;
                int role = result.role;

                // Set cookies and session same as before
                CookieOptions options = new CookieOptions
                {
                    Path = "/",
                    HttpOnly = false,
                    SameSite = SameSiteMode.Lax,
                    Secure = false,
                };

                Response.Cookies.Append("IShopId", shopId.ToString(), options);
                HttpContext.Session.SetInt32("IShopId", shopId);

                // Redirect based on role
                string redirectUrl = (role == 0)
                 ? "/Admin/Dashboard"
                 : (role == 1 && Helper.IsCheckout)
                     ? "/Home/Checkout"
                     : "/Home/Index";

                Helper.IsCheckout = false;

                // Base JS
                string js = "<script>\n";

                // Conditionally add localStorage only for /Home/Index
                if (redirectUrl == "/Home/Index")
                {
                    js += "localStorage.setItem('cartSynced', 'true');\n";
                }

                js += $"window.location.href = '{redirectUrl}';\n";
                js += "</script>";

                return Content(js, "text/html");


            }
            else
            {
                // Read error message from API response
                var errorContent = await response.Content.ReadAsStringAsync();
                var errorObj = JsonConvert.DeserializeObject<dynamic>(errorContent);
                string errorMessage = errorObj?.message ?? "Login failed.";

                ViewBag.Error = errorMessage;
                TempData.Keep("IsCheckout"); 
                return View("Login");
            }
        }

        [HttpGet]
        public async Task<ContentResult> Logout()
        {
            // Get the API URL from config or hardcode
            string baseUrl = _configuration["APIURL"];  // e.g. "https://yourdomain.com/api"
            string apiUrl = $"{baseUrl}/account/v1/logout"; // Adjust path according to your routing

            // Call API logout endpoint using HttpClient
            var response = await _httpClient.PostAsync(apiUrl, null); // POST with no body

            if (response.IsSuccessStatusCode)
            {
                // API logout success, now clear local server session and cookies as fallback (optional)
                HttpContext.Session.Clear();
                Response.Cookies.Delete("IShopId");
                Response.Cookies.Delete("cartItems");

                // Return JS to clear localStorage and redirect client
                string js = @"
        <script>
            localStorage.removeItem('cartItems');
            localStorage.removeItem('checkoutItems');
            localStorage.removeItem('selectedAddress');
            localStorage.removeItem('orderPlaced');
            localStorage.removeItem('cartSynced');
            window.location.href = '/Account/Login';
        </script>";

                return Content(js, "text/html");
            }
            else
            {
                // If API call failed, show error or fallback
                return Content("<script>alert('Logout failed. Please try again.');window.location.href = '/Account/Login';</script>", "text/html");
            }
        }

    }
}
