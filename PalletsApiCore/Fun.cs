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

        // La rama Peabody se decide por el tipo declarado del item, nunca por la forma del serial.
        public static bool EsPeabody(Product item) => item.type == "PEABODY";

        // EXTERNO: igual, por tipo declarado. Su serial es el EAN de productos_clientes_cd (cada escaneo = 1 unidad).
        public static bool EsExterno(Product item) => item.type == "EXTERNO";

        // asociar-productos: recorta item.serial y rechaza los items PEABODY cuyo serial no sea EAN/DUN valido
        // (mismo criterio que GET api/productos: 13/14 digitos). Devuelve el mensaje de error o null.
        // Los items de otro tipo siguen el camino generico, salvo que su serial tenga forma de EAN/DUN (400).
        public static string? NormalizarYValidarItems(IEnumerable<Product> items)
        {
            string? error = null;
            foreach (var item in items)
            {
                item.serial = item.serial?.Trim();
                if (error is null && EsPeabody(item) && !EsCodigoPeabody(item.serial))
                    error = $"El serial '{item.serial}' del producto {item.productCode ?? item.productId.ToString()} no es un EAN/DUN valido (numerico de 13 o 14 digitos). No se cargo nada.";
                // Series no Peabody son int (max 10 digitos): una forma de EAN/DUN con otro type es un type mal enviado.
                else if (error is null && EsExterno(item) && string.IsNullOrEmpty(item.serial))
                    error = $"El serial del producto externo {item.productCode ?? item.productId.ToString()} es requerido (EAN). No se cargo nada.";
                else if (error is null && !EsPeabody(item) && !EsExterno(item) && EsCodigoPeabody(item.serial))
                    error = $"El serial {item.serial} parece un EAN/DUN: el type debe ser PEABODY";
            }
            return error;
        }

        public sealed record PeabodyFila(string Serie, Guid ProductoId, string Codigo, string Descripcion,
            int CantXPallet, string? CodigoDun, decimal? UnidadesPorBulto);

        // GET api/pallets/productos: una linea por (producto, serie). cantidad = filas activas de ese codigo;
        // esDun/unidades = lo que representa UN item de ese codigo (DUN = unidades por bulto, EAN = 1).
        public static List<Product> LineasPeabody(IEnumerable<PeabodyFila> filasActivas) =>
            filasActivas
                .GroupBy(f => (f.ProductoId, f.Serie))
                .Select(g =>
                {
                    var f = g.First();
                    var esDun = !string.IsNullOrEmpty(f.CodigoDun) && f.Serie == f.CodigoDun;
                    return new Product
                    {
                        serial = f.Serie,
                        productId = f.ProductoId,
                        productCode = f.Codigo,
                        description = f.Descripcion,
                        type = "PEABODY",
                        maxCantByPallet = f.CantXPallet,
                        isAvailable = true,
                        esDun = esDun,
                        unidades = CalcularUnidades(esDun, f.UnidadesPorBulto),
                        cantidad = g.Count()
                    };
                })
                .OrderBy(l => l.productCode, StringComparer.Ordinal)
                .ThenBy(l => l.serial, StringComparer.Ordinal)
                .ToList();

        public sealed record ExternoFila(string Serie, Guid ProductoId, string? Codigo, string? Descripcion,
            int? CantidadPallet, decimal? PesoTotalPalletKg);

        // GET api/pallets/productos: EXTERNO una linea por (producto, serie). cantidad = filas activas (1 unidad cada una),
        // unidades = 1. Lineas propias: no se mezclan con las Peabody (que agrupan por producto+codigo).
        public static List<Product> LineasExterno(IEnumerable<ExternoFila> filasActivas) =>
            filasActivas
                .GroupBy(f => (f.ProductoId, f.Serie))
                .Select(g =>
                {
                    var f = g.First();
                    return new Product
                    {
                        serial = f.Serie,
                        productId = f.ProductoId,
                        productCode = f.Codigo,
                        description = f.Descripcion,
                        type = "EXTERNO",
                        maxCantByPallet = f.CantidadPallet ?? 0,
                        isAvailable = true,
                        unidades = 1,
                        cantidad = g.Count(),
                        pesoTotalPalletKg = f.PesoTotalPalletKg
                    };
                })
                .OrderBy(l => l.productCode, StringComparer.Ordinal)
                .ThenBy(l => l.serial, StringComparer.Ordinal)
                .ToList();

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
            // Una misma serie puede resolver a varios productos (caso importados, desambiguado por EAN),
            // por eso la disponibilidad se evalua por la combinacion serie + producto, no solo por serie.
            var exists = await context.cenker_prod_x_pallet
                .AnyAsync(c => c.serie == serial && c.producto_id == productoId && c.activo);
            return !exists;
        }
    }
}
