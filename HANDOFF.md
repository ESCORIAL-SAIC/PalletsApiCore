## Equipo
- Investigador: Pixel (e6ca7bbb5afe)
- Desarrollador: Nibble (faf2b7f73f2e)
- Tester: Tester1 (id: completar)

## Desarrollador (Nibble) — vuelta 1
Cambios (diff chico, rama temprana en Program.cs antes de TryNormalizarSerie):
- Program.cs: `tipo=PEABODY` en GET api/productos, `numero` string trim, match exacto contra ud_producto.codigogs1 / codigo_dun (vacíos ignorados). Sin control final, sin ean.
- Fun.CalcularUnidades(esDun, unidadesPorBulto): DUN = max(round(x ?? 1),1), EAN = 1.
- ud_producto: + codigo_dun. Product: + `esDun` (bool?) y `unidades` (int?) nullable: otros tipos mandan null (campos nuevos, no rompen).
- Tests: PalletsApiCore.Tests/CalcularUnidadesTests.cs (EAN, DUN, 0/null/negativo). 404/409 no testeables sin DB.
Decisiones:
- Inexistente → 404. Mismo código en >1 producto distinto → 409 Conflict.
- serial = 0, isAvailable = true (sin serie, EAN repetido entre unidades).
- Mantengo DbSets producto/ud_producto (no agregué vistas V_*; no verificable sin DB: si V_PRODUCTO filtra borrados, revisar).
- `dotnet test` requiere `DOTNET_ROLL_FORWARD=Major` aquí (solo runtime 10 instalado). 23 OK.
Prueba manual:
- curl "/api/productos?tipo=PEABODY&numero=<EAN13>"  → esDun=false, unidades=1
- curl "/api/productos?tipo=PEABODY&numero=<DUN14>"  → esDun=true, unidades=unidadesporbulto
- código inexistente → 404.
