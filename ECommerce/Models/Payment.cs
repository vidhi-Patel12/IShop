using System.ComponentModel.DataAnnotations;

namespace ECommerce.Models
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }
        public string TransactionId { get; set; }
        public string OrderId { get; set; }
        public string IShopId { get; set; }
        public string PaymentMode { get; set; }
        public double Amount { get; set; }
        public string Status { get; set; }
        public DateTime PaymentDate { get; set; }

    }
}
