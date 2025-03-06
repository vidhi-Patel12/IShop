using System.ComponentModel.DataAnnotations;

namespace ECommerce.Models
{
    public class Checkout
    {
        [Key]
        public int CheckoutId { get; set; }
        public int IShopId { get; set; }
        public string OrderId { get; set; }
        public int AddressId { get; set; }
        public string PaymentMode { get; set; }
        public DateTime OrderDate { get; set; }
        public double TotalAmount { get; set; }
        public double Tax { get; set; }
        public double DelivaryCharge { get; set; }
        public double FinalAmount { get; set; }
        public double PromoAmount { get; set; }
        public double OrderAmount { get; set; }
        public bool IsActive { get; set; }

        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
    }

    public class OrderItem
    {
        public int OrderItemId { get; set; }
        public int ProductId { get; set; }
        public int ProductsImageId { get; set; }
        public int OrderQty { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
