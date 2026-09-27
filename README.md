# battlehub-game-trivia

Microservicio del juego **Trivia Battle** (Equipo 5) del proyecto BattleHub.

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
  trivia-frontend/                   → microfrontend Aurelia
    src/contracts/                   → GameModule y GameContext (contrato de battlehub-contracts)
    src/components/                  → vistas del juego (pregunta, opciones, marcador)
    src/services/                    → conexión al hub y cliente de la API
tests/
  BattleHub.Trivia.UnitTests/        → pruebas unitarias del backend (Category=Unit)
  BattleHub.Trivia.IntegrationTests/ → pruebas de integración API + base de datos (Category=Integration)
  trivia-frontend/                   → pruebas unitarias del microfrontend
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

Pendiente: se completará cuando se cree el proyecto del microfrontend.
