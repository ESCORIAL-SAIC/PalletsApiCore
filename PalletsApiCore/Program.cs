using Microsoft.EntityFrameworkCore;
using PalletsApiCore;
using PalletsApiCore.Models;

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
        .Where(cenker_pallets => cenker_pallets.codigo == numero)
        .Select(cenker_pallets => cenker_pallets.id)
        .FirstOrDefaultAsync();
    var palletProducts = await context.cenker_prod_x_pallet
        .Where(cenker_prod_x_pallet => cenker_prod_x_pallet.pallet_id == palletId && cenker_prod_x_pallet.activo)
        .ToListAsync();
    var productos = new List<Product>();
    foreach (var item in palletProducts)
    {
        var producto = await context.producto
            .FirstOrDefaultAsync(producto => producto.id == item.producto_id);
        var etiqueta = await context.vp_etiquetas
            .FirstOrDefaultAsync(vp_etiquetas => vp_etiquetas.numero == int.Parse(item.serie) && vp_etiquetas.producto_id == producto!.id);
        if (producto is not null)
            productos.Add(new Product
            {
                serial = int.Parse(item.serie),
                productId = producto.id,
                productCode = producto.codigo,
                description = producto.descripcion,
                type = etiqueta?.tipo,
                maxCantByPallet = 1,
                isAvailable = true
            });
    }
    return Results.Ok(productos);
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

    var controlFinal = await context.aux_controlcalidad
        .Where(c => c.Etiqueta == numero &&
            c.ControladorEstado &&
            c.PuestoControlN == "Control Final")
        .FirstOrDefaultAsync();

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

app.MapGet("api/cocinas", async (int? numero, ESCORIALContext context) =>
{
    var query = context.etiquetas_maestro_cocinas.AsQueryable();
    if (numero.HasValue)
        query = query.Where(c => c.numero == numero.Value);
    var cocinas = await query
        .ToListAsync();
    var productos = new List<Product>();
    foreach (var cocina in cocinas)
    {
        var producto = await context.producto
            .FirstOrDefaultAsync(producto => producto.codigo == cocina.idproducto);
        if (producto is not null)
            productos.Add(new Product
            {
                serial = cocina.numero,
                productId = producto.id,
                productCode = producto.codigo,
                description = producto.descripcion,
                type = "COCINA",
                maxCantByPallet = 8,
                isAvailable = await Fun.IsAvailableAsync(cocina.numero, context)
            });
    }
    if (numero.HasValue && productos.Count < 1)
        return Results.NotFound();
    return productos.Count == 1 ? Results.Ok(productos.FirstOrDefault()) : Results.Ok(productos);
})
.WithName("getCocinas")
.WithOpenApi();

app.MapGet("api/termos", async (int? numero, ESCORIALContext context) =>
{
    var query = context.etiquetas_maestro_termotanques.AsQueryable();
    if (numero.HasValue)
        query = query.Where(c => c.numero == numero.Value);
    var termos = await query
        .ToListAsync();
    var productos = new List<Product>();
    foreach (var termo in termos)
    {
        var producto = await context.producto
            .FirstOrDefaultAsync(producto => producto.codigo == termo.idproducto);
        if (producto is not null)
            productos.Add(new Product
            {
                serial = termo.numero,
                productId = producto.id,
                productCode = producto.codigo,
                description = producto.descripcion,
                type = "TERMOTANQUE",
                maxCantByPallet = 12,
                isAvailable = await Fun.IsAvailableAsync(termo.numero, context)
            });
    }
    if (numero.HasValue && productos.Count < 1)
        return Results.NotFound();
    return productos.Count == 1 ? Results.Ok(productos.FirstOrDefault()) : Results.Ok(productos);
})
.WithName("getTermos")
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

await app.RunAsync()
    ;