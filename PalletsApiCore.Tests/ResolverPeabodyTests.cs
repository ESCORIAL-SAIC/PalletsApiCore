using PalletsApiCore;
using PalletsApiCore.Models;
using Xunit;

namespace PalletsApiCore.Tests;

// Consulta real (Fun.PeabodyMatches) sobre listas en memoria: mismo predicado que ejecuta EF contra la DB.
public class ResolverPeabodyTests
{
    const string Ean = "7791234567890";
    const string Dun = "17791234567897";

    static (List<producto> P, List<ud_producto> U) Datos(params (string? ean, string? dun)[] filas)
    {
        var p = new List<producto>();
        var u = new List<ud_producto>();
        foreach (var (ean, dun) in filas)
        {
            var ud = new ud_producto { id = Guid.NewGuid(), codigogs1 = ean, codigo_dun = dun, cant_x_pallet = 100 };
            u.Add(ud);
            p.Add(new producto { id = Guid.NewGuid(), boextension_id = ud.id, codigo = "P" + p.Count, descripcion = "d", unidadesporbulto = 12 });
        }
        return (p, u);
    }

    static Task<(int Status, string? Error, Product? Producto)> Resolver(string numero, (List<producto> P, List<ud_producto> U) d) =>
        Fun.ResolverPeabodyAsync(numero, "PEABODY",
            c => Task.FromResult(Fun.PeabodyMatches(d.P.AsQueryable(), d.U.AsQueryable(), c).ToList()));

    [Fact]
    public async Task Ean_Resuelve_UnaUnidad()
    {
        var r = await Resolver(Ean, Datos((Ean, Dun)));
        Assert.Equal(200, r.Status);
        Assert.False(r.Producto!.esDun);
        Assert.Equal(1, r.Producto.unidades);
    }

    [Fact]
    public async Task Dun_Resuelve_NUnidades()
    {
        var r = await Resolver(Dun, Datos((Ean, Dun)));
        Assert.Equal(200, r.Status);
        Assert.True(r.Producto!.esDun);
        Assert.Equal(12, r.Producto.unidades);
    }

    [Fact]
    public async Task Inexistente_404() =>
        Assert.Equal(404, (await Resolver("7790000000000", Datos((Ean, Dun)))).Status);

    [Fact]
    public async Task Ambiguo_409() =>
        Assert.Equal(409, (await Resolver(Ean, Datos((Ean, null), (Ean, null)))).Status);

    [Fact]
    public async Task VaciosNoCoinciden_404() =>
        Assert.Equal(404, (await Resolver(Ean, Datos(("", ""), (null, null)))).Status);

    [Theory]
    [InlineData("779123456789")]     // 12
    [InlineData("177912345678975")]  // 15
    [InlineData("77912345678AB")]    // no numerico
    public async Task CodigoInvalido_400_SinConsultar(string numero)
    {
        var consultas = 0;
        var r = await Fun.ResolverPeabodyAsync(numero, "PEABODY", _ => { consultas++; return Task.FromResult(new List<Fun.PeabodyMatch>()); });
        Assert.Equal(400, r.Status);
        Assert.Equal(0, consultas);
    }

    [Fact]
    public async Task EspaciosAlrededor_SeIgnoran() =>
        Assert.Equal(200, (await Resolver($" {Ean} ", Datos((Ean, Dun)))).Status);
}
