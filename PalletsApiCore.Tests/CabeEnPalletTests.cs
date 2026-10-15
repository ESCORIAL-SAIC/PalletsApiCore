using PalletsApiCore;
using Xunit;

namespace PalletsApiCore.Tests;

public class CabeEnPalletTests
{
    [Fact] public void Exacto() => Assert.True(Fun.CabeEnPallet(48, 12, 60));
    [Fact] public void ExcedidoPorDun() => Assert.False(Fun.CabeEnPallet(50, Fun.CalcularUnidades(true, 12), 60));
    [Fact] public void CompletadoConEan() => Assert.True(Fun.CabeEnPallet(59, Fun.CalcularUnidades(false, 12), 60));
    [Fact] public void UnidadesPorBultoNuloOCero_CaeA1()
    {
        Assert.Equal(1, Fun.CalcularUnidades(true, null));
        Assert.Equal(1, Fun.CalcularUnidades(true, 0));
        Assert.True(Fun.CabeEnPallet(59, Fun.CalcularUnidades(true, 0), 60));
    }
    [Fact] public void TopeCeroRechaza() => Assert.False(Fun.CabeEnPallet(0, 1, 0));
}
