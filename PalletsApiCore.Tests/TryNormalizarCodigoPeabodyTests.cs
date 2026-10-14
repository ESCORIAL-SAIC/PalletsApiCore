using PalletsApiCore;
using Xunit;

namespace PalletsApiCore.Tests;

public class TryNormalizarCodigoPeabodyTests
{
    [Theory]
    [InlineData("7791234567890", "7791234567890")]
    [InlineData("17791234567897", "17791234567897")]
    [InlineData("  7791234567890 ", "7791234567890")]
    public void Valido(string entrada, string esperado)
    {
        Assert.True(Fun.TryNormalizarCodigoPeabody(entrada, out var c));
        Assert.Equal(esperado, c);
    }

    [Theory]
    [InlineData("779123456789")]
    [InlineData("779123456789012")]
    [InlineData("77912345678AB")]
    [InlineData("")]
    [InlineData(null)]
    public void Invalido(string? entrada) => Assert.False(Fun.TryNormalizarCodigoPeabody(entrada, out _));
}
