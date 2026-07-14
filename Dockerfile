# Etapa build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY PalletsApiCore/PalletsApiCore.csproj PalletsApiCore/
RUN dotnet restore PalletsApiCore/PalletsApiCore.csproj
COPY PalletsApiCore/ PalletsApiCore/
ARG VERSION=0.0.0-dev
RUN dotnet publish PalletsApiCore/PalletsApiCore.csproj -c Release -o /app/publish \
    /p:UseAppHost=false /p:MinVerVersionOverride=$VERSION

# Etapa runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ARG VERSION=0.0.0-dev
LABEL org.opencontainers.image.version=$VERSION \
      org.opencontainers.image.source=https://github.com/ESCORIAL-SAIC/PalletsApiCore
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8080
# El appsettings.json llega por bind-mount en runtime (ver docker-compose.yml), NO se copia aca.
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -fsS http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "PalletsApiCore.dll"]
