using ECommerce.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }
        public DbSet<Register> Register { get; set; }  
        public DbSet<Login> Login { get; set; }
        public DbSet<Products> Products { get; set; }
        public DbSet<ProductsImage> ProductsImage { get; set; }
        public DbSet<DelivaryAddresses> DelivaryAddresses { get; set; }
        public DbSet<Orders> Orders{ get; set; }
        public DbSet<Checkout> Checkout { get; set; }
        public DbSet<Coupan> Coupan { get; set; }
    }
}
