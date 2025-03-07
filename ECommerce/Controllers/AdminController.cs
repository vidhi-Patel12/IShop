using ECommerce.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using static Azure.Core.HttpHeader;

namespace ECommerce.Controllers
{
    public class AdminController : Controller
    {
        private readonly string _connectionString;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminController(IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: List of Products
        public async Task<IActionResult> Index()
        {
            List<Products> productsList = new List<Products>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand("GetAllProducts", conn))
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

                            if (!reader.IsDBNull(reader.GetOrdinal("ProductsImageId")))
                            {
                                productDict[productId].ProductImages.Add(new ProductsImage
                                {
                                    ProductsImageId = reader.GetInt32(reader.GetOrdinal("ProductsImageId")),
                                    ProductId = productId,
                                    Type = reader.GetString(reader.GetOrdinal("Type")),
                                    Color = reader.GetString(reader.GetOrdinal("Color")),
                                    //Image = reader.GetString(reader.GetOrdinal("ImagePath")),
                                    Quantity = reader.GetDouble(reader.GetOrdinal("Quantity")),
                                    MRP = reader.GetDouble(reader.GetOrdinal("MRP")),
                                    Discount = reader.GetInt32(reader.GetOrdinal("Discount")),
                                    Price = reader.GetDouble(reader.GetOrdinal("Price")),
                                    ArrivingDays = reader.GetInt32(reader.GetOrdinal("ArrivingDays")),
                                    IsActive = reader.GetBoolean(reader.GetOrdinal("ImageIsActive"))
                                });
                            }
                        }

