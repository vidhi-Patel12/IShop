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
        public string? Slug {get; set;}
        public bool IsActive { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedDateTime { get; set; }   
        public int UpdatedBy { get; set; }
        public DateTime UpdatedDateTime { get; set; }

        [NotMapped]
        public IFormFile LargeImageFile { get; set; }
        [NotMapped]
        public IFormFile MediumImageFile { get; set; }
        [NotMapped]
        public IFormFile SmallImageFile { get; set; }

        public virtual ICollection<ProductsImage> ProductImages { get; set; } = new List<ProductsImage>();


    }
}