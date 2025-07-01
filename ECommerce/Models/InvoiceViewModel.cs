namespace ECommerce.Models
{
    public class InvoiceViewModel
    {
        public int Id { get; set; }
        public string OrderId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerAddress { get; set; }
        public DateTime OrderDate { get; set; }
        public int PaymentId { get; set; }

        public string ShopName { get; set; }
        //public string ShopLogoUrl { get; set; }
        public string ShopEmail { get; set; }
        public string ShopPhone { get; set; }
        //public string GSTNumber { get; set; }

        public List<InvoiceItem> Items { get; set; }

        //public string SignatureUrl { get; set; }

        public string PaymentMethod {  get; set; }

        public float PaymentAmount { get; set; }

        // Add other properties as required like Discount, Total, Subtotal

        public float Subtotal { get; set; }
        public float Discount { get; set; }
        public float Total { get; set; }
    }

    public class InvoiceItem
    {
        public string Name { get; set; }
        public string Type {  get; set; }
        public string Color { get; set; }
        public float Price { get; set; }
        public int Quantity { get; set; }
        public float Total { get; set; }
    }

}

