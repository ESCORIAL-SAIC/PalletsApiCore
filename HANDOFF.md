## Equipo
- Investigador: Byte (0858c84b5893)
- Desarrollador: (yo, ver `office-workers list`)
- Tester: ver abajo
- QA: pendiente

## Desarrollador — vuelta 1
Cambios:
- `Fun.ResolverPeabodyAsync` (EAN/DUN exacto sobre `producto` JOIN `ud_producto`) y `Fun.CalcularUnidades` (pura).
- `Program.cs`: rama `tipo == "PEABODY"` antes de `TryNormalizarSerie`; 404 si no existe, 400 si numero vacio (check previo). No exige ean, sin control final.
- `ud_producto.codigo_dun` agregado; `Product.esDun`/`unidades` agregados.
- Tests: `CalcularUnidadesTests.cs`. `DOTNET_ROLL_FORWARD=Major dotnet test` -> 23 OK (solo hay runtime 10 local).

Decisiones / riesgos:
- Usé tablas `producto`+`ud_producto` (no vistas v_producto/v_ud_producto): evité modelos nuevos. NO verificado contra la DB (appsettings.Development.json tiene placeholders): confirmar que la tabla `ud_producto` tenga columnas `codigo_dun` y `codigogs1` (en expedicion-api se leen de V_UD_PRODUCTO). Si no, hay que mapear la vista con ToView + efpt.config.json.
- EAN con varios productos: se toma el primero ordenado por codigo,id (sin 409).
- `isAvailable` = true (issue #16). `serial` queda 0 (issue #15 lo pasa a string).
- Sin test de la query EF (no hay InMemory/SQLite; no agregué paquetes).
- Probar: GET api/productos?tipo=PEABODY&numero=<EAN> / <DUN> -> esDun false/true; inexistente -> 404.
