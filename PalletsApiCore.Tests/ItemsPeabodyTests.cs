using PalletsApiCore;
using PalletsApiCore.Models;
using Xunit;

namespace PalletsApiCore.Tests;

public class ItemsPeabodyTests
{
    static Product P(string? serial, string type = "PEABODY") =>
        new() { serial = serial!, type = type, productCode = "X1", productId = Guid.NewGuid() };

    [Theory]
    [InlineData("779123456789")]      // 12
    [InlineData("177912345678975")]   // 15
    [InlineData("77912345678AB")]     // no numerico
    [InlineData("")]
    [InlineData(null)]
    public void Peabody_SerialInvalido_Rechaza(string? serial) =>
        Assert.NotNull(Fun.NormalizarYValidarItems(new[] { P("7791234567890"), P(serial) }));

    [Fact]
    public void Peabody_SerialValido_Ok() =>
        Assert.Null(Fun.NormalizarYValidarItems(new[] { P("7791234567890"), P("17791234567897") }));

    [Fact]
    public void Serial_ConEspacios_SeTrimea()
    {
        var item = P(" 7791234567890 ");
        Assert.Null(Fun.NormalizarYValidarItems(new[] { item }));
        Assert.Equal("7791234567890", item.serial);
    }

    [Fact]
    public void NoPeabody_13Digitos_NoEntraAlCaminoPeabody()
    {
        var item = P("7791234567890", "IMPORTADO");
        Assert.Null(Fun.NormalizarYValidarItems(new[] { item }));
        Assert.False(Fun.EsPeabody(item));
    }

    [Fact]
    public void NoPeabody_SerialNoValidoPorForma_NoSeRechaza() =>
        Assert.Null(Fun.NormalizarYValidarItems(new[] { P("260400005", "COCINA"), P("abc", "IMPORTADO") }));

    [Fact]
    public void Peabody_SeDecidePorTipo() => Assert.True(Fun.EsPeabody(P("cualquiera")));
}
