# 📦 Pallets API

API RESTful desarrollada en **.NET 8** (minimal API) con **Entity Framework Core** y **PostgreSQL** para la gestión de pallets y la asociación de productos en la planta de Escorial.

> ℹ️ Este proyecto **no tiene front-end**. Toda la interacción es vía HTTP. En entorno *Development* se expone **Swagger UI** para explorar y probar los endpoints.

---

## 🧰 Stack

- **.NET 8** / ASP.NET Core — *minimal API* (endpoints definidos en `Program.cs`, sin controllers)
- **Entity Framework Core** (enfoque *database-first*) + **Npgsql**
- **PostgreSQL**
- **Swagger / OpenAPI** (solo en *Development*)

---

## 🚀 Instalación y ejecución

1. **Clonar el repositorio:**
    ```bash
    git clone https://github.com/maurih5/pallets-api-core
    cd pallets-api-core
    ```

2. **Configurar la cadena de conexión.** El archivo `PalletsApiCore/appsettings.json` está *gitignored* (contiene credenciales reales). Copiá `PalletsApiCore/appsettings.Development.json` a `PalletsApiCore/appsettings.json` y completá los valores:
    ```json
    {
      "ConnectionStrings": {
        "EscorialPostgreSql": "Host=localhost;Database=escorial_db;Username=usuario;Password=contraseña"
      }
    }
    ```

3. **Restaurar paquetes y ejecutar:**
    ```bash
    dotnet restore
    dotnet build
    dotnet run --project PalletsApiCore
    ```

4. En *Development*, abrir **Swagger UI** en `/swagger` sobre la URL que informe la consola (por ejemplo `http://localhost:5047/swagger`).

> 🐳 Para desplegar en un servidor Linux con Docker (build, push a registry y actualización del container), ver **[DEPLOY.md](DEPLOY.md)**.

---

## 🔐 Autenticación

El login (`POST /api/login`) es una **validación plana de usuario/contraseña** contra la base (join `empleado` → `ud_empleado` → `v_persona`). **No** emite token ni JWT, y **no** hay middleware de autenticación: el resto de los endpoints no exigen credenciales. La gestión de sesión queda del lado del cliente.

---

