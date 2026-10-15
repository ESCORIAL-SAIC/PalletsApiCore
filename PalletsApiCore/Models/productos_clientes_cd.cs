using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PalletsApiCore.Models
{
    // ponytail: schema "public" es supuesto (DBeaver lo mostraba bajo information_schema); confirmar. id sin PK declarada en DB.
    [Table("productos_clientes_cd", Schema = "public")]
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
