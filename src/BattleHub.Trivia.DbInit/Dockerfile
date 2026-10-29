# syntax=docker/dockerfile:1
# Se construye desde la raíz del repositorio:
#   docker build --file src/BattleHub.Trivia.DbInit/Dockerfile --tag battlehub-trivia-dbinit .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/BattleHub.Trivia.DbInit/BattleHub.Trivia.DbInit.csproj src/BattleHub.Trivia.DbInit/
COPY src/BattleHub.Trivia.Data/BattleHub.Trivia.Data.csproj src/BattleHub.Trivia.Data/
COPY src/BattleHub.Trivia.Domain/BattleHub.Trivia.Domain.csproj src/BattleHub.Trivia.Domain/
RUN dotnet restore src/BattleHub.Trivia.DbInit/BattleHub.Trivia.DbInit.csproj
COPY src/ src/
RUN dotnet publish src/BattleHub.Trivia.DbInit/BattleHub.Trivia.DbInit.csproj \
    --configuration Release --no-restore --output /app/publish -p:UseAppHost=false
RUN rm -rf /app/publish/runtimes/win* /app/publish/runtimes/osx* /app/publish/runtimes/linux-x64/native/libmsalruntime.so

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine
RUN apk add --no-cache icu-data-full icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "BattleHub.Trivia.DbInit.dll"]
