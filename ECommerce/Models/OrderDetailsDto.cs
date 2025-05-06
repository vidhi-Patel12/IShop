namespace ECommerce.Models
{
    public class OrderDetailsDto
    {
        public string OrderId { get; set; }
        public int AddressId { get; set; }
       
        public string PaymentMode { get; set; }
        public DateTime OrderDate { get; set; }
        public float DelivaryCharge { get; set; }
        public float PromoAmount { get; set; }
        public float OrderAmount { get; set; }

        public string FullName { get; set; }
        public string Mobile { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public string Zipcode { get; set; }

        public int ProductsImageId { get; set; }
        public int OrderQty { get; set; }
        public float TotalAmount { get; set; }

        public int ProductId { get; set; }
        public string Type { get; set; }
        public string Color { get; set; }
        public float Price { get; set; }

        public string ProductName { get; set; }
        public string Email { get; set; }

        public string PaymentModeInPaymentTable { get; set; }
        public float PaymentAmount { get; set; }
        public DateTime PaymentDate { get; set; }
        public int PaymentId { get; set; }
    }
}
