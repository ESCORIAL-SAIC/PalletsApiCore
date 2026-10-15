using Microsoft.EntityFrameworkCore;
using PalletsApiCore.Models;

namespace PalletsApiCore
{
    public static class Fun
    {
        // DUN: unidades por bulto (minimo 1, entero); EAN: siempre 1.
        public static int CalcularUnidades(bool esDun, decimal? unidadesPorBulto) =>
            esDun ? (int)Math.Max(Math.Round(unidadesPorBulto ?? 1, MidpointRounding.AwayFromZero), 1) : 1;

        // Cupo por pallet: una caja (DUN) entra entera o no entra. Tope <= 0 (dato mal cargado) = no entra.
        public static bool CabeEnPallet(int yaAsociadas, int unidades, int tope) =>
            tope > 0 && yaAsociadas + unidades <= tope;

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

        // PEABODY: serie = EAN 13 o DUN 14. Sin recortar ni parsear a int (excede int).
        // ponytail: sin validar contra DB; el DUN se resuelve por ud_producto.codigo_dun (#14).
        public static bool TryNormalizarCodigoPeabody(string? codigo, out string serie)
        {
            serie = codigo ?? string.Empty;
            return EsCodigoPeabody(codigo);
        }

        public static bool EsCodigoPeabody(string? c) =>
            c is { Length: 13 or 14 } && c.All(char.IsAsciiDigit);

        // PEABODY: valida que el codigo escaneado sea el EAN (codigogs1) o el DUN (codigo_dun) del producto
        // y calcula las unidades que representa. DUN = unidades por bulto, EAN = 1. Se calcula en servidor.
        // Si no coincide con ninguno devuelve false (el endpoint rechaza sin cargar ni bajar nada).
        public static bool TryUnidadesPeabody(string? serial, string? codigogs1, string? codigoDun,
            decimal? unidadesPorBulto, out int unidades)
        {
            unidades = 0;
            if (string.IsNullOrEmpty(serial))
                return false;
            var esDun = !string.IsNullOrEmpty(codigoDun) && serial == codigoDun;
            if (!esDun && !(!string.IsNullOrEmpty(codigogs1) && serial == codigogs1))
                return false;
            unidades = CalcularUnidades(esDun, unidadesPorBulto);
            return true;
        }

        public sealed class PeabodyMatch
        {
            public producto Producto { get; init; } = null!;
            public ud_producto Ud { get; init; } = null!;
        }

        // PEABODY: productos cuyo EAN (codigogs1) o DUN (codigo_dun) es exactamente `codigo` (se ignoran vacios).
        public static IQueryable<PeabodyMatch> PeabodyMatches(IQueryable<producto> productos, IQueryable<ud_producto> uds, string codigo) =>
            from p in productos
            join ud in uds on p.boextension_id equals (Guid?)ud.id
            where (ud.codigogs1 != null && ud.codigogs1 != "" && ud.codigogs1 == codigo)
               || (ud.codigo_dun != null && ud.codigo_dun != "" && ud.codigo_dun == codigo)
            select new PeabodyMatch { Producto = p, Ud = ud };

        // GET api/productos?tipo=PEABODY: valida el codigo (13/14 digitos) y resuelve el producto.
        // `buscar` ejecuta la consulta (en produccion PeabodyMatches contra la DB). Devuelve el status HTTP:
        // 200 producto, 400 codigo invalido, 404 inexistente, 409 ambiguo.
        public static async Task<(int Status, string? Error, Product? Producto)> ResolverPeabodyAsync(
            string numero, string tipo, Func<string, Task<List<PeabodyMatch>>> buscar)
        {
            var codigo = numero.Trim();
            if (!TryNormalizarCodigoPeabody(codigo, out _))
                return (400, "El codigo PEABODY debe ser numerico de 13 (EAN) o 14 (DUN) digitos", null);

            var matches = await buscar(codigo);
            if (matches.Count == 0)
                return (404, "No se encontro un producto con ese codigo EAN/DUN", null);
            if (matches.Select(m => m.Producto.id).Distinct().Count() > 1)
                return (409, "El codigo EAN/DUN corresponde a mas de un producto", null);

            var m0 = matches[0];
            var esDun = m0.Ud.codigo_dun == codigo;
            return (200, null, new Product
            {
                serial = codigo,
                productId = m0.Producto.id,
                productCode = m0.Producto.codigo,
                description = m0.Producto.descripcion,
                type = tipo,
                maxCantByPallet = m0.Ud.cant_x_pallet,
                isAvailable = true,
                esDun = esDun,
                unidades = CalcularUnidades(esDun, m0.Producto.unidadesporbulto)
            });
        }

        // Indices de las `cantidad` filas mas recientes (por fecha_alta). null si hay menos filas que `cantidad`
        // (no se baja nada parcial). fecha_alta es string dependiente de cultura: se parsea, lo no parseable
        // queda como mas antiguo y el empate lo gana el ultimo de la lista.
        public static List<int>? IndicesMasRecientes(IReadOnlyList<string?> fechasAlta, int cantidad)
        {
            if (cantidad < 0 || fechasAlta.Count < cantidad)
                return null;
            return Enumerable.Range(0, fechasAlta.Count)
                .OrderByDescending(i => DateTime.TryParse(fechasAlta[i], out var d) ? d : DateTime.MinValue)
                .ThenByDescending(i => i)
                .Take(cantidad)
                .ToList();
        }

        public static async Task<bool> IsAvailableAsync(string serial, Guid productoId, ESCORIALContext context)
        {
            // PEABODY: el mismo EAN/DUN se repite en todas las unidades.
            if (EsCodigoPeabody(serial))
                return true;
            // Una misma serie puede resolver a varios productos (caso importados, desambiguado por EAN),
            // por eso la disponibilidad se evalua por la combinacion serie + producto, no solo por serie.
            var exists = await context.cenker_prod_x_pallet
                .AnyAsync(c => c.serie == serial && c.producto_id == productoId && c.activo);
            return !exists;
        }
    }
}
