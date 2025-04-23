using ECommerce.Data;
using ECommerce.Models;
using Microsoft.AspNetCore.Hosting;
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
using System.Web;
using static Azure.Core.HttpHeader;

namespace ECommerce.Controllers
{
    public class AdminController : Controller
    {
        private readonly string _connectionString;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext dbContext, IConfiguration configuration, IWebHostEnvironment webHostEnvironment, ILogger<AdminController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _webHostEnvironment = webHostEnvironment;
            _dbContext = dbContext;
            _logger = logger;
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
                string? userId = Request.Cookies["IShopId"];

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string slug = SlugHelper.GenerateSlug(model.Name);
                    string typeslug = SlugHelper.GenerateSlug(model.Type);
                    string colorslug = SlugHelper.GenerateSlug(model.Color);

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
                            cmd.Parameters.AddWithValue("@Slug", slug);
                            cmd.Parameters.AddWithValue("@IsActive", true);
                            cmd.Parameters.AddWithValue("@CreatedBy", userId);
                            cmd.Parameters.AddWithValue("@CreatedDateTime", DateTime.Now);
                            cmd.Parameters.AddWithValue("@UpdatedBy", null);
                            cmd.Parameters.AddWithValue("@UpdatedDateTime", null);

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
                        cmd.Parameters.AddWithValue("@TypeSlug", typeslug);
                        cmd.Parameters.AddWithValue("@ColorSlug", colorslug);
                        cmd.Parameters.AddWithValue("@IsActive", true);
                        cmd.Parameters.AddWithValue("@CreatedBy", userId);
                        cmd.Parameters.AddWithValue("@CreatedDateTime", DateTime.Now);
                        cmd.Parameters.AddWithValue("@UpdatedBy", null);
                        cmd.Parameters.AddWithValue("@UpdatedDateTime", null);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, int productsImageId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("GetProductById", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@ProductId", id);
                        cmd.Parameters.AddWithValue("@ProductsImageId", productsImageId);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            List<Products> product = new List<Products>();

                            while (await reader.ReadAsync())
                            {
                                var products = new Products
                                {
                                    ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                                    Name = reader.GetString(reader.GetOrdinal("Name")),
                                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                                    CreatedBy = reader.GetInt32(reader.GetOrdinal("CreatedBy")),
                                    CreatedDateTime = reader.GetDateTime(reader.GetOrdinal("CreatedDateTime")),
                                    // Initialize ProductImages as an empty list
                                    ProductImages = new List<ProductsImage>()
                                };

                                if (!reader.IsDBNull(reader.GetOrdinal("ProductsImageId")))
                                {
                                    var productImage = new ProductsImage
                                    {
                                        ProductsImageId = reader.GetInt32(reader.GetOrdinal("ProductsImageId")),
                                        Type = reader.GetString(reader.GetOrdinal("Type")),
                                        Color = reader.GetString(reader.GetOrdinal("Color")),
                                        LargeImage = !reader.IsDBNull(reader.GetOrdinal("LargeImage")) && !string.IsNullOrEmpty(reader.GetString(reader.GetOrdinal("LargeImage")))
                                        ? $"{Request.Scheme}://{Request.Host}" + reader.GetString(reader.GetOrdinal("LargeImage"))
                                        : "https://via.placeholder.com/300",

                                        MediumImage = !reader.IsDBNull(reader.GetOrdinal("MediumImage")) && !string.IsNullOrEmpty(reader.GetString(reader.GetOrdinal("MediumImage")))
                                        ? $"{Request.Scheme}://{Request.Host}" + reader.GetString(reader.GetOrdinal("MediumImage"))
                                        : "https://via.placeholder.com/150",

                                        SmallImage = !reader.IsDBNull(reader.GetOrdinal("SmallImage")) && !string.IsNullOrEmpty(reader.GetString(reader.GetOrdinal("SmallImage")))
                                        ? $"{Request.Scheme}://{Request.Host}" + reader.GetString(reader.GetOrdinal("SmallImage"))
                                        : "https://via.placeholder.com/150",
                                        Description = reader.GetString(reader.GetOrdinal("Description")),
                                        Quantity = reader.GetDouble(reader.GetOrdinal("Quantity")),
                                        MRP = reader.GetDouble(reader.GetOrdinal("MRP")),
                                        Discount = reader.GetInt32(reader.GetOrdinal("Discount")),
                                        Price = reader.GetDouble(reader.GetOrdinal("Price")),
                                        ArrivingDays = reader.GetInt32(reader.GetOrdinal("ArrivingDays")),
                                        IsActive = reader.GetBoolean(reader.GetOrdinal("ImageIsActive")),
                                        CreatedBy = reader.GetInt32(reader.GetOrdinal("CreatedBy")),
                                        CreatedDateTime = reader.GetDateTime(reader.GetOrdinal("CreatedDateTime")),
                                    };

                                    products.ProductImages.Add(productImage);
                                }

                                product.Add(products);
                            }

                            if (product.Count == 0)
                            {
                                return NotFound(new { success = false, message = "Product not found." });
                            }

                            return Ok(product.FirstOrDefault());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error fetching product data", error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, int productsImageId, [FromForm] List<Products> productList, IFormFile? largeImageFile, IFormFile? mediumImageFile, IFormFile? smallImageFile)
        {
            foreach (var model in productList)
            {
                string? userId = Request.Cookies["IShopId"];

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // Check if product exists
                    using (SqlCommand checkCmd = new SqlCommand("SELECT COUNT(*) FROM Products WHERE ProductId = @ProductId", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@ProductId", model.ProductId);
                        int count = (int)await checkCmd.ExecuteScalarAsync();

                        if (count == 0)
                        {
                            return Json(new { success = false, message = "Product not found." });
                        }
                    }

                    // Update product details
                    using (SqlCommand cmd = new SqlCommand(@"UPDATE Products SET Name = @Name,IsActive = @IsActive,UpdatedBy = @UpdatedBy,UpdatedDateTime = @UpdatedDateTime WHERE ProductId = @ProductId", conn))
                    {
                        cmd.Parameters.AddWithValue("@ProductId", model.ProductId);
                        cmd.Parameters.AddWithValue("@Name", model.Name);
                        cmd.Parameters.AddWithValue("@IsActive", true);
                        cmd.Parameters.AddWithValue("@UpdatedBy", userId);
                        cmd.Parameters.AddWithValue("@UpdatedDateTime", DateTime.Now);

                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Define base path for product images
                    string basePath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "Products", model.ProductId.ToString());

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

                    // Update product image details in database
                    using (SqlCommand cmd = new SqlCommand(@"UPDATE ProductsImage SET Type = @Type,Color = @Color,LargeImage = @LargeImage,MediumImage = @MediumImage,SmallImage = @SmallImage,
                        Description = @Description,Quantity = @Quantity,MRP = @MRP,Discount = @Discount,Price = @Price,ArrivingDays = @ArrivingDays,IsActive = @IsActive,UpdatedBy = @UpdatedBy,
                         UpdatedDateTime = @UpdatedDateTime  WHERE ProductId = @ProductId AND ProductsImageId = @ProductsImageId", conn))
                    {
                        cmd.Parameters.AddWithValue("@ProductId", model.ProductId);
                        cmd.Parameters.AddWithValue("@ProductsImageId", model.ProductsImageId);
                        cmd.Parameters.AddWithValue("@Type", model.Type);
                        cmd.Parameters.AddWithValue("@Color", model.Color);
                        cmd.Parameters.AddWithValue("@LargeImage", $"/uploads/Products/{model.ProductId}/Large/{Path.GetFileName(model.LargeImage)}");
                        cmd.Parameters.AddWithValue("@MediumImage", $"/uploads/Products/{model.ProductId}/Medium/{Path.GetFileName(model.MediumImage)}");
                        cmd.Parameters.AddWithValue("@SmallImage", $"/uploads/Products/{model.ProductId}/Small/{Path.GetFileName(model.SmallImage)}");
                        cmd.Parameters.AddWithValue("@Description", model.Description ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Quantity", model.Quantity);
                        cmd.Parameters.AddWithValue("@MRP", model.MRP);
                        cmd.Parameters.AddWithValue("@Discount", model.Discount);
                        cmd.Parameters.AddWithValue("@Price", model.MRP - (model.MRP * model.Discount / 100));
                        cmd.Parameters.AddWithValue("@ArrivingDays", model.ArrivingDays);
                        cmd.Parameters.AddWithValue("@IsActive", true);
                        cmd.Parameters.AddWithValue("@UpdatedBy", userId);
                        cmd.Parameters.AddWithValue("@UpdatedDateTime", DateTime.Now);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            return Json(new { success = true, message = "Product updated successfully." });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id, int productsImageId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                List<string> imagePaths = new List<string>();

                //  Fetch all image paths for this image
                using (SqlCommand getProductCmd = new SqlCommand("SELECT LargeImage, MediumImage, SmallImage FROM ProductsImage WHERE ProductsImageId = @ProductsImageId", conn))
                {
                    getProductCmd.Parameters.AddWithValue("@ProductsImageId", productsImageId);
                    SqlDataReader reader = await getProductCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        imagePaths.Add(reader["LargeImage"]?.ToString());
                        imagePaths.Add(reader["MediumImage"]?.ToString());
                        imagePaths.Add(reader["SmallImage"]?.ToString());
                    }
                    reader.Close();
                }

                //  Update `IsActive = 0` for the specific image
                using (SqlCommand updateCmd = new SqlCommand("UPDATE ProductsImage SET IsActive = 0 WHERE ProductsImageId = @ProductsImageId", conn))
                {
                    updateCmd.Parameters.AddWithValue("@ProductsImageId", productsImageId);
                    await updateCmd.ExecuteNonQueryAsync();
                }

                //  Check if all images for this product are inactive
                int activeImageCount = 0;
                using (SqlCommand checkCmd = new SqlCommand("SELECT COUNT(*) FROM ProductsImage WHERE ProductId = @ProductId AND IsActive = 1", conn))
                {
                    checkCmd.Parameters.AddWithValue("@ProductId", id);
                    activeImageCount = (int)await checkCmd.ExecuteScalarAsync();
                }

                //  If no active images remain, update `IsActive = 0` in the Products table
                if (activeImageCount == 0)
                {
                    using (SqlCommand updateProductCmd = new SqlCommand("UPDATE Products SET IsActive = 0 WHERE ProductId = @ProductId", conn))
                    {
                        updateProductCmd.Parameters.AddWithValue("@ProductId", id);
                        await updateProductCmd.ExecuteNonQueryAsync();
                    }
                }

                //  Delete Image Files from Server
                foreach (var imagePath in imagePaths)
                {
                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        string fullImagePath = Path.Combine(_webHostEnvironment.WebRootPath, imagePath.TrimStart('/'));

                        if (System.IO.File.Exists(fullImagePath))
                        {
                            System.IO.File.Delete(fullImagePath);  // Delete specific image
                        }
                    }
                }

                //  Delete the product folder if it is empty
                string productFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "Products", id.ToString());
                if (Directory.Exists(productFolder))
                {
                    // Ensure all files inside are deleted
                    foreach (string file in Directory.GetFiles(productFolder))
                    {
                        try
                        {
                            System.IO.File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error deleting file {file}: {ex.Message}");
                        }
                    }

                    // Ensure all subdirectories are deleted
                    foreach (string dir in Directory.GetDirectories(productFolder))
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error deleting directory {dir}: {ex.Message}");
                        }
                    }

                    // Attempt to delete the directory after clearing its contents
                    try
                    {
                        Directory.Delete(productFolder, true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting folder {productFolder}: {ex.Message}");
                    }
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
                                    IsActive = reader.GetBoolean(reader.GetOrdinal("ImageIsActive"))
                                };

                                productDict[productId].ProductImages.Add(image);
                            }
                        }

                        product = productDict.Values.FirstOrDefault();
                        if (product != null && productsImageId.HasValue)
                        {
                            // Filter images based on provided productsImageId
                            product.ProductImages = product.ProductImages
                                .Where(img => img.ProductsImageId == productsImageId.Value)
                                .ToList();
                        }
                    }
                }
            }