                        productsList = new List<Products>(productDict.Values);
                    }
                }
            }

            return View(productsList);
        }

        [HttpGet]
        public IActionResult Create()

        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] List<Products> productList, IFormFile largeImageFile, IFormFile mediumImageFile, IFormFile smallImageFile)
        {
            foreach (var model in productList)
            {
                int productId;

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // Check if product exists
                    using (SqlCommand checkCmd = new SqlCommand("SELECT ProductId FROM Products WHERE Name = @Name", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@Name", model.Name);
                        object result = await checkCmd.ExecuteScalarAsync();
                        productId = result != null ? Convert.ToInt32(result) : 0;
                    }

                    // If product does not exist, insert it
                    if (productId == 0)
                    {
                        using (SqlCommand cmd = new SqlCommand("AddProduct", conn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@Name", model.Name);
                            cmd.Parameters.AddWithValue("@IsActive", true);

                            SqlParameter outputIdParam = new SqlParameter("@NewProductId", SqlDbType.Int)
                            {
                                Direction = ParameterDirection.Output
                            };
                            cmd.Parameters.Add(outputIdParam);

                            await cmd.ExecuteNonQueryAsync();
                            productId = (int)outputIdParam.Value;
                        }
                    }

                    // Define base path for product images
                    string basePath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "Products", productId.ToString());

                    // Create directory if it does not exist
                    if (!Directory.Exists(basePath))
                    {
                        Directory.CreateDirectory(basePath);
                    }

                    // Save Large Image
                    if (!string.IsNullOrEmpty(model.LargeImage))
                    {
                        string largeImagePath = Path.Combine(basePath, "Large");
                        if (!Directory.Exists(largeImagePath))
                        {
                            Directory.CreateDirectory(largeImagePath);
                        }
                        //Directory.CreateDirectory(Path.GetDirectoryName(largeImagePath));
                        string filePath = Path.Combine(largeImagePath, model.LargeImage);
                        // Save the file to the server
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await largeImageFile.CopyToAsync(stream);
                        }
                    }

                    // Save Medium Image
                    if (!string.IsNullOrEmpty(model.MediumImage))
                    {
                        string mediumImagePath = Path.Combine(basePath, "Medium");
                        if (!Directory.Exists(mediumImagePath))
                        {
                            Directory.CreateDirectory(mediumImagePath);
                        }
                        string filePath = Path.Combine(mediumImagePath, model.MediumImage);
                        // Save the file to the server
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await mediumImageFile.CopyToAsync(stream);
                        }
                    }

                    // Save Small Image
                    if (!string.IsNullOrEmpty(model.SmallImage))
                    {
                        string smallImagePath = Path.Combine(basePath, "Small");
                        if (!Directory.Exists(smallImagePath))
                        {
                            Directory.CreateDirectory(smallImagePath);
                        }
                        string filePath = Path.Combine(smallImagePath, model.SmallImage);
                        // Save the file to the server
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await smallImageFile.CopyToAsync(stream);
                        }
                    }

                    // Insert image record into the database
                    using (SqlCommand cmd = new SqlCommand("AddProductImage", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@ProductId", productId);
                        cmd.Parameters.AddWithValue("@Type", model.Type);
                        cmd.Parameters.AddWithValue("@Color", model.Color);
                        cmd.Parameters.AddWithValue("@LargeImage", $"/uploads/Products/{productId}/Large/{Path.GetFileName(model.LargeImage)}");
                        cmd.Parameters.AddWithValue("@MediumImage", $"/uploads/Products/{productId}/Medium/{Path.GetFileName(model.MediumImage)}");
                        cmd.Parameters.AddWithValue("@SmallImage", $"/uploads/Products/{productId}/Small/{Path.GetFileName(model.SmallImage)}");
                        cmd.Parameters.AddWithValue("@Description", model.Description ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Quantity", model.Quantity);
                        cmd.Parameters.AddWithValue("@MRP", model.MRP);
                        cmd.Parameters.AddWithValue("@Discount", model.Discount);
                        cmd.Parameters.AddWithValue("@Price", model.MRP - (model.MRP * model.Discount / 100));
                        cmd.Parameters.AddWithValue("@ArrivingDays", model.ArrivingDays);
                        cmd.Parameters.AddWithValue("@IsActive", true);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            ProductsImage image = new ProductsImage();
            string productName = "";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand(@"
            SELECT pi.*, p.Name AS ProductName 
            FROM ProductsImage pi
            JOIN Products p ON pi.ProductId = p.ProductId
            WHERE pi.ProductsImageId = @ProductsImageId", conn))
                {
                    cmd.Parameters.AddWithValue("@ProductsImageId", id);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            image.ProductsImageId = reader.GetInt32(reader.GetOrdinal("ProductsImageId"));
                            image.ProductId = reader.GetInt32(reader.GetOrdinal("ProductId"));
                            image.Type = reader.GetString(reader.GetOrdinal("Type"));
                            image.Color = reader.GetString(reader.GetOrdinal("Color"));
                            image.LargeImage = reader.GetString(reader.GetOrdinal("Image"));
                            image.Description = reader.GetString(reader.GetOrdinal("Description"));
                            image.Quantity = reader.GetDouble(reader.GetOrdinal("Quantity"));
                            image.MRP = reader.GetDouble(reader.GetOrdinal("MRP"));
                            image.Discount = reader.GetInt32(reader.GetOrdinal("Discount"));
                            image.Price = reader.GetDouble(reader.GetOrdinal("Price"));
                            image.ArrivingDays = reader.GetInt32(reader.GetOrdinal("ArrivingDays"));
                            // Get Product Name
                            productName = reader.GetString(reader.GetOrdinal("ProductName"));
                        }
                    }
                }
            }

            // Pass product name to View using ViewBag or ViewData
            ViewBag.ProductName = productName;

            return View(image);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ProductsImage model, IFormFile ImageFile, string ProductName, string ExistingImage)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string imagePath = model.LargeImage;

                // Upload new image if provided
                if (ImageFile == null || ImageFile.Length == 0)
                {
                    imagePath = ExistingImage;
                }
                else
                {
                    // Upload new image
                    string uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "Products", model.ProductId.ToString());
                    if (!Directory.Exists(uploadPath))
                    {
                        Directory.CreateDirectory(uploadPath);
                    }

                    string fileName = $"{model.ProductsImageId}_{Path.GetFileName(ImageFile.FileName)}";
                    string filePath = Path.Combine(uploadPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(stream);
                    }

                    imagePath = $"/uploads/Products/{model.ProductId}/{fileName}";
                }

                // Update Product Image Details
                using (SqlCommand cmd = new SqlCommand(@"
            UPDATE ProductsImage 
            SET Type = @Type, Color = @Color, MRP = @MRP, Discount = @Discount, 
                Price = @Price,ArrivingDays = @ArrivingDays, Image = @Image 
            WHERE ProductsImageId = @ProductsImageId", conn))
                {
                    cmd.Parameters.AddWithValue("@ProductsImageId", model.ProductsImageId);
                    cmd.Parameters.AddWithValue("@Type", model.Type);
                    cmd.Parameters.AddWithValue("@Color", model.Color);
                    cmd.Parameters.AddWithValue("@Description", model.Description);
                    cmd.Parameters.AddWithValue("@Quantity", model.Quantity);
                    cmd.Parameters.AddWithValue("@MRP", model.MRP);
                    cmd.Parameters.AddWithValue("@Discount", model.Discount);
                    cmd.Parameters.AddWithValue("@Price", model.MRP - (model.MRP * model.Discount / 100));
                    cmd.Parameters.AddWithValue("@ArrivingDays", model.ArrivingDays);
                    cmd.Parameters.AddWithValue("@Image", imagePath);

                    await cmd.ExecuteNonQueryAsync();
                }

                if (!string.IsNullOrEmpty(ProductName))
                {
                    using (SqlCommand cmd = new SqlCommand("UPDATE Products SET Name = @Name WHERE ProductId = @ProductId", conn))
                    {
                        cmd.Parameters.AddWithValue("@ProductId", model.ProductId);
                        cmd.Parameters.AddWithValue("@Name", ProductName);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id, int productId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string imagePath = "";
                bool isLastImage = false;

                if (productId == 0)
                {
                    using (SqlCommand getProductCmd = new SqlCommand("SELECT ProductId,Image FROM ProductsImage WHERE ProductsImageId = @ProductsImageId", conn))
                    {
                        getProductCmd.Parameters.AddWithValue("@ProductsImageId", id);

                        // Declare reader outside the using block
                        SqlDataReader reader = await getProductCmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            productId = reader.GetInt32(0);
                            imagePath = reader.GetString(1);
                        }
                        reader.Close();
                    }
                }

                //  Step 1: Update `IsActive = 0` for the specific image
                using (SqlCommand updateCmd = new SqlCommand("UPDATE ProductsImage SET IsActive = 0 WHERE ProductsImageId = @ProductsImageId", conn))
                {
                    updateCmd.Parameters.AddWithValue("@ProductsImageId", id);
                    await updateCmd.ExecuteNonQueryAsync();
                }

                //  Step 2: Check if all images for this product are inactive
                int activeImageCount = 0;
                using (SqlCommand checkCmd = new SqlCommand("SELECT COUNT(*) FROM ProductsImage WHERE ProductId = @ProductId AND IsActive = 1", conn))
                {
                    checkCmd.Parameters.AddWithValue("@ProductId", productId);
                    activeImageCount = (int)await checkCmd.ExecuteScalarAsync();
                }

                // ✅ Step 3: If no active images remain, update `IsActive = 0` in the Products table
                if (activeImageCount == 0)
                {
                    using (SqlCommand updateProductCmd = new SqlCommand("UPDATE Products SET IsActive = 0 WHERE ProductId = @ProductId", conn))
                    {
                        updateProductCmd.Parameters.AddWithValue("@ProductId", productId);
                        await updateProductCmd.ExecuteNonQueryAsync();
                    }
                }

                if (isLastImage)
                {
                    using (SqlCommand updateProductCmd = new SqlCommand("UPDATE Products SET IsActive = 0 WHERE ProductId = @ProductId", conn))
                    {
                        updateProductCmd.Parameters.AddWithValue("@ProductId", productId);
                        await updateProductCmd.ExecuteNonQueryAsync();
                    }
                }

                // Step 4: Delete the image file from the server               
                if (productId > 0)
                {
                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        string fullImagePath = Path.Combine(_webHostEnvironment.WebRootPath, imagePath.TrimStart('/')); // Convert to full path

                        if (System.IO.File.Exists(fullImagePath))
                        {
                            System.IO.File.Delete(fullImagePath); // Delete only the specific image
                        }
                    }
                }

                string productFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "Products", productId.ToString());

                if (Directory.Exists(productFolder) && Directory.GetFiles(productFolder).Length == 0)
                {
                    Directory.Delete(productFolder); // Delete folder if empty
                }

            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetById(int id, int? productsImageId)
        {
            Products product = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand("GetProductById", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@ProductId", id);

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

                            if (!reader.IsDBNull(reader.GetOrdinal("ProductsImageId")))
                            {
                                var imagewithlist = new ProductsImage
                                {
                                    ProductsImageId = reader.GetInt32(reader.GetOrdinal("ProductsImageId")),
                                    ProductId = productId,
                                    Type = reader.GetString(reader.GetOrdinal("Type")),
                                    Color = reader.GetString(reader.GetOrdinal("Color")),
                                    LargeImage = reader.GetString(reader.GetOrdinal("Image")),
                                    Description = reader.GetString(reader.GetOrdinal("Description")),
                                    Quantity = reader.GetDouble(reader.GetOrdinal("Quantity")),
                                    MRP = reader.GetDouble(reader.GetOrdinal("MRP")),
                                    Discount = reader.GetInt32(reader.GetOrdinal("Discount")),
                                    Price = reader.GetDouble(reader.GetOrdinal("Price")),
                                    ArrivingDays = reader.GetInt32(reader.GetOrdinal("ArrivingDays")),
                                    IsActive = reader.GetBoolean(reader.GetOrdinal("ImageIsActive"))
                                };

                                productDict[productId].ProductImages.Add(imagewithlist);
                            }
                        }

                        product = productDict.Values.FirstOrDefault();
                    }
                }
            }

            if (product == null)
            {
                return NotFound();
            }

            ViewBag.SelectedImageId = productsImageId; // Pass the specific image ID to the view

            return View(product);
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
                                CoupanCode = reader.GetString(reader.GetOrdinal("CoupanCode")),
                                Discount = reader.GetDouble(reader.GetOrdinal("Discount")),
                                ExpiryDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("ExpiryDate"))),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                            });
                        }
                    }
                }
            }

            return View(coupons);
        }

        [HttpGet]
        public async Task<IActionResult> AddOrUpdateCoupan(int? id)
        {
            Coupan model = new Coupan();

            if (id != null && id != 0) // Editing existing coupon
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("SELECT * FROM Coupan WHERE CoupanId = @CoupanId", conn))
                    {
                        cmd.Parameters.AddWithValue("@CoupanId", id);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                model.CoupanId = reader.GetInt32(reader.GetOrdinal("CoupanId"));
                                model.CoupanName = reader.GetString(reader.GetOrdinal("CoupanName"));
                                model.Discount = reader.GetDouble(reader.GetOrdinal("Discount"));
                                model.ExpiryDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("ExpiryDate")));
                            }
                        }
                    }
                }
            }

            return View(model); // Pass model to view (for both add & edit)
        }


        [HttpPost]
        public async Task<IActionResult> AddOrUpdateCoupan(Coupan model)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("AddUpdateCoupan", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@CoupanId", model.CoupanId != 0 ? model.CoupanId : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@CoupanName", model.CoupanName);
                        cmd.Parameters.AddWithValue("@Discount", model.Discount);
                        cmd.Parameters.AddWithValue("@ExpiryDate", model.ExpiryDate.ToDateTime(TimeOnly.MinValue)); //  Convert DateOnly to DateTime
                        cmd.Parameters.AddWithValue("@IsActive", true);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = "Coupon saved successfully!";
                return RedirectToAction("Coupan");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error: " + ex.Message;
                return View(model);
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

            return View(model);
        }

        //  Fetch Coupon Details from Database
        private async Task<Coupan> FetchCoupanById(int id)
        {
            Coupan model = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand("GetCoupanById", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@CoupanId", id);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            model = new Coupan
                            {
                                CoupanId = reader.GetInt32(reader.GetOrdinal("CoupanId")),
                                CoupanName = reader.GetString(reader.GetOrdinal("CoupanName")),
                                CoupanCode = reader.GetString(reader.GetOrdinal("CoupanCode")),
                                Discount = reader.GetDouble(reader.GetOrdinal("Discount")),
                                ExpiryDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("ExpiryDate"))),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                            };
                        }
                    }
                }
            }

            return model;
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCoupan(int id)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (SqlCommand updateCmd = new SqlCommand("UPDATE Coupan SET IsActive = 0 WHERE CoupanId = @CoupanId", conn))
                {
                    updateCmd.Parameters.AddWithValue("@CoupanId", id);
                    await updateCmd.ExecuteNonQueryAsync();
                }
                return RedirectToAction("Coupan");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ValidateCoupan(string coupanCode)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("SELECT Discount FROM Coupan WHERE CoupanCode = @CoupanCode AND IsActive = 1 AND ExpiryDate >= GETDATE()", conn))
                    {
                        cmd.Parameters.AddWithValue("@CoupanCode", coupanCode);

                        var discount = await cmd.ExecuteScalarAsync();

                        if (discount != null)
                        {
                            return Json(new { success = true, discount = Convert.ToDouble(discount) });
                        }
                        else
                        {
                            return Json(new { success = false, message = "Invalid or expired coupon code." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
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
                    //cmd.Parameters.AddWithValue("@IShopId", iShopId);
                    //cmd.Parameters.AddWithValue("@OrderId", DBNull.Value); //  Pass NULL for OrderId

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
    }
}
