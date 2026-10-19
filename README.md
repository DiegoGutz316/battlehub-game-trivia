# battlehub-game-trivia

[![CI](https://github.com/DiegoGutz316/battlehub-game-trivia/actions/workflows/ci.yml/badge.svg)](https://github.com/DiegoGutz316/battlehub-game-trivia/actions/workflows/ci.yml)

Microservicio del juego **Trivia Battle** (Equipo 5) del proyecto BattleHub.

## Entrega de integración local

La implementación local del Shell y el juego se explica en [Integración con Shell y Matchmaking](docs/integracion-shell-matchmaking.md). Incluye partidas SignalR, resultado persistido y cola M2M. Revisar esa guía antes de los comandos históricos de este README; no implica publicación ni aprobación de extensiones del contrato central.

Contratos, arquitectura y reglas del proyecto: [battlehub-contracts](https://github.com/javiercoulon-public/battlehub-contracts).

## Componentes

- **API REST** en .NET 10: `/api/games/trivia/...` (resultados, historial y estadísticas por jugador).
- **Hub de SignalR** propio: `/hubs/trivia` (mecánica del juego en tiempo real).
- **Base de datos propia** para partidas, resultados y estadísticas (motor definido por ADR en `battlehub-contracts/adrs`).
- **Microfrontend** en Aurelia, cargado por el Shell mediante Module Federation.

## Estructura

```text
BattleHub.Trivia.slnx                → solución .NET (agrupa los 5 proyectos)
docs/                                → documentación del equipo (reglas del juego, contratos internos)
.github/workflows/                   → pipeline de CI (secretos, build, pruebas y cobertura)
.env.example                         → nombres de las variables de configuración, sin valores reales
.dockerignore                        → contexto de la imagen de la API: solo sus tres proyectos de .NET
src/
  BattleHub.Trivia.Api/              → backend .NET 10 (punto de entrada)
    Dockerfile                       → imagen Docker de la API (se construye desde la raíz del repositorio)
    Controllers/                     → endpoints REST /api/games/trivia/...
    Hubs/                            → hub de SignalR /hubs/trivia
    Services/                        → lógica del juego (preguntas, puntajes, ganador) y notificación a Matchmaking
    Dtos/                            → modelos de entrada/salida de la API
  BattleHub.Trivia.Domain/           → núcleo del dominio, sin dependencias de otros proyectos
    Entities/                        → entidades (partida, jugador, resultado, pregunta)
    Interfaces/                      → contratos de repositorios (ej. IResultRepository)
  BattleHub.Trivia.Data/             → persistencia (depende de Domain)
    Context/                         → contexto/conexión de la base de datos
    Repositories/                    → implementaciones de las interfaces de Domain
  trivia-frontend/                   → microfrontend Aurelia 2 (webpack + TypeScript), remote de Module Federation
    mf-shared.js                     → dependencias compartidas con el Shell (ADR-003, no modificar)
    src/game-module.*                → GameModule: lo que el Shell carga (initialize, start, pause, dispose)
    src/contracts/                   → GameModule y GameContext (contrato de battlehub-contracts)
    src/dev/                         → arnés de desarrollo que simula el Shell (solo para npm start)
    src/components/                  → vistas del juego (pregunta, opciones, marcador)
    src/services/                    → conexión al hub y cliente de la API
    test/                            → pruebas del microfrontend (Jest, archivos *.spec.ts), con las mismas carpetas que src/
tests/
  BattleHub.Trivia.UnitTests/        → pruebas unitarias del backend (Category=Unit)
  BattleHub.Trivia.IntegrationTests/ → pruebas de integración API + base de datos (Category=Integration)
```

## Cómo correr localmente

### Backend (.NET 10)

Requisito: [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0).

Antes del primer `dotnet run` hay que configurar `Auth:PublicKey`, la clave pública con la que la API valida los tokens. Es una clave pública, no un secreto. Cómo configurarla se explica en [Integración con Shell y Matchmaking](docs/integracion-shell-matchmaking.md#autenticación-de-trivia). Sin esa clave la API no arranca. Por ejemplo, con user-secrets:

```bash
# Desde la raíz del repositorio
dotnet user-secrets set "Auth:PublicKey" "<clave pública en Base64>" --project src/BattleHub.Trivia.Api
```

Desde la raíz del repositorio (donde está `BattleHub.Trivia.slnx`):

```bash
dotnet restore
dotnet build
dotnet run --project src/BattleHub.Trivia.Api
```

`dotnet run` usa el perfil `http` de `launchSettings.json`, con el entorno *Development*. Con ese perfil la API aplica las migraciones y carga sola las 30 preguntas del banco inicial, sin scripts. Si la base LocalDB `BattleHubTrivia` no existe, la crea; si ya tiene preguntas, no las vuelve a cargar.

La API queda en `http://localhost:5185`. Para verificar que está arriba: `GET http://localhost:5185/health` → `Healthy`.

Pruebas (el CI ejecuta todas las pruebas de cada proyecto):

```bash
dotnet test tests/BattleHub.Trivia.UnitTests          # pruebas unitarias
dotnet test tests/BattleHub.Trivia.IntegrationTests   # pruebas de integración (SQL Server)
```

Las pruebas de integración usan SQL Server en un contenedor (Testcontainers, requiere Docker). Sin Docker se puede usar LocalDB definiendo antes `TRIVIA_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true'`.

#### Docker

`src/BattleHub.Trivia.Api/Dockerfile` tiene dos etapas: compila y publica la API con el SDK de .NET 10 y copia solo el resultado a la imagen de ASP.NET para Alpine, que corre sin root y escucha en el puerto 8080. La imagen incluye las bibliotecas ICU, porque SqlClient no funciona sin ellas, y no lleva ningún secreto: la configuración se pasa al ejecutar el contenedor.

Como la API usa los proyectos Data y Domain, la imagen se construye desde la raíz del repositorio. El `.dockerignore` de la raíz deja pasar solo esos tres proyectos:

```bash
docker build --file src/BattleHub.Trivia.Api/Dockerfile --tag battlehub-trivia-api .
docker run --rm -p 5185:8080 --env-file .env battlehub-trivia-api
```

La API queda en `http://localhost:5185`, el mismo puerto que con `dotnet run`. El archivo `.env` se crea copiando `.env.example` (ver [Configuración y secretos](#configuración-y-secretos)):

- `Auth__PublicKey` es obligatoria: sin ella el contenedor no arranca.
- `ConnectionStrings__Trivia` debe apuntar a un SQL Server con usuario y contraseña de SQL, que acepte conexiones TCP. LocalDB no funciona dentro de un contenedor Linux. Para usar el SQL Server de la propia máquina, el servidor es `host.docker.internal`, por ejemplo `Server=host.docker.internal,1433;Database=BattleHubTrivia;User Id=<usuario>;Password=<contraseña>;TrustServerCertificate=true`. En Linux hay que agregar `--add-host=host.docker.internal:host-gateway` al `docker run`.
- Para que el contenedor aplique las migraciones y cargue las 30 preguntas al arrancar, se agregan `Database__MigrateOnStartup=true` y `Database__SeedQuestions=true`. Dentro del contenedor no se usa `launchSettings.json` y el entorno es *Production*, así que por defecto están en `false`.

El job `backend-docker` del CI construye la imagen en cada pull request y la levanta junto a un SQL Server temporal, con una contraseña y una clave pública que se generan en el momento y no se guardan. Comprueba que `GET /health` responda `Healthy` y que la base tenga 5 migraciones, 30 preguntas y 120 opciones. El tamaño de la imagen se muestra en el resumen del job `backend-docker`.

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

La partida y el jugador simulados se cambian por la URL. Para probar con dos jugadores, abrir dos pestañas:

```text
http://localhost:4002/
http://localhost:4002/?userId=user-dev-2&displayName=Jugador%202
```

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

- **Backend:** las pruebas unitarias y de integración se ejecutan con `--collect:"XPlat Code Coverage"` (coverlet) y ReportGenerator une los dos reportes. El detalle por clase queda en el resumen del job `backend` en GitHub Actions.
- **Frontend:** Jest genera `coverage/coverage-summary.json` (y el reporte HTML en `coverage/lcov-report/`).
- **En cada pull request**, el job `coverage-comment` publica un comentario con la cobertura de backend y frontend, y lo actualiza en cada push en vez de crear uno nuevo.

El mínimo es **85 %** de líneas (`COVERAGE_THRESHOLD` en `.github/workflows/ci.yml`). Mientras la cobertura no lo alcance, `ENFORCE_COVERAGE` está en `"false"`: el CI muestra un aviso pero no falla. Para que los jobs `backend` y `frontend` fallen por debajo del mínimo, cambiar esa línea a `"true"`.

Para ver la cobertura localmente:

```bash
# Backend (desde la raíz del repositorio)
dotnet tool restore
dotnet test tests/BattleHub.Trivia.UnitTests --collect:"XPlat Code Coverage" --results-directory coverage/raw
dotnet test tests/BattleHub.Trivia.IntegrationTests --collect:"XPlat Code Coverage" --results-directory coverage/raw
dotnet reportgenerator "-reports:coverage/raw/**/coverage.cobertura.xml" -targetdir:coverage/backend -reporttypes:Html
# abrir coverage/backend/index.html

# Frontend (desde src/trivia-frontend)
npx jest
# abrir coverage/lcov-report/index.html
```

## Configuración y secretos

Ningún valor sensible (contraseñas, tokens, API keys) se escribe en archivos del repositorio. `appsettings.json` solo tiene valores públicos y deja vacíos los sensibles.

| Clave en `appsettings.json`                           | Variable de entorno                                     | ¿Sensible?                          | Dónde va                                                                                                                                                                                                      |
| ----------------------------------------------------- | ------------------------------------------------------- | ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:Trivia`                            | `ConnectionStrings__Trivia`                             | Sí, si incluye usuario y contraseña | Local: user-secrets (vacía = LocalDB `BattleHubTrivia`). Docker o servidor: variable de entorno                                                                                                               |
| `Matchmaking:ServiceToken`                            | `Matchmaking__ServiceToken`                             | **Sí**                              | Local: user-secrets. Docker o servidor: variable de entorno                                                                                                                                                   |
| `Auth:Issuer`, `Auth:Audience`                        | `Auth__Issuer`, `Auth__Audience`                        | No                                  | `appsettings.json`; se pueden reemplazar según el ambiente                                                                                                                                                    |
| `Auth:PublicKey`                                      | `Auth__PublicKey`                                       | No, es una clave pública            | Local: user-secrets o configuración del entorno. Nunca incluir la clave privada                                                                                                                               |
| `Matchmaking:BaseUrl`, `Cors:Origins`                 | `Matchmaking__BaseUrl`, `Cors__Origins__0`, etc.        | No                                  | `appsettings.json`; se reemplazan por variable de entorno según el ambiente                                                                                                                                   |
| `Database:MigrateOnStartup`, `Database:SeedQuestions` | `Database__MigrateOnStartup`, `Database__SeedQuestions` | No                                  | `false` en `appsettings.json` y `true` en el perfil de desarrollo (`launchSettings.json`). Fuera de _Development_, o con `dotnet run --no-launch-profile`, se activan por configuración o variable de entorno |
| `TRIVIA_API_URL` (frontend)                           | `TRIVIA_API_URL`                                        | No                                  | Variable de entorno antes de `npm start` o `npm run build`                                                                                                                                                    |

**En local**, los valores sensibles se guardan con [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), fuera de la carpeta del repositorio (en Windows, `%APPDATA%\Microsoft\UserSecrets\`). Solo se leen en el entorno _Development_:

```bash
# Desde la raíz del repositorio
dotnet user-secrets set "ConnectionStrings:Trivia" "Server=...;Database=BattleHubTrivia;..." --project src/BattleHub.Trivia.Api
dotnet user-secrets list --project src/BattleHub.Trivia.Api
```

**En Docker o en un servidor**, copiar `.env.example` como `.env` (ignorado por git), completarlo ahí y pasarlo con `docker run --env-file .env ...`, o usar la configuración de secretos del servidor.

**Revisión automática:** el job `secrets` del CI ejecuta [gitleaks](https://github.com/gitleaks/gitleaks) sobre todo el historial en cada pull request y en cada push a `main`. Si encuentra un secreto, el job falla y el PR no se puede mergear. Para revisarlo antes de subir cambios: `gitleaks git .` desde la raíz del repositorio.

**Si se sube un secreto por error**, hay que considerarlo expuesto: borrarlo del código no alcanza, porque sigue en el historial. Hay que cambiarlo de inmediato (por ejemplo, revocar el token expuesto y generar uno nuevo) y avisar al equipo. Si gitleaks marca algo que no es un secreto, se agrega su _fingerprint_ a `.gitleaksignore`, explicando el motivo en el PR.
