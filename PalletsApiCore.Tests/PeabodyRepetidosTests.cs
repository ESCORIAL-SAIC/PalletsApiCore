using PalletsApiCore;
using Xunit;

namespace PalletsApiCore.Tests;

public class PeabodyRepetidosTests
{
    const string Ean = "7791234567890";
    const string Dun = "17791234567897";

    static int U(string serial, decimal? ub = 12) =>
        Fun.TryUnidadesPeabody(serial, Ean, Dun, ub, out var u) ? u : -1;

    static string F(int dia) => new DateTime(2026, 1, dia).ToString();

    [Fact]
    public void DosEanIguales_DosFilas() => Assert.Equal(2, U(Ean) + U(Ean));

    [Fact]
    public void Dun_NUnidades() => Assert.Equal(12, U(Dun));

    [Fact]
    public void Dun_SinUnidadesPorBulto_MinimoUno() => Assert.Equal(1, U(Dun, null));

    [Theory]
    [InlineData("7790000000000")]   // EAN de otro producto
    [InlineData("17790000000007")]  // DUN de otro producto
    [InlineData("")]
    [InlineData(null)]
    public void CodigoQueNoPerteneceAlProducto_Rechaza(string? serial) =>
        Assert.False(Fun.TryUnidadesPeabody(serial, Ean, Dun, 12, out _));

    [Fact]
    public void ProductoSinEanNiDun_Rechaza() =>
        Assert.False(Fun.TryUnidadesPeabody(Ean, null, null, 12, out _));

    [Fact]
    public void DunQueNoEntra_Rechaza()
    {
        Assert.False(Fun.CabeEnPallet(95, 12, 100));
        Assert.True(Fun.CabeEnPallet(88, 12, 100));
    }

    [Fact]
    public void Desasociar_Ean_BajaLaMasReciente() =>
        Assert.Equal(new[] { 1 }, Fun.IndicesMasRecientes(new List<string?> { F(2), F(20), F(10) }, 1));

    [Fact]
    public void Desasociar_Dun_BajaNFilasMasRecientes()
    {
        var f = new List<string?> { F(2), F(20), F(10), F(5), F(15) };
        Assert.Equal(new[] { 1, 4, 2 }, Fun.IndicesMasRecientes(f, 3));
    }

    [Fact]
    public void Desasociar_ConMenosFilasQueUnidades_Rechaza()
    {
        var f = new List<string?> { F(2), F(3), F(4) };
        Assert.Null(Fun.IndicesMasRecientes(f, 12));
        Assert.Null(Fun.IndicesMasRecientes(new List<string?>(), 1));
        Assert.Equal(3, Fun.IndicesMasRecientes(f, 3)!.Count);
    }
}
