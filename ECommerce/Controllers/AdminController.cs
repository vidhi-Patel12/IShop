using ECommerce.Data;
using ECommerce.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Drawing;
using System.Globalization;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Web;
using static Azure.Core.HttpHeader;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ECommerce.Controllers
{
    public class AdminController : Controller
    {
        private readonly string _connectionString;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<AdminController> _logger;

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public AdminController(ApplicationDbContext dbContext, IConfiguration configuration, IWebHostEnvironment webHostEnvironment, ILogger<AdminController> logger, HttpClient httpClient, IConfiguration config)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _webHostEnvironment = webHostEnvironment;
            _dbContext = dbContext;
            _logger = logger;
            _httpClient = httpClient;
            _config = config;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            _httpClient = new HttpClient(handler);
        }

        public async Task<IActionResult> Index()
        {
            List<Products> productsList = new List<Products>();

            // Get base API URL from config
            string baseUrl = _config["APIURL"]; // e.g., "https://localhost:5001"
            string apiUrl = $"{baseUrl}/admin/v1/product";

            HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();

                productsList = JsonSerializer.Deserialize<List<Products>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to fetch product data.";
            }

            return View(productsList);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] List<Products> productList, IFormFile? largeImageFile, IFormFile? mediumImageFile, IFormFile? smallImageFile)
        {
            string? userId = HttpContext?.Request.Cookies["IShopId"];
            if (userId == null)
            {
                return BadRequest(new { message = "User not logged in" });
            }

            string baseUrl = _config["APIURL"];
            string url = $"{baseUrl}/admin/v1/product/create";

            using var form = new MultipartFormDataContent();

            try
            {
                foreach (var product in productList)
                {
                    product.IsActive = true;
                }

                // Step 1: Add product data to form
                string jsonProductList = JsonConvert.SerializeObject(productList);
                var jsonContent = new StringContent(jsonProductList, Encoding.UTF8, "application/json");
                form.Add(jsonContent, "productList");

                form.Add(new StringContent(userId), "userId");

                // Step 2: Add image files to form
                if (largeImageFile != null)
                {
                    var content = new StreamContent(largeImageFile.OpenReadStream());
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(largeImageFile.ContentType);
                    form.Add(content, "largeImageFile", largeImageFile.FileName);
                }

                if (mediumImageFile != null)
                {
                    var content = new StreamContent(mediumImageFile.OpenReadStream());
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediumImageFile.ContentType);
                    form.Add(content, "mediumImageFile", mediumImageFile.FileName);
                }

                if (smallImageFile != null)
                {
                    var content = new StreamContent(smallImageFile.OpenReadStream());
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(smallImageFile.ContentType);
                    form.Add(content, "smallImageFile", smallImageFile.FileName);
                }

                // Step 3: Send form to API
                var response = await _httpClient.PostAsync(url, form);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return BadRequest(new { message = "API Error", details = errorContent });
                }

                // Step 4: Extract created products from response
                var jsonResponse = await response.Content.ReadAsStringAsync();

                var jObject = JObject.Parse(jsonResponse);
                var productId = jObject["productId"]?.ToObject<int>();

                if (productId == null || productId == 0)
                    return BadRequest(new { message = "Invalid product ID." });

                string id = productId.ToString();
                string basePath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "Products", id);

                async Task<string?> SaveImage(IFormFile? file, string subfolder)
                {
                    if (file == null) return null;

                    string folder = Path.Combine(basePath, subfolder);
                    Directory.CreateDirectory(folder);

                    string filePath = Path.Combine(folder, file.FileName);
                    using var stream = new FileStream(filePath, FileMode.Create);
                    await file.CopyToAsync(stream);

                    return $"/uploads/Products/{id}/{subfolder}/{file.FileName}";
                }

                await SaveImage(largeImageFile, "Large");
                await SaveImage(mediumImageFile, "Medium");
                await SaveImage(smallImageFile, "Small");


                return Content(jsonResponse, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", details = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, int? productsImageId)
        {
            Products product = null;

            string baseUrl = _config["APIURL"]; // e.g., https://localhost:5001
            string url = $"{baseUrl}/admin/v1/product/{id}";

            if (productsImageId.HasValue)
            {
                url += $"?productsImageId={productsImageId.Value}";
            }


            HttpResponseMessage response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                product = JsonSerializer.Deserialize<Products>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to fetch product details.";
                return RedirectToAction("Index"); // or return NotFound()
            }
            return Json(product);
        }

        [HttpPost]
        public async Task<IActionResult> Edit([FromForm] List<Products> productList, IFormFile? largeImageFile, IFormFile? mediumImageFile, IFormFile? smallImageFile)
        {
            string? userId = HttpContext?.Request.Cookies["IShopId"];
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { message = "User not logged in" });
            }

            string baseUrl = _config["APIURL"];
            string url = $"{baseUrl}/admin/v1/product/update";  // Pointing to your update endpoint

            using var form = new MultipartFormDataContent();

            try
            {
                // Step 1: Serialize product list to string as expected by the update API
                string jsonProductList = JsonConvert.SerializeObject(productList);
                var jsonContent = new StringContent(jsonProductList, Encoding.UTF8, "application/json");
                form.Add(jsonContent, "productList");

                // Step 2: Add userId to the form data
                form.Add(new StringContent(userId), "userId");

                // Step 3: Attach image files
                if (largeImageFile != null)
                {
                    var content = new StreamContent(largeImageFile.OpenReadStream());
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(largeImageFile.ContentType);
                    form.Add(content, "largeImageFile", largeImageFile.FileName);
                }

                if (mediumImageFile != null)
                {
                    var content = new StreamContent(mediumImageFile.OpenReadStream());
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediumImageFile.ContentType);
                    form.Add(content, "mediumImageFile", mediumImageFile.FileName);
                }

                if (smallImageFile != null)
                {
                    var content = new StreamContent(smallImageFile.OpenReadStream());
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(smallImageFile.ContentType);
                    form.Add(content, "smallImageFile", smallImageFile.FileName);
                }

                // Step 4: Call the API
                var response = await _httpClient.PostAsync(url, form);
                var responseContent = await response.Content.ReadAsStringAsync();

                // Check if the update API responded with success
                if (!response.IsSuccessStatusCode)
                {
                    return BadRequest(new { message = "Update API failed", details = responseContent });
                }

                // Step 5: Extract productId from the response (if needed for further actions)
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var jObject = JObject.Parse(jsonResponse);
                var productId = jObject["productId"]?.ToObject<int>();

                if (productId == null || productId == 0)
                    return BadRequest(new { message = "Invalid product ID." });

                string id = productId.ToString();
                string basePath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "Products", id);

                // Step 6: Save images after the API response
                async Task<string?> SaveImage(IFormFile? file, string subfolder)
                {
                    if (file == null) return null;

                    string folder = Path.Combine(basePath, subfolder);
                    Directory.CreateDirectory(folder);

                    string filePath = Path.Combine(folder, file.FileName);
                    using var stream = new FileStream(filePath, FileMode.Create);
                    await file.CopyToAsync(stream);

                    return $"/uploads/Products/{id}/{subfolder}/{file.FileName}";
                }

                // Save all images after successful API response
                await SaveImage(largeImageFile, "Large");
                await SaveImage(mediumImageFile, "Medium");
                await SaveImage(smallImageFile, "Small");

                // Return successful response
                return Content(jsonResponse, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", details = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id, int productsImageId)
        {
            string baseUrl = _config["APIURL"]; // e.g. "https://localhost:5001"
            string endpoint = $"{baseUrl}/admin/v1/product/delete?id={id}&productsImageId={productsImageId}";
            try
            {
                var response = await _httpClient.PostAsync(endpoint, null); // POST with no body, just query params

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    // Optionally parse json if you need to check `{ success = true }`
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to delete product image.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Exception: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetById(int id, int? productsImageId)
        {
            Products product = null;

            string baseUrl = _config["APIURL"]; // e.g., https://localhost:5001
            string url = $"{baseUrl}/admin/v1/product/{id}";

            if (productsImageId.HasValue)
            {
                url += $"?productsImageId={productsImageId.Value}";
            }

            HttpResponseMessage response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                product = JsonSerializer.Deserialize<Products>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to fetch product details.";
                return RedirectToAction("Index"); // or return NotFound()
            }

            return Json(product);
        }

        [HttpGet]
        public async Task<IActionResult> Coupan()
        {
            string baseUrl = _config["APIURL"];
            string requestUrl = $"{baseUrl}/admin/v1/coupan";

            HttpResponseMessage response = await _httpClient.GetAsync(requestUrl);

            if (response.IsSuccessStatusCode)
            {
                var coupons = await response.Content.ReadFromJsonAsync<List<Coupan>>();
                return View(coupons); // assuming it's MVC
            }

            return StatusCode((int)response.StatusCode, "Failed to load coupons.");
        }

        [HttpGet]
        public async Task<IActionResult> AddOrUpdateCoupan(int? id)
        {
            Coupan model = new Coupan();

            if (id != null && id != 0) // Editing existing coupon
            {
                model = await FetchCoupanById(id.Value);
                if (model == null)
                {
                    return NotFound("Coupan not found");
                }
            }

            return Json(model);
        }

        [HttpPost]
        public async Task<IActionResult> AddOrUpdateCoupan([FromBody] Coupan model)
        {
            try
            {
                if (model.ExpiryDate == default)
                    throw new Exception("Invalid expiry date");

                model.IsActive = true;
                model.CoupanCode ??= "string";

                string baseUrl = _config["APIURL"];
                string requestUrl = $"{baseUrl}/admin/v1/coupan";

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(model, options);
                Console.WriteLine("Sending JSON to API: " + json);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync(requestUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ApiResult>(responseBody, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    TempData["SuccessMessage"] = result?.Message ?? "Coupon saved!";
                    return Ok(new { success = true, message = result?.Message });
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("API Error Response: " + error);
                    TempData["ErrorMessage"] = "API Error: " + error;
                    return Json(model);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error: " + ex.Message;
                return Json(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCoupanById(int id)
        {
            Coupan model = await FetchCoupanById(id);

            if (model == null)
            {
                TempData["ErrorMessage"] = "Coupon not found!";
                return RedirectToAction("Coupan"); // Redirect if not found
            }

            return Json(model);
        }

        private async Task<Coupan> FetchCoupanById(int id)
        {
            // Get base URL from config like: "https://localhost:44358"
            string baseUrl = _config["APIURL"];
            string requestUrl = $"{baseUrl}/admin/v1/coupan/{id}";

            HttpResponseMessage response = await _httpClient.GetAsync(requestUrl);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Coupan>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }

            return null;
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCoupan(int id)
        {

            // Replace with your API base URL
            string baseUrl = _config["APIURL"];
            string requestUrl = $"{baseUrl}/admin/v1/coupan/{id}";

            HttpResponseMessage response = await _httpClient.DeleteAsync(requestUrl);

            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction("Coupan");
            }
            else
            {
                // Handle errors or logging
                return View("Error");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ValidateCoupan(string coupanCode)
        {
            if (string.IsNullOrWhiteSpace(coupanCode))
            {
                return BadRequest("Coupon code is required.");
            }

            string baseUrl = _config["APIURL"]; // Ensure this is in appsettings.json
            string apiUrl = $"{baseUrl}/admin/v1/coupan/validate?coupanCode={Uri.EscapeDataString(coupanCode)}";
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();

                    // Deserialize to dynamic instead of a typed model
                    var result = JsonSerializer.Deserialize<dynamic>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return Json(result); // Return the raw dynamic JSON result
                }
                else
                {
                    return Json(new { success = false, message = "API Error: " + response.StatusCode });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Exception: " + ex.Message });
            }
        }

        public async Task<IActionResult> Orders()
        {
            //  Read IShopId from cookies
            string shopIdString = Request.Cookies["IShopId"];
            if (string.IsNullOrEmpty(shopIdString) || !int.TryParse(shopIdString, out int iShopId))
            {
                return BadRequest("Invalid or missing IShopId in cookies.");
            }

            var orders = new List<OrderDetails>();

            // Prepare the API URL (adjust base URL if necessary)
            string baseUrl = _config["APIURL"]; // e.g., "https://localhost:5001"
            string apiEndpoint = $"{baseUrl}/admin/v1/order?shopId={iShopId}";

            HttpResponseMessage response = await _httpClient.GetAsync(apiEndpoint);

            if (response.IsSuccessStatusCode)
            {
                string jsonString = await response.Content.ReadAsStringAsync();
                orders = JsonSerializer.Deserialize<List<OrderDetails>>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            else
            {
                // Optional: handle API error response
                TempData["ErrorMessage"] = "Failed to fetch orders from API.";
            }
            return View(orders);
        }

        public async Task<IActionResult> ViewOrder(string id)
        {
            // Read IShopId from cookies
            string shopIdString = Request.Cookies["IShopId"];
            if (string.IsNullOrEmpty(shopIdString) || !int.TryParse(shopIdString, out int iShopId))
            {
                return BadRequest("Invalid or missing IShopId in cookies.");
            }

            var orders = new List<OrderDetails>();

            // Build the API URL
            string baseUrl = _config["APIURL"]; // e.g., https://localhost:5001
            string apiEndpoint = $"{baseUrl}/admin/v1/order/{id}";


            HttpResponseMessage response = await _httpClient.GetAsync(apiEndpoint);

            if (response.IsSuccessStatusCode)
            {
                string jsonString = await response.Content.ReadAsStringAsync();
                orders = JsonSerializer.Deserialize<List<OrderDetails>>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to fetch order details from API.";
            }

            if (orders == null || !orders.Any())
            {
                return NotFound("No order found with the given ID.");
            }

            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardChartData(string timeRange = "day")
        {
            string shopIdString = Request.Cookies["IShopId"];
            if (string.IsNullOrEmpty(shopIdString))
            {
                return BadRequest("Missing IShopId in cookies.");
            }

            object chartData = null;

            string baseUrl = _config["APIURL"];
            string apiEndpoint = $"{baseUrl}/admin/v1/dashboard/chart-data?timeRange={timeRange}";

            _httpClient.DefaultRequestHeaders.Add("Cookie", $"IShopId={shopIdString}");

            HttpResponseMessage response = await _httpClient.GetAsync(apiEndpoint);
            if (response.IsSuccessStatusCode)
            {
                string jsonString = await response.Content.ReadAsStringAsync();
                chartData = JsonSerializer.Deserialize<object>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            else
            {
                return StatusCode((int)response.StatusCode, "Error retrieving chart data.");
            }

            return new JsonResult(chartData);
        }

        [HttpGet]
        public IActionResult Dashboard()
        {
            return View();
        }

    }
}