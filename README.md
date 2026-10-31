# battlehub-game-trivia

[![CI](https://github.com/DiegoGutz316/battlehub-game-trivia/actions/workflows/ci.yml/badge.svg)](https://github.com/DiegoGutz316/battlehub-game-trivia/actions/workflows/ci.yml)

Microservicio del juego **Trivia Battle** (Equipo 5) del proyecto BattleHub.

## Entrega de integración local

Trivia no tiene contacto con Auth0, Profile ni Matchmaking: solo se comunica con el Shell. Lo que el Shell debe entregar y escuchar está en [Integración con el Shell](docs/integracion-shell.md).

Contratos, arquitectura y reglas del proyecto: [battlehub-contracts](https://github.com/javiercoulon-public/battlehub-contracts).

## Componentes

- **API REST** en .NET 10: `/api/games/trivia/...` (resultados, historial y estadísticas por jugador).
- **Hub de SignalR** propio: `/hubs/trivia` (mecánica del juego en tiempo real).
- **Base de datos propia** para partidas, resultados y estadísticas (motor definido por ADR en `battlehub-contracts/adrs`).
- **Microfrontend** en Aurelia, cargado por el Shell mediante Module Federation.

## Estructura

```text
BattleHub.Trivia.slnx                → solución .NET
docker-compose.yml                   → SQL Server + migraciones + seed + API + frontend
.env.example                         → plantilla de variables (copiar a .env; no subir secretos)
.dockerignore                        → contexto de las imágenes .NET (Api, Data, DbInit, Domain)
docs/                                → documentación del equipo
.github/workflows/                   → pipeline de CI
src/
  BattleHub.Trivia.Api/              → backend .NET 10 (punto de entrada)
    Dockerfile                       → imagen de la API (construir desde la raíz)
  BattleHub.Trivia.DbInit/           → proceso independiente: migrate | seed
    Dockerfile                       → imagen one-shot de esquema y datos
  BattleHub.Trivia.Domain/           → dominio (entidades e interfaces)
  BattleHub.Trivia.Data/             → EF Core, migraciones, InitialDataGenerator
  trivia-frontend/                   → microfrontend Aurelia 2 + Dockerfile (nginx)
tests/
  BattleHub.Trivia.UnitTests/
  BattleHub.Trivia.IntegrationTests/
```

## Docker Compose (comando único)

Requisito: [Docker Desktop](https://www.docker.com/products/docker-desktop/) (o Docker Engine + Compose v2).

Una sola vez, desde la raíz del repositorio:

```bash
cp .env.example .env
# Editar .env: MSSQL_SA_PASSWORD y la misma contraseña dentro de ConnectionStrings__Trivia
```

Arranque completo (construye imágenes, crea contenedores, espera SQL Server, aplica migraciones, inserta las 30 preguntas, levanta API y frontend):

```bash
docker compose up --build
```

Apagado (conserva el volumen `trivia-sql-data`; los datos persisten):

```bash
docker compose down
```

| Servicio | URL / puerto | Rol |
|---|---|---|
| Frontend | http://localhost:4002 | Microfrontend (también `remoteEntry.js`) |
| API | http://localhost:5185 | REST + SignalR (`/health` → `Healthy`) |
| SQL Server | localhost:1433 | Persistencia (volumen `trivia-sql-data`) |

Orden real de arranque:

1. `sqlserver` — healthy cuando acepta `SELECT 1`.
2. `db-migrate` — aplica las migraciones EF y termina.
3. `db-seed` — inserta el banco de preguntas con `InitialDataGenerator` (idempotente) y termina.
4. `api` — arranca solo cuando el seed terminó bien; no migra ni siembra.
5. `frontend` — arranca cuando `/health` de la API responde `Healthy`.

La generación de datos **no** ocurre en `Program.cs`. Es el servicio `db-seed` (`BattleHub.Trivia.DbInit seed`). Si se vuelve a ejecutar `docker compose up`, el seed no duplica preguntas.

Logs:

```bash
docker compose logs -f api
docker compose logs db-migrate
docker compose logs db-seed
```

Problemas frecuentes:

| Síntoma | Qué revisar |
|---|---|
| Compose pide `MSSQL_SA_PASSWORD` | Falta `.env` o la variable está vacía. |
| Login de SQL falla | La contraseña de `ConnectionStrings__Trivia` debe ser la misma que `MSSQL_SA_PASSWORD`. |
| El frontend no habla con la API | `TRIVIA_API_URL` debe ser la URL del **navegador** (`http://localhost:5185`), no el nombre del servicio Docker. Hay que reconstruir el frontend si se cambia. |

## Cómo correr localmente (sin Compose)

### Backend (.NET 10)

Requisito: [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0).

Preparar esquema y datos, luego arrancar la API (LocalDB por defecto si no hay cadena de conexión):

```bash
dotnet restore
dotnet build
dotnet run --project src/BattleHub.Trivia.DbInit -- migrate
dotnet run --project src/BattleHub.Trivia.DbInit -- seed
dotnet run --project src/BattleHub.Trivia.Api
```

La API queda en `http://localhost:5185` (`GET /health` → `Healthy`).

Pruebas:

```bash
dotnet test tests/BattleHub.Trivia.UnitTests
dotnet test tests/BattleHub.Trivia.IntegrationTests
```

Las pruebas de integración usan SQL Server en un contenedor (Testcontainers). Sin Docker: `TRIVIA_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true'`.

#### Imágenes individuales

```bash
docker build --file src/BattleHub.Trivia.Api/Dockerfile --tag battlehub-trivia-api .
docker build --file src/BattleHub.Trivia.DbInit/Dockerfile --tag battlehub-trivia-dbinit .
docker run --rm -p 5185:8080 --env-file .env battlehub-trivia-api
```

El job `backend-docker` del CI construye API y DbInit, aplica `migrate` + `seed` (y repite `seed` para comprobar que no hay duplicados), levanta la API y verifica 6 migraciones, 30 preguntas y 120 opciones.

### Frontend (Aurelia)

Requisito: [Node.js 24 LTS](https://nodejs.org/) (`>=24.11.0 <25`, fijado en `.nvmrc` y en `engines` por el ADR-003).

Todos los comandos se corren desde `src/trivia-frontend`:

```bash
cd src/trivia-frontend
npm ci           # instalar dependencias (usa package-lock.json)
npm start        # juego solo, en http://localhost:4002 (modo desarrollo, sin Shell)
```

Los mismos pasos que corre el CI:

```bash
npm run lint     # ESLint (código y pruebas) + Stylelint
npm run build    # build de producción en dist/ (genera dist/remoteEntry.js)
npm test         # lint + pruebas unitarias con Jest
```

#### Docker

`src/trivia-frontend/Dockerfile` tiene dos etapas: compila con Node 24 Alpine y copia solo `dist/` a nginx sin root (`nginx-unprivileged`, variante `alpine-slim`), que escucha en el puerto 8080. Desde la raíz del repositorio:

```bash
docker build -t battlehub-trivia-frontend src/trivia-frontend
docker run --rm -p 4002:8080 battlehub-trivia-frontend
```

El juego queda en `http://localhost:4002` y el Shell lo carga desde `http://localhost:4002/remoteEntry.js`, igual que con `npm start`. nginx agrega el encabezado CORS que pide el ADR-003 y evita que el navegador guarde una versión vieja de `remoteEntry.js`.

La URL de la API queda escrita en el bundle al compilar, así que se cambia al construir la imagen: `docker build --build-arg TRIVIA_API_URL=https://api.ejemplo.com -t battlehub-trivia-frontend src/trivia-frontend`.

El job `frontend-docker` del CI construye la imagen en cada pull request, la levanta y comprueba que sirva `remoteEntry.js` con CORS.

#### Modo desarrollo (sin Shell)

`npm start` abre un arnés que simula lo que hace el Shell: llama a `initialize()` con un contexto de prueba y tiene botones para `start()`, `pause()` y `dispose()`. Si el juego rechaza una llamada, muestra el error como lo haría el Shell (`LIFECYCLE_ERROR`).

#### Modo playground (sin Shell)

Sin el Shell no hay sesión ni sala, así que el juego ofrece el botón **Jugar en modo playground**: una partida de un solo jugador con las mismas pantallas (diez preguntas, temporizador, revisión y resultado) contra el hub `/hubs/trivia/practice` de la API. No necesita Auth0 ni ningún otro servicio, solo la API de Trivia. La partida no se guarda en el historial ni cuenta en las estadísticas, y al terminar se puede jugar otra vez.

```text
http://localhost:4002/
http://localhost:4002/?displayName=Jugador%202   (otro nombre para el jugador)
```

El modo práctica es distinto: lo abre el Shell, sin sala, para un usuario con acceso al juego. Ver [Modos de juego](docs/integracion-shell.md#modos-de-juego).

#### Integración con el Shell (Module Federation, ADR-003)

| Dato                  | Valor                                                                             |
| --------------------- | --------------------------------------------------------------------------------- |
| Nombre del remote     | `triviaGame`                                                                      |
| Punto de entrada      | `http://localhost:4002/remoteEntry.js` (local)                                    |
| Módulo expuesto       | `./GameModule`, que exporta la clase `GameModule` (elemento `trivia-game-module`) |
| Prefijo de clases CSS | `trivia-game`                                                                     |

Entrada para el `remotes.config.json` del Shell:

```json
{
  "trivia": {
    "scope": "triviaGame",
    "url": "http://localhost:4002/remoteEntry.js",
    "module": "./GameModule"
  }
}
```

Para probarlo dentro del Shell se puede usar la prueba de concepto del Equipo 3 ([battlehub-shell/poc](https://github.com/KeynerMC/battlehub-shell/tree/main/poc)): con `npm start` corriendo aquí, poner esa entrada en `poc/shell/config/remotes.local.json` y `gameType: 'trivia'` en `poc/shell/src/my-app.ts`. Capturas de esa prueba: `docs/evidencias/adr-003/`.

Si se usa otro paquete `@aurelia/*` además de los de `mf-shared.js`, hay que agregarlo ahí y avisar al Equipo 3.

#### Pruebas

Las pruebas viven en `src/trivia-frontend/test/`, dentro del proyecto como en la plantilla del ADR-003, y repiten las carpetas de `src/` (por ejemplo, `test/dev/` prueba `src/dev/`). No necesitan configuración especial: Jest las busca en `test/` y `npm run lint` las revisa junto con `src/`.

## Cobertura de código

El CI mide la cobertura de líneas en cada ejecución:

- **Backend:** las pruebas unitarias y de integración se ejecutan con `--collect:"XPlat Code Coverage"` (coverlet) y ReportGenerator une los dos reportes. El detalle por clase queda en el resumen del job `backend` en GitHub Actions. No se cuenta el código que genera el compilador (`*.g.cs`, `*.generated.cs`), como `OpenApiXmlCommentSupport.generated.cs` de `Microsoft.AspNetCore.OpenApi`: no lo escribe el equipo y se crea en `obj/` al compilar.
- **Frontend:** Jest genera `coverage/coverage-summary.json` (y el reporte HTML en `coverage/lcov-report/`).
- **En cada pull request**, el job `coverage-comment` publica un comentario con la cobertura de backend y frontend, y lo actualiza en cada push en vez de crear uno nuevo.

El mínimo es **85 %** de líneas (`COVERAGE_THRESHOLD` en `.github/workflows/ci.yml`), tanto en el backend como en el frontend. Con `ENFORCE_COVERAGE: "true"`, si alguna de las dos partes queda por debajo, su job (`backend` o `frontend`) falla y el PR no pasa el CI. Exactamente 85 % cumple.

Para ver la cobertura localmente:

```bash
# Backend (desde la raíz del repositorio)
dotnet tool restore
dotnet test tests/BattleHub.Trivia.UnitTests --collect:"XPlat Code Coverage" --results-directory coverage/raw
dotnet test tests/BattleHub.Trivia.IntegrationTests --collect:"XPlat Code Coverage" --results-directory coverage/raw
dotnet reportgenerator "-reports:coverage/raw/**/coverage.cobertura.xml" -targetdir:coverage/backend -reporttypes:Html "-filefilters:-*.g.cs;-*.generated.cs"
# abrir coverage/backend/index.html

# Frontend (desde src/trivia-frontend)
npx jest
# abrir coverage/lcov-report/index.html
```

## Configuración y secretos

Ningún valor sensible (contraseñas, tokens, API keys) se escribe en archivos del repositorio. `appsettings.json` solo tiene valores públicos y deja vacíos los sensibles.

| Clave en `appsettings.json` | Variable de entorno                    | ¿Sensible?                          | Dónde va                                                                                                             |
| --------------------------- | -------------------------------------- | ----------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:Trivia`  | `ConnectionStrings__Trivia`            | Sí, si incluye usuario y contraseña | Local: user-secrets (vacía = LocalDB `BattleHubTrivia`). Docker o servidor: variable de entorno                      |
| `Cors:Origins`              | `Cors__Origins__0`, `Cors__Origins__1` | No                                  | `appsettings.json`; se reemplazan por variable de entorno según el ambiente                                          |
| `MSSQL_SA_PASSWORD`         | `MSSQL_SA_PASSWORD`                    | **Sí**                              | Solo en `.env` para Docker Compose                                                                                   |
| `TRIVIA_API_URL` (frontend) | `TRIVIA_API_URL`                       | No                                  | Build-arg del frontend / variable antes de `npm start` o `npm run build`. En Compose: URL del navegador hacia la API |

**En local**, los valores sensibles se guardan con [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), fuera de la carpeta del repositorio (en Windows, `%APPDATA%\Microsoft\UserSecrets\`). Solo se leen en el entorno _Development_:

```bash
# Desde la raíz del repositorio
dotnet user-secrets set "ConnectionStrings:Trivia" "Server=...;Database=BattleHubTrivia;..." --project src/BattleHub.Trivia.Api
dotnet user-secrets list --project src/BattleHub.Trivia.Api
```

**En Docker o en un servidor**, copiar `.env.example` como `.env` (ignorado por git), completarlo ahí y pasarlo con `docker run --env-file .env ...`, o usar la configuración de secretos del servidor.

**Revisión automática:** el job `secrets` del CI ejecuta [gitleaks](https://github.com/gitleaks/gitleaks) sobre todo el historial en cada pull request y en cada push a `main`. Si encuentra un secreto, el job falla y el PR no se puede mergear. Para revisarlo antes de subir cambios: `gitleaks git .` desde la raíz del repositorio.

**Si se sube un secreto por error**, hay que considerarlo expuesto: borrarlo del código no alcanza, porque sigue en el historial. Hay que cambiarlo de inmediato (por ejemplo, revocar el token expuesto y generar uno nuevo) y avisar al equipo. Si gitleaks marca algo que no es un secreto, se agrega su _fingerprint_ a `.gitleaksignore`, explicando el motivo en el PR.
