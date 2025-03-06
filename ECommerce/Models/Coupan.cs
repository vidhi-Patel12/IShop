using System.ComponentModel.DataAnnotations;

namespace ECommerce.Models
{
    public class Coupan
    {
        [Key]
        public int CoupanId { get; set; }
        [Required]
        public string CoupanName { get;set; }
        public string CoupanCode { get; set; }
        [Required]
        public double Discount { get; set; }
        [Required]
        public DateOnly ExpiryDate { get; set; }
        public bool IsActive { get; set; }
    }
}
