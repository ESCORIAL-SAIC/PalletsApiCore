using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PalletsApiCore.Models
{
    [Table("productos_clientes_cd", Schema = "web")]
    public class productos_clientes_cd
    {
        [Key]
        public Guid id { get; set; }
        [StringLength(50)]
        public string? codigo_ean { get; set; }
        [StringLength(50)]
        public string? codigo_producto { get; set; }
        [StringLength(100)]
        public string? descripcion { get; set; }
        public int? cantidad_pallet { get; set; }
        public decimal? peso_total_pallet_kg { get; set; }
        public DateTime? fecha_ingreso { get; set; }
        public DateTime? ultima_actualizacion { get; set; }
    }
}
