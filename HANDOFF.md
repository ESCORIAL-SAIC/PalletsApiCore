## Equipo
- Investigador: Byte (96e55deca882)
- Desarrollador: Sprocket (293fd9885564)
- Tester: (pendiente)

## Desarrollador (Sprocket) — vuelta 1
Cambios: Product.serial string; Fun.IsAvailableAsync(string); Fun.TryNormalizarCodigoPeabody/EsCodigoPeabody (13/14 dígitos, sin trim/int); getProductosByPallet y asociar/desasociar sin int.Parse; filas Peabody -> type="PEABODY"; README; tests PeabodyCodigoTests.
TryNormalizarSerie sin cambios (no se usa para PEABODY; la resolución en api/productos es de #14).
Pendientes/ponytail: no se resuelve producto por EAN/DUN en getProductosByPallet (ya viene producto_id de cenker_prod_x_pallet, no hace falta); ud_producto sin CODIGO_DUN -> #14. PR debe llevar label release:major.
Probar: `DOTNET_ROLL_FORWARD=Major dotnet test` (27 OK). Sin DB.
