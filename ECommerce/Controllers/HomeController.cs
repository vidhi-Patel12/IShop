using System.Data;
using System.Diagnostics;
using ECommerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using System.Reflection.PortableExecutable;
using Braintree;
using ECommerce.Data;
using System.Net.Mail;
using System.Reflection.Metadata;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Document = iTextSharp.text.Document;
using MimeKit;
using MailKit.Security;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Utils;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;
using iTextSharp.text.html.simpleparser;
using iTextSharp.tool.xml;
using JsonSerializer = System.Text.Json.JsonSerializer;
using System.Text;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;


namespace ECommerce.Controllers
{
    public class HomeController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        private readonly IViewRenderService _viewRenderService;
        private readonly HttpClient _httpClient;


        public HomeController(IConfiguration configuration, ILogger<HomeController> logger, ApplicationDbContext context, IViewRenderService viewRenderService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
            _configuration = configuration;
            _context = context;
            _viewRenderService = viewRenderService;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            _httpClient = new HttpClient(handler);
        }

        public async Task<IActionResult> Index(bool showAll = false)
        {
            List<Products> products = new List<Products>();

            string baseUrl = _configuration["APIURL"]; // e.g., "https://localhost:5001/api/ProductApi"
            string endpoint = showAll ? "all" : "limited";
            string apiUrl = $"{baseUrl}/user/v1/{endpoint}";


            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    products = JsonSerializer.Deserialize<List<Products>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                else
                {
                    ViewBag.Error = $"API Error: {response.StatusCode}";
                    return View(new List<Products>());
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Exception: {ex.Message}";
                return View(new List<Products>());
            }


            return View(products);
        }

        public async Task<IActionResult> QuickViewByProductImageId(int productImageId)
        {
            List<Products> products = new List<Products>();
            string baseUrl = _configuration["APIURL"];
            string apiUrl = $"{baseUrl}/user/v1/quickview/{productImageId}";


            var response = await _httpClient.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                products = JsonSerializer.Deserialize<List<Products>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }

