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

        // Igual que GREATEST(COALESCE(UNIDADESPORBULTO,1),1) de expedicion-api: 0/NULL/negativo -> 1.
        public static int CalcularUnidades(bool esDun, decimal? unidadesPorBulto)
            => esDun ? (int)Math.Max(unidadesPorBulto ?? 1, 1) : 1;

        // Resuelve un producto Peabody por EAN13 (codigogs1) o DUN14 (codigo_dun), match exacto.
        // Un codigo puede dar varios productos: se toma el primero ordenado por codigo e id (determinista).
        // ponytail: si el negocio exige rechazar ambiguedad, devolver 409 cuando haya >1.
        public static async Task<Product?> ResolverPeabodyAsync(string codigo, ESCORIALContext context)
        {
            var cod = codigo.Trim();
            var r = await (
                from p in context.producto
                join ud in context.ud_producto on p.boextension_id equals (Guid?)ud.id
                where ud.codigogs1 == cod || ud.codigo_dun == cod
                orderby p.codigo, p.id
                select new { p.id, p.codigo, p.descripcion, p.unidadesporbulto, ud.cant_x_pallet, ud.codigo_dun }
            ).FirstOrDefaultAsync();

            if (r is null) return null;
            var esDun = r.codigo_dun == cod;
            return new Product
            {
                productId = r.id,
                productCode = r.codigo,
                description = r.descripcion,
                type = "PEABODY",
                maxCantByPallet = r.cant_x_pallet,
                isAvailable = true, // Peabody permite codigos repetidos (issue #16)
                esDun = esDun,
                unidades = CalcularUnidades(esDun, r.unidadesporbulto)
            };
        }

        public static async Task<bool> IsAvailableAsync(int serial, Guid productoId, ESCORIALContext context)
        {
            // Una misma serie puede resolver a varios productos (caso importados, desambiguado por EAN),
            // por eso la disponibilidad se evalua por la combinacion serie + producto, no solo por serie.
            var exists = await context.cenker_prod_x_pallet
                .AnyAsync(c => c.serie == serial.ToString() && c.producto_id == productoId && c.activo);
            return !exists;
        }
    }
}
