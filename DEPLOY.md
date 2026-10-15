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
- **Healthcheck.** El container define un `HEALTHCHECK` (y el compose otro equivalente) contra `GET /health/live` → *liveness* sin dependencias. Para el estado real (incluida la DB) está `GET /health/ready`.

---

## 🤖 Publicación automática (GitHub Actions)

**No buildeás ni pusheás a mano.** Flujo:

```
PR a dev  -> [test] + [Compilar imagen] (imagen con versión X.Y.Z-rc.N) -> merge -> [Publicar]
PR dev->main -> mismo flujo, versión X.Y.Z
```

- `tests.yml`: `dotnet test`, un check por test. Check requerido `test`.
- `build-pr.yml`: construye la imagen UNA vez en la PR con la versión final. Check requerido `Compilar imagen`.
- `publicar.yml`: al mergear, verifica que el artefacto sea lo mergeado, pushea ESE mismo `imagen-*.tar.gz` a GHCR, crea el tag anotado (`versionCode=N`) y el release. No recompila.
- La versión vive en los tags (sin prefijo `v`): `1.2.0-rc.3`, `1.2.0`. El CI no pushea commits a `dev`/`main`.

**Etiqueta de la PR** (sube la versión): `bugfix` (default, PATCH), `feature` (MINOR), `breaking` (MAJOR). Con un rc en curso, un cambio del mismo nivel o menor suma 1 al rc.

| Merge | Imágenes en GHCR |
|-------|------------------|
| PR a `dev` | `…:X.Y.Z-rc.N`, `…:dev` |
| PR `dev` -> `main` | `…:X.Y.Z`, `…:X.Y`, `…:latest` |

Versión inicial del esquema: `0.1.0-rc.1` (`VERSION_INICIAL` en `build-pr.yml` y `publicar.yml`). Los tags viejos con prefijo `v` se ignoran. MinVer sigue sirviendo solo para builds locales.

Rulesets: `dev` exige `test` + `Compilar imagen` y rama al día; `main` exige los mismos sin "al día" (el build verifica que main quede idéntico a dev).

---

## 🚀 Primer despliegue (en el servidor Linux)

```bash
mkdir -p /opt/pallets-api && cd /opt/pallets-api

# Copiar a esta carpeta:
#   1) docker-compose.yml
#   2) appsettings.json real (con la connection string de producción)
#   3) (opcional) un .env con IMAGE_TAG=dev si este server corre el entorno de prueba

docker compose pull            # sin login: el paquete es público
docker compose up -d
docker compose logs -f          # verificar que arrancó sin errores (Ctrl+C para salir)
```

> **Qué tag corre.** El compose usa `ghcr.io/escorial-saic/pallets-api:${IMAGE_TAG:-latest}`: sin `.env` corre `:latest` (lo que se publica desde `main`). Para fijar otro tag, poné un archivo `.env` junto al `docker-compose.yml`, p. ej. el entorno de prueba:
> ```
> IMAGE_TAG=dev
> ```
> o una versión fija de release:
> ```
> IMAGE_TAG=0.1.0
> ```

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
# 1) En tu máquina: subir el cambio. GitHub Actions buildea y publica la imagen solo.
git push                        # push/merge a dev -> tag :dev ; a main -> tag :latest

# 2) En el servidor: traer la nueva imagen y recrear el container
cd /opt/pallets-api
docker compose pull
docker compose up -d            # recrea el container sólo si cambió la imagen
docker image prune -f           # (opcional) limpiar imágenes viejas sin usar
```

> Esperá a que el workflow termine en verde (pestaña **Actions**) antes del `docker compose pull` en el server.

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
