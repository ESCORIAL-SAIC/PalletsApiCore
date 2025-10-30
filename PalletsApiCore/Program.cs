using Microsoft.EntityFrameworkCore;
using PalletsApiCore;
using PalletsApiCore.Models;
using System.Linq;

var builder = WebApplication.CreateBuilder();
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ESCORIALContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("EscorialPostgreSql")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Ok("Nothing to see here. Pallets API. There is no front-end. Checkout README.md for more information!"));

app.MapPost("/api/login", async (LoginDto login, ESCORIALContext context) =>
{
    var user = await context.empleado
        .Join(context.ud_empleado,
            e => e.boextension_id,
            u => u.id,
            (e, u) => new { e, u })
        .Join(context.v_persona,
            eu => eu.e.enteasociado_id,
            p => p.id,
            (eu, p) => new { eu.e, eu.u, p })
        .Where(x =>
            x.u.usuario_sistema != "" &&
            x.e.activestatus == 0 &&
            x.u.usuario_sistema == login.User &&
            x.u.password == login.Password)
        .Select(x => new
        {
            x.p.id,
            x.u.usuario_sistema,
            x.p.nombre
        })
        .FirstOrDefaultAsync();
    return user is null ? Results.NotFound("Usuario no encontrado.") : Results.Ok(user);
})
.WithName("login")
.WithOpenApi();

app.MapGet("api/pallets", async (string? numero, ESCORIALContext context) =>
{
    if (string.IsNullOrWhiteSpace(numero))
        return Results.BadRequest("El numero de pallet es requerido");
    var pallet = await context.cenker_pallets
        .Where(cenker_pallets => cenker_pallets.codigo == numero)
        .FirstOrDefaultAsync();
    return pallet is null ? Results.NotFound("Pallet no encontrado.") : Results.Ok(pallet);
})
.WithName("getPallets")
.WithOpenApi();

app.MapGet("api/pallets/productos", async (string? numero, ESCORIALContext context) =>
{
    if (string.IsNullOrWhiteSpace(numero))
        return Results.BadRequest("El numero de pallet es requerido");

    var palletId = await context.cenker_pallets
        .Where(p => p.codigo == numero)
        .Select(p => p.id)
        .FirstOrDefaultAsync();
    if (palletId == Guid.Empty)
        return Results.NotFound("Pallet no encontrado");
    var productosBase = await (
        from c in context.cenker_prod_x_pallet
        join p in context.producto on c.producto_id equals p.id
        join u in context.ud_producto on p.boextension_id equals u.id
        where c.pallet_id == palletId && c.activo
        select new
        {
            Serie = c.serie,
            ProductoId = p.id,
            p.codigo,
            p.descripcion,
            u.cant_x_pallet
        }
    )
    .AsNoTracking()
    .ToListAsync();
    var series = productosBase.Select(x => int.Parse(x.Serie)).Distinct().ToList();
    var productoIds = productosBase.Select(x => x.ProductoId).Distinct().ToList();
    var etiquetas = await context.vp_etiquetas
        .Where(v => series.Contains((int)v.numero!) && productoIds.Contains((Guid)v.producto_id!))
        .AsNoTracking()
        .ToListAsync();
    var result = productosBase.Select(x =>
    {
        var etiqueta = etiquetas.FirstOrDefault(e =>
            e.numero == int.Parse(x.Serie) && e.producto_id == x.ProductoId);

        return new Product
        {
            serial = int.Parse(x.Serie),
            productId = x.ProductoId,
            productCode = x.codigo,
            description = x.descripcion,
            type = etiqueta?.tipo,
            maxCantByPallet = x.cant_x_pallet,
            isAvailable = true
        };
    }).ToList();

    return Results.Ok(result);
})
.WithName("getProductosByPallet")
.WithOpenApi();



app.MapGet("api/productos", async (string? tipo, int? numero, ESCORIALContext context) =>
{
    if (string.IsNullOrWhiteSpace(tipo))
        return Results.BadRequest("El tipo de producto es requerido");
    if (!numero.HasValue)
        return Results.BadRequest("El numero de producto es requerido");

    var etiqueta = await context.vp_etiquetas
        .FirstOrDefaultAsync(vp_etiquetas => vp_etiquetas.tipo == tipo && vp_etiquetas.numero == numero.Value);
    if (etiqueta is null)
        return Results.NotFound("No se encontro el numero de serie");

    var controlFinal = await context.api_pallets_controlfinal
        .FirstOrDefaultAsync(c =>
            c.Numero == numero &&
            (c.Usuario == "postventa"
                || (c.PuestoControl == "Control Final"
                    && (c.ControladorEstado || c.ReparadorEstado))));

    if (controlFinal is null)
        return Results.NotFound("El numero de serie no posee control final");

    var producto = await context.producto
        .FirstOrDefaultAsync(producto => producto.id == etiqueta.producto_id);
    if (producto is null)
        return Results.NotFound("No se encontro un producto correspondiente al numero de serie");

    var udProducto = await context.ud_producto
        .FirstOrDefaultAsync(ud_producto => ud_producto.id == producto.boextension_id);
    if (udProducto is null)
        return Results.NotFound("No se encontro la unidad de negocio del producto");

    var product = new Product
    {
        serial = (int)etiqueta.numero!,
        productId = producto.id,
        productCode = producto.codigo,
        description = producto.descripcion,
        type = etiqueta.tipo,
        maxCantByPallet = udProducto.cant_x_pallet,
        isAvailable = await Fun.IsAvailableAsync((int)etiqueta.numero, context)
    };

    return Results.Ok(product);
})
.WithName("getProductos")
.WithOpenApi();

