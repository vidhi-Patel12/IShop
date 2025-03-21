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

namespace ECommerce.Controllers
{
    public class HomeController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;


        public HomeController(IConfiguration configuration, ILogger<HomeController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
            _configuration = configuration;
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
                                    Slug = reader.GetString(reader.GetOrdinal("Slug")),
                                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                                    ProductImages = new List<ProductsImage>()
                                };
                            }

                            // Add only the first active image
                            if (!reader.IsDBNull(reader.GetOrdinal("ProductsImageId")))
                            {
                                var image = new ProductsImage();
                                image.ProductsImageId = reader.GetInt32(reader.GetOrdinal("ProductsImageId"));
                                image.ProductId = productId;
                                image.Type = reader.GetString(reader.GetOrdinal("Type"));
                                image.Color = reader.GetString(reader.GetOrdinal("Color"));
                                image.LargeImage = reader.GetString(reader.GetOrdinal("LargeImage"));
                                image.MediumImage = reader.GetString(reader.GetOrdinal("MediumImage"));
                                image.Description = reader.GetString(reader.GetOrdinal("Description"));
                                image.Quantity = reader.GetDouble(reader.GetOrdinal("Quantity"));
                                image.MRP = reader.GetDouble(reader.GetOrdinal("MRP"));
                                image.Discount = reader.GetInt32(reader.GetOrdinal("Discount"));
                                image.Price = reader.GetDouble(reader.GetOrdinal("Price"));
                                image.ArrivingDays = reader.GetInt32(reader.GetOrdinal("ArrivingDays"));
                                image.TypeSlug = reader.GetString(reader.GetOrdinal("TypeSlug"));
                                image.ColorSlug = reader.GetString(reader.GetOrdinal("ColorSlug"));
                                image.IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"));

                                productDict[productId].ProductImages.Add(image);
                            }
                        }

                        products = productDict.Values.ToList();
                    }
                }
            }

            var result = products.Count > 4 ? products.Take(4) : products;
            return View(result.ToList());
        }

        public IActionResult QuickViewByProductImageId(int productImageId)
        {
            List<Products> products = new List<Products>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand("GetProductsAndImages", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        Dictionary<int, Products> productDict = new Dictionary<int, Products>();

                        while (reader.Read())
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
                                var image = new ProductsImage();
                                var imageId = reader.GetInt32(reader.GetOrdinal("ProductsImageId"));
                                if(imageId == productImageId)
                                {
                                    image.ProductsImageId = reader.GetInt32(reader.GetOrdinal("ProductsImageId"));
                                    image.ProductId = productId;
                                    image.Type = reader.GetString(reader.GetOrdinal("Type"));
                                    image.Color = reader.GetString(reader.GetOrdinal("Color"));
                                    image.LargeImage = reader.GetString(reader.GetOrdinal("LargeImage"));
                                    image.Description = reader.GetString(reader.GetOrdinal("Description"));
                                    image.Quantity = reader.GetDouble(reader.GetOrdinal("Quantity"));
                                    image.MRP = reader.GetDouble(reader.GetOrdinal("MRP"));
                                    image.Discount = reader.GetInt32(reader.GetOrdinal("Discount"));
                                    image.Price = reader.GetDouble(reader.GetOrdinal("Price"));
                                    image.ArrivingDays = reader.GetInt32(reader.GetOrdinal("ArrivingDays"));
                                    image.IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"));

                                    productDict[productId].ProductImages.Add(image);
                                }
                            }
                        }

                        products = productDict.Values.ToList();
                    }
                }
            }

            var result = products.Where(X => X.ProductImages.Count != 0);
            return PartialView("_QuickView", result.ToList());
        }

        
        [HttpGet]
        [Route("home/productdetails/{slug}/{typeslug}/{colorslug}")]
        public async Task<IActionResult> ProductDetails(string slug,string typeslug,string colorslug)
        {
            if (string.IsNullOrEmpty(slug))
            {
                return NotFound();
            }

            int productsImageId = 0;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // **Single JOIN query to get ProductId and ProductsImageId using Slug**
                string query = @"
                SELECT pi.ProductsImageId 
                FROM Products p
                INNER JOIN ProductsImage pi ON p.ProductId = pi.ProductId
                WHERE p.Slug = @Slug
                AND pi.TypeSlug = @TypeSlug
                AND pi.ColorSlug = @ColorSlug;";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Slug", slug);
                    cmd.Parameters.AddWithValue("@TypeSlug", typeslug);
                    cmd.Parameters.AddWithValue("@ColorSlug", colorslug);

                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null)
                    {
                        productsImageId = Convert.ToInt32(result);
                    }
                }
            }

            if (productsImageId == 0)
            {
                return NotFound("Product Image not found.");
            }

            // Fetch Product ID from DB

            ViewBag.ProductImageId = productsImageId; // 

            Products product = null;
            List<ProductsImage> productImages = new List<ProductsImage>();
            List<Products> allProducts = new List<Products>();
            int productId = 0;
            int pImageId = 0;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Fetch Specific Product record using Slug
                using (SqlCommand cmd = new SqlCommand("GetProductsAndImages", conn)) // Call Stored Procedure
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    //cmd.Parameters.AddWithValue("@ProductsImageId", id);
                    cmd.Parameters.AddWithValue("@Slug", slug);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!reader.HasRows)
                        {
                            Console.WriteLine("No data found for given slug.");
                            return NotFound();
                        }

                        while (await reader.ReadAsync())
                        {
                            productId = reader.GetInt32(reader.GetOrdinal("ProductId"));
                            pImageId = reader.GetInt32(reader.GetOrdinal("ProductsImageId"));
                            slug = reader.GetString(reader.GetOrdinal("Slug"));
                            product = new Products
                            {
                                ProductId = productId,
                                Name = reader.GetString(reader.GetOrdinal("Name")),
                                Slug = slug, // Ensure Slug is retrieved
                                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                                ProductImages = new List<ProductsImage>()
                            };
                        }
                    }
                }

                if (product == null)
                {
                    return NotFound();
                }

                // Fetch Product Images
                using (SqlCommand cmd = new SqlCommand("GetProductsAndImages", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@ProductId", productId);
                    cmd.Parameters.AddWithValue("@TypeSlug", typeslug);
                    cmd.Parameters.AddWithValue("@ColorSlug", colorslug);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var image = new ProductsImage
                            {
                                ProductsImageId = reader.GetInt32(reader.GetOrdinal("ProductsImageId")),
                                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                                Type = reader.GetString(reader.GetOrdinal("Type")),
                                Color = reader.GetString(reader.GetOrdinal("Color")),
                                LargeImage = reader.GetString(reader.GetOrdinal("LargeImage")),
                                MediumImage = reader.GetString(reader.GetOrdinal("MediumImage")),
                                SmallImage = reader.GetString(reader.GetOrdinal("SmallImage")),
                                Description = reader.GetString(reader.GetOrdinal("Description")),
                                Quantity = reader.GetDouble(reader.GetOrdinal("Quantity")),
                                MRP = reader.GetDouble(reader.GetOrdinal("MRP")),
                                Discount = reader.GetInt32(reader.GetOrdinal("Discount")),
                                Price = reader.GetDouble(reader.GetOrdinal("Price")),
                                ArrivingDays = reader.GetInt32(reader.GetOrdinal("ArrivingDays")),
                                TypeSlug = reader.GetString(reader.GetOrdinal("TypeSlug")),
                                ColorSlug = reader.GetString(reader.GetOrdinal("ColorSlug")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                            };

                            productImages.Add(image);
                        }
                    }
                }

                // Fetch Related Products
                using (SqlCommand cmd = new SqlCommand("GetRelatedProducts", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@ProductId", productId);
                    cmd.Parameters.AddWithValue("@Slug", slug);
                    cmd.Parameters.AddWithValue("@TypeSlug", typeslug);
                    cmd.Parameters.AddWithValue("@ColorSlug", colorslug);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int prodId = reader.GetInt32(reader.GetOrdinal("ProductId"));
                            string prodName = reader.GetString(reader.GetOrdinal("Name"));
                            string prodSlug = reader.GetString(reader.GetOrdinal("Slug")); // Fetch Slug
                            bool isActive = reader.GetBoolean(reader.GetOrdinal("IsActive"));

                            if (prodId == productId)
                            {
                                continue;
                            }

                            var existingProduct = allProducts.FirstOrDefault(p => p.ProductId == prodId);
                            if (existingProduct == null)
                            {
                                existingProduct = new Products
                                {
                                    ProductId = prodId,
                                    Name = prodName,
                                    Slug = prodSlug, // Store Slug
                                    IsActive = isActive,
                                    ProductImages = new List<ProductsImage>()
                                };
                                allProducts.Add(existingProduct);
                            }

                            if (reader["ProductsImageId"] != DBNull.Value)
                            {
                                var image = new ProductsImage
                                {
                                    ProductsImageId = reader.GetInt32(reader.GetOrdinal("ProductsImageId")),
                                    LargeImage = reader.GetString(reader.GetOrdinal("LargeImage")),
                                    Price = reader.GetDouble(reader.GetOrdinal("Price")),
                                    TypeSlug = reader.GetString(reader.GetOrdinal("TypeSlug")),
                                    ColorSlug = reader.GetString(reader.GetOrdinal("ColorSlug"))
                                };

                                existingProduct.ProductImages.Add(image);
                            }
                        }
                    }
                }
            }

            product.ProductImages = productImages ?? new List<ProductsImage>(); //  Ensure it's not null
            ViewBag.ProductImageId = productsImageId > 0 ? productsImageId : 0;
            ViewBag.AllProducts = allProducts;

            return View(product);
        }


        public async Task<IActionResult> Products()
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
                                    Slug = reader.GetString(reader.GetOrdinal("Slug")),
                                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                                    ProductImages = new List<ProductsImage>()
                                };
                            }

                            // Add only the first active image
                            if (!reader.IsDBNull(reader.GetOrdinal("ProductsImageId")))
                            {
                                var image = new ProductsImage();
                                image.ProductsImageId = reader.GetInt32(reader.GetOrdinal("ProductsImageId"));
                                image.ProductId = productId;
                                image.Type = reader.GetString(reader.GetOrdinal("Type"));
                                image.Color = reader.GetString(reader.GetOrdinal("Color"));
                                image.LargeImage = reader.GetString(reader.GetOrdinal("LargeImage"));
                                image.Description = reader.GetString(reader.GetOrdinal("Description"));
                                image.Quantity = reader.GetDouble(reader.GetOrdinal("Quantity"));
                                image.MRP = reader.GetDouble(reader.GetOrdinal("MRP"));
                                image.Discount = reader.GetInt32(reader.GetOrdinal("Discount"));
                                image.Price = reader.GetDouble(reader.GetOrdinal("Price"));
                                image.ArrivingDays = reader.GetInt32(reader.GetOrdinal("ArrivingDays"));
                                image.TypeSlug = reader.GetString(reader.GetOrdinal("TypeSlug"));
                                image.ColorSlug = reader.GetString(reader.GetOrdinal("ColorSlug"));                            
                                image.IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"));

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
        public async Task<IActionResult> GetCart()
        {
            List<ShoppingCart> cartItems = new List<ShoppingCart>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand("SELECT * FROM ShoppingCart WHERE IShopId = @IShopId", conn))
                {
                    string? userId = Request.Cookies["IShopId"];
                    
                    cmd.Parameters.AddWithValue("@IShopId", userId);
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())  
                        {
                            cartItems.Add(new ShoppingCart
                            {
                                IShopId = reader.GetInt32(reader.GetOrdinal("IShopId")),
                                ArrivingDays = reader.IsDBNull(reader.GetOrdinal("ArrivingDays")) ? 0 : reader.GetInt32(reader.GetOrdinal("ArrivingDays")), // ? Handle null values
                                Color = reader["Color"]?.ToString() ?? "", 
                                Description = reader["Description"]?.ToString() ?? "",
                                Image = reader["Image"]?.ToString() ?? "",
                                Name = reader["Name"]?.ToString() ?? "",
                                Price = reader.IsDBNull(reader.GetOrdinal("Price")) ? 0.0 : reader.GetDouble(reader.GetOrdinal("Price")),
                                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                                ProductsImageId = reader.GetInt32(reader.GetOrdinal("ProductsImageId")),
                                Quantity = reader.IsDBNull(reader.GetOrdinal("Quantity")) ? 0 : reader.GetDouble(reader.GetOrdinal("Quantity")),
                                Total = reader.IsDBNull(reader.GetOrdinal("Total")) ? 0.0 : reader.GetDouble(reader.GetOrdinal("Total")),
                                Type = reader["Type"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }

            return Json(new { success = true, cartItems });
        }

        [HttpGet]
        public async Task<IActionResult> Cart()
        {
            List<ShoppingCart> cartItems = new List<ShoppingCart>();

            //  Try to get IShopId from cookies (for logged-in users)
            string? userId = Request.Cookies["IShopId"];

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                //  If user is logged in, fetch cart from the database
                if (!string.IsNullOrEmpty(userId))
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT * FROM ShoppingCart WHERE IShopId = @IShopId", conn))
                    {
                        cmd.Parameters.AddWithValue("@IShopId", userId);
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
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
                else
                {
                    //  Load cart from localStorage for guest users
                    var guestCart = HttpContext.Session.GetString("GuestCart");
                    if (!string.IsNullOrEmpty(guestCart))
                    {
                        cartItems = JsonConvert.DeserializeObject<List<ShoppingCart>>(guestCart);
                    }
                }
            }

            return View(cartItems);  //  Render Cart View with Data
        }

        //[HttpPost]
        //public IActionResult SaveCart([FromBody] List<ShoppingCart> cartItems)
        //{
        //    try
        //    {
        //        string? userId = Request.Cookies["IShopId"];

        //        if (string.IsNullOrEmpty(userId))
        //        {
        //            Console.WriteLine(" No user ID found in cookies.");
        //            return Json(new { success = false, message = "User not logged in." });
        //        }

        //        if (cartItems == null || cartItems.Count == 0)
        //        {
        //            Console.WriteLine(" Cart is empty.");
        //            return Json(new { success = false, message = "Cart is empty." });
        //        }

        //        Console.WriteLine($" Saving {cartItems.Count} items for user {userId}");

        //        using (SqlConnection conn = new SqlConnection(_connectionString))
        //        {
        //            conn.Open();

        //            foreach (var item in cartItems)
        //            {
        //                using (SqlCommand checkCmd = new SqlCommand(@"
        //                    SELECT COUNT(*) FROM ShoppingCart 
        //                    WHERE IShopId = @IShopId AND ProductsImageId = @ProductsImageId", conn))
        //                   {
        //                    checkCmd.Parameters.AddWithValue("@IShopId", userId);
        //                    checkCmd.Parameters.AddWithValue("@ProductsImageId", item.ProductsImageId);

        //                    int count = (int)checkCmd.ExecuteScalar();

        //                    if (count > 0)
        //                    {
        //                        //  Use a different variable name (updateCmd)
        //                        using (SqlCommand updateCmd = new SqlCommand("SaveOrUpdateCart", conn))
        //                        {
        //                            updateCmd.CommandType = CommandType.StoredProcedure;
        //                            updateCmd.Parameters.AddWithValue("@IShopId", userId);
        //                            updateCmd.Parameters.AddWithValue("@ArrivingDays", item.ArrivingDays);
        //                            updateCmd.Parameters.AddWithValue("@Color", item.Color ?? (object)DBNull.Value);
        //                            updateCmd.Parameters.AddWithValue("@Description", item.Description ?? (object)DBNull.Value);
        //                            updateCmd.Parameters.AddWithValue("@Image", item.Image ?? (object)DBNull.Value);
        //                            updateCmd.Parameters.AddWithValue("@Name", item.Name ?? (object)DBNull.Value);
        //                            updateCmd.Parameters.AddWithValue("@Price", item.Price);
        //                            updateCmd.Parameters.AddWithValue("@ProductId", item.ProductId);
        //                            updateCmd.Parameters.AddWithValue("@ProductsImageId", item.ProductsImageId);
        //                            updateCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
        //                            updateCmd.Parameters.AddWithValue("@Total", item.Total);
        //                            updateCmd.Parameters.AddWithValue("@Type", item.Type ?? (object)DBNull.Value);


        //                            updateCmd.ExecuteNonQuery();
        //                        }
        //                    }
        //                    else
        //                    {
        //                        // Use a different variable name (insertCmd)
        //                        using (SqlCommand insertCmd = new SqlCommand("SaveOrUpdateCart", conn))
        //                        {
        //                            insertCmd.CommandType = CommandType.StoredProcedure;
        //                            insertCmd.Parameters.AddWithValue("@IShopId", userId);
        //                            insertCmd.Parameters.AddWithValue("@ArrivingDays", item.ArrivingDays);
        //                            insertCmd.Parameters.AddWithValue("@Color", item.Color);
        //                            insertCmd.Parameters.AddWithValue("@Description", item.Description);
        //                            insertCmd.Parameters.AddWithValue("@Image", item.Image);
        //                            insertCmd.Parameters.AddWithValue("@Name", item.Name);
        //                            insertCmd.Parameters.AddWithValue("@Price", item.Price);
        //                            insertCmd.Parameters.AddWithValue("@ProductId", item.ProductId);
        //                            insertCmd.Parameters.AddWithValue("@ProductsImageId", item.ProductsImageId);
        //                            insertCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
        //                            insertCmd.Parameters.AddWithValue("@Total", item.Total);
        //                            insertCmd.Parameters.AddWithValue("@Type", item.Type);
        //                            insertCmd.ExecuteNonQuery();
        //                        }
        //                    }
        //                }
        //            }
        //        }

        //        return Json(new { success = true, message = " Cart saved successfully!" });
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($" Error saving cart: {ex.Message}");
        //        return Json(new { success = false, message = "An error occurred while saving the cart.", error = ex.Message });
        //    }
        //}

        [HttpPost]
        public IActionResult SaveCart([FromBody] List<ShoppingCart> cartItems)
        {
            try
            {
                string? userId = Request.Cookies["IShopId"]; // Get user ID from cookies

                if (cartItems == null || cartItems.Count == 0)
                {
                    return Json(new { success = false, message = "Cart is empty." });
                }

                if (!string.IsNullOrEmpty(userId)) // User is logged in, save to DB
                {
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
                                cmd.Parameters.AddWithValue("@Color", item.Color ?? (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@Description", item.Description ?? (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@Image", item.Image ?? (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@Name", item.Name ?? (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@Price", item.Price);
                                cmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                                cmd.Parameters.AddWithValue("@ProductsImageId", item.ProductsImageId);
                                cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                                cmd.Parameters.AddWithValue("@Total", item.Total);
                                cmd.Parameters.AddWithValue("@Type", item.Type ?? (object)DBNull.Value);

                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                    return Json(new { success = true, message = "Cart saved to database." });
                }
                else // Guest user: store in localStorage & cookies
                {
                    Response.Cookies.Append("GuestCart", System.Text.Json.JsonSerializer.Serialize(cartItems),
                        new CookieOptions { Expires = DateTime.UtcNow.AddDays(7) });

                    return Json(new { success = true, message = "Cart saved in localStorage & cookies for guests." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred.", error = ex.Message });
            }
        }


        [HttpPost]
        public async Task<IActionResult> CheckCartItem([FromBody] ShoppingCart item)
        {
            try
            {
                string? userId = Request.Cookies["IShopId"];
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { exists = false, message = "User not logged in." });
                }

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("CheckCartItemExists", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IShopId", userId);
                        cmd.Parameters.AddWithValue("@ProductsImageId", item.ProductsImageId);

                        int count = (int)await cmd.ExecuteScalarAsync();
                        return Json(new { exists = count > 0 });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { exists = false, error = ex.Message });
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

                if (request == null || request.ProductId == 0 || request.ProductsImageId == 0) // Delete all items if ProductId is not provided
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

        [HttpGet]
        public async Task<IActionResult> Coupan()
        {
            List<Coupan> coupons = new List<Coupan>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand("SELECT * FROM Coupan where IsActive = 1", conn)) // Raw SQL query
                {
                    cmd.CommandType = CommandType.Text; //  Use CommandType.Text for raw SQL

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            coupons.Add(new Coupan
                            {
                                CoupanId = reader.GetInt32(reader.GetOrdinal("CoupanId")),
                                CoupanName = reader.GetString(reader.GetOrdinal("CoupanName")),
                                CoupanType = reader.GetString(reader.GetOrdinal("CoupanType")),
                                CoupanCode = reader.GetString(reader.GetOrdinal("CoupanCode")),
                                Discount = reader.GetDouble(reader.GetOrdinal("Discount")),
                                ExpiryDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("ExpiryDate"))),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                            });
                        }
                    }
                }
            }

            return Json(coupons);
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

        //Fetch order details for a specific OrderId
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
                                LargeImage = reader["LargeImage"].ToString(),
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
