namespace ECommerce.Models
{
    public class OrderDetails
    {
        public string OrderId { get; set; }
        public int IShopId { get; set; }
        public DateTime OrderDate { get; set; }
        public string PaymentMode { get; set; }
        public double TotalAmount { get; set; }
        public bool IsActive { get; set; }
        public double Tax { get; set; }
        public double DelivaryCharge { get; set; }
        public double FinalAmount { get; set; }
        public double PromoAmount { get; set; }
        public double OrderAmount { get; set; }

        // Product Details
        public string ProductName { get; set; }
        public int ProductsImageId { get; set; }
        public string Image { get; set; }
        public string Type { get; set; }
        public string Color { get; set; }

        //Address Details
        public string FullName { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State{ get; set; }
        public string Country { get; set; }
        public int ZipCode { get; set; }
        public long Mobile { get; set; }
    }
}
