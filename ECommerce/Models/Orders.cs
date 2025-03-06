namespace ECommerce.Models
{
    public class Orders
    {
        public int Id { get; set; }
        public string OrderId { get; set; }
        public int IShopId { get; set; }
        public int ProductId { get; set; }
        public int ProductsImageId { get; set; }
        public double OrderQty { get; set; }
        public double TotalAmount { get; set; }
       
    }
}
