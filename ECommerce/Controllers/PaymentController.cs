using Braintree;
using ECommerce.Models;
using MailKit.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Text;

namespace ECommerce.Controllers
{
    public class PaymentController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<PaymentController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IBraintreeGateway _braintreeGateway;
        private readonly HttpClient _httpClient;

        public PaymentController(IConfiguration configuration, ILogger<PaymentController> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection");

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            _httpClient = new HttpClient(handler);

            // Initialize Braintree gateway
            _braintreeGateway = new BraintreeGateway
            {
                Environment = Braintree.Environment.SANDBOX,
                MerchantId = _configuration["Braintree:MerchantId"],
                PublicKey = _configuration["Braintree:PublicKey"],
                PrivateKey = _configuration["Braintree:PrivateKey"]
            };            
        }

        [HttpGet]
        public async Task<IActionResult> Payment(string orderId)
        {
            string baseUrl = _configuration["APIURL"]; // e.g., https://localhost:5001
            string apiUrl = $"{baseUrl}/user/v1/payment?orderId={orderId}";

            try
            {
                var response = await _httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    ViewData["PaymentMessage"] = "Failed to fetch payment details.";
                    return View();
                }

                var jsonString = await response.Content.ReadAsStringAsync();

                // Parse JSON manually
                var json = JObject.Parse(jsonString);

                string finalOrderId = json["orderId"]?.ToString();
                decimal orderAmount = json["orderAmount"] != null ? json["orderAmount"].Value<decimal>() : 0;
                string paymentMode = json["paymentMode"]?.ToString();
                bool isPaid = json["isPaymentDone"] != null && json["isPaymentDone"].Value<bool>();
                string message = json["message"]?.ToString();

                if (!string.IsNullOrEmpty(paymentMode))
                    HttpContext.Session.SetString("PaymentMode", paymentMode);

                // COD logic
                if (paymentMode == "COD" && !isPaid)
                {
                    return await SaveCashPayment(finalOrderId, orderAmount);
                }

                ViewData["OrderId"] = finalOrderId;
                ViewData["OrderAmount"] = orderAmount;
                ViewData["PaymentMessage"] = message;

                return View();
            }
            catch (Exception ex)
            {
                ViewData["PaymentMessage"] = "Error occurred while processing payment: " + ex.Message;
                return View();
            }
        }

        private async Task<IActionResult> SaveCashPayment (string orderId, decimal orderAmount)
        {
            if (string.IsNullOrEmpty(orderId) || orderAmount <= 0)
                return BadRequest("Invalid order details.");

            try
            {
                string apiBaseUrl = _configuration["APIURL"]; // e.g. "https://localhost:5001"
                string apiUrl = $"{apiBaseUrl}/user/v1/savecashpayment?orderId={orderId}&amount={orderAmount}";

                var response = await _httpClient.PostAsync(apiUrl, null); // No body; params in query string

                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction("GenerateAndSendInvoice", "Home", new { orderId });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, $"API error: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal error: {ex.Message}");
            }
        }

        //  Generate Client Token (Required for Frontend)
        [HttpGet("braintree/client-token")]
        public async Task<IActionResult> GetClientToken()
        {
            var clientToken = await _braintreeGateway.ClientToken.GenerateAsync();
            return Ok(new { token = clientToken });
        }

        [HttpGet("braintree/get-checkout-data")]
        public IActionResult GetCheckoutData()
        {
            string paymentMode = HttpContext.Session.GetString("PaymentMode") ?? "Braintree";
            return Ok(new { paymentMode = paymentMode });
        }

        [HttpPost("braintree/checkout")]
        public async Task<IActionResult> ProcessPayment([FromBody] PaymentRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.OrderId) || string.IsNullOrEmpty(request.Nonce))
            {
                return BadRequest(new { success = false, error = "Invalid payment request" });
            }

            try
            {
                string apiBaseUrl = _configuration["APIURL"];
                string apiUrl = $"{apiBaseUrl}/user/v1/braintree/checkout";

                var requestData = new
                {
                    OrderId = request.OrderId,
                    Nonce = request.Nonce,
                    PaymentMode = request.PaymentMode
                };

                var json = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var jsonResponse = JsonConvert.DeserializeObject<dynamic>(responseString);

                    bool success = jsonResponse.success ?? false;
                    string transactionId = jsonResponse.transactionId ?? string.Empty;
                    string status = jsonResponse.status ?? string.Empty;
                    string redirectUrl = jsonResponse.redirectUrl ?? string.Empty;
                    string message = jsonResponse.message ?? string.Empty;

                    if (success)
                    {
                        return Ok(new
                        {
                            success = true,
                            transactionId,
                            status,
                            redirectUrl,
                            message
                        });
                    }
                    else
                    {
                        return BadRequest(new { success = false, error = message });
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, $"API Error: {error}");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal error: {ex.Message}");
            }
        }

        //  Define Payment Request Model
        public class PaymentRequest
        {
            public string OrderId { get; set; }  // Order ID for reference
            public string Nonce { get; set; } // Payment nonce from the frontend
            public string PaymentMode { get; set; }
        }
    }
}