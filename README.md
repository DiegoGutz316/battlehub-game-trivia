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
.github/workflows/                   → pipeline de CI (build + pruebas unitarias + pruebas de integración)
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
    test/                            → pruebas del microfrontend (Jest, archivos *.spec.ts), con las mismas carpetas que src/
tests/
  BattleHub.Trivia.UnitTests/        → pruebas unitarias del backend (Category=Unit)
  BattleHub.Trivia.IntegrationTests/ → pruebas de integración API + base de datos (Category=Integration)
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
