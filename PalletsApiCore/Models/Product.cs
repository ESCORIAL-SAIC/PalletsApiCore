# nullable disable

namespace PalletsApiCore.Models
{
    public class Product
    {
        public int serial { get; set; }
        public Guid productId { get; set; }
        public string productCode { get; set; }
        public string description { get; set; }
        public string type { get; set; }
        public int maxCantByPallet { get; set; }
        public bool isAvailable { get; set; }
        public bool deleted { get; set; }
        // Solo PEABODY: el codigo escaneado era el DUN14 y cuantas unidades representa.
        public bool esDun { get; set; }
        public int unidades { get; set; } = 1;
    }
}
