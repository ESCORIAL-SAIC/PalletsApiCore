using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PalletsApiCore;
using PalletsApiCore.Models;
using System.Linq;
using System.Reflection;
using System.Text.Json;

var builder = WebApplication.CreateBuilder();
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ESCORIALContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("EscorialPostgreSql")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ESCORIALContext>("db", tags: new[] { "ready" });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

static string AppVersion() =>
    (Assembly.GetEntryAssembly()?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0").Split('+')[0];

app.MapGet("/version", () => Results.Ok(new { version = AppVersion() }))
    .WithName("version")
    .WithOpenApi();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            version = AppVersion(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
});

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
            u.cant_x_pallet,
            u.codigo_dun,
            p.unidadesporbulto
        }
    )
    .AsNoTracking()
    .ToListAsync();
    // PEABODY: serie = EAN13/DUN14 (no entra en int ni en la vista de etiquetas)
    var series = productosBase.Where(x => !Fun.EsCodigoPeabody(x.Serie))
        .Select(x => int.Parse(x.Serie)).Distinct().ToList();
    var productoIds = productosBase.Select(x => x.ProductoId).Distinct().ToList();
    var etiquetas = await context.vp_etiquetas_con_importados
        .Where(v => series.Contains((int)v.numero!) && productoIds.Contains((Guid)v.producto_id!))
        .AsNoTracking()
        .ToListAsync();
    // PEABODY: una linea por (producto, codigo); cantidad = filas activas de ese codigo.
    var peabodyAgrupado = Fun.LineasPeabody(productosBase.Where(x => Fun.EsCodigoPeabody(x.Serie))
        .Select(x => new Fun.PeabodyFila(x.Serie, x.ProductoId, x.codigo, x.descripcion, x.cant_x_pallet, x.codigo_dun, x.unidadesporbulto)));
    // EXTERNO: producto_id apunta a productos_clientes_cd (no a producto, asi que productosBase no lo incluye); lineas propias.
    var externosBase = await (
        from c in context.cenker_prod_x_pallet
        join x in context.productos_clientes_cd on c.producto_id equals x.id
        where c.pallet_id == palletId && c.activo
        select new Fun.ExternoFila(c.serie, x.id, x.codigo_producto, x.descripcion, x.cantidad_pallet, x.peso_total_pallet_kg)
    ).AsNoTracking().ToListAsync();
    var externosAgrupado = Fun.LineasExterno(externosBase);
    var result = productosBase.Where(x => !Fun.EsCodigoPeabody(x.Serie)).Select(x =>
    {
        var peabody = Fun.EsCodigoPeabody(x.Serie);
        var etiqueta = peabody ? null : etiquetas.FirstOrDefault(e =>
            e.numero == int.Parse(x.Serie) && e.producto_id == x.ProductoId);

        return new Product
        {
            serial = x.Serie,
            productId = x.ProductoId,
            productCode = x.codigo,
            description = x.descripcion,
            type = peabody ? "PEABODY" : etiqueta?.tipo,
            maxCantByPallet = x.cant_x_pallet,
            isAvailable = true
        };
    }).Concat(peabodyAgrupado).Concat(externosAgrupado).ToList();

    return Results.Ok(result);
})
.WithName("getProductosByPallet")
.WithOpenApi();



