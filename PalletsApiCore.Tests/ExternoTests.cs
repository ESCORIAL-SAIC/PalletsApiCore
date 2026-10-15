using PalletsApiCore;
using PalletsApiCore.Models;
using Xunit;

namespace PalletsApiCore.Tests
{
    public class ExternoTests
    {
        static Product E(string? serial, string type = "EXTERNO") => new() { serial = serial, type = type, productId = Guid.NewGuid() };

        [Fact]
        public void EsExterno_PorType_NoPorSerial()
        {
            Assert.True(Fun.EsExterno(E("7791234567890")));
            Assert.False(Fun.EsExterno(E("7791234567890", "PEABODY")));
            Assert.False(Fun.EsExterno(E("123456", "COCINA")));
            Assert.False(Fun.EsPeabody(E("7791234567890")));
        }

        [Fact]
        public void Externo_ConEan13_NoDa400_YSeTrimea()
        {
            var item = E(" 7791234567890 ");
            Assert.Null(Fun.NormalizarYValidarItems(new[] { item }));
            Assert.Equal("7791234567890", item.serial);
        }

        [Fact]
        public void Externo_SerialVacio_Da400() =>
            Assert.NotNull(Fun.NormalizarYValidarItems(new[] { E("  ") }));

        [Fact]
        public void NoExterno_ConForma_EanDun_SigueDando400() =>
            Assert.NotNull(Fun.NormalizarYValidarItems(new[] { E("7791234567890", "COCINA") }));

        [Fact]
        public void LineasExterno_UnaPorProductoYSerie_ConCantidadYOrden()
        {
            var a = Guid.NewGuid(); var b = Guid.NewGuid();
            var l = Fun.LineasExterno(new[]
            {
                new Fun.ExternoFila("222", b, "B", "desc b", 10, 5m),
                new Fun.ExternoFila("111", a, "A", "desc a", null, null),
                new Fun.ExternoFila("111", a, "A", "desc a", null, null),
                new Fun.ExternoFila("222", b, "B", "desc b", 10, 5m),
                new Fun.ExternoFila("222", b, "B", "desc b", 10, 5m),
            });
            Assert.Equal(2, l.Count);
            Assert.Equal("A", l[0].productCode);
            Assert.Equal(2, l[0].cantidad);
            Assert.Equal(0, l[0].maxCantByPallet);
            Assert.Equal(1, l[0].unidades);
            Assert.Equal("EXTERNO", l[0].type);
            Assert.Equal(3, l[1].cantidad);
            Assert.Equal(5m, l[1].pesoTotalPalletKg);
            Assert.Null(l[1].esDun);
        }

        [Fact]
        public void LineasExterno_NoMezclaConPeabody()
        {
            var id = Guid.NewGuid();
            var ext = Fun.LineasExterno(new[] { new Fun.ExternoFila("7791234567890", id, "X", "d", 4, null) });
            var pea = Fun.LineasPeabody(new[] { new Fun.PeabodyFila("7791234567890", id, "X", "d", 4, null, null) });
            Assert.Equal("EXTERNO", Assert.Single(ext).type);
            Assert.Equal("PEABODY", Assert.Single(pea).type);
        }

        [Fact]
        public void Cupo_Externo_UnaUnidadPorItem()
        {
            Assert.True(Fun.CabeEnPallet(9, 1, 10));
            Assert.False(Fun.CabeEnPallet(10, 1, 10));
            Assert.False(Fun.CabeEnPallet(0, 1, 0));
        }
    }
}
