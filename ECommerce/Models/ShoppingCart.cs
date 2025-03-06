using System.ComponentModel.DataAnnotations;

namespace ECommerce.Models
{
    public class ShoppingCart
    {
        [Key]
        public int ShoppingCartId {get;set;}
        public  int IShopId { get; set; }
        public int ArrivingDays { get; set; }
        public string Color { get; set; }
        public string Description { get; set; }
        public string Image { get; set; }
        public string Name { get; set; }
        public double Price { get; set; }
        public int ProductId  { get; set; }
        public int ProductsImageId { get; set; }
        public double Quantity { get; set; }
        public double Total { get; set; }
        public string Type { get; set; }
    }
}
