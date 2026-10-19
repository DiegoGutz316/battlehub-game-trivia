# battlehub-game-trivia

[![CI](https://github.com/DiegoGutz316/battlehub-game-trivia/actions/workflows/ci.yml/badge.svg)](https://github.com/DiegoGutz316/battlehub-game-trivia/actions/workflows/ci.yml)

Microservicio del juego **Trivia Battle** (Equipo 5) del proyecto BattleHub.

## Entrega de integración local

La implementación local del Shell y el juego se explica en [Integración con Auth0 y Matchmaking](docs/integracion-shell-auth0.md). Incluye partidas SignalR, resultado persistido y cola M2M. Revisar esa guía antes de los comandos históricos de este README; no implica publicación ni aprobación de extensiones del contrato central.

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
src/
  BattleHub.Trivia.Api/              → backend .NET 10 (punto de entrada)
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
tests/
  BattleHub.Trivia.UnitTests/        → pruebas unitarias del backend (Category=Unit)
  BattleHub.Trivia.IntegrationTests/ → pruebas de integración API + base de datos (Category=Integration)
  trivia-frontend/                   → pruebas unitarias del microfrontend (Jest, archivos *.spec.ts)
```

## Cómo correr localmente

### Backend (.NET 10)

Requisito: [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0).

Desde la raíz del repositorio (donde está `BattleHub.Trivia.slnx`):

```bash
dotnet restore
dotnet build
dotnet run --project src/BattleHub.Trivia.Api
```

La API queda en `http://localhost:5185`. Para verificar que está arriba: `GET http://localhost:5185/health` → `Healthy`.

Pruebas (el CI ejecuta todas las pruebas de cada proyecto):

```bash
dotnet test tests/BattleHub.Trivia.UnitTests          # pruebas unitarias
dotnet test tests/BattleHub.Trivia.IntegrationTests   # pruebas de integración (SQL Server)
```

Las pruebas de integración usan SQL Server en un contenedor (Testcontainers, requiere Docker). Sin Docker se puede usar LocalDB definiendo antes `TRIVIA_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true'`.

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

#### Modo desarrollo (sin Shell)

`npm start` abre un arnés que simula lo que hace el Shell: llama a `initialize()` con un contexto de prueba y tiene botones para `start()`, `pause()` y `dispose()`. Si el juego rechaza una llamada, muestra el error como lo haría el Shell (`LIFECYCLE_ERROR`).

La partida y el jugador simulados se cambian por la URL. Para probar con dos jugadores, abrir dos pestañas:

```text
http://localhost:4002/
http://localhost:4002/?userId=user-dev-2&displayName=Jugador%202
```

#### Integración con el Shell (Module Federation, ADR-003)

| Dato | Valor |
|---|---|
| Nombre del remote | `triviaGame` |
| Punto de entrada | `http://localhost:4002/remoteEntry.js` (local) |
| Módulo expuesto | `./GameModule`, que exporta la clase `GameModule` (elemento `trivia-game-module`) |
| Prefijo de clases CSS | `trivia-game` |

Entrada para el `remotes.config.json` del Shell:

```json
{ "trivia": { "scope": "triviaGame", "url": "http://localhost:4002/remoteEntry.js", "module": "./GameModule" } }
```

Para probarlo dentro del Shell se puede usar la prueba de concepto del Equipo 3 ([battlehub-shell/poc](https://github.com/KeynerMC/battlehub-shell/tree/main/poc)): con `npm start` corriendo aquí, poner esa entrada en `poc/shell/config/remotes.local.json` y `gameType: 'trivia'` en `poc/shell/src/my-app.ts`. Capturas de esa prueba: `docs/evidencias/adr-003/`.

Si se usa otro paquete `@aurelia/*` además de los de `mf-shared.js`, hay que agregarlo ahí y avisar al Equipo 3.

#### Pruebas

Las pruebas viven en `tests/trivia-frontend/` (fuera del proyecto, como pide la estructura mínima del proyecto). Para que funcionen desde ahí:

- Jest busca las pruebas en esa carpeta y resuelve los paquetes desde `src/trivia-frontend/node_modules` (configuración `jest` en `package.json`).
- `npm run lint` ejecuta ESLint desde la raíz del repositorio para poder revisar también esa carpeta.
- `tests/trivia-frontend/tsconfig.json` permite que el editor reconozca los tipos de Aurelia y Jest en las pruebas.

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

Ningún valor sensible (contraseñas, Client Secret, tokens, API keys) se escribe en archivos del repositorio. `appsettings.json` solo tiene valores públicos y deja vacíos los sensibles.

| Clave en `appsettings.json` | Variable de entorno | ¿Sensible? | Dónde va |
|---|---|---|---|
| `ConnectionStrings:Trivia` | `ConnectionStrings__Trivia` | Sí, si incluye usuario y contraseña | Local: user-secrets (vacía = LocalDB `BattleHubTrivia`). Docker o servidor: variable de entorno |
| `Matchmaking:Auth0:ClientSecret` | `Matchmaking__Auth0__ClientSecret` | **Sí** | Local: user-secrets. Docker o servidor: variable de entorno |
| `Matchmaking:Auth0:Domain`, `Matchmaking:Auth0:Audience`, `Matchmaking:Auth0:ClientId` | `Matchmaking__Auth0__Domain`, etc. | No: identificadores públicos de Auth0 | `appsettings.json` |
| `Matchmaking:BaseUrl`, `Cors:Origins`, `Database:MigrateOnStartup`, `Database:SeedQuestions` | `Matchmaking__BaseUrl`, `Cors__Origins__0`, etc. | No | `appsettings.json`; se reemplazan por variable de entorno según el ambiente |
| `TRIVIA_API_URL` (frontend) | `TRIVIA_API_URL` | No | Variable de entorno antes de `npm start` o `npm run build` |

**En local**, los valores sensibles se guardan con [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), fuera de la carpeta del repositorio (en Windows, `%APPDATA%\Microsoft\UserSecrets\`). Solo se leen en el entorno *Development*:

```bash
# Desde la raíz del repositorio
dotnet user-secrets set "ConnectionStrings:Trivia" "Server=...;Database=BattleHubTrivia;..." --project src/BattleHub.Trivia.Api
dotnet user-secrets list --project src/BattleHub.Trivia.Api
```

Para el Client Secret conviene usar `scripts/Set-MatchmakingCredentials.ps1`, que lo pide de forma oculta: así no queda en el historial de la terminal.

**En Docker o en un servidor**, copiar `.env.example` como `.env` (ignorado por git), completarlo ahí y pasarlo con `docker run --env-file .env ...`, o usar la configuración de secretos del servidor.

**Revisión automática:** el job `secrets` del CI ejecuta [gitleaks](https://github.com/gitleaks/gitleaks) sobre todo el historial en cada pull request y en cada push a `main`. Si encuentra un secreto, el job falla y el PR no se puede mergear. Para revisarlo antes de subir cambios: `gitleaks git .` desde la raíz del repositorio.

**Si se sube un secreto por error**, hay que considerarlo expuesto: borrarlo del código no alcanza, porque sigue en el historial. Hay que cambiarlo de inmediato (por ejemplo, generar un Client Secret nuevo en Auth0) y avisar al equipo. Si gitleaks marca algo que no es un secreto, se agrega su *fingerprint* a `.gitleaksignore`, explicando el motivo en el PR.
