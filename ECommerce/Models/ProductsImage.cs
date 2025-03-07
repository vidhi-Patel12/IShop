using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerce.Models
{
    public class ProductsImage
    {
        [Key]
        public int ProductsImageId { get; set; }
        [ForeignKey("Products")]
        public int ProductId { get; set; }
        [Required]
        public string Type { get; set; }
        [Required]
        public string Color { get; set; }
        [Required]
        public string LargeImage { get; set; }
        [Required]
        public string MediumImage { get; set; }
        [Required]
        public string SmallImage { get; set; }
        [Required]
        public string Description { get; set; }
        [Required]
        public double Quantity { get; set; }
        [Required]
        public double MRP { get; set; }
        public int Discount { get; set; }
        public double Price { get; set; }
        public int ArrivingDays { get; set; }
        public bool IsActive { get; set; }

        public virtual Products Product { get; set; }
    }
}
