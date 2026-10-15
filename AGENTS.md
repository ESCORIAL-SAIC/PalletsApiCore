# AGENTS.md

.NET 8 minimal API (PostgreSQL, EF Core database-first). No controllers: all endpoints live in `PalletsApiCore/Program.cs`; helpers in `PalletsApiCore/Fun.cs`. Docs/comments are in Spanish.

## Commands
- Run: `dotnet run --project PalletsApiCore` (Swagger at `/swagger`, Development only).
- Tests (xUnit, only `Fun.TryNormalizarSerie` covered): `dotnet test PalletsApiCore.sln`; single: `dotnet test PalletsApiCore.Tests --filter "FullyQualifiedName~Importado_20Digitos"`.
- Version scripts have own check: `bash .github/scripts/test-version.sh` (CI runs it before dotnet tests).

## Reglas obligatorias
- PRs de trabajo siempre a `dev`, nunca directo a `main`. Solo `dev` -> `main` para releases.
- Mergear PRs siempre con merge commit (no squash, no rebase).
- Prohibido tocar la base de datos bajo cualquier punto de vista: nada de escrituras, migraciones, DDL ni scripts contra la DB (ni dev ni prod).

## Gotchas
- `PalletsApiCore/appsettings.json` is gitignored (real creds). Copy from `appsettings.Development.json` and set `ConnectionStrings:EscorialPostgreSql`. Never commit it.
- `Models/*` is scaffolded by EF Power Tools (`efpt.config.json`); regenerate rather than hand-edit.
- No auth: `/api/login` is plain user/password check, no token/middleware.
- Version comes from git tags via MinVer (tag prefix `v`), not from code.

## CI / release
- Required checks: `test` (tests.yml) and `Compilar` (build-pr.yml, builds Docker image with final version).
- PR label sets bump: `breaking` / `feature` / default `bugfix`. Dev gets `X.Y.Z-rc.N`; main gets stable `X.Y.Z`.
- PR to `dev` must be up to date with `dev`; `main` only takes `dev` -> `main` PRs. `publicar.yml` publishes the PR's artifact without rebuilding.
- Deploy: see `DEPLOY.md`.