            if (product == null || (productsImageId.HasValue && product.ProductImages.Count == 0))
            {
                return NotFound(new { message = "Product or product image not found" });
            }

            return Json(product);
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
                                ExpiryDate = reader.GetDateTime(reader.GetOrdinal("ExpiryDate")).Date,
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
                                model.CoupanType = reader.GetString(reader.GetOrdinal("CoupanType"));
                                model.Discount = reader.GetDouble(reader.GetOrdinal("Discount"));
                                model.ExpiryDate = reader.GetDateTime(reader.GetOrdinal("ExpiryDate")).Date;
                            }
                        }
                    }
                }
            }

            return Json(model); // Pass model to view (for both add & edit)
        }

        [HttpPost]
        public async Task<IActionResult> AddOrUpdateCoupan([FromBody] Coupan model)
        {
            try
            {
                if (model.ExpiryDate == default)
                    throw new Exception("Invalid expiry date");

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("AddUpdateCoupan", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@CoupanId", model.CoupanId != 0 ? model.CoupanId : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@CoupanName", model.CoupanName);
                        cmd.Parameters.AddWithValue("@CoupanType", model.CoupanType);
                        cmd.Parameters.AddWithValue("@Discount", model.Discount);
                        cmd.Parameters.AddWithValue("@ExpiryDate", model.ExpiryDate.Date);
                        cmd.Parameters.AddWithValue("@IsActive", true);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = "Coupon saved successfully!";
                return Ok(new { success = true, message = "Coupon saved successfully!" });
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
                                CoupanType = reader.GetString(reader.GetOrdinal("CoupanType")),
                                CoupanCode = reader.GetString(reader.GetOrdinal("CoupanCode")),
                                Discount = reader.GetDouble(reader.GetOrdinal("Discount")),
                                ExpiryDate = reader.GetDateTime(reader.GetOrdinal("ExpiryDate")).Date,
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

        //Fetch order details for a specific OrderId
        public async Task<IActionResult> ViewOrder(string id)
        {
            //Read IShopId from cookies
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

        [HttpGet]
        public async Task<IActionResult> GetDashboardChartData(string timeRange = "day")
        {
            try
            {
                var authId = Request.Cookies["IShopId"];
                if (string.IsNullOrEmpty(authId))
                {
                    return BadRequest("Authentication ID not found.");
                }

                var labels = new List<string>();
                var productDataDict = new Dictionary<string, int>();
                var orderDataDict = new Dictionary<string, double>();
                var revenueDataDict = new Dictionary<string, double>();
                var duePaymentDataDict = new Dictionary<string, double>();

                if (timeRange == "day")
                {
                    int currentYear = DateTime.Now.Year;
                    int currentMonth = DateTime.Now.Month;
                    int daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);

                    for (int i = 1; i <= daysInMonth; i++)
                    {
                        string label = i.ToString();
                        labels.Add(label);
                        productDataDict[label] = 0;
                        orderDataDict[label] = 0;
                        revenueDataDict[label] = 0;
                        duePaymentDataDict[label] = 0;
                    }
                }
                else if (timeRange == "week")
                {
                    var calendar = System.Globalization.DateTimeFormatInfo.CurrentInfo.Calendar;
                    var dtf = System.Globalization.DateTimeFormatInfo.CurrentInfo;

                    var previousMonthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1);
                    var currentMonthEnd = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1).AddDays(-1);

                    var tempDate = previousMonthStart;
                    var weekLabels = new HashSet<string>();

                    while (tempDate <= currentMonthEnd)
                    {
                        int weekNum = calendar.GetWeekOfYear(tempDate, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                        string label = $"Week {weekNum}, {tempDate.Year}";

                        if (!weekLabels.Contains(label))
                        {
                            weekLabels.Add(label);
                            labels.Add(label);
                            productDataDict[label] = 0;
                            orderDataDict[label] = 0;
                            revenueDataDict[label] = 0;
                            duePaymentDataDict[label] = 0;
                        }

                        tempDate = tempDate.AddDays(7);
                    }
                }
                else if (timeRange == "month")
                {
                    for (int i = 1; i <= 12; i++)
                    {
                        string label = new DateTime(DateTime.Now.Year, i, 1).ToString("MMM");
                        labels.Add(label);
                        productDataDict[label] = 0;
                        orderDataDict[label] = 0;
                        revenueDataDict[label] = 0;
                        duePaymentDataDict[label] = 0;
                    }
                }
                else if (timeRange == "year")
                {
                    int currentYear = DateTime.Now.Year;
                    for (int i = currentYear - 10 + 1; i <= currentYear; i++)
                    {
                        string label = i.ToString();
                        labels.Add(label);
                        productDataDict[label] = 0;
                        orderDataDict[label] = 0;
                        revenueDataDict[label] = 0;
                        duePaymentDataDict[label] = 0;
                    }
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand("GetDashboardData", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@TimeRange", timeRange.ToLower());

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int year = reader.GetInt32(0);
                                string label;
                                int productCount;
                                double orderCount, revenue, duePayment;

                                if (timeRange == "day")
                                {
                                    var date = reader.GetDateTime(1);
                                    label = date.Day.ToString();
                                    productCount = reader.GetInt32(2);
                                    orderCount = reader.GetInt32(3);
                                    revenue = reader.IsDBNull(4) ? 0 : reader.GetDouble(4);
                                    duePayment = reader.IsDBNull(5) ? 0 : reader.GetDouble(5);
                                }
                                else if (timeRange == "week")
                                {
                                    int week = reader.GetInt32(1);
                                    label = $"Week {week}, {year}";
                                    productCount = reader.GetInt32(2);
                                    orderCount = reader.GetInt32(3);
                                    revenue = reader.IsDBNull(4) ? 0 : reader.GetDouble(4);
                                    duePayment = reader.IsDBNull(5) ? 0 : reader.GetDouble(5);
                                }
                                else if (timeRange == "month")
                                {
                                    int month = reader.GetInt32(1);
                                    label = new DateTime(year, month, 1).ToString("MMM");
                                    productCount = reader.GetInt32(2);
                                    orderCount = reader.GetInt32(3);
                                    revenue = reader.IsDBNull(4) ? 0 : reader.GetDouble(4);
                                    duePayment = reader.IsDBNull(5) ? 0 : reader.GetDouble(5);
                                }
                                else // year
                                {
                                    label = year.ToString();
                                    productCount = reader.GetInt32(1);
                                    orderCount = reader.GetInt32(2);
                                    revenue = reader.IsDBNull(3) ? 0 : reader.GetDouble(3);
                                    duePayment = reader.IsDBNull(4) ? 0 : reader.GetDouble(4);
                                }

                                if (productDataDict.ContainsKey(label))
                                {
                                    productDataDict[label] = productCount;
                                    orderDataDict[label] = orderCount;
                                    revenueDataDict[label] = revenue;
                                    duePaymentDataDict[label] = duePayment;
                                }
                            }
                        }
                    }
                }

                return Json(new
                {
                    labels = labels,
                    productData = labels.Select(l => productDataDict[l]).ToList(),
                    orderData = labels.Select(l => orderDataDict[l]).ToList(),
                    revenueData = labels.Select(l => revenueDataDict[l]).ToList(),
                    duePaymentData = labels.Select(l => duePaymentDataDict[l]).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard chart data error.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpGet]
        public IActionResult Dashboard()
        {
            return View(); 
        }

    }
}