using System.Data;
using System.Diagnostics;
using ECommerce.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using Newtonsoft.Json;

namespace ECommerce.Controllers
{
    public class HomeController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IConfiguration configuration, ILogger<HomeController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
        }
        public async Task<IActionResult> Index()
        {
            List<Products> products = new List<Products>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand("GetProductsAndImages", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        Dictionary<int, Products> productDict = new Dictionary<int, Products>();

                        while (await reader.ReadAsync())
                        {
                            int productId = reader.GetInt32(reader.GetOrdinal("ProductId"));

                            if (!productDict.ContainsKey(productId))
                            {
                                productDict[productId] = new Products
                                {
                                    ProductId = productId,
                                    Name = reader.GetString(reader.GetOrdinal("Name")),
                                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                                    ProductImages = new List<ProductsImage>()
                                };
                            }

                            // Add only the first active image
                            if (!reader.IsDBNull(reader.GetOrdinal("ProductsImageId")))
                            {
                                var image = new ProductsImage
                                {
                                    ProductsImageId = reader.GetInt32(reader.GetOrdinal("ProductsImageId")),
                                    ProductId = productId,
                                    Type = reader.GetString(reader.GetOrdinal("Type")),
                                    Color = reader.GetString(reader.GetOrdinal("Color")),
                                    LargeImage = reader.GetString(reader.GetOrdinal("LargeImage")),
                                    Description = reader.GetString(reader.GetOrdinal("Description")),
                                    Quantity = reader.GetDouble(reader.GetOrdinal("Quantity")),
                                    MRP = reader.GetDouble(reader.GetOrdinal("MRP")),
                                    Discount = reader.GetInt32(reader.GetOrdinal("Discount")),
                                    Price = reader.GetDouble(reader.GetOrdinal("Price")),
                                    ArrivingDays = reader.GetInt32(reader.GetOrdinal("ArrivingDays")),
                                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                                };

                                productDict[productId].ProductImages.Add(image);
                            }
                        }

                        products = productDict.Values.ToList();
                    }
                }
            }

            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> ProductDetails(int id)
        {
            Products product = null;
            List<ProductsImage> productImages = new List<ProductsImage>();
            List<Products> allProducts = new List<Products>();
            var productId = 0;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                //Fetch Speific Product record with all details
                using (SqlCommand cmd = new SqlCommand("GetProductsAndImages", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@ProductsImageId", id);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!reader.HasRows)
                        {
                            Console.WriteLine("No data found for given ProductsImageId.");
                            return NotFound();
                        }

                        while (await reader.ReadAsync()) // Ensure we read all images
                        {
                            productId = reader.GetInt32(reader.GetOrdinal("ProductId"));
                            product = new Products
                            {
                                ProductId = productId,
                                Name = reader.GetString(reader.GetOrdinal("Name")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                                ProductImages = new List<ProductsImage>()
                            };
                        }
                    }
                }

                //Fetch All Products record with details by ProductId
                using (SqlCommand cmd = new SqlCommand("GetProductsAndImages", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@ProductId", productId);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!reader.HasRows)
                        {
                            Console.WriteLine("No data found for given ProductId.");
                            return NotFound();
                        }

                        while (await reader.ReadAsync()) // Ensure we read all images
                        {
                            var image = new ProductsImage
                            {
                                ProductsImageId = reader.GetInt32(reader.GetOrdinal("ProductsImageId")),
                                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                                Type = reader.GetString(reader.GetOrdinal("Type")),
                                Color = reader.GetString(reader.GetOrdinal("Color")),
                                LargeImage = reader.GetString(reader.GetOrdinal("Image")),
                                Description = reader.GetString(reader.GetOrdinal("Description")),
                                Quantity = reader.GetDouble(reader.GetOrdinal("Quantity")),
                                MRP = reader.GetDouble(reader.GetOrdinal("MRP")),
                                Discount = reader.GetInt32(reader.GetOrdinal("Discount")),
                                Price = reader.GetDouble(reader.GetOrdinal("Price")),
                                ArrivingDays = reader.GetInt32(reader.GetOrdinal("ArrivingDays")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                            };

                            productImages.Add(image);
                        }
                    }
                }

                // Fetch ALL Active Products along with their images
                using (SqlCommand cmd = new SqlCommand("GetRelatedProducts", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@ProductId", productId); // ProductId passed from method

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int prodId = reader.GetInt32(reader.GetOrdinal("ProductId"));
                            string prodName = reader.GetString(reader.GetOrdinal("Name"));
                            bool isActive = reader.GetBoolean(reader.GetOrdinal("IsActive"));

                            //  Correct condition: Skip adding the product if it's the selected productId
                            if (prodId == productId)
                            {
                                continue;
                            }

                            // Check if product already exists in list
                            var existingProduct = allProducts.FirstOrDefault(p => p.ProductId == prodId);
                            if (existingProduct == null)
                            {
                                existingProduct = new Products
                                {
                                    ProductId = prodId,
                                    Name = prodName,
                                    IsActive = isActive,
                                    ProductImages = new List<ProductsImage>() // Initialize image list
                                };
                                allProducts.Add(existingProduct);
                            }

                            // Ensure the product has images before adding them
                            if (reader["ProductsImageId"] != DBNull.Value)
                            {
                                var image = new ProductsImage
                                {
                                    ProductsImageId = reader.GetInt32(reader.GetOrdinal("ProductsImageId")),
                                    LargeImage = reader.GetString(reader.GetOrdinal("Image")),
                                    Price = reader.GetDouble(reader.GetOrdinal("Price"))
                                };

                                existingProduct.ProductImages.Add(image);
                            }
                        }
                    }
                }

            }

            if (product == null)
            {
                return NotFound();
            }
            product.ProductImages = productImages;
            ViewBag.ProductImageId = id > 0 ? id : 0;
            ViewBag.AllProducts = allProducts;

            return View(product);
        }

        [HttpGet]
        public async Task<IActionResult> Cart()
        {
            string? userId = Request.Cookies["IShopId"];

            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine(" No user ID found in cookies.");
                return Json(new { success = false, message = "User not logged in." });
            }

            List<ShoppingCart> cartItems = new List<ShoppingCart>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM ShoppingCart WHERE IShopId = @IShopId", conn))
                {
                    cmd.Parameters.AddWithValue("@IShopId", userId);
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            cartItems.Add(new ShoppingCart
                            {
                                IShopId = reader.GetInt32(reader.GetOrdinal("IShopId")),
                                ArrivingDays = reader.GetInt32(reader.GetOrdinal("ArrivingDays")),
                                Color = reader["Color"].ToString(),
                                Description = reader["Description"].ToString(),
                                Image = reader["Image"].ToString(),
                                Name = reader["Name"].ToString(),
                                Price = reader.GetDouble(reader.GetOrdinal("Price")),
                                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                                ProductsImageId = reader.GetInt32(reader.GetOrdinal("ProductsImageId")),
                                Quantity = reader.GetDouble(reader.GetOrdinal("Quantity")),
                                Total = reader.GetDouble(reader.GetOrdinal("Total")),
                                Type = reader["Type"].ToString()
                            });
                        }
                    }
                }
            }

            return View(cartItems);  //  Render View with Data
        }


        [HttpPost]
        public IActionResult SaveCart([FromBody] List<ShoppingCart> cartItems)
        {
            try
            {
                string? userId = Request.Cookies["IShopId"];

                if (string.IsNullOrEmpty(userId))
                {
                    Console.WriteLine(" No user ID found in cookies.");
                    return Json(new { success = false, message = "User not logged in." });
                }

                if (cartItems == null || cartItems.Count == 0)
                {
                    Console.WriteLine("Cart is empty.");
                    
                    return Json(new { success = false, message = "Cart is empty." });
                }

                Console.WriteLine($"Saving {cartItems.Count} items for user {userId}");

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    foreach (var item in cartItems)
                    {
                        using (SqlCommand cmd = new SqlCommand("SaveOrUpdateCart", conn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;

                            cmd.Parameters.AddWithValue("@IShopId", userId);
                            cmd.Parameters.AddWithValue("@ArrivingDays", item.ArrivingDays);
                            cmd.Parameters.AddWithValue("@Color", item.Color);
                            cmd.Parameters.AddWithValue("@Description", item.Description);
                            cmd.Parameters.AddWithValue("@Image", item.Image);
                            cmd.Parameters.AddWithValue("@Name", item.Name);
                            cmd.Parameters.AddWithValue("@Price", item.Price);
                            cmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                            cmd.Parameters.AddWithValue("@ProductsImageId", item.ProductsImageId);
                            cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                            cmd.Parameters.AddWithValue("@Total", item.Total);
                            cmd.Parameters.AddWithValue("@Type", item.Type);

                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                return Json(new { success = true, message = "Cart saved successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Error saving cart: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while saving the cart.", error = ex.Message });
            }
        }


        [HttpPost]
        public IActionResult DeleteCartItem([FromBody] ShoppingCart request)
        {
            string? userId = Request.Cookies["IShopId"];

            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine("No user ID found in cookies.");
                return Json(new { success = false, message = "User not logged in." });
            }

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                if (request == null || request.ProductId == 0) // Delete all items if ProductId is not provided
                {
                    using (SqlCommand cmdDeleteAll = new SqlCommand("DELETE FROM ShoppingCart WHERE IShopId = @IShopId", conn))
                    {
                        cmdDeleteAll.Parameters.AddWithValue("@IShopId", userId);
                        int rowsDeleted = cmdDeleteAll.ExecuteNonQuery();

                        if (rowsDeleted > 0)
                            return Json(new { success = true, message = "All items deleted successfully." });
                        else
                            return Json(new { success = false, message = "No items found to delete." });
                    }
                }

                // Delete specific item
                using (SqlCommand cmdDelete = new SqlCommand("DeleteShoppingCart", conn))
                {
                    cmdDelete.CommandType = CommandType.StoredProcedure;
                    cmdDelete.Parameters.AddWithValue("@IShopId", userId);
                    cmdDelete.Parameters.AddWithValue("@ProductId", request.ProductId);
                    cmdDelete.Parameters.AddWithValue("@ProductsImageId", request.ProductsImageId);

                    int rowsAffected = cmdDelete.ExecuteNonQuery();

                    if (rowsAffected > 0)
                        return Json(new { success = true, message = "Item deleted successfully." });
                    else
                        return Json(new { success = false, message = "Failed to delete item." });
                }
            }
        }

        // Define the model for request
        public class CartItemDeleteRequest
        {
            public int? UserId { get; set; }
            public int ProductId { get; set; }
            public int ProductsImageId { get; set; }
        }


        //[HttpPost]
        //public IActionResult SaveCart()
        //{
        //    // Get the user ID from session
        //    int? userId = HttpContext.Session.GetInt32("IShopId");

        //    if (userId == null)
        //    {
        //        ViewBag.Message = "User not logged in.";
        //        return View("Cart");
        //    }

        //    // Get cart items from localStorage (simulated here)
        //    string cartItemsJson = HttpContext.Request.Form["cartItems"];
        //    if (string.IsNullOrEmpty(cartItemsJson))
        //    {
        //        ViewBag.Message = "Cart is empty.";
        //        return View("Cart");
        //    }

        //    List<ShoppingCart> cartItems = JsonConvert.DeserializeObject<List<ShoppingCart>>(cartItemsJson);

        //    using (SqlConnection conn = new SqlConnection(_connectionString))
        //    {
        //        conn.Open();

        //        foreach (var item in cartItems)
        //        {
        //            using (SqlCommand cmd = new SqlCommand("SaveOrUpdateCart", conn))
        //            {
        //                cmd.CommandType = CommandType.StoredProcedure;

        //                cmd.Parameters.AddWithValue("@IShopId", userId);
        //                cmd.Parameters.AddWithValue("@ArrivingDays", item.ArrivingDays);
        //                cmd.Parameters.AddWithValue("@Color", item.Color);
        //                cmd.Parameters.AddWithValue("@Description", item.Description);
        //                cmd.Parameters.AddWithValue("@Image", item.Image);
        //                cmd.Parameters.AddWithValue("@Name", item.Name);
        //                cmd.Parameters.AddWithValue("@Price", item.Price);
        //                cmd.Parameters.AddWithValue("@ProductId", item.ProductId);
        //                cmd.Parameters.AddWithValue("@ProductsImageId", item.ProductsImageId);
        //                cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
        //                cmd.Parameters.AddWithValue("@Total", item.Total);
        //                cmd.Parameters.AddWithValue("@Type", item.Type);

        //                cmd.ExecuteNonQuery();
        //            }
        //        }
        //    }

        //    ViewBag.Message = "Cart saved successfully!";
        //    return View("Cart");
        //}


        [HttpGet]
        public IActionResult Checkout()
        {
            var IShopId = Request.Cookies["IShopId"]; // Fetch IShopId from cookies

            if (string.IsNullOrEmpty(IShopId))
            {
                return RedirectToAction("Login", "Account"); // Redirect to Login if null
            }
            return View();
        }


        [HttpGet]
        public async Task<IActionResult> GetAddresses()
        {
            List<DelivaryAddresses> addresses = new List<DelivaryAddresses>();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    //  Get `IShopId` from cookie if not provided in query

                    string shopIdString = Request.Cookies["IShopId"];
                    if (string.IsNullOrEmpty(shopIdString))
                    {
                        return BadRequest(new { success = false, message = "Invalid or missing IShopId in cookie." });
                    }

                    //  Fetch Addresses using IShopId
                    using (SqlCommand cmd = new SqlCommand("GetAddressesByShopId", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IShopId", shopIdString);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                addresses.Add(new DelivaryAddresses
                                {
                                    AddressId = reader.GetInt32(0),
                                    FullName = reader.GetString(1),
                                    Mobile = reader.GetInt64(2),
                                    Address = reader.GetString(3),
                                    City = reader.GetString(4),
                                    State = reader.GetString(5),
                                    ZipCode = reader.GetInt32(6),
                                    Country = reader.GetString(7),
                                    IsActive = reader.GetBoolean(8)
                                });
                            }
                        }
                    }
                }

                if (addresses.Count == 0)
                {
                    return NotFound(new { success = false, message = "No addresses found." });
                }

                return Ok(addresses);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error fetching addresses", error = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult SaveAddress([FromBody] DelivaryAddresses model)
        {
            if (model == null)
            {
                return BadRequest("Invalid address data.");
            }

            int shopId = 0; // Variable to store fetched IShopId

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    //  Fetch IShopId using Mobile number
                    using (SqlCommand fetchShopIdCmd = new SqlCommand("SELECT IShopId FROM Register WHERE Mobile = @Mobile", conn))
                    {
                        fetchShopIdCmd.Parameters.AddWithValue("@Mobile", model.Mobile);
                        object result = fetchShopIdCmd.ExecuteScalar();
                        if (result != null)
                        {
                            shopId = Convert.ToInt32(result);
                        }
                        else
                        {
                            return BadRequest("IShopId not found for the given mobile number.");
                        }
                    }

                    using (SqlCommand cmd = new SqlCommand("SaveDeliveryAddress", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // Output parameter for AddressId
                        SqlParameter addressIdParam = new SqlParameter("@AddressId", SqlDbType.Int)
                        {
                            Direction = ParameterDirection.InputOutput,
                            Value = model.AddressId > 0 ? model.AddressId : (object)DBNull.Value
                        };

                        cmd.Parameters.Add(addressIdParam);
                        cmd.Parameters.AddWithValue("@IShopId", shopId); //  Pass the fetched IShopId
                        cmd.Parameters.AddWithValue("@FullName", model.FullName);
                        cmd.Parameters.AddWithValue("@Mobile", model.Mobile);
                        cmd.Parameters.AddWithValue("@Country", model.Country);
                        cmd.Parameters.AddWithValue("@State", model.State);
                        cmd.Parameters.AddWithValue("@City", model.City);
                        cmd.Parameters.AddWithValue("@ZipCode", model.ZipCode);
                        cmd.Parameters.AddWithValue("@Address", model.Address);
                        cmd.Parameters.AddWithValue("@IsActive", model.IsActive);

                        cmd.ExecuteNonQuery();

                        // Get AddressId if it was an insert
                        model.AddressId = (int)addressIdParam.Value;
                    }
                }

                return Ok(new { success = true, message = "Address saved successfully!", addressId = model.AddressId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error saving address", error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveOrder([FromBody] List<Orders> orders)
        {
            // Fetch IShopId from cookies
            string shopIdString = Request.Cookies["IShopId"];
            if (string.IsNullOrEmpty(shopIdString) || !int.TryParse(shopIdString, out int IShopId))
            {
                return BadRequest(new { success = false, message = "Invalid or missing IShopId in cookie." });
            }

            if (orders == null || orders.Count == 0)
                return BadRequest(new { message = "No order items provided." });

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                foreach (var order in orders)
                {
                    using (SqlCommand cmd = new SqlCommand("SaveOrder", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@OrderId", order.OrderId);
                        cmd.Parameters.AddWithValue("@IShopId", IShopId);
                        cmd.Parameters.AddWithValue("@ProductId", order.ProductId);
                        cmd.Parameters.AddWithValue("@ProductsImageId", order.ProductsImageId);
                        cmd.Parameters.AddWithValue("@OrderQty", order.OrderQty);
                        cmd.Parameters.AddWithValue("@TotalAmount", order.TotalAmount);
                        cmd.Parameters.AddWithValue("@IsActive", true);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            return Ok(new { message = "Order saved successfully!" });
        }

        [HttpPost]
        public async Task<IActionResult> SaveCheckout([FromBody] Checkout checkout)
        {
            Console.WriteLine(" Received Checkout Data: " + JsonConvert.SerializeObject(checkout));

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (var transaction = conn.BeginTransaction()) // Start Transaction
                {
                    try
                    {                      

                        // Fetch IShopId from cookies
                        string shopIdString = Request.Cookies["IShopId"];
                        if (string.IsNullOrEmpty(shopIdString) || !int.TryParse(shopIdString, out int IShopId))
                        {
                            throw new Exception("Invalid or missing IShopId in cookie.");
                            //return BadRequest(new { success = false, message = "Invalid or missing IShopId in cookie." });
                        }

                        string orderId = "SELECT  OrderId FROM Orders WHERE IShopId = @IShopId ORDER BY Id DESC";

                        if (checkout == null)
                        {
                            throw new Exception("Invalid checkout data.");
                            //return BadRequest(new { message = "Invalid checkout data." });
                        }

                        // Set OrderDate to current timestamp
                        DateTime OrderDate = DateTime.Now; //  Correctly defined

                        // Attempt to Save Checkout
                        using (SqlCommand cmd = new SqlCommand("SaveCheckout", conn, transaction))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;

                            cmd.Parameters.AddWithValue("@OrderId", checkout.OrderId ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@IShopId", IShopId);
                            cmd.Parameters.AddWithValue("@AddressId", checkout.AddressId);
                            cmd.Parameters.AddWithValue("@PaymentMode", checkout.PaymentMode);
                            cmd.Parameters.AddWithValue("@OrderDate", OrderDate);
                            cmd.Parameters.AddWithValue("@TotalAmount", checkout.TotalAmount);
                            cmd.Parameters.AddWithValue("@Tax", checkout.Tax);
                            cmd.Parameters.AddWithValue("@DelivaryCharge", checkout.DelivaryCharge);
                            cmd.Parameters.AddWithValue("@FinalAmount", checkout.FinalAmount);
                            cmd.Parameters.AddWithValue("@PromoAmount", checkout.PromoAmount);
                            cmd.Parameters.AddWithValue("@OrderAmount", checkout.OrderAmount);
                            cmd.Parameters.AddWithValue("@IsActive", true);

                            await cmd.ExecuteNonQueryAsync();
                        }

                        using (SqlCommand cmdStock = new SqlCommand("UpdateStockAfterCheckout", conn, transaction))
                        {
                            cmdStock.CommandType = CommandType.StoredProcedure;
                            cmdStock.Parameters.AddWithValue("@OrderId", checkout.OrderId);
                            await cmdStock.ExecuteNonQueryAsync();
                        }


                        // Commit transaction if everything is successful
                        transaction.Commit();
                        return Ok(new { message = "Checkout saved successfully!" });
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            //  Fetch the latest OrderId from Orders table
                            string fetchOrderQuery = "SELECT TOP 1 OrderId FROM Orders WHERE IShopId = @IShopId ORDER BY Id DESC";

                            string orderId = null;

                            string shopIdString = Request.Cookies["IShopId"];
                            if (string.IsNullOrEmpty(shopIdString) || !int.TryParse(shopIdString, out int IShopId))
                            {
                                throw new Exception("Invalid or missing IShopId in cookie.");
                                //return BadRequest(new { success = false, message = "Invalid or missing IShopId in cookie." });
                            }

                            using (SqlCommand fetchCmd = new SqlCommand(fetchOrderQuery, conn, transaction))
                            {
                                fetchCmd.Parameters.AddWithValue("@IShopId", IShopId);

                                object result = await fetchCmd.ExecuteScalarAsync(); // Execute the query
                                if (result != null)
                                {
                                    orderId = result.ToString(); // Store the actual OrderId
                                }
                            }

                            //  If OrderId is found, delete the related records
                            if (!string.IsNullOrEmpty(orderId))
                            {
                                using (SqlCommand deleteCmd = new SqlCommand("DELETE FROM Orders WHERE OrderId = @OrderId", conn, transaction))
                                {
                                    deleteCmd.Parameters.AddWithValue("@OrderId", orderId);
                                    await deleteCmd.ExecuteNonQueryAsync();
                                }

                                //  Commit transaction to permanently delete data
                                transaction.Commit();
                                return BadRequest(new { success = true, message = "Orders deleted successfully!" });
                            }
                            else
                            {
                                // Rollback if no OrderId is found
                                transaction.Rollback();
                                return BadRequest(new { success = false, message = "No matching OrderId found. Nothing deleted." });
                            }
                        }
                        catch (Exception deleteEx)
                        {
                            return BadRequest(new { success = false, message = "Checkout failed. Order deletion also failed.", error = deleteEx.Message });
                        }
                       
                    }
                }
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

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand("GetOrdersWithDetails", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IShopId", iShopId);
                    cmd.Parameters.AddWithValue("@OrderId", DBNull.Value); //  Pass NULL for OrderId

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            orders.Add(new OrderDetails
                            {
                                OrderId = reader["OrderId"].ToString(),
                                OrderDate = Convert.ToDateTime(reader["OrderDate"]),
                                PaymentMode = reader["PaymentMode"].ToString(),
                                OrderAmount = Convert.ToDouble(reader["OrderAmount"]),
                                IsActive = Convert.ToBoolean(reader["IsActive"])
                            });
                        }
                    }
                }
            }

            return View(orders); //  Pass the list of orders to the view
        }


        //  Method 2: Fetch order details for a specific OrderId
        public async Task<IActionResult> ViewOrder(string id)
        {
            //  Read IShopId from cookies
            string shopIdString = Request.Cookies["IShopId"];
            if (string.IsNullOrEmpty(shopIdString) || !int.TryParse(shopIdString, out int iShopId))
            {
                return BadRequest("Invalid or missing IShopId in cookies.");
            }

            var orders = new List<OrderDetails>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand("GetOrdersWithDetails", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IShopId", iShopId);
                    cmd.Parameters.AddWithValue("@OrderId", id); //  Use `id` from URL

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            orders.Add(new OrderDetails
                            {
                                OrderId = reader["OrderId"].ToString(),
                                IShopId = Convert.ToInt32(reader["IShopId"]),
                                OrderDate = Convert.ToDateTime(reader["OrderDate"]),
                                PaymentMode = reader["PaymentMode"].ToString(),
                                TotalAmount = Convert.ToDouble(reader["TotalAmount"]),
                                IsActive = Convert.ToBoolean(reader["IsActive"]),
                                Tax = Convert.ToDouble(reader["Tax"]),
                                DelivaryCharge = Convert.ToDouble(reader["DelivaryCharge"]),
                                FinalAmount = Convert.ToDouble(reader["FinalAmount"]),
                                PromoAmount = Convert.ToDouble(reader["PromoAmount"]),
                                OrderAmount = Convert.ToDouble(reader["OrderAmount"]),
                                ProductName = reader["ProductName"].ToString(),
                                ProductsImageId = Convert.ToInt32(reader["ProductsImageId"]),
                                Image = reader["Image"].ToString(),
                                Type = reader["Type"].ToString(),
                                Color = reader["Color"].ToString(),
                                FullName = reader["FullName"].ToString(),
                                Address = reader["Address"].ToString(),
                                City = reader["City"].ToString(),
                                State = reader["State"].ToString(),                                
                                Country = reader["Country"].ToString(),
                                ZipCode = Convert.ToInt32(reader["IShopId"]),
                                Mobile = reader["Mobile"] != DBNull.Value ? Convert.ToInt64(reader["Mobile"]) : 0

                            });
                        }
                    }
                }
            }

            if (orders.Count == 0)
            {
                return NotFound("No order found with the given ID.");
            }

            return View(orders); //  Return list to the view
        }

        [HttpPost]
        public async Task<IActionResult> CancelOrder(string id)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (SqlTransaction transaction = conn.BeginTransaction()) // Start a transaction
                {
                    try
                    {
                        // Update Checkout table
                        using (SqlCommand cmd = new SqlCommand("UPDATE Checkout SET IsActive = 0 WHERE OrderId = @OrderId", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@OrderId", id);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Update Orders table
                        using (SqlCommand cmd = new SqlCommand("UPDATE Orders SET IsActive = 0 WHERE OrderId = @OrderId", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@OrderId", id);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit(); // Commit the transaction if both updates succeed
                    }
                    catch (Exception)
                    {
                        transaction.Rollback(); // Rollback in case of any error
                        return BadRequest("Failed to cancel order.");
                    }
                }
            }

            return RedirectToAction("Index"); // Redirect back to the order list after cancellation
        }


        [HttpGet]
        public async Task<IActionResult> CheckStockAvailability(int productImageId, int requestedQty)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand("CheckStockAvailability", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@ProductsImageId", productImageId);
                    cmd.Parameters.AddWithValue("@RequestedQty", requestedQty);

                    SqlParameter outputParam = new SqlParameter("@StockAvailable", SqlDbType.Bit)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(outputParam);

                    await cmd.ExecuteNonQueryAsync();
                    bool stockAvailable = Convert.ToBoolean(outputParam.Value);

                    if (!stockAvailable)
                    {
                        return BadRequest(new { success = false, message = "Out of Stock!" });
                    }

                    return Ok(new { success = true, message = "Stock Available!" });
                }
            }
        }



        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
