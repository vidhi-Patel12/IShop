using System.ComponentModel.DataAnnotations;

namespace ECommerce.Models
{
    public class Login
    {
        [Key]
        public int LoginId { get; set; }
        public int IShopId { get; set; }
        public int OTP { get; set; }
        public bool IsValid { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
}
