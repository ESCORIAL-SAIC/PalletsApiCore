using System;
using Microsoft.EntityFrameworkCore;

namespace PalletsApiCore.Models;

[Keyless]
public partial class aux_controlcalidad
{
    public Guid Id { get; set; }
    public int Etiqueta { get; set; }
    public Guid PuestoControlId { get; set; }
    public bool ControladorEstado { get; set; }
}
