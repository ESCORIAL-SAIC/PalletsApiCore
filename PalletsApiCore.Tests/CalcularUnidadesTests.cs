using Xunit;

namespace PalletsApiCore.Tests
{
    public class CalcularUnidadesTests
    {
        [Theory]
        [InlineData(false, 12, 1)]   // EAN: siempre 1
        [InlineData(false, null, 1)]
        [InlineData(true, 12, 12)]   // DUN
        [InlineData(true, 0, 1)]
        [InlineData(true, null, 1)]
        [InlineData(true, -3, 1)]
        public void Unidades(bool esDun, int? upb, int esperado)
            => Assert.Equal(esperado, Fun.CalcularUnidades(esDun, upb));
    }
}
