using System.ComponentModel.DataAnnotations;

namespace PalletsApiCore.Models
{
    public class ud_producto
    {
        [Key]
        public Guid id { get; set; }
        [StringLength(40)]
        public int cant_x_pallet { get; set; }
        public string? codigogs1 { get; set; }
    }
}
