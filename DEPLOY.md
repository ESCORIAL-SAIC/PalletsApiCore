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

**No buildeás ni pusheás a mano, ni tocás números de versión.** El workflow `.github/workflows/docker-publish.yml` corre en cada push a `dev`/`main`, calcula el próximo número de versión, **crea el tag git** y buildea+pushea la imagen — todo en la misma corrida:

| Push a… | Versión | Imágenes publicadas |
|---------|---------|---------------------|
| `dev`   | pre-release `vX.Y.Z-rc.N` | `…:X.Y.Z-rc.N`, `…:dev`, `…:sha-<commit>` |
| `main`  | final `vX.Y.Z` | `…:X.Y.Z`, `…:X.Y`, `…:latest`, `…:sha-<commit>` |
| tag manual `vX.Y.Z` | ese tag tal cual | `…:X.Y.Z`, `…:X.Y`, `…:latest`, `…:sha-<commit>` |
| sin PR / `workflow_dispatch` / label `release:skip` | sin bump | sólo `…:latest` o `…:dev` + `…:sha-<commit>` |

GitHub buildea con el `Dockerfile` y sube la imagen a **GitHub Container Registry** usando su `GITHUB_TOKEN` automático — **no hace falta crear ningún token (PAT)**. El mismo `GITHUB_TOKEN` (con permiso `contents: write`) pushea el tag git. Tu único paso es:

```bash
git push        # (o el merge de la PR a dev/main) — Actions calcula versión, taggea y publica
```

Seguí el progreso en la pestaña **Actions** del repo.

### 🏷️ Versionado dirigido por labels de la PR (SemVer)

La versión de la API es **una única fuente de verdad: los tags git `vMAJOR.MINOR.PATCH`**. Ahora esos tags **los crea el CI automáticamente** al mergear, según las labels de la PR. [MinVer](https://github.com/adamralph/minver) sigue leyendo esos tags para los builds locales (`dotnet minver`) y para inyectar la versión en el assembly que expone `GET /version`.

**El tamaño del bump lo decide la label de la PR:**

| Label en la PR | Bump al mergear | Ejemplo (desde `v1.4.2`) |
|----------------|-----------------|--------------------------|
| `release:major` | `MAJOR` (resetea minor y patch) | → `v2.0.0` |
| `release:minor` | `MINOR` (resetea patch) | → `v1.5.0` |
| *(ninguna label de release)* | **`PATCH`** (default) | → `v1.4.3` |
| `release:skip` | **no crea versión**; sólo buildea la imagen de branch (`:dev`/`:latest` + `:sha`) | *(sin tag)* |

**Qué pasa al mergear:**

- **PR a `dev`** → se crea un **pre-release** `vX.Y.Z-rc.N`. El `X.Y.Z` es el próximo final calculado desde el último tag final según la label; `N` se autoincrementa por cada rc de ese mismo target (rc.1, rc.2, …). Publica `…:X.Y.Z-rc.N` + `…:dev` + `…:sha`.
- **PR a `main`** → se crea el **release final** `vX.Y.Z`. Publica `…:X.Y.Z` + `…:X.Y` + `…:latest` + `…:sha`.
- **Push de un tag manual `vX.Y.Z`** → se respeta ese tag tal cual (el CI **no** crea otro). Publica `…:X.Y.Z` + `…:X.Y` + `…:latest` + `…:sha`. Útil como escape para forzar una versión puntual.

El número lo calcula `.github/scripts/compute-version.sh` en el runner y lo pasa al build como `--build-arg VERSION=…` (el `.git` no entra a la imagen por `.dockerignore`; el Dockerfile la aplica con `/p:MinVerVersionOverride`). El label OCI `org.opencontainers.image.version` de la imagen refleja la versión publicada.

> 💡 El script es testeable en local sin GitHub con overrides por env, p. ej.:
> ```bash
> DRY_RUN=1 GITHUB_REF=refs/heads/main PR_LABELS="release:minor" \
>   MOCK_TAGS="v0.1.0 v0.1.1" bash .github/scripts/compute-version.sh
> # -> version=0.2.0
> ```

### 🔧 Setup inicial del versionado (una sola vez)

1. **Crear las labels de release en el repo:**
   ```bash
   gh label create release:minor --description "Bump MINOR al mergear" --color 1d76db
   gh label create release:major --description "Bump MAJOR al mergear" --color b60205
   gh label create release:skip  --description "No crear versión (sólo imagen de branch)" --color cfd3d7
   ```
2. **Sembrar el primer tag** para que el cálculo tenga una base (si no hay tags, el primer patch arranca en `v0.0.1`):
   ```bash
   git tag v0.1.0
   git push origin v0.1.0
   ```

### ⚙️ Configuración única en GitHub (una sola vez)

1. **Tras el primer run exitoso**, hacer el paquete **público** para que el server baje sin login:
   `ESCORIAL-SAIC → Packages → pallets-api → Package settings → Change visibility → Public`.
2. (Opcional) *Connect repository* en esa misma pantalla, para linkear el package al repo.
3. Si el workflow falla al pushear por permisos:
   `repo → Settings → Actions → General → Workflow permissions → Read and write permissions`.

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
