#nullable disable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations.Schema;

namespace PalletsApiCore.Models
{
    [Keyless]
    public class api_pallets_controlfinal
    {
        [Column("fecha_ingreso_stock")]
        public DateTime? FechaIngresoStock { get; set; }
        [Column("usuario")]
        public string? Usuario { get; set; } = string.Empty;
        [Column("numero")]
        public int? Numero { get; set; }
        [Column("controlador_fechahora")]
        public DateTime? ControladorFechaHora { get; set; }
        [Column("controlador_empleado_n")]
        public string? ControladorEmpleado { get; set; } = string.Empty;
        [Column("controlador_estado")]
        public bool ControladorEstado { get; set; }
        [Column("puestocontrol_n")]
        public string? PuestoControl { get; set; } = string.Empty;
        [Column("reparador_estado")]
        public bool ReparadorEstado { get; set; }
        [Column("tipo_producto")]
        public string? TipoProducto { get; set; } = string.Empty;
    }
}
