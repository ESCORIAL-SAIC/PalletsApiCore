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
        // ponytail: DUN no se resuelve contra DB aun (ud_producto no tiene CODIGO_DUN); agregar con #14.
        public static bool TryNormalizarCodigoPeabody(string? codigo, out string serie)
        {
            serie = codigo ?? string.Empty;
            return EsCodigoPeabody(codigo);
        }

        public static bool EsCodigoPeabody(string? c) =>
            c is { Length: 13 or 14 } && c.All(char.IsAsciiDigit);

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
