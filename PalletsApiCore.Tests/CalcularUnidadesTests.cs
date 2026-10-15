using PalletsApiCore;
using Xunit;

namespace PalletsApiCore.Tests
{
    public class CalcularUnidadesTests
    {
        [Theory]
        [InlineData(false, 12d, 1)]   // EAN
        [InlineData(false, null, 1)]
        [InlineData(true, 12d, 12)]   // DUN
        [InlineData(true, 0d, 1)]
        [InlineData(true, null, 1)]
        [InlineData(true, -3d, 1)]
        public void Calcula(bool esDun, double? upb, int esperado) =>
            Assert.Equal(esperado, Fun.CalcularUnidades(esDun, (decimal?)upb));
    }
}