            return PartialView("_QuickView", products);
        }

        [HttpGet]
        [Route("home/productdetails/{slug}/{typeslug}/{colorslug}")]
        public async Task<IActionResult> ProductDetails(string slug, string typeslug, string colorslug, [FromServices] IHttpClientFactory clientFactory)
        {
            if (string.IsNullOrEmpty(slug))
                return NotFound();

            string baseUrl = _configuration["APIURL"];
            string apiUrl = $"{baseUrl}/user/v1/productdetails/{slug}/{typeslug}/{colorslug}";

            var response = await _httpClient.GetAsync(apiUrl);

            if (!response.IsSuccessStatusCode)
                return NotFound("API call failed.");

            var json = await response.Content.ReadAsStringAsync();

            // Use JsonDocument to read values without defining a DTO
            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;

                int productImageId = root.GetProperty("productImageId").GetInt32();
                var productJson = root.GetProperty("product").GetRawText();
                var relatedProductsJson = root.GetProperty("relatedProducts").GetRawText();

                // Deserialize to your existing Products model
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                var product = JsonSerializer.Deserialize<Products>(productJson, options);
                var relatedProducts = JsonSerializer.Deserialize<List<Products>>(relatedProductsJson, options);

                if (product == null)
                    return NotFound("Product data missing.");

                ViewBag.ProductImageId = productImageId;
                ViewBag.AllProducts = relatedProducts;

                return View(product);
            }
        }

        public async Task<IActionResult> Products()
        {
            List<Products> products = new List<Products>();
            string baseUrl = _configuration["APIURL"]; // Example: "https://localhost:5001"
            string apiUrl = $"{baseUrl}/user/v1/all";


            var response = await _httpClient.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                products = JsonSerializer.Deserialize<List<Products>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            else
            {
                ViewBag.Error = "Unable to fetch products from API.";
            }

            return View(products);
        }

        public async Task<IActionResult> GetCart()
        {
            List<ShoppingCart> cartItems = new List<ShoppingCart>();
            string baseUrl = _configuration["APIURL"]; // e.g., "https://yourdomain.com/api"
            string apiUrl = $"{baseUrl}/user/v1/getcart"; // Adjust based on your route


            var cookie = Request.Cookies["IShopId"];
            if (!string.IsNullOrEmpty(cookie))
            {
                _httpClient.DefaultRequestHeaders.Add("Cookie", $"IShopId={cookie}");
            }

            var response = await _httpClient.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(json);

                if (result.TryGetProperty("cartItems", out var items))
                {
                    cartItems = JsonSerializer.Deserialize<List<ShoppingCart>>(items.GetRawText(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
            }

            return Json(new { success = true, cartItems });
        }

        [HttpGet]
        public async Task<IActionResult> Cart()
        {
            List<ShoppingCart> cartItems = new List<ShoppingCart>();
            string baseUrl = _configuration["APIURL"];
            string apiUrl = $"{baseUrl}/user/v1/getcart";

            // Pass IShopId cookie manually
            var iShopId = Request.Cookies["IShopId"];
            if (!string.IsNullOrEmpty(iShopId))
            {
                _httpClient.DefaultRequestHeaders.Add("Cookie", $"IShopId={iShopId}");

                var response = await _httpClient.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(json);

                    if (result.TryGetProperty("cartItems", out var items))
                    {
                        cartItems = JsonSerializer.Deserialize<List<ShoppingCart>>(items.GetRawText(), new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                }
                else
                {
                    ViewBag.Error = "Unable to fetch products from API.";
                }
            }
            else
            {
                // Fallback: Load guest cart from session
                var guestCart = HttpContext.Session.GetString("GuestCart");
                if (!string.IsNullOrEmpty(guestCart))
                {
                    cartItems = JsonConvert.DeserializeObject<List<ShoppingCart>>(guestCart);
                }
            }

            return View(cartItems);
        }

        [HttpPost]
        public async Task<IActionResult> SaveCart([FromBody] List<ShoppingCart> cartItems)
        {
            if (cartItems == null || cartItems.Count == 0)
                return Json(new { success = false, message = "Cart is empty." });

            string? userId = Request.Cookies["IShopId"];
            string baseUrl = _configuration["APIURL"];
            string apiUrl = $"{baseUrl}/user/v1/savecart";


            // Pass cookie manually
            if (!string.IsNullOrEmpty(userId))
            {
                _httpClient.DefaultRequestHeaders.Add("Cookie", $"IShopId={userId}");
            }

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(cartItems),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(apiUrl, jsonContent);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                return Content(responseJson, "application/json");
            }
            else
            {
                return StatusCode((int)response.StatusCode, new
                {
                    success = false,
                    message = "Failed to save cart via API.",
                    status = response.StatusCode
                });
            }
        }

        public async Task<IActionResult> CheckCartItem(ShoppingCart item)
        {
            string? baseUrl = _configuration["APIURL"];
            string apiUrl = $"{baseUrl}/user/v1//checkcartitem";


            // Pass cookies if needed
            var userId = Request.Cookies["IShopId"];
            if (!string.IsNullOrEmpty(userId))
            {
                _httpClient.DefaultRequestHeaders.Add("Cookie", $"IShopId={userId}");
            }

            // Serialize the request body
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(item),
                Encoding.UTF8,
                "application/json");

            // Send the POST request
            var response = await _httpClient.PostAsync(apiUrl, jsonContent);

            if (response.IsSuccessStatusCode)
            {
                string resultJson = await response.Content.ReadAsStringAsync();

                // Pass through the same JSON structure returned by the API
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(resultJson);
                return Json(jsonElement);
            }
            else
            {
                return Json(new
                {
                    exists = false,
                    message = "Failed to contact CheckCartItem API.",
                    error = response.ReasonPhrase
                });
            }
        }

        public async Task<IActionResult> DeleteCartItem([FromBody] ShoppingCart request)
        {
            // Extract the IShopId from the cookie to identify the user
            string? userId = Request.Cookies["IShopId"];

            // Prepare the base URL from your configuration
            string baseUrl = _configuration["APIURL"];  // Base URL from configuration
            string apiUrl = $"{baseUrl}/user/v1/deletecartitem";  // The endpoint of the deletecartitem API

            // Create the request object (CartItemDeleteRequest)
            var deleteRequest = new CartItemDeleteRequest
            {
                ProductId = request.ProductId,  // Pass ProductId from ShoppingCart
                ProductsImageId = request.ProductsImageId  // Pass ProductsImageId from ShoppingCart
            };

            try
            {
                // Add IShopId cookie to the header if needed by the API
                if (!string.IsNullOrEmpty(userId))
                {
                    _httpClient.DefaultRequestHeaders.Add("Cookie", $"IShopId={userId}");
                }

                // Serialize the request body into JSON
                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(deleteRequest),
                    Encoding.UTF8,
                    "application/json");

                // Send the POST request to the API
                HttpResponseMessage response = await _httpClient.PostAsync(apiUrl, jsonContent);

                // Handle the response from the API
                if (response.IsSuccessStatusCode)
                {
                    string resultJson = await response.Content.ReadAsStringAsync();

                    // Return the result as JSON back to the caller
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(resultJson);
                    return Json(jsonElement);
                }
                else
                {
                    // If the request fails, return an error message
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    return Json(new
                    {
                        success = false,
                        message = "Failed to contact DeleteCartItem API.",
                        error = response.ReasonPhrase,
                        errorDetails = errorResponse
                    });
                }
            }
            catch (Exception ex)
            {
                // If any exception occurs during the API call, return an error message
                return Json(new
                {
                    success = false,
                    message = $"Error occurred: {ex.Message}"
                });
            }
        }

        public class CartItemDeleteRequest
        {
            public int? UserId { get; set; }
            public int? ProductId { get; set; }
            public int ProductsImageId { get; set; }
        }

        [HttpGet]
        public IActionResult Checkout()
        {
            var IShopId = Request.Cookies["IShopId"]; // Fetch IShopId from cookies

            if (string.IsNullOrEmpty(IShopId))
            {
                Helper.IsCheckout = true;
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetAddresses()
        {
            try
            {
                string? shopId = Request.Cookies["IShopId"];
                if (string.IsNullOrEmpty(shopId))
                {
                    return BadRequest(new { success = false, message = "Invalid or missing IShopId in cookie." });
                }

                string baseUrl = _configuration["APIURL"];
                string apiUrl = $"{baseUrl}/user/v1/getaddresses";

                // Add cookie manually to the request header
                _httpClient.DefaultRequestHeaders.Add("Cookie", $"IShopId={shopId}");

                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string resultJson = await response.Content.ReadAsStringAsync();

                    //  Deserialize the raw JSON into a list directly
                    var addresses = JsonSerializer.Deserialize<List<DelivaryAddresses>>(resultJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (addresses == null || addresses.Count == 0)
                    {
                        return NotFound(); // Only 404 with no extra message
                    }

                    return Ok(addresses); //  Return the list directly
                }
                else
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        success = false,
                        message = $"Failed to retrieve addresses. Status code: {response.StatusCode}"
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error occurred while calling address API.",
                    error = ex.Message
                });
            }
        }

        public async Task<IActionResult> SaveAddress([FromBody] DelivaryAddresses model)
        {
            if (model == null)
            {
                return BadRequest("Model is null.");
            }

            try
            {
                // Get the base URL from configuration
                string baseUrl = _configuration["APIURL"]; // From appsettings
                string apiUrl = $"{baseUrl}/user/v1/save"; // Make sure your API URL is correct

                // Serialize the model into JSON
                var json = JsonSerializer.Serialize(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");


                // Send POST request to the internal API
                HttpResponseMessage response = await _httpClient.PostAsync(apiUrl, content);

                // Check if the request was successful
                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, "Failed to save address.");
                }

                // Read and parse the response from the API
                string responseBody = await response.Content.ReadAsStringAsync();

                // Parse the response body as JSON
                using var document = JsonDocument.Parse(responseBody);
                var root = document.RootElement;

                // Extract values from the JSON response
                bool success = root.GetProperty("success").GetBoolean();
                string message = root.GetProperty("message").GetString();
                int addressId = root.GetProperty("addressId").GetInt32();

                // Returning an OK response with the success message and addressId
                return Ok(new { success = success, message = message, addressId = addressId });
            }
            catch (Exception ex)
            {
                // Handle any errors that occur during the API call
                return StatusCode(500, $"Error calling SaveAddress API: {ex.Message}");
            }
        }

        public async Task<IActionResult> SaveOrder([FromBody] List<Orders> orders)
        {
            if (orders == null || orders.Count == 0)
                return BadRequest(new { success = false, message = "Orders list is empty." });

            try
            {
                string baseUrl = _configuration["APIURL"]; // From appsettings.json
                string apiUrl = $"{baseUrl}/user/v1/saveorder"; // Adjust route if needed


                //  Add cookie header if needed
                if (Request.Cookies.TryGetValue("IShopId", out var iShopId))
                {
                    _httpClient.DefaultRequestHeaders.Add("Cookie", $"IShopId={iShopId}");
                }

                var json = JsonSerializer.Serialize(orders);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(apiUrl, content);

                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        success = false,
                        message = "Failed to save order.",
                        error = responseBody
                    });
                }

                //  Parse the message from response JSON
                using var doc = JsonDocument.Parse(responseBody);
                string message = doc.RootElement.GetProperty("message").GetString();

                TempData["Message"] = message;
                return Ok(new { success = true, message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Exception occurred while calling order API.",
                    error = ex.Message
                });
            }
        }

        public async Task<IActionResult> SaveCheckout([FromBody] Checkout checkout)
        {
            if (checkout == null)
                return BadRequest("Checkout data is null.");

            try
            {
                string baseUrl = _configuration["APIURL"]; // e.g., https://yourapi.com
                string apiUrl = $"{baseUrl}/user/v1/savecheckout"; // Adjust based on your route

                // Send cookie if needed (IShopId)
                if (Request.Cookies.TryGetValue("IShopId", out var iShopId))
                {
                    _httpClient.DefaultRequestHeaders.Add("Cookie", $"IShopId={iShopId}");
                }

                var json = JsonSerializer.Serialize(checkout);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(apiUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, $"Checkout API call failed: {errorContent}");
                }

                var responseBody = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(responseBody);
                var message = doc.RootElement.GetProperty("message").GetString();
                var paymentMode = doc.RootElement.TryGetProperty("paymentMode", out var pm) ? pm.GetString() : null;
                var orderId = doc.RootElement.TryGetProperty("orderId", out var oid) ? oid.GetString() : null;


                TempData["Message"] = message;
                TempData["PaymentMode"] = paymentMode;

                return Ok(new { message = message, paymentMode = paymentMode, orderId = orderId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error while calling checkout API: {ex.Message}");
            }
        }

        [HttpGet("/Home/Coupan/{total}")]
        public async Task<IActionResult> Coupan(double total)
        {
            try
            {
                string baseUrl = _configuration["APIURL"]; // e.g., "https://localhost:5001"
                string apiUrl = $"{baseUrl}/user/v1/Coupan/{total}";

                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string resultJson = await response.Content.ReadAsStringAsync();

                    // Deserialize to List<Coupan>
                    var coupons = JsonSerializer.Deserialize<List<Coupan>>(resultJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (coupons == null || coupons.Count == 0)
                    {
                        return NotFound(new { success = false, message = "No coupons found." });
                    }

                    return Json(coupons);
                }
                else
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        success = false,
                        message = $"Failed to retrieve coupons. Status: {response.StatusCode}"
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error occurred while calling coupon API.", error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Orders()
        {
            // Read IShopId from cookies
            string shopIdString = Request.Cookies["IShopId"];
            if (string.IsNullOrEmpty(shopIdString) || !int.TryParse(shopIdString, out int iShopId))
            {
                return RedirectToAction("Login", "Account");
            }

            // API URL to get orders
            string baseUrl = _configuration["APIURL"];
            string apiUrl = $"{baseUrl}/user/v1/orders";

            // Add cookie manually to the request header
            _httpClient.DefaultRequestHeaders.Add("Cookie", $"IShopId={shopIdString}");

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string resultJson = await response.Content.ReadAsStringAsync();

                    // Deserialize to List<OrderDetails>
                    var orders = JsonSerializer.Deserialize<List<OrderDetails>>(resultJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (orders == null || orders.Count == 0)
                    {
                        ViewBag.ErrorMessage = "No orders found.";
                        return View(new List<OrderDetails>());
                        // return NotFound(new { success = false, message = "No orders found." });
                    }

                    return View(orders);  // Pass the list of orders to the view
                }
                else
                {
                    //return StatusCode((int)response.StatusCode, new
                    //{
                    //    success = false,
                    //    message = $"Failed to retrieve orders. Status: {response.StatusCode}"
                    //});
                    ViewBag.ErrorMessage = "No orders found.";
                    return View(new List<OrderDetails>());
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error occurred while calling Orders API.", error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ViewOrder(string id)
        {
            string shopIdString = Request.Cookies["IShopId"];
            if (string.IsNullOrEmpty(shopIdString))
                return RedirectToAction("Login", "Account");

            string baseUrl = _configuration["APIURL"];
            string apiUrl = $"{baseUrl}/user/v1/{id}";

            _httpClient.DefaultRequestHeaders.Add("Cookie", $"IShopId={shopIdString}");

            var response = await _httpClient.GetAsync(apiUrl);

            if (!response.IsSuccessStatusCode)
                return View(new List<OrderDetails>());

            var json = await response.Content.ReadAsStringAsync();
            var orders = JsonSerializer.Deserialize<List<OrderDetails>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return View(orders);
        }

        [HttpPost]
        public async Task<IActionResult> CancelOrder(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Order ID is required.");

            try
            {
                string baseUrl = _configuration["APIURL"]; // e.g., https://localhost:5001
                string apiUrl = $"{baseUrl}/user/v1/cancelorder?id={id}";

                var response = await _httpClient.PostAsync(apiUrl, null); // POST with query string, no body

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, $"Cancel API failed: {error}");
                }

                // var content = await response.Content.ReadAsStringAsync();

                return RedirectToAction("Index"); // Navigate back to order list or desired page
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error calling CancelOrder API: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckStockAvailability(int productImageId, int requestedQty)
        {
            try
            {
                string baseUrl = _configuration["APIURL"]; // e.g. "https://your-api.com"
                string apiUrl = $"{baseUrl}/user/v1/CheckAvailability?productImageId={productImageId}&requestedQty={requestedQty}";

                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string resultJson = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

                    return Ok(result);
                }
                else
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        success = false,
                        message = $"API call failed with status: {response.StatusCode}"
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "HTTP call failed", error = ex.Message });
            }
        }
        public async Task<List<Products>> SearchProduct(string query)
        {
            string baseUrl = _configuration["APIURL"];
            string apiUrl = $"{baseUrl}/user/v1/SearchProduct?query={query}";

            var response = await _httpClient.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<Products>>(jsonString);
            }

            return new List<Products>();
        }

        [HttpGet]
        public async Task<IActionResult> MyAccount([FromServices] IHttpClientFactory httpClientFactory)
        {
            string shopIdString = Request.Cookies["IShopId"];
            if (string.IsNullOrEmpty(shopIdString) || !int.TryParse(shopIdString, out int iShopId))
            {
                return RedirectToAction("Login", "Account");
            }


            string baseUrl = _configuration["APIURL"];
            string apiUrl = $"{baseUrl}/user/v1/MyAccount?iShopId={iShopId}";

            HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);

            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Profile not found.";
                TempData["Message"] = "";
                return View();
            }

            var json = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<Register>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> MyAccount(Register model, [FromServices] IHttpClientFactory httpClientFactory)
        {
            string shopIdString = Request.Cookies["IShopId"];
            if (string.IsNullOrEmpty(shopIdString) || !int.TryParse(shopIdString, out int iShopId))
            {
                return RedirectToAction("Login", "Account");
            }


            string baseUrl = _configuration["APIURL"];
            string apiUrl = $"{baseUrl}/user/v1/MyAccount?iShopId={iShopId}";

            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                TempData["Message"] = "Account updated successfully!";
                return RedirectToAction("MyAccount");
            }
            else
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                TempData["Message"] = "Update failed: " + errorMsg;
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> MyAddress([FromServices] IHttpClientFactory httpClientFactory)
        {
            string shopIdString = Request.Cookies["IShopId"];
            if (string.IsNullOrEmpty(shopIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            string baseUrl = _configuration["APIURL"];
            string apiUrl = $"{baseUrl}/user/v1/getaddresses";

            // Send the cookie manually
            _httpClient.DefaultRequestHeaders.Add("Cookie", $"IShopId={shopIdString}");

            HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);

            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Failed to retrieve addresses.";
                return View();
            }

            string json = await response.Content.ReadAsStringAsync();

            var addressList = JsonSerializer.Deserialize<List<DelivaryAddresses>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (addressList == null || addressList.Count == 0)
            {
                ViewBag.Error = "No addresses found.";
            }

            ViewBag.AddressList = addressList;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> MyAddress(DelivaryAddresses model, string formMode, [FromServices] IHttpClientFactory httpClientFactory)
        {
            string shopIdString = Request.Cookies["IShopId"];
            if (string.IsNullOrEmpty(shopIdString) || !int.TryParse(shopIdString, out int iShopId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {

                string baseUrl = _configuration["APIURL"];
                string apiUrl = $"{baseUrl}/user/v1/saveaddress?iShopId={iShopId}&formMode={Uri.EscapeDataString(formMode)}";

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(model),
                    Encoding.UTF8,
                    "application/json"
                );

                HttpResponseMessage response = await _httpClient.PostAsync(apiUrl, jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Message"] = formMode == "Add Address" ? "Address added successfully!" : "Address updated successfully!";
                    return RedirectToAction("MyAddress");
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    ViewBag.Error = $"API Error: {response.StatusCode} - {errorContent}";
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "An error occurred while saving address: " + ex.Message;
                return View(model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAddress(int addressId)
        {
            if (addressId <= 0)
            {
                return BadRequest("Invalid address ID");
            }

            using (var httpClient = new HttpClient())
            {
                string baseUrl = _configuration["APIURL"];
                string apiUrl = $"{baseUrl}/user/v1/deleteaddress/{addressId}";
                var response = await httpClient.DeleteAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Message"] = "Address deleted successfully!";
                }
                else
                {
                    TempData["Message"] = "Failed to delete address.";
                }
            }

            return RedirectToAction("MyAddress");
        }

        [HttpGet]
        public async Task<IActionResult> ContactUs()
        {
            int? ishopId = HttpContext.Session.GetInt32("IShopId");

            var user = await _context.Register
                .FirstOrDefaultAsync(u => u.IShopId == ishopId);

            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> AboutUs()
        {
            int? ishopId = HttpContext.Session.GetInt32("IShopId");

            var user = await _context.Register
                .FirstOrDefaultAsync(u => u.IShopId == ishopId);

            return View(user);
        }

        public async Task<List<OrderDetailsDto>> GetOrderDetailsByOrderId(Guid orderId)
        {
            // Replace with your actual API base URL
            string baseUrl = _configuration["APIURL"];
            string apiUrl = $"{baseUrl}/user/v1/orderdetails/{orderId}";

            var response = await _httpClient.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<OrderDetailsDto>>(jsonString);
            }

            return new List<OrderDetailsDto>();
        }

        public async Task<IActionResult> GenerateAndSendInvoice(Guid orderId)
        {
            var orderDetails = await GetOrderDetailsByOrderId(orderId);
            if (!orderDetails.Any())
            {
                return NotFound("No details found for the provided Order ID.");
            }

            var order = orderDetails.First();

            if (string.IsNullOrWhiteSpace(order.Email))
                return BadRequest("Customer email address is missing.");

            // Create an invoice model
            var invoiceModel = new InvoiceViewModel
            {
                // Populate the properties from the order
                Id = order.PaymentId,
                OrderId = order.OrderId,
                CustomerName = order.FullName,
                CustomerEmail = order.Email,
                CustomerPhone = order.Mobile,
                CustomerAddress = $"{order.Address}, {order.City}, {order.State},{order.Country}, {order.Zipcode}",
                OrderDate = order.OrderDate,
                Subtotal = order.OrderAmount,
                Discount = order.PromoAmount,
                Total = order.TotalAmount,
                PaymentMethod = order.PaymentMode,
                PaymentAmount = order.PaymentAmount,
                PaymentId = order.PaymentId,

                // Add shop information (populate this as required)
                ShopName = "IShop",
                //ShopLogoUrl = logoDataUrl,
                ShopEmail = "IShop@gmail.com",
                ShopPhone = "+91 9876543210",
                //GSTNumber = "GST1234567890",  

                // Set items in the invoice
                Items = orderDetails.Select(item => new InvoiceItem
                {
                    Name = item.ProductName,
                    Type = item.Type,
                    Color = item.Color,
                    Price = item.Price,
                    Quantity = item.OrderQty,
                    Total = item.TotalAmount
                }).ToList(),

                // Signature URL (if available)
                /* SignatureUrl = "iShop"*/
            };

            // Render the HTML content for the invoice view
            string htmlContent = await _viewRenderService.RenderToStringAsync("Invoice", invoiceModel);

            // Generate PDF from the HTML content
            byte[] pdfBytes = GeneratePdfFromHtml(htmlContent);

            // Send Email with the generated PDF as an attachment
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Vidhi Patel", "vidhi.p.ivorytechnolab@gmail.com"));
            message.To.Add(new MailboxAddress(invoiceModel.CustomerName, invoiceModel.CustomerEmail));
            message.Subject = "Your Invoice";

            var builder = new BodyBuilder
            {
                TextBody = "Thank you for your order! Please find the invoice attached."
            };
            builder.Attachments.Add("Invoice.pdf", pdfBytes, new ContentType("application", "pdf"));
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync("vidhi.p.ivorytechnolab@gmail.com", "qbvryemjvxibgvjd");
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            // Return a success message
            TempData["InvoiceSuccess"] = "Thank you for your order! Visit again.";
            return RedirectToAction("OrderConfirmation"); // You can redirect to another page after email is sent
        }

        private byte[] GeneratePdfFromHtml(string htmlContent)
        {
            // Step 1: Fallback HTML if input is null or empty
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                htmlContent = "<html><body><p>No content to display.</p></body></html>";
            }

            using (var ms = new MemoryStream())
            {
                using (var doc = new Document(PageSize.A4, 25, 25, 30, 30))
                {
                    PdfWriter writer = PdfWriter.GetInstance(doc, ms);
                    doc.Open();

                    bool success = false;

                    try
                    {
                        using (var sr = new StringReader(htmlContent))
                        {
                            XMLWorkerHelper.GetInstance().ParseXHtml(writer, doc, sr);
                            success = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error (optional)
                        Console.WriteLine("PDF generation failed: " + ex.Message);
                    }

                    // Step 2: Fallback content if HTML failed or rendered no page
                    if (!success || writer.PageNumber == 0)
                    {
                        doc.NewPage(); // forcefully create a page
                        doc.Add(new Paragraph("Unable to render the requested content. Please try again later."));
                    }

                    doc.Close();
                }

                return ms.ToArray();
            }
        }

        [HttpGet]
        public IActionResult Invoice()
        {
            return View();
        }

        [HttpGet]
        public IActionResult OrderConfirmation()
        {
            ViewBag.ShowSuccessModal = TempData["InvoiceSuccess"] != null;
            ViewBag.SuccessMessage = TempData["InvoiceSuccess"]?.ToString();
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}