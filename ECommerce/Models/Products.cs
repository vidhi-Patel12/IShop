using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerce.Models
{
    public class Products : ProductsImage
    {
        [Key]
        public int ProductId { get; set; }
        [Required]
        public string Name { get; set; }       
        public bool IsActive { get; set; }

        [NotMapped]
        public IFormFile LargeImageFile { get; set; }
        [NotMapped]
        public IFormFile MediumImageFile { get; set; }
        [NotMapped]
        public IFormFile SmallImageFile { get; set; }
        public virtual ICollection<ProductsImage> ProductImages { get; set; } = new List<ProductsImage>();
    }
}
