using PalletsApiCore;
using Xunit;

namespace PalletsApiCore.Tests;

public class LineasPeabodyTests
{
    const string Ean = "7791234567890";
    const string Dun = "17791234567897";
    static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid();

    static Fun.PeabodyFila F(string serie, Guid prod) => new(serie, prod, "P", "d", 100, Dun, 12);

    [Fact]
    public void EanMasDun_MismoProducto_DosLineas()
    {
        // 2 filas EAN + 12 filas del DUN (1 caja)
        var filas = new[] { F(Ean, A), F(Ean, A) }.Concat(Enumerable.Range(0, 12).Select(_ => F(Dun, A)));
        var l = Fun.LineasPeabody(filas);

        Assert.Equal(2, l.Count);
        var ean = Assert.Single(l, x => x.serial == Ean);
        var dun = Assert.Single(l, x => x.serial == Dun);
        Assert.Equal((false, 1, 2), (ean.esDun!.Value, ean.unidades!.Value, ean.cantidad!.Value));
        Assert.Equal((true, 12, 12), (dun.esDun!.Value, dun.unidades!.Value, dun.cantidad!.Value));
        Assert.All(l, x => Assert.Equal("PEABODY", x.type));
    }

    [Fact]
    public void ProductosDistintos_MismoCodigo_LineasSeparadas() =>
        Assert.Equal(2, Fun.LineasPeabody(new[] { F(Ean, A), F(Ean, B) }).Count);

    [Fact]
    public void SinCodigoDun_EsEan()
    {
        var l = Assert.Single(Fun.LineasPeabody(new[] { new Fun.PeabodyFila(Ean, A, "P", "d", 100, null, 12) }));
        Assert.False(l.esDun);
        Assert.Equal(1, l.unidades);
    }
}
