using PalletsApiCore;
using Xunit;

namespace PalletsApiCore.Tests
{
    public class TryNormalizarSerieTests
    {
        // --- IMPORTADO: 20 digitos -> ultimos 9 ---

        [Fact]
        public void Importado_20Digitos_TomaUltimos9()
        {
            // "12345678901234567890" -> ultimos 9 = "234567890"
            var ok = Fun.TryNormalizarSerie("12345678901234567890", "IMPORTADO", out var serie);
            Assert.True(ok);
            Assert.Equal(234567890, serie);
        }

        [Fact]
        public void Importado_20Digitos_Ultimos9ConCerosALaIzquierda()
        {
            // 11 digitos + "000123456" al final -> ultimos 9 = "000123456" -> 123456
            var ok = Fun.TryNormalizarSerie("99999999999000123456", "IMPORTADO", out var serie);
            Assert.True(ok);
            Assert.Equal(123456, serie);
        }

        [Fact]
        public void Importado_Ultimos9NoNumericos_False()
        {
            // ultimos 9 = "3456789AB" -> no parsea
            var ok = Fun.TryNormalizarSerie("123456789013456789AB", "IMPORTADO", out var serie);
            Assert.False(ok);
            Assert.Equal(0, serie);
        }

        [Fact]
        public void Importado_MenosDe9Caracteres_UsaStringCompleto()
        {
            var ok = Fun.TryNormalizarSerie("12345", "IMPORTADO", out var serie);
            Assert.True(ok);
            Assert.Equal(12345, serie);
        }

        [Fact]
        public void Importado_Exactamente9Digitos_UsaCompleto()
        {
            var ok = Fun.TryNormalizarSerie("123456789", "IMPORTADO", out var serie);
            Assert.True(ok);
            Assert.Equal(123456789, serie);
        }

        [Fact]
        public void Importado_UltimosDigitosMaximos_NoDesborda()
        {
            // ultimos 9 = "999999999" (< int.MaxValue 2147483647)
            var ok = Fun.TryNormalizarSerie("00000000000999999999", "IMPORTADO", out var serie);
            Assert.True(ok);
            Assert.Equal(999999999, serie);
        }

        // --- No IMPORTADO: sin recorte ---

        [Fact]
        public void NoImportado_SerieNumericaNormal_ValorCompleto()
        {
            var ok = Fun.TryNormalizarSerie("456789", "LAVARROPAS", out var serie);
            Assert.True(ok);
            Assert.Equal(456789, serie);
        }

        [Fact]
        public void NoImportado_20Digitos_DesbordaInt_False()
        {
            var ok = Fun.TryNormalizarSerie("12345678901234567890", "LAVARROPAS", out var serie);
            Assert.False(ok);
            Assert.Equal(0, serie);
        }

        [Fact]
        public void NoImportado_10Digitos_NoSeRecorta()
        {
            // 1234567890 cabe en int y NO debe recortarse a 234567890
            var ok = Fun.TryNormalizarSerie("1234567890", "LAVARROPAS", out var serie);
            Assert.True(ok);
            Assert.Equal(1234567890, serie);
            Assert.NotEqual(234567890, serie);
        }

        [Fact]
        public void NoImportado_ConEspaciosAlrededor_SeTrimea()
        {
            var ok = Fun.TryNormalizarSerie("  123456  ", "LAVARROPAS", out var serie);
            Assert.True(ok);
            Assert.Equal(123456, serie);
        }

        // --- null / vacio / whitespace ---

        [Fact]
        public void Null_False()
        {
            var ok = Fun.TryNormalizarSerie(null, "IMPORTADO", out var serie);
            Assert.False(ok);
            Assert.Equal(0, serie);
        }

        [Fact]
        public void Vacio_False()
        {
            var ok = Fun.TryNormalizarSerie("", "LAVARROPAS", out var serie);
            Assert.False(ok);
            Assert.Equal(0, serie);
        }

        [Fact]
        public void Whitespace_False()
        {
            var ok = Fun.TryNormalizarSerie("     ", "IMPORTADO", out var serie);
            Assert.False(ok);
            Assert.Equal(0, serie);
        }

        // --- extras: robustez ---

        [Theory]
        [InlineData("abc", "LAVARROPAS")]
        [InlineData("12.5", "LAVARROPAS")]
        [InlineData("12 34", "LAVARROPAS")]
        public void NoImportado_NoNumerico_False(string numero, string tipo)
        {
            var ok = Fun.TryNormalizarSerie(numero, tipo, out var serie);
            Assert.False(ok);
            Assert.Equal(0, serie);
        }

        [Fact]
        public void Importado_TrimSeAplicaAntesDelRecorte()
        {
            // Con espacios: tras trim queda "12345678901234567890" -> ultimos 9 = "234567890"
            var ok = Fun.TryNormalizarSerie("   12345678901234567890   ", "IMPORTADO", out var serie);
            Assert.True(ok);
            Assert.Equal(234567890, serie);
        }
    }
}