## 📚 Endpoints

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET`  | `/` | Mensaje de salud / bienvenida (sin front-end). |
| `POST` | `/api/login` | Autentica un empleado por usuario y contraseña. |
| `GET`  | `/api/pallets` | Devuelve un pallet por su código. |
| `GET`  | `/api/pallets/productos` | Lista los productos asociados a un pallet. |
| `GET`  | `/api/productos` | Resuelve **un** producto por tipo + número de serie (y **EAN** cuando corresponde). `PEABODY` se resuelve por EAN/DUN. |
| `POST` | `/api/pallets/asociar-productos` | Asocia y/o desasocia productos de un pallet. |
| `POST` | `/api/pallets/transferirExpedicion` | Marca uno o más pallets como transferidos a expedición. |
| `GET`  | `/version` | Devuelve la versión (SemVer) de la API en ejecución. |
| `GET`  | `/health/live` | *Liveness*: responde `200` si el proceso está vivo (sin dependencias). |
| `GET`  | `/health/ready` | *Readiness*: responde `200`/`503` según el estado real, incluida la conexión a PostgreSQL. |

---

### `POST /api/login`

**Body:**
```json
{
  "user": "usuario",
  "password": "contraseña"
}
```

**Respuestas:**
- `200 OK` → `{ "id": "<guid>", "usuario_sistema": "usuario", "nombre": "Nombre Apellido" }`
- `404 Not Found` → `"Usuario no encontrado."`

---

### `GET /api/pallets?numero=<codigoPallet>`

Devuelve los datos del pallet cuyo `codigo` coincide con `numero`.

**Respuestas:**
- `200 OK` → objeto del pallet.
- `400 Bad Request` → `"El numero de pallet es requerido"`
- `404 Not Found` → `"Pallet no encontrado."`

---

### `GET /api/pallets/productos?numero=<codigoPallet>`

Lista los productos **activos** asociados al pallet. Las líneas `PEABODY` vienen una por **producto + código** (`serial` EAN o DUN, con `cantidad`, `esDun` y `unidades`); ver [Peabody: tope por pallet](#peabody-tope-por-pallet).

**Respuestas:**
- `200 OK` → arreglo de [`Product`](#-modelo-product).
- `400 Bad Request` → `"El numero de pallet es requerido"`
- `404 Not Found` → `"Pallet no encontrado"`

---

### `GET /api/productos?tipo=<tipo>&numero=<serie>&ean=<ean>`

Resuelve **un** producto a partir de su etiqueta, aplicando las validaciones de negocio previas a la asociación. Es el paso que el cliente debe ejecutar por cada serie escaneada **antes** de asociarla a un pallet.

**Parámetros (query string):**

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `tipo` | `string` | Sí | Tipo de etiqueta (`IMPORTADO`, `COCINA`, `TERMOTANQUE`, `PEABODY`, …). Vocabulario de la API: `IMPORTADO` (no `IMPORT`). `PEABODY` no pasa por control final (igual que expedición). |
| `numero` | `int` / `string` | Sí | Número de serie de la etiqueta. Con `tipo=PEABODY` es string: el código **EAN (13 dígitos) o DUN (14 dígitos)** escaneado, solo numérico; otro formato devuelve `400`. |
| `ean` | `string` | Condicional | Código EAN/GS1 escaneado. **Requerido para todos los tipos excepto `COCINA`, `TERMOTANQUE` y `PEABODY` (en `PEABODY` no se usa).** |

**🏷️ Validación de EAN (productos importados).** Un mismo número de serie importado puede corresponder a **varios productos distintos**, diferenciados únicamente por su código EAN (GS1). Por eso, para los tipos que validan EAN, el `ean` escaneado se usa para **seleccionar cuál** de los productos de esa serie corresponde — no solo para validarlo contra uno resuelto arbitrariamente. Los tipos `COCINA` y `TERMOTANQUE` quedan desambiguados por el propio `tipo` y no requieren EAN.

**Respuestas:**

| Código | Cuerpo | Cuándo |
|--------|--------|--------|
| `200 OK` | [`Product`](#-modelo-product) | Serie válida y (si aplica) EAN correcto. |
| `400 Bad Request` | `"El tipo de producto es requerido"` | Falta `tipo`. |
| `400 Bad Request` | `"El numero de producto es requerido"` | Falta `numero`. |
| `400 Bad Request` | `"El código EAN es requerido para este producto"` | Tipo que valida EAN y `ean` vacío. |
| `400 Bad Request` | `"El código EAN no coincide con el producto"` | El `ean` no coincide con ningún producto de la serie. |
| `400 Bad Request` | `"El producto no tiene EAN configurado"` | El producto no tiene EAN cargado en el sistema. |
| `404 Not Found` | `"No se encontro el numero de serie"` | No existe la etiqueta para ese `tipo` + `numero`. |
| `404 Not Found` | `"El numero de serie no posee control final"` | Falta control final (aplica a todo tipo **salvo** `IMPORTADO`). |

**Ejemplo:**
```
GET /api/productos?tipo=IMPORTADO&numero=260400005&ean=7798013733390
```

**Tipo `PEABODY`.** No usa serie, control final ni `ean`: `numero` es el código escaneado, **EAN (GS1) o DUN**, con match exacto (se ignoran vacíos). Responde con `serial` = el código escaneado (EAN/DUN), `isAvailable = true` y los campos extra `esDun` y `unidades` (`unidades` = unidades por bulto si es DUN, mínimo 1; `1` si es EAN). En los demás tipos esos campos vienen `null`.

| Código | Mensaje | Causa |
|--------|---------|-------|
| `400 Bad Request` | `"El codigo PEABODY debe ser numerico de 13 (EAN) o 14 (DUN) digitos"` | `numero` no numérico o de largo distinto de 13/14. |
| `404 Not Found` | `"No se encontro un producto con ese codigo EAN/DUN"` | El código no existe. |
| `409 Conflict` | `"El codigo EAN/DUN corresponde a mas de un producto"` | El código está en más de un producto. |

```
GET /api/productos?tipo=PEABODY&numero=7798013733390   # EAN (13 dígitos) -> esDun=false, unidades=1
GET /api/productos?tipo=PEABODY&numero=17798013733397  # DUN (14 dígitos) -> esDun=true, unidades=unidadesporbulto
```

> **Tipo `EXTERNO` (`productos_clientes_cd`): pendiente, aún no implementado.** El repo no tiene modelo EF para esa tabla; hasta definirla `tipo=EXTERNO` responde como cualquier tipo desconocido.

---

### `POST /api/pallets/asociar-productos`

Asocia y/o desasocia productos de un pallet en una única operación transaccional. El campo `deleted` de cada producto define la acción: `false` asocia, `true` desasocia (baja lógica vía `activo`). Cada operación deja un registro de auditoría.

**Body:**
```json
{
  "codigo": "PALLET001",
  "usuario": "usuarioLogueado",
  "products": [
    {
      "serial": "12345",
      "productId": "269ef17c-b822-43dd-a6f1-668d7f40e284",
      "type": "IMPORTADO",
      "deleted": false
    },
    {
      "serial": "17791234567897",
      "productId": "957fc1cf-fbce-421d-af8a-54af3d60fc7c",
      "productCode": "P001",
      "type": "PEABODY",
      "deleted": true
    }
  ]
}
```

**Respuestas:**
- `204 No Content` → operación exitosa.
- `400 Bad Request` → `"El codigo de pallet es requerido."` / `"No hay productos para asociar."` / error no controlado (rollback).
- `400 Bad Request` (Peabody, sin cargar nada) → serial de un ítem `type: "PEABODY"` que no es numérico de 13/14 dígitos; el código no coincide con el EAN (`codigogs1`) ni DUN (`codigo_dun`) del `productId`; o ítem **no** PEABODY cuyo serial tiene forma de EAN/DUN (`"El serial X parece un EAN/DUN: el type debe ser PEABODY"`).
- `404 Not Found` → `"No se encontro el pallet."` / `"No se encontro el producto <id>."` (Peabody).
- `422 Unprocessable Entity` → `ITEM_QUOTA_REACHED` (el alta supera `cant_x_pallet`) o `ITEM_UNITS_NOT_AVAILABLE` (se desasocia un código de N unidades y hay menos de N filas activas). Rollback, no se cambia nada.

> **`type` es obligatorio para Peabody:** la rama Peabody se decide por `type == "PEABODY"` (exacto), no por la forma del serial. Los ítems no Peabody siguen el camino genérico.

> Para tipos no Peabody este endpoint **confía** en los datos del payload (los obtenidos previamente vía `GET /api/productos`); no vuelve a validar serie ni EAN. Para `PEABODY` sí valida el código contra el producto y el cupo (ver arriba). La asociación es idempotente: si la serie ya está activa en el pallet, se ignora.

---

### `POST /api/pallets/transferirExpedicion`

Marca como transferidos (`transferir = true`) uno o más pallets identificados por su `codigo`. Los pallets no encontrados se ignoran. El endpoint solo marca el pallet; no valida ni envía etiquetas: quién procesa `transferir = true` y cómo trata etiquetas EAN/DUN (13/14 dígitos) y repetidas se resuelve fuera de este repo.

**Body:**
```json
[
  { "codigo": "PALLET001" },
  { "codigo": "PALLET002" }
]
```

**Respuestas:**
- `204 No Content` → operación exitosa.
- `400 Bad Request` → `"No hay pallets para transferir."`

---

### `GET /version`

Devuelve la versión SemVer de la API en ejecución. Los tags git los crea el CI al mergear, según la etiqueta de la PR (`bugfix`/`feature`/`breaking`); ver [DEPLOY.md](DEPLOY.md).

**Respuestas:**
- `200 OK` → `{ "version": "0.1.0" }`

---

### `GET /health/live`

*Liveness probe*: indica sólo que el proceso está vivo, sin chequear dependencias.

**Respuestas:**
- `200 OK` → el proceso responde.

---

### `GET /health/ready`

*Readiness probe*: chequea el estado real, incluida la conexión a PostgreSQL (`AddDbContextCheck`).

**Respuestas:**
- `200 OK` → `{ "status": "Healthy", "version": "0.1.0", "checks": [{ "name": "db", "status": "Healthy", "description": null }] }`
- `503 Service Unavailable` → mismo formato con `status`/check `db` en `Unhealthy` (p. ej. DB caída).

---

## 🧾 Modelo `Product`

DTO de transporte usado en las respuestas de productos y en el payload de asociación.

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `serial` | `string` | Número de serie de la etiqueta. |
| `productId` | `guid` | Identificador del producto. |
| `productCode` | `string` | Código interno del producto. |
| `description` | `string` | Descripción del producto. |
| `type` | `string` | Tipo de etiqueta (`IMPORTADO`, `COCINA`, `PEABODY`, …). **En requests de `asociar-productos` es obligatorio para Peabody** (`PEABODY` exacto). |
| `maxCantByPallet` | `int` | Cantidad máxima por pallet (unidad de negocio). |
| `isAvailable` | `bool` | `true` si la serie no está ya asociada activamente a un pallet. |
| `deleted` | `bool` | Solo en requests: marca la desasociación. |
| `esDun` | `bool?` | Solo `PEABODY`: el código escaneado es DUN. |
| `unidades` | `int?` | Solo `PEABODY`: unidades que representa un ítem del código (por bulto si es DUN, `1` si es EAN). |
| `cantidad` | `int?` | Solo `PEABODY` en `GET /api/pallets/productos`: filas activas de ese código en el pallet. |

> `serial` es string: para `PEABODY` es el EAN 13 / DUN 14 (no entra en `int`). **Cambio de contrato** (label `breaking`). Filas `PEABODY` en `api/pallets/productos` devuelven `type: "PEABODY"`.

---

## 🔄 Flujo típico de armado de un pallet

1. **Login** (`POST /api/login`) para identificar al operario.
2. Por cada etiqueta escaneada, **resolver el producto** con `GET /api/productos` (para importados y afines, escaneando también el **EAN**). Aquí se validan control final, existencia y disponibilidad, y el EAN determina el producto exacto.
3. **Asociar** los productos resueltos al pallet con `POST /api/pallets/asociar-productos`.
4. Cuando el pallet está listo, **transferir a expedición** con `POST /api/pallets/transferirExpedicion`.

---

## ✍️ Autor

Mauricio

## Peabody: tope por pallet

- Una fila activa de `web.cenker_prod_x_pallet` = **una unidad**. Un EAN agrega 1 fila; un DUN agrega N filas (N = `Fun.CalcularUnidades`, `unidadesporbulto` nulo/0 cae a 1 y se loguea warning). Todas con la misma `serie` (el código escaneado). No se toca el esquema.
- `POST api/pallets/asociar-productos`: valida `ya_asociadas + unidades <= ud_producto.cant_x_pallet` por producto, dentro de la transacción y todo o nada. Si no entra: **422** (`title: ITEM_QUOTA_REACHED`) y no se carga nada. Las desasociaciones del mismo request se restan antes de validar. `cant_x_pallet` nulo/0 = rechaza (no hay tope válido).
- `GET api/pallets/productos`: Peabody viene **agrupado por producto + código**: una línea por (producto, `serial`). `serial` = ese código (EAN o DUN), `cantidad` = filas activas de ese código, `esDun` según coincida con `codigo_dun` y `unidades` = unidades que representa un ítem de ese código (DUN = unidades por bulto, EAN = 1). Un mismo producto con EAN y DUN da 2 líneas. El cliente desasocia con el `serial` exacto de la línea. **Cambio de contrato (menor):** antes una línea por producto con `unidades` = cantidad acumulada; ahora `unidades` es por ítem y la cantidad va en `cantidad`. Otros tipos: una línea por serie, sin cambios.
- Un pallet **puede mezclar** Peabody con otros tipos: el cupo es por producto, no por pallet.
- Desasociar Peabody envía el mismo código y baja las unidades que representa: **DUN baja N filas activas** (las más recientes del código/producto, con una auditoría por fila), **EAN baja 1**. N se calcula en servidor igual que al asociar. Si hay menos filas activas que N: **422** (`title: ITEM_UNITS_NOT_AVAILABLE`), rollback y no se baja nada.
- En `asociar-productos` la rama Peabody se decide por `type == "PEABODY"` del ítem (no por la forma del serial). Se hace `Trim()` del `serial`; si el ítem es PEABODY y el serial no es numérico de 13/14 dígitos: **400** y no se carga nada. Ítems de otro tipo siguen el camino genérico sin cambios.
- Asociar y desasociar validan que el código (EAN 13 / DUN 14) coincida con `ud_producto.codigogs1` o `codigo_dun` del `productId` indicado; si no, **400** y no se cambia nada.
- Límite: sin bloqueo de concurrencia; dos requests simultáneos al mismo pallet podrían pasarse del tope.
