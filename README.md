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

Pruebas (los mismos comandos que corre el CI):

```bash
dotnet test --filter Category=Unit          # pruebas unitarias
dotnet test --filter Category=Integration   # pruebas de integración
```

Cada clase de prueba debe marcarse con `[Trait("Category", "Unit")]` o `[Trait("Category", "Integration")]` para que el CI la ejecute.

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
