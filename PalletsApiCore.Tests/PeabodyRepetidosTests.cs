using PalletsApiCore;
using Xunit;

namespace PalletsApiCore.Tests;

public class PeabodyRepetidosTests
{
    [Fact]
    public void DosEanIguales_DosFilas() =>
        Assert.Equal(2, new[] { "7791234567890", "7791234567890" }
            .Sum(s => Fun.UnidadesPeabody(s, "17791234567897", 12)));

    [Fact]
    public void Dun_NUnidades() =>
        Assert.Equal(12, Fun.UnidadesPeabody("17791234567897", "17791234567897", 12));

    [Fact]
    public void Dun_SinUnidadesPorBulto_MinimoUno() =>
        Assert.Equal(1, Fun.UnidadesPeabody("17791234567897", "17791234567897", null));

    [Fact]
    public void DunQueNoEntra_Rechaza()
    {
        Assert.False(Fun.EntraEnPallet(95, 12, 100));
        Assert.True(Fun.EntraEnPallet(88, 12, 100));
    }

    [Fact]
    public void Desasociar_EligeMasReciente()
    {
        var f = new List<string?> { new DateTime(2026, 1, 2).ToString(), new DateTime(2026, 3, 1).ToString(), new DateTime(2026, 2, 1).ToString() };
        Assert.Equal(1, Fun.IndiceMasReciente(f));
    }
}
