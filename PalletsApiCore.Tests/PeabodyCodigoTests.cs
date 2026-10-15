using PalletsApiCore;
using Xunit;

namespace PalletsApiCore.Tests
{
    public class PeabodyCodigoTests
    {
        [Theory]
        [InlineData("7790001234567")]
        [InlineData("17790001234564")]
        [InlineData("0000000000001")]
        public void Valido(string c)
        {
            Assert.True(Fun.TryNormalizarCodigoPeabody(c, out var s));
            Assert.Equal(c, s);
        }

        [Theory]
        [InlineData("779000123456")]
        [InlineData("177900012345645")]
        [InlineData("77900012345AB")]
        [InlineData(" 7790001234567")]
        [InlineData("7790001234567 ")]
        [InlineData("")]
        [InlineData(null)]
        public void Invalido(string? c) => Assert.False(Fun.TryNormalizarCodigoPeabody(c, out _));
    }
}