app.MapPost("api/pallets/asociar-productos", async (cenker_pallets pallet, ESCORIALContext context) =>
{
    await using var transaction = await context.Database.BeginTransactionAsync();

    try
    {
        if (string.IsNullOrEmpty(pallet.codigo))
            return Results.BadRequest("El codigo de pallet es requerido.");
        if (pallet.Products.Count < 1)
            return Results.BadRequest("No hay productos para asociar.");

        var palletId = await context.cenker_pallets
            .FirstOrDefaultAsync(p => p.codigo == pallet.codigo);

        if (palletId is null)
            return Results.NotFound("No se encontro el pallet.");

        var productosAsociar = pallet.Products.Where(p => !p.deleted);
        var productosDesasociar = pallet.Products.Where(p => p.deleted);

        var palletDesasociar = pallet.Products.All(p => p.deleted);

        if (palletDesasociar)
        {
            palletId.transferir = false;
            palletId.procesado_transactor = false;
            palletId.fecha_procesado = null;
        }

        var existentes = await context.cenker_prod_x_pallet
            .Where(p => p.pallet_id == palletId.id)
            .ToListAsync();

        foreach (var item in productosAsociar)
        {
            var exists = existentes
                .FirstOrDefault(e => e.serie == item.serial.ToString() && e.activo) is not null;
            if (exists)
                continue;
            var pXp = new cenker_prod_x_pallet
            {
                id = Guid.NewGuid(),
                pallet_id = palletId.id,
                producto_id = item.productId,
                activo = true,
                fecha_alta = DateTime.Now.ToString(),
                fecha_modificacion = DateTime.Now.ToString(),
                serie = item.serial.ToString()
            };
            context.cenker_prod_x_pallet.Add(pXp);

            var auditoria = new cenker_pallets_auditoria
            {
                id = Guid.NewGuid(),
                fecha = DateTime.Now,
                evento = "ASOCIAR PRODUCTO",
                objeto = "web.cenker_prod_x_pallet",
                elemento_asociado = pXp.id,
                valor_anterior = string.Empty,
                valor_actual = pXp.serie,
                usuario = pallet.Usuario
            };
            context.cenker_pallets_auditoria.Add(auditoria);
        }

        foreach (var item in productosDesasociar)
        {
            var pXp = existentes
                .FirstOrDefault(e => e.serie == item.serial.ToString() && e.activo);
            if (pXp is null)
                continue;
            pXp.activo = false;
            pXp.fecha_modificacion = DateTime.Now.ToString();
            var auditoria = new cenker_pallets_auditoria
            {
                id = Guid.NewGuid(),
                fecha = DateTime.Now,
                evento = "DESASOCIAR PRODUCTO",
                objeto = "web.cenker_prod_x_pallet",
                elemento_asociado = pXp.id,
                valor_anterior = pXp.serie,
                valor_actual = string.Empty,
                usuario = pallet.Usuario
            };
            context.cenker_pallets_auditoria.Add(auditoria);
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Results.NoContent();
    }
    catch (Exception)
    {
        await transaction.RollbackAsync();
        return Results.BadRequest("Se produjo una excepci�n no controlada. Los cambios no se guardar�n.");
    }
    finally
    {
        transaction.Dispose();
    }
})
.WithName("asociarProductos")
.WithOpenApi();

app.MapPost("api/pallets/transferirExpedicion", async (List<cenker_pallets> pallets, ESCORIALContext context) =>
{
    if (pallets.Count < 1)
        return Results.BadRequest("No hay pallets para transferir.");
    foreach (var pallet in pallets)
    {
        var palletDb = await context.cenker_pallets
            .FirstOrDefaultAsync(p => p.codigo == pallet.codigo);
        if (palletDb is null)
            continue;
        palletDb.transferir = true;
    }
    context.SaveChanges();
    return Results.NoContent();
})
.WithName("transferirExpedicion")
.WithOpenApi();

await app.RunAsync();