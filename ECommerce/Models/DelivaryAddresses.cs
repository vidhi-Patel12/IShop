using System.ComponentModel.DataAnnotations;

namespace ECommerce.Models
{
    public class DelivaryAddresses
    {
        [Key]
        public int AddressId { get; set; }
        public int IShopId { get; set; }
        [Required]
        public string FullName {  get; set; }
        public long Mobile {  get; set; }
        public string Country {  get; set; }
        public string State { get; set; }
        public string City { get; set; }
        public int ZipCode { get; set; }
        public string Address { get; set; }
        public bool IsActive { get; set; }
    }
}
