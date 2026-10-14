using Microsoft.EntityFrameworkCore;
using PalletsApiCore.Models;

namespace PalletsApiCore
{
    public static class Fun
    {
        // Los importados llegan con una serie de 20 digitos, pero la serie real (la que existe
        // en las vistas y tablas) son los 9 digitos de la derecha.
        public static bool TryNormalizarSerie(string? numero, string tipo, out int serie)
        {
            serie = 0;
            if (string.IsNullOrWhiteSpace(numero))
                return false;

            var s = numero.Trim();
            if (tipo == "IMPORTADO")
                s = s.Length >= 9 ? s.Substring(s.Length - 9) : s;

            return int.TryParse(s, out serie);
        }

        // PEABODY: la serie es el codigo EAN13/DUN14 completo (solo digitos, sin recortar ni parsear a int).
        public static bool TryNormalizarCodigoPeabody(string? numero, out string codigo)
        {
            codigo = "";
            var s = numero?.Trim();
            if (string.IsNullOrEmpty(s) || (s.Length != 13 && s.Length != 14) || !s.All(c => c is >= '0' and <= '9'))
                return false;
            codigo = s;
            return true;
        }

        // ponytail: solo resuelve por ud_producto.codigogs1 (EAN); el modelo no tiene columna DUN. Sin control final (es por numero int).
        public static async Task<IResult> GetProductoPeabodyAsync(string? numero, ESCORIALContext context)
        {
            if (!TryNormalizarCodigoPeabody(numero, out var codigo))
                return Results.BadRequest("El numero de producto no es valido");

            var c = await (
                from p in context.producto
                join ud in context.ud_producto on p.boextension_id equals (Guid?)ud.id
                where ud.codigogs1 != null && ud.codigogs1.Trim() == codigo
                select new { p, ud }
            ).Take(2).ToListAsync();

            if (c.Count == 0)
                return Results.NotFound("No se encontro el numero de serie");
            if (c.Count > 1)
                return Results.Conflict("El codigo corresponde a mas de un producto");
            var r = c[0];

            return Results.Ok(new Product
            {
                serial = codigo,
                productId = r.p.id,
                productCode = r.p.codigo,
                description = r.p.descripcion,
                type = "PEABODY",
                maxCantByPallet = r.ud.cant_x_pallet,
                isAvailable = await IsAvailableAsync(codigo, r.p.id, context)
            });
        }

        public static async Task<bool> IsAvailableAsync(string serial, Guid productoId, ESCORIALContext context)
        {
            // Una misma serie puede resolver a varios productos (caso importados, desambiguado por EAN),
            // por eso la disponibilidad se evalua por la combinacion serie + producto, no solo por serie.
            var exists = await context.cenker_prod_x_pallet
                .AnyAsync(c => c.serie == serial && c.producto_id == productoId && c.activo);
            return !exists;
        }
    }
}