app.MapGet("api/productos", async (string? tipo, string? numero, string? ean, ESCORIALContext context) =>
{
    if (string.IsNullOrWhiteSpace(tipo))
        return Results.BadRequest("El tipo de producto es requerido");
    if (string.IsNullOrWhiteSpace(numero))
        return Results.BadRequest("El numero de producto es requerido");

    if (tipo == "EXTERNO")
    {
        var ean13 = numero.Trim();
        var filas = await context.productos_clientes_cd
            .Where(x => x.codigo_ean != null && x.codigo_ean == ean13)
            .AsNoTracking().ToListAsync();
        if (filas.Count == 0)
            return Results.NotFound("No se encontro un producto con ese codigo EAN");
        if (filas.Count > 1)
            return Results.Conflict("El codigo EAN corresponde a mas de un producto");
        var f = filas[0];
        if ((f.cantidad_pallet ?? 0) <= 0)
            return Results.UnprocessableEntity("El producto no tiene cantidad por pallet configurada");
        return Results.Ok(new Product
        {
            serial = ean13,
            productId = f.id,
            productCode = f.codigo_producto,
            description = f.descripcion,
            type = tipo,
            maxCantByPallet = f.cantidad_pallet!.Value,
            isAvailable = true,
            pesoTotalPalletKg = f.peso_total_pallet_kg
        });
    }

    if (tipo == "PEABODY")
    {
        // Sin serie, control final ni EAN: el codigo escaneado es EAN o DUN (match exacto, 13/14 digitos).
        var (status, error, resuelto) = await Fun.ResolverPeabodyAsync(numero, tipo,
            c => Fun.PeabodyMatches(context.producto, context.ud_producto, c).ToListAsync());
        return status switch
        {
            200 => Results.Ok(resuelto),
            400 => Results.BadRequest(error),
            404 => Results.NotFound(error),
            _ => Results.Conflict(error)
        };
    }

    if (!Fun.TryNormalizarSerie(numero, tipo, out var serie))
        return Results.BadRequest("El numero de producto no es valido");

    // La serie puede resolver a varios productos (caso importados); el EAN desambigua cuál es.
    var candidatos = await (
        from e in context.vp_etiquetas_con_importados
        join p in context.producto on e.producto_id equals (Guid?)p.id
        join ud in context.ud_producto on p.boextension_id equals (Guid?)ud.id
        where e.tipo == tipo && e.numero == serie
        select new { producto = p, udProducto = ud }
    ).ToListAsync();

    if (candidatos.Count == 0)
        return Results.NotFound("No se encontro el numero de serie");

    // Control final: aplica a todo tipo salvo IMPORTADO (comportamiento existente, por numero de serie).
    if (tipo != "IMPORTADO")
    {
        var controlFinal = await context.api_pallets_controlfinal
            .FirstOrDefaultAsync(c =>
                c.Numero == serie &&
                (c.Usuario == "postventa"
                    || (c.PuestoControl == "Control Final"
                        && (c.ControladorEstado || c.ReparadorEstado))));

        if (controlFinal is null)
            return Results.NotFound("El numero de serie no posee control final");
    }

    // Selección del producto. Para tipos que validan EAN (todos salvo COCINA/TERMOTANQUE),
    // el EAN escaneado elige cuál de los productos de la serie corresponde.
    var seleccionado = candidatos[0];
    if (tipo != "COCINA" && tipo != "TERMOTANQUE")
    {
        if (string.IsNullOrWhiteSpace(ean))
            return Results.BadRequest("El código EAN es requerido para este producto");

        var match = candidatos.FirstOrDefault(c =>
            !string.IsNullOrWhiteSpace(c.udProducto.codigogs1) &&
            string.Equals(ean.Trim(), c.udProducto.codigogs1.Trim(), StringComparison.Ordinal));

        if (match is null)
        {
            var algunoConEan = candidatos.Any(c => !string.IsNullOrWhiteSpace(c.udProducto.codigogs1));
            return Results.BadRequest(algunoConEan
                ? "El código EAN no coincide con el producto"
                : "El producto no tiene EAN configurado");
        }

        seleccionado = match;
    }

    var product = new Product
    {
        serial = serie.ToString(),
        productId = seleccionado.producto.id,
        productCode = seleccionado.producto.codigo,
        description = seleccionado.producto.descripcion,
        type = tipo,
        maxCantByPallet = seleccionado.udProducto.cant_x_pallet,
        isAvailable = await Fun.IsAvailableAsync(serie.ToString(), seleccionado.producto.id, context)
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

        var errorItems = Fun.NormalizarYValidarItems(pallet.Products);
        if (errorItems is not null)
            return Results.BadRequest(errorItems);

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

        // EXTERNO: se decide por item.type == "EXTERNO" (no por la forma del serial); producto_id apunta a productos_clientes_cd.
        var idsExterno = pallet.Products.Where(Fun.EsExterno).Select(p => p.productId).Distinct().ToList();
        if (idsExterno.Count > 0)
        {
            var existen = await context.productos_clientes_cd.Where(x => idsExterno.Contains(x.id)).Select(x => x.id).ToListAsync();
            var faltante = idsExterno.FirstOrDefault(id => !existen.Contains(id));
            if (faltante != Guid.Empty)
                return Results.NotFound($"No se encontro el producto externo {faltante}.");
        }

        // Peabody: cada codigo (EAN=1, DUN=N unidades) se guarda como N filas activas con la misma serie.
        // Productos de otros tipos siguen una fila por serie.
        var unidadesPorItem = new Dictionary<Product, int>();
        foreach (var item in pallet.Products.Where(Fun.EsPeabody))
        {
            var datos = await (
                from p in context.producto
                join ud in context.ud_producto on p.boextension_id equals (Guid?)ud.id
                where p.id == item.productId
                select new { p.unidadesporbulto, ud.codigo_dun, ud.codigogs1 }
            ).AsNoTracking().FirstOrDefaultAsync();
            if (datos is null)
                return Results.NotFound($"No se encontro el producto {item.productId}.");
            // El codigo debe ser el EAN o el DUN del producto indicado (aplica a asociar y desasociar).
            if (!Fun.TryUnidadesPeabody(item.serial, datos.codigogs1, datos.codigo_dun, datos.unidadesporbulto, out var unidades))
                return Results.BadRequest($"El codigo {item.serial} no corresponde al producto {item.productCode ?? item.productId.ToString()} (no coincide con su EAN ni DUN). No se cargo nada.");
            if (item.serial == datos.codigo_dun && (datos.unidadesporbulto ?? 0) <= 0)
                app.Logger.LogWarning("Producto {ProductoId} sin unidadesporbulto valido; se usa 1", item.productId);
            unidadesPorItem[item] = unidades;
        }

        // Desasociar primero: resta del conteo antes de validar el cupo.
        var aDesactivar = new List<cenker_prod_x_pallet>();
        foreach (var item in productosDesasociar)
        {
            var filas = existentes.Where(e => e.serie == item.serial && e.activo && !aDesactivar.Contains(e));
            if (Fun.EsPeabody(item))
            {
                // PEABODY: baja N filas (las mas recientes del codigo/producto): DUN = unidades del bulto, EAN = 1.
                var candidatas = filas.Where(e => e.producto_id == item.productId).ToList();
                var unidades = unidadesPorItem[item];
                var indices = Fun.IndicesMasRecientes(candidatas.Select(c => c.fecha_alta).ToList(), unidades);
                if (indices is null)
                {
                    await transaction.RollbackAsync();
                    return Results.Problem(
                        title: "ITEM_UNITS_NOT_AVAILABLE",
                        detail: $"No se puede desasociar {item.serial} del producto {item.productCode}: representa {unidades} unidades y el pallet solo tiene {candidatas.Count} activas. No se cambio nada.",
                        statusCode: StatusCodes.Status422UnprocessableEntity);
                }
                aDesactivar.AddRange(indices.Select(i => candidatas[i]));
                continue;
            }
            if (Fun.EsExterno(item))
            {
                // EXTERNO: baja UNA fila (la mas reciente del producto) por item deleted.
                var candidatas = filas.Where(e => e.producto_id == item.productId).ToList();
                var idx = Fun.IndicesMasRecientes(candidatas.Select(c => c.fecha_alta).ToList(), 1);
                if (idx is not null)
                    aDesactivar.Add(candidatas[idx[0]]);
                continue;
            }
            filas = filas.Take(1);
            aDesactivar.AddRange(filas.ToList());
        }

        // Cupo por producto (ud_producto.cant_x_pallet), todo o nada.
        var nuevasPorProducto = productosAsociar.Where(i => Fun.EsPeabody(i) || Fun.EsExterno(i))
            .GroupBy(i => i.productId);
        foreach (var g in nuevasPorProducto)
        {
            var ya = existentes.Count(e => e.activo && e.producto_id == g.Key && !aDesactivar.Contains(e));
            var pedidas = g.Sum(i => Fun.EsExterno(i) ? 1 : unidadesPorItem[i]);
            var tope = Fun.EsExterno(g.First())
                ? await context.productos_clientes_cd.Where(x => x.id == g.Key)
                    .Select(x => x.cantidad_pallet ?? 0).FirstOrDefaultAsync()
                : await (
                    from p in context.producto
                    join ud in context.ud_producto on p.boextension_id equals (Guid?)ud.id
                    where p.id == g.Key
                    select ud.cant_x_pallet
                ).FirstOrDefaultAsync();
            if (!Fun.CabeEnPallet(ya, pedidas, tope))
            {
                await transaction.RollbackAsync();
                return Results.Problem(
                    title: "ITEM_QUOTA_REACHED",
                    detail: $"El pallet ya tiene {ya} unidades del producto {g.First().productCode}; agregar {pedidas} supera el maximo de {tope}. No se cargo nada.",
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        }

        foreach (var item in productosAsociar)
        {
            var peabody = Fun.EsPeabody(item);
            if (!peabody && !Fun.EsExterno(item) && existentes.Any(e => e.serie == item.serial && e.activo))
                continue;
            for (var i = 0; i < (peabody ? unidadesPorItem[item] : 1); i++)
            {
            var pXp = new cenker_prod_x_pallet
            {
                id = Guid.NewGuid(),
                pallet_id = palletId.id,
                producto_id = item.productId,
                activo = true,
                fecha_alta = DateTime.Now.ToString(),
                fecha_modificacion = DateTime.Now.ToString(),
                serie = item.serial
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
        }

        foreach (var pXp in aDesactivar)
        {
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