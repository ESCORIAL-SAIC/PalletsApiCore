using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PalletsApiCore.Models;

[Table("aux_controlcalidad", Schema = "public")]
public partial class aux_controlcalidad
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }
    [Column("etiqueta")]
    public int Etiqueta { get; set; }
    [Column("puestocontrol_id")]
    public Guid PuestoControlId { get; set; }
    [Column("puestocontrol_n")]
    public string PuestoControlN { get; set; }
    [Column("controlador_estado")]
    public bool ControladorEstado { get; set; }
    [Column("reparador_estado")]
    public bool ReparadorEstado { get; set; }
}
