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
| `GET`  | `/api/productos` | Resuelve **un** producto por tipo + número de serie (y **EAN** cuando corresponde). |
| `POST` | `/api/pallets/asociar-productos` | Asocia y/o desasocia productos de un pallet. |
| `POST` | `/api/pallets/transferirExpedicion` | Marca uno o más pallets como transferidos a expedición. |

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

Lista los productos **activos** asociados al pallet.

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
| `tipo` | `string` | Sí | Tipo de etiqueta (`IMPORTADO`, `COCINA`, `TERMOTANQUE`, …). |
| `numero` | `int` | Sí | Número de serie de la etiqueta. |
| `ean` | `string` | Condicional | Código EAN/GS1 escaneado. **Requerido para todos los tipos excepto `COCINA` y `TERMOTANQUE`.** |

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
      "serial": 12345,
      "productId": "269ef17c-b822-43dd-a6f1-668d7f40e284",
      "deleted": false
    },
    {
      "serial": 54321,
      "productId": "957fc1cf-fbce-421d-af8a-54af3d60fc7c",
      "deleted": true
    }
  ]
}
```

**Respuestas:**
- `204 No Content` → operación exitosa.
- `400 Bad Request` → `"El codigo de pallet es requerido."` / `"No hay productos para asociar."` / error no controlado (rollback).
- `404 Not Found` → `"No se encontro el pallet."`

> Este endpoint **confía** en los datos que llegan en el payload (los obtenidos previamente vía `GET /api/productos`); no vuelve a validar serie ni EAN. La asociación es idempotente: si la serie ya está activa en el pallet, se ignora.

---

### `POST /api/pallets/transferirExpedicion`

Marca como transferidos (`transferir = true`) uno o más pallets identificados por su `codigo`. Los pallets no encontrados se ignoran.

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

## 🧾 Modelo `Product`

DTO de transporte usado en las respuestas de productos y en el payload de asociación.

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `serial` | `int` | Número de serie de la etiqueta. |
| `productId` | `guid` | Identificador del producto. |
| `productCode` | `string` | Código interno del producto. |
| `description` | `string` | Descripción del producto. |
| `type` | `string` | Tipo de etiqueta (`IMPORTADO`, `COCINA`, …). |
| `maxCantByPallet` | `int` | Cantidad máxima por pallet (unidad de negocio). |
| `isAvailable` | `bool` | `true` si la serie no está ya asociada activamente a un pallet. |
| `deleted` | `bool` | Solo en requests: marca la desasociación. |

---

## 🔄 Flujo típico de armado de un pallet

1. **Login** (`POST /api/login`) para identificar al operario.
2. Por cada etiqueta escaneada, **resolver el producto** con `GET /api/productos` (para importados y afines, escaneando también el **EAN**). Aquí se validan control final, existencia y disponibilidad, y el EAN determina el producto exacto.
3. **Asociar** los productos resueltos al pallet con `POST /api/pallets/asociar-productos`.
4. Cuando el pallet está listo, **transferir a expedición** con `POST /api/pallets/transferirExpedicion`.

---

## ✍️ Autor

Mauricio
