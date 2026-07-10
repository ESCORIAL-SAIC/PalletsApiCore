# 🐳 Despliegue con Docker

Guía para **containerizar** la Pallets API, **desplegarla** en un servidor Linux y **actualizarla** cuando haya cambios.

> ℹ️ La base de datos PostgreSQL es **externa** (no se contineriza). El container sólo necesita red hacia ella.

---

## 🧱 Archivos de contenedor

| Archivo | Rol |
|---------|-----|
| `Dockerfile` | Build *multi-stage*: compila con el SDK `dotnet/sdk:8.0` y la imagen final lleva sólo el runtime `dotnet/aspnet:8.0` + el *publish*. Arranca con `dotnet PalletsApiCore.dll`. |
| `.dockerignore` | Evita copiar el `appsettings.json` real (con credenciales), `bin/obj`, `.git`, etc. dentro de la imagen. |
| `docker-compose.yml` | Definición del servicio para el servidor: imagen del registry, puerto, `restart`, variables de entorno y el *bind-mount* del `appsettings.json`. |

---

## ⚙️ Puntos clave del diseño

- **Puerto: `8080`.** La app no fija puerto en código; la imagen base `aspnet:8.0` escucha por defecto en `8080` (`ASPNETCORE_HTTP_PORTS`). El `docker-compose.yml` mapea `8080:8080` (host:container).
- **Secretos por *bind-mount*.** El `appsettings.json` (que tiene la contraseña real de la DB) **vive en el servidor** y se monta como volumen de sólo lectura (`:ro`). **Nunca** se copia dentro de la imagen.
- **⚠️ El `appsettings.json` es obligatorio.** El código lo carga con `optional: false`; si el archivo no está montado, **el container crashea al arrancar**. Debe existir junto al `docker-compose.yml` en el servidor.
- **Swagger.** Sólo se expone si `ASPNETCORE_ENVIRONMENT=Development`. En `Production` (valor por defecto del compose) queda deshabilitado.
- **Healthcheck simple.** `GET /` responde `200` con texto → sirve como *liveness*.

---

## 🏷️ Configurar el registry

Antes de empezar, en `docker-compose.yml` reemplazá el placeholder `TU_REGISTRY/palletsapi` por tu registry real:

- **Docker Hub:** `tuusuario/palletsapi`
- **Registry privado:** `host:puerto/palletsapi`

---

## 📦 Build y push (en tu máquina)

Desde la raíz del repo:

```bash
docker build -t TU_REGISTRY/palletsapi:1.0 -t TU_REGISTRY/palletsapi:latest .
docker login                     # una sola vez
docker push TU_REGISTRY/palletsapi:1.0
docker push TU_REGISTRY/palletsapi:latest
```

> Usá siempre un **tag de versión** (`:1.0`, `:1.1`, …) además de `:latest` para poder volver atrás ante un problema.

---

## 🚀 Primer despliegue (en el servidor Linux)

```bash
mkdir -p /opt/palletsapi && cd /opt/palletsapi

# Copiar a esta carpeta:
#   1) docker-compose.yml
#   2) appsettings.json real (con la connection string de producción)

docker compose pull
docker compose up -d
docker compose logs -f          # verificar que arrancó sin errores (Ctrl+C para salir)
```

Ejemplo de `appsettings.json` en el servidor:

```json
{
  "ConnectionStrings": {
    "EscorialPostgreSql": "Host=10.90.98.7;Database=ESCORIAL;Username=usuario;Password=contraseña"
  }
}
```

Verificar que responde:

```bash
curl http://localhost:8080/
```

---

## 🔄 Actualizar cuando hagas cambios

```bash
# 1) En tu máquina: rebuild + push con un tag nuevo
docker build -t TU_REGISTRY/palletsapi:1.1 -t TU_REGISTRY/palletsapi:latest .
docker push TU_REGISTRY/palletsapi:1.1
docker push TU_REGISTRY/palletsapi:latest

# 2) En el servidor: traer la nueva imagen y recrear el container
cd /opt/palletsapi
docker compose pull
docker compose up -d            # recrea el container sólo si cambió la imagen
docker image prune -f           # (opcional) limpiar imágenes viejas sin usar
```

---

## 🧪 Prueba de humo local (opcional, antes de pushear)

Requiere Docker Desktop corriendo. Buildea y corre montando el `appsettings.json` real:

```bash
docker build -t palletsapi:test .

docker run --rm -p 8080:8080 \
  -v "$PWD/PalletsApiCore/appsettings.json:/app/appsettings.json:ro" \
  palletsapi:test
```

En otra terminal:

```bash
curl http://localhost:8080/                 # 200 + mensaje de salud
curl "http://localhost:8080/api/pallets?numero=PALLET001"   # confirma conectividad a la DB
```

> 🪟 **Windows + Git Bash:** la conversión automática de rutas rompe el `-v` (el destino `/app/appsettings.json` se reescribe a ruta Windows y el archivo no se monta → el container sale con *"appsettings.json was not found"*). Prefijá con `MSYS_NO_PATHCONV=1` y usá ruta host absoluta:
> ```bash
> MSYS_NO_PATHCONV=1 docker run --rm -p 8080:8080 \
>   -v "D:/source/net/PalletsApiCore/PalletsApiCore/appsettings.json:/app/appsettings.json:ro" \
>   palletsapi:test
> ```
> En el **servidor Linux esto no aplica**: el `docker compose` con la ruta relativa `./appsettings.json` funciona sin ajustes.

---

## 🩺 Troubleshooting

| Síntoma | Causa probable | Solución |
|---------|----------------|----------|
| El container reinicia en loop / sale al arrancar | Falta el `appsettings.json` (por `optional: false`) | Verificá que el archivo esté junto al `docker-compose.yml` y que el volumen `:ro` apunte bien. Mirá `docker compose logs`. |
| Errores de conexión a la DB | El servidor no llega a `10.90.98.7:5432` | Revisá firewall/red del servidor hacia la DB. |
| No aparece Swagger | `ASPNETCORE_ENVIRONMENT=Production` | Poné `Development` en `environment` del compose (sólo si querés exponer el UI). |
| El puerto 8080 ya está en uso | Otro servicio en ese puerto | Cambiá el mapeo host en el compose, p.ej. `"80:8080"`. |
