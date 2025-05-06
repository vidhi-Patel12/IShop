using Braintree;
using ECommerce.Models;
using MailKit.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ECommerce.Controllers
{
    public class PaymentController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<PaymentController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IBraintreeGateway _braintreeGateway;

        public PaymentController(IConfiguration configuration, ILogger<PaymentController> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection");

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
        public IActionResult Payment(string orderId)
        {
            //string orderId = null;
            decimal orderAmount = 0;
            bool isPaymentDone = false;
            string paymentMode = null;
            string message = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Step 1: Fetch Order Details using provided OrderId (if available)
                if (!string.IsNullOrEmpty(orderId))
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT OrderId, OrderAmount, PaymentMode FROM Checkout WHERE OrderId = @OrderId", conn))
                    {
                        cmd.Parameters.AddWithValue("@OrderId", orderId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                orderId = reader.GetString(0);
                                orderAmount = (decimal)reader.GetDouble(1);
                                paymentMode = reader.GetString(2);
                                HttpContext.Session.SetString("PaymentMode", paymentMode);
                            }
                        }
                    }
                }

                // If orderId is still null, fetch the latest order
                if (string.IsNullOrEmpty(orderId))
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 OrderId, OrderAmount, PaymentMode FROM Checkout ORDER BY CheckoutId DESC", conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                orderId = reader.GetString(0);  // Last Order ID
                                orderAmount = (decimal)reader.GetDouble(1); // Total Amount
                                paymentMode = reader.GetString(2); // Payment Mode
                                HttpContext.Session.SetString("PaymentMode", paymentMode);
                            }
                            else
                            {
                                message = "No orders found.";
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(orderId))
                {
                    // Step 2: Check if payment is already done
                    using (SqlCommand checkCmd = new SqlCommand("SELECT TransactionId, Status FROM Payment WHERE OrderId = @OrderId", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@OrderId", orderId);
                        using (SqlDataReader reader = checkCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string existingTransactionId = reader["TransactionId"].ToString();
                                string existingStatus = reader["Status"].ToString();

                                if (!string.IsNullOrEmpty(existingTransactionId) && existingStatus == "Success")
                                {
                                    isPaymentDone = true;
                                    message = "Payment is already done for this order.";
                                }
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(orderId))
            {
                message = "No orders found.";
            }

            // Step 3: If PaymentMode is Cash, directly save payment and redirect
            if (paymentMode == "COD" && !isPaymentDone)
            {
                return SaveCashPayment(orderId, orderAmount);
            }

            if (isPaymentDone)
            {
                message = "Payment is already done for this order.";
            }

            // Step 4: Pass Data to View (Only if not cash payment)
            ViewData["OrderId"] = orderId;
            ViewData["OrderAmount"] = orderAmount;
            ViewData["PaymentMessage"] = message;
            return View();
        }

        // Separate method to handle direct cash payments
        private IActionResult SaveCashPayment(string orderId, decimal orderAmount)
        {
            string transactionId = Guid.NewGuid().ToString("N").Substring(0, 8);
            string paymentStatus = "Pending";
            DateTime paymentDate = DateTime.Now;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand("SavePaymentAndUpdateOrder", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@TransactionId", transactionId);
                    cmd.Parameters.AddWithValue("@OrderId", orderId);
                    cmd.Parameters.AddWithValue("@PaymentMode", "COD");
                    cmd.Parameters.AddWithValue("@Amount", orderAmount);
                    cmd.Parameters.AddWithValue("@Status", paymentStatus);
                    cmd.Parameters.AddWithValue("@PaymentDate", paymentDate);

                    cmd.ExecuteNonQuery();
                }
            }
            return RedirectToAction("GenerateAndSendInvoice", "Home", new { orderId });
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

        //  Process Payment
        [HttpPost("braintree/checkout")]
        public async Task<IActionResult> ProcessPayment([FromBody] PaymentRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.OrderId) || string.IsNullOrEmpty(request.Nonce))
            {
                return BadRequest(new { error = "Invalid payment request" });
            }

            string paymentStatus;
            string transactionId = null;
            double orderAmount = 0;
            int iShopId = 0;
            string paymentMode = null;
            DateTime paymentDate = DateTime.Now;
            Result<Transaction> result = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                //  Step 1: Check if Payment Already Exists
                using (SqlCommand checkCmd = new SqlCommand("SELECT TransactionId, Status FROM Payment WHERE OrderId = @OrderId", conn))
                {
                    checkCmd.Parameters.AddWithValue("@OrderId", request.OrderId);
                    using (SqlDataReader reader = checkCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string existingTransactionId = reader["TransactionId"].ToString();
                            string existingStatus = reader["Status"].ToString();

                            if (!string.IsNullOrEmpty(existingTransactionId) && existingStatus == "Success")
                            {
                                return BadRequest(new { error = "Payment is already done for this order." });
                            }
                        }
                    }
                }

                // Fetch Order Details (Amount and IShopId)
                using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 IShopId, OrderAmount FROM Checkout WHERE OrderId = @OrderId ORDER BY CheckoutId DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@OrderId", request.OrderId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            iShopId = reader.GetInt32(0);
                            orderAmount = reader.GetDouble(1);
                        }
                        else
                        {
                            return BadRequest(new { error = "Order not found" });
                        }
                    }
                }
                                
                // Process Payment using Braintree
                var transactionRequest = new TransactionRequest
                {
                    Amount = (decimal)orderAmount,  // Use Amount from Database
                    PaymentMethodNonce = request.Nonce,
                    Options = new TransactionOptionsRequest
                    {
                        SubmitForSettlement = true
                    }
                };

                result = await _braintreeGateway.Transaction.SaleAsync(transactionRequest);

                paymentStatus = result.IsSuccess() ? "Success" : (result.Transaction?.Status == TransactionStatus.SUBMITTED_FOR_SETTLEMENT ? "Pending" : "Failed");
                transactionId = result.IsSuccess() ? result.Target.Id : result.Transaction?.Id;

                // Save Payment and Update Order
                using (SqlCommand cmd = new SqlCommand("SavePaymentAndUpdateOrder", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@TransactionId", transactionId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@OrderId", request.OrderId);
                    cmd.Parameters.AddWithValue("@PaymentMode", request.PaymentMode);
                    cmd.Parameters.AddWithValue("@Amount", orderAmount);
                    cmd.Parameters.AddWithValue("@Status", paymentStatus);
                    cmd.Parameters.AddWithValue("@PaymentDate", paymentDate);

                    cmd.ExecuteNonQuery();
                }
            }

            if (paymentStatus == "Success")
            {
                // Redirect to ViewOrder page with OrderId as a query parameter
                return Ok(new { success = true, transactionId = transactionId, redirectUrl = Url.Action("GenerateAndSendInvoice", "Home", new { orderId = request.OrderId }) });
            }
            else if (paymentStatus == "Pending")
            {
                return Ok(new { success = true, transactionId = transactionId, redirectUrl = Url.Action("GenerateAndSendInvoice", "Home", new { orderId = request.OrderId }), message = "Payment is pending. Please wait for confirmation.", paymentDate = paymentDate });
            }
            else
            {
                return BadRequest(new { success = false, error = result?.Message ?? "Payment failed", paymentDate = paymentDate });
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