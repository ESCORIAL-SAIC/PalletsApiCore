# nullable disable

namespace PalletsApiCore.Models
{
    public class Product
    {
        public string serial { get; set; }
        public Guid productId { get; set; }
        public string productCode { get; set; }
        public string description { get; set; }
        public string type { get; set; }
        public int maxCantByPallet { get; set; }
        public bool isAvailable { get; set; }
        public bool deleted { get; set; }
    }
}
