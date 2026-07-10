# Etapa build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY PalletsApiCore/PalletsApiCore.csproj PalletsApiCore/
RUN dotnet restore PalletsApiCore/PalletsApiCore.csproj
COPY PalletsApiCore/ PalletsApiCore/
RUN dotnet publish PalletsApiCore/PalletsApiCore.csproj -c Release -o /app/publish /p:UseAppHost=false

# Etapa runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
# El appsettings.json llega por bind-mount en runtime (ver docker-compose.yml), NO se copia aca.
ENTRYPOINT ["dotnet", "PalletsApiCore.dll"]
