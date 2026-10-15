## Equipo
- Investigador: Byte (96e55deca882)
- Desarrollador: Sprocket (293fd9885564)
- Tester: Cadena
- QA: Bolt (75ea78f047c3)

## Desarrollador (Sprocket) — vuelta 1
Cambios: Product.serial string; Fun.IsAvailableAsync(string); Fun.TryNormalizarCodigoPeabody/EsCodigoPeabody (13/14 dígitos, sin trim/int); getProductosByPallet y asociar/desasociar sin int.Parse; filas Peabody -> type="PEABODY"; README; tests PeabodyCodigoTests.
TryNormalizarSerie sin cambios (no se usa para PEABODY; la resolución en api/productos es de #14).
Pendientes/ponytail: no se resuelve producto por EAN/DUN en getProductosByPallet (ya viene producto_id de cenker_prod_x_pallet, no hace falta); ud_producto sin CODIGO_DUN -> #14. PR debe llevar label release:major.
Probar: `DOTNET_ROLL_FORWARD=Major dotnet test` (27 OK). Sin DB.

## Tester (Cadena) — vuelta 1
Tests: 27 OK. Diff revisado (git show HEAD). Sin bloqueantes.
Observaciones menores (no bloquean):
- Program.cs:1 se agregó BOM UTF-8 por accidente (ruido en diff).
- asociar-productos: `serial` null/vacío ya no falla como antes (int=0); se guardaría serie null/"" . Sin validación (pre-existente en espíritu).
- Clientes que manden `serial` numérico JSON reciben 400 (cambio de contrato, cubierto por release:major).
- int.Parse en api/pallets/productos sigue tirando con series no-Peabody de 11-12 dígitos (pre-existente).

## QA (Bolt) — vuelta 1
Tests 27 OK. No hay AGENTS.md. Lógica y README correctos.
Corregir (bloquea, diff limpio):
- PalletsApiCore/Program.cs:1 BOM UTF-8 agregado por accidente; la línea 1 aparece modificada. Sacarlo (`sed -i '1s/^\xEF\xBB\xBF//'`), amendar/commitear y verificar que `git diff HEAD~1 -- PalletsApiCore/Program.cs` no toque la línea 1.
Opcional: validar `serial` null/vacío en asociar-productos (hoy guardaría null/"").
Después de arreglar, pasárselo a tu tester.
