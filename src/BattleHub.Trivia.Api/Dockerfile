# syntax=docker/dockerfile:1
# Se construye desde la raíz del repositorio, porque la API usa los proyectos Data y Domain:
#   docker build --file src/BattleHub.Trivia.Api/Dockerfile --tag battlehub-trivia-api .

# Etapa 1: restaura y publica la API con el SDK de .NET 10.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Primero solo los .csproj: la capa del restore se reutiliza mientras no cambien las dependencias.
COPY src/BattleHub.Trivia.Api/BattleHub.Trivia.Api.csproj src/BattleHub.Trivia.Api/
COPY src/BattleHub.Trivia.Data/BattleHub.Trivia.Data.csproj src/BattleHub.Trivia.Data/
COPY src/BattleHub.Trivia.Domain/BattleHub.Trivia.Domain.csproj src/BattleHub.Trivia.Domain/
RUN dotnet restore src/BattleHub.Trivia.Api/BattleHub.Trivia.Api.csproj
COPY src/ src/
RUN dotnet publish src/BattleHub.Trivia.Api/BattleHub.Trivia.Api.csproj --configuration Release --no-restore --output /app/publish -p:UseAppHost=false
# SqlClient trae msalruntime (MSAL) para conectarse con Microsoft Entra en Windows, macOS y Linux (~60 MB).
# La API se conecta con usuario y contraseña de SQL, así que se quitan esas bibliotecas nativas
# y las de Windows y macOS. Si algo falla por esto, basta con borrar esta línea.
RUN rm -rf /app/publish/runtimes/win* /app/publish/runtimes/osx* /app/publish/runtimes/linux-x64/native/libmsalruntime.so

# Etapa 2: solo lo publicado, sobre el runtime de ASP.NET en Alpine y sin root.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
# SqlClient no funciona en modo invariante ("Globalization Invariant Mode is not supported"):
# se instalan las bibliotecas ICU de Alpine y se desactiva ese modo.
# curl se usa en el healthcheck de docker-compose.
RUN apk add --no-cache icu-data-full icu-libs curl
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build /app/publish .
# Usuario sin privilegios que ya trae la imagen oficial.
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=12 \
  CMD curl --fail --silent http://localhost:8080/health | grep -q Healthy || exit 1
ENTRYPOINT ["dotnet", "BattleHub.Trivia.Api.dll"]
