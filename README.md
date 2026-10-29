# battlehub-game-trivia

[![CI](https://github.com/DiegoGutz316/battlehub-game-trivia/actions/workflows/ci.yml/badge.svg)](https://github.com/DiegoGutz316/battlehub-game-trivia/actions/workflows/ci.yml)

Microservicio del juego **Trivia Battle** (Equipo 5) del proyecto BattleHub.

## Entrega de integración local

La integración con el Shell y Matchmaking (partidas SignalR, resultado persistido y cola M2M) está en [Integración con Shell y Matchmaking](docs/integracion-shell-matchmaking.md).

Contratos, arquitectura y reglas del proyecto: [battlehub-contracts](https://github.com/javiercoulon-public/battlehub-contracts).

## Componentes

- **API REST** en .NET 10: `/api/games/trivia/...` (resultados, historial y estadísticas por jugador). Requiere JWT del Shell.
- **Hub de SignalR**: `/hubs/trivia` (partida con sesión) y `/hubs/trivia/practice` (modo práctica / Playground, anónimo).
- **Base de datos** SQL Server propia (esquema EF + banco de preguntas).
- **Microfrontend** Aurelia 2, cargado por el Shell con Module Federation (`triviaGame` / `remoteEntry.js`).

## Estructura

```text
BattleHub.Trivia.slnx                → solución .NET
docker-compose.yml                   → SQL Server + migraciones + seed + API + frontend
.env.example                         → plantilla de variables (copiar a .env; no subir secretos)
.dockerignore                        → contexto de las imágenes .NET (Api, Data, DbInit, Domain)
docs/                                → documentación del equipo
.github/                             → workflows de CI y plantilla de Pull Requests
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

---

## Cómo ejecutar Trivia (oficial: solo Docker Compose)

La forma **oficial** de levantar el sistema completo es Docker Compose. No hace falta instalar .NET ni Node, ni arrancar SQL Server a mano, ni aplicar migraciones ni el seed por separado: Compose construye las imágenes y orquesta todo.

### 1. Herramientas necesarias

- [Git](https://git-scm.com/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Windows/macOS) **o** Docker Engine + Compose v2 (Linux)

Comprueba:

```bash
docker --version
docker compose version
```

### 2. Obtener el repositorio

```bash
git clone https://github.com/DiegoGutz316/battlehub-game-trivia.git
cd battlehub-game-trivia
```

### 3. Configuración inicial y variables de entorno

Copia la plantilla y edita las contraseñas (una sola vez):

```bash
cp .env.example .env
```

En `.env` debes:

1. Definir `MSSQL_SA_PASSWORD` (política fuerte de SQL Server: mayúscula, minúscula, número y símbolo).
2. Usar **la misma** contraseña dentro de `ConnectionStrings__Trivia`.
3. Dejar `TRIVIA_API_URL=http://localhost:5185` si accedes desde el navegador en la máquina anfitriona.
4. Completar `Matchmaking__ClientSecret` solo si vas a avisar el fin de partida a Matchmaking (opcional para probar el juego local).

No subas `.env` al repositorio (está en `.gitignore`).

### 4. Iniciar el proyecto

Desde la raíz del repositorio:

```bash
docker compose up --build
```

Ese único comando:

1. Construye las imágenes de API, DbInit y frontend.
2. Arranca SQL Server y espera el healthcheck.
3. Ejecuta `db-migrate` (esquema EF) y termina.
4. Ejecuta `db-seed` (30 preguntas / 120 opciones, idempotente) y termina.
5. Arranca la API cuando el seed terminó bien.
6. Arranca el frontend cuando `GET /health` de la API responde `Healthy`.

La generación de datos **no** ocurre en el arranque de la API (`Program.cs`). La hacen los servicios one-shot `db-migrate` y `db-seed` (`BattleHub.Trivia.DbInit`).

### 5. Acceder al frontend

- Juego / arnés local: [http://localhost:4002](http://localhost:4002)
- Module Federation: [http://localhost:4002/remoteEntry.js](http://localhost:4002/remoteEntry.js)
- Modo práctica (Playground, sin Shell): abre `http://localhost:4002/` con la API ya arriba vía Compose. Opcional: `http://localhost:4002/?displayName=Jugador%202`

### 6. Acceder a la API

- Base HTTP: [http://localhost:5185](http://localhost:5185)
- Salud: [http://localhost:5185/health](http://localhost:5185/health) → cuerpo `Healthy`
- OpenAPI (solo si la API corre en Development): `/openapi/v1.json`

### 7. Confirmar que los servicios funcionan

En otra terminal:

```bash
docker compose ps
curl -s http://localhost:5185/health
curl -sI http://localhost:4002/ | head -n 1
curl -sI http://localhost:4002/remoteEntry.js | head -n 1
```

Esperado: contenedores `api` y `frontend` en ejecución; `/health` responde `Healthy`; el frontend responde HTTP 200.

### 8. Detener los contenedores

```bash
docker compose down
```

Conserva el volumen `trivia-sql-data` (esquema y preguntas persisten). No uses `docker compose down -v` salvo que quieras borrar la base local a propósito.

### 9. Logs ante errores

```bash
docker compose logs -f api
docker compose logs db-migrate
docker compose logs db-seed
docker compose logs frontend
docker compose logs sqlserver
```

| Síntoma | Qué revisar |
|---|---|
| Compose pide `MSSQL_SA_PASSWORD` | Falta `.env` o la variable está vacía. |
| Login de SQL falla | La contraseña de `ConnectionStrings__Trivia` debe coincidir con `MSSQL_SA_PASSWORD`. |
| El frontend no habla con la API | `TRIVIA_API_URL` debe ser la URL del **navegador** (`http://localhost:5185`), no el nombre del servicio Docker. Rebuild del frontend si la cambias. |
| Matchmaking no recibe el fin de partida | Completar `Matchmaking__ClientSecret` en `.env`. |

---

## Infraestructura: base de datos, variables y puertos

### Motor de base de datos

| Dato | Valor real en este repo |
|---|---|
| Motor | Microsoft SQL Server |
| Imagen Docker | `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04` |
| Servicio Compose | `sqlserver` |
| Base | `BattleHubTrivia` (en la cadena de conexión) |
| Volumen | `trivia-sql-data` → `/var/opt/mssql` |
| Puerto publicado | `1433:1433` |

La API, `db-migrate` y `db-seed` se conectan **dentro de la red Docker** con el host `sqlserver` (nombre del servicio), no con `localhost`. Desde el anfitrión, SQL Server queda en `localhost:1433` si necesitas un cliente externo.

**Inicialización del esquema (R-28 / R-27):** servicio `db-migrate` → `BattleHub.Trivia.DbInit migrate`.

**Datos iniciales (R-28):** servicio `db-seed` → `BattleHub.Trivia.DbInit seed` (`InitialDataGenerator`, idempotente). No duplica preguntas si vuelves a hacer `docker compose up`.

### Variables de entorno

Definidas en `.env.example` / `.env` y usadas por Compose:

| Variable | Descripción | Obligatoria | Ejemplo |
|---|---|---|---|
| `ACCEPT_EULA` | Aceptación de la EULA de SQL Server | Sí (Compose) | `Y` |
| `MSSQL_SA_PASSWORD` | Contraseña del usuario `sa` del contenedor SQL | Sí | `Change_Me_Strong_Passw0rd!` (cámbiala) |
| `ConnectionStrings__Trivia` | Cadena EF Core para migrate/seed/API **en la red Docker** | Sí | `Server=sqlserver,1433;Database=BattleHubTrivia;User Id=sa;Password=…;TrustServerCertificate=True;Encrypt=False` |
| `TRIVIA_API_URL` | URL de la API que ve el **navegador** (build-arg del frontend) | Sí para el frontend | `http://localhost:5185` |
| `Matchmaking__ClientSecret` | Secreto M2M para avisar fin de partida | No (solo Matchmaking) | _(vacío en `.env.example`)_ |
| `ASPNETCORE_URLS` | URL de escucha dentro del contenedor API | Fija en Compose | `http://+:8080` |
| `Cors__Origins__0` / `__1` | Orígenes CORS permitidos | Fijas en Compose | `http://localhost:4000`, `http://localhost:4002` |

Valores públicos opcionales (comentados en `.env.example`; por defecto vienen de `appsettings.json`): `Auth__Issuer`, `Auth__Audience`, `Matchmaking__BaseUrl`, `Matchmaking__TokenEndpoint`, `Matchmaking__Audience`, `Matchmaking__ClientId`, `Profile__BaseUrl`.

No publiques contraseñas reales ni tokens en el README ni en commits.

### Puertos

| Servicio Compose | Puerto interno | Puerto publicado | Propósito |
|---|---|---|---|
| `sqlserver` | 1433 | 1433 | Base de datos |
| `api` | 8080 | 5185 | Backend REST + SignalR |
| `frontend` | 8080 | 4002 | Microfrontend (nginx) |
| `db-migrate` / `db-seed` | — | — | One-shot; sin puertos publicados |

**Interno vs navegador:** entre contenedores se usa la red `trivia` (p. ej. la API habla con `sqlserver:1433`). El navegador usa los puertos **publicados** (`localhost:5185`, `localhost:4002`). Por eso `TRIVIA_API_URL` es `http://localhost:5185` y no `http://api:8080`.

---

## Ejemplos de uso de la API con curl

Base asumida con Compose: `http://localhost:5185`.

### Público: salud de la API

Comprueba que el contenedor `api` está listo. No requiere autenticación.

```bash
curl -s -i http://localhost:5185/health
```

Respuesta esperada:

```http
HTTP/1.1 200 OK
...

Healthy
```

### Autenticación de los endpoints REST

Los endpoints bajo `/api/games/trivia/...` exigen un **JWT Bearer** emitido por el emisor configurado en `Auth:Issuer` / `Auth:Audience` (sesión del Shell). El claim `sub` identifica al jugador.

- Lectura (`GET` historial, stats, resultado): política `TriviaRead` (jugador autenticado o cliente de máquina con permiso de escritura).
- Escritura (`POST` resultados): política `TriviaWrite` (cliente M2M con permiso `game.trivia.results.write`).

Sustituye `TU_TOKEN_JWT` por un token de prueba; **no** uses secretos de producción en ejemplos.

Sin token, la API responde `401 Unauthorized`:

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5185/api/games/trivia/players/user-demo/stats
# 401
```

### Consultar estadísticas de un jugador

```bash
curl -s -X GET "http://localhost:5185/api/games/trivia/players/user-demo/stats" \
  -H "Authorization: Bearer TU_TOKEN_JWT" \
  -H "Accept: application/json"
```

Ejemplo de cuerpo (forma real del DTO `PlayerStatsResponse`):

```json
{
  "userId": "user-demo",
  "matchesPlayed": 2,
  "wins": 1,
  "averageScore": 200,
  "bestScore": 300
}
```

Si el `sub` del token no coincide con `user-demo` (y no eres cliente writer), la API responde `403 Forbidden`.

### Consultar historial de partidas

```bash
curl -s -X GET "http://localhost:5185/api/games/trivia/players/user-demo/history" \
  -H "Authorization: Bearer TU_TOKEN_JWT" \
  -H "Accept: application/json"
```

Devuelve un arreglo de `MatchResultResponse` (más reciente primero).

### Obtener el resultado de una partida

```bash
curl -s -X GET "http://localhost:5185/api/games/trivia/results/match-demo-001" \
  -H "Authorization: Bearer TU_TOKEN_JWT" \
  -H "Accept: application/json"
```

- `200` + JSON si existe y tienes permiso.
- `404` si no hay partida con ese `matchId`.
- `403` si no participaste y no eres writer.

### Guardar un resultado de partida (escritura M2M)

Requiere token de cliente de servicio con permiso de escritura (no el token de un jugador normal).

```bash
curl -s -X POST "http://localhost:5185/api/games/trivia/results" \
  -H "Authorization: Bearer TU_TOKEN_M2M" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json" \
  -d '{
    "matchId": "match-demo-001",
    "gameType": "trivia",
    "startedAt": "2026-10-29T18:00:00Z",
    "finishedAt": "2026-10-29T18:10:00Z",
    "winnerUserId": "user-a",
    "isTie": false,
    "decidedByTiebreak": false,
    "metadata": {},
    "players": [
      {
        "userId": "user-a",
        "displayName": "Ana",
        "score": 300,
        "correctAnswers": 7,
        "correctAnswersTimeMs": 45000,
        "position": 1
      },
      {
        "userId": "user-b",
        "displayName": "Bruno",
        "score": 150,
        "correctAnswers": 4,
        "correctAnswersTimeMs": 52000,
        "position": 2
      }
    ]
  }'
```

Respuesta esperada: `201 Created` con el mismo resultado en el cuerpo. Si `matchId` ya existe: `409 Conflict`.

### Qué no hay como REST

- **No** existen endpoints HTTP públicos de listado de preguntas ni de categorías. El banco de preguntas vive en SQL Server y se entrega durante la partida por **SignalR** (no es un `GET` REST).
- La partida en vivo usa WebSockets / SignalR, no `curl` REST convencional:

| Hub | Ruta | Auth |
|---|---|---|
| Partida con Shell | `/hubs/trivia` | JWT (`access_token` en query al negociar) |
| Modo práctica / Playground | `/hubs/trivia/practice` | Anónimo (`AllowAnonymous`) |

Para ejercitar el Playground usa el frontend en `http://localhost:4002` (botón de modo práctica), no un `curl` HTTP a las preguntas.

---

## Integración con el Shell (Module Federation, ADR-003)

| Dato | Valor |
| --- | --- |
| Nombre del remote | `triviaGame` |
| Punto de entrada | `http://localhost:4002/remoteEntry.js` |
| Módulo expuesto | `./GameModule` (elemento `trivia-game-module`) |
| Prefijo CSS | `trivia-game` |

```json
{
  "trivia": {
    "scope": "triviaGame",
    "url": "http://localhost:4002/remoteEntry.js",
    "module": "./GameModule"
  }
}
```

Con Compose arriba, el Shell puede cargar ese `remoteEntry.js`. Evidencias: `docs/evidencias/adr-003/`.

---

## Pull Requests

Los títulos de PR deben seguir [Conventional Commits](https://www.conventionalcommits.org/), por ejemplo:

- `feat: implementar modo playground`
- `fix(api): corregir conflicto al guardar resultado`
- `docs: actualizar instrucciones de Docker Compose`
- `ci: validar títulos semánticos de Pull Requests`

Hay plantilla en [`.github/PULL_REQUEST_TEMPLATE.md`](.github/PULL_REQUEST_TEMPLATE.md) y un workflow que valida el formato al abrir o editar un PR. Los títulos históricos fuera de convención están listados (sin renombrar) en [docs/pr-titulos-historicos.md](docs/pr-titulos-historicos.md).

---

## Desarrollo y CI (solo contribuidores)

Estas herramientas **no** sustituyen `docker compose up` para levantar el sistema. Sirven para editar código, correr pruebas o el pipeline.

- Backend: [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) — `dotnet test tests/BattleHub.Trivia.UnitTests`
- Frontend: [Node.js 22 LTS](https://nodejs.org/) (`>=22 <23`, `.nvmrc`) — desde `src/trivia-frontend`: `npm ci`, `npm test`, `npm run lint`
- Integración: Testcontainers (hace falta Docker) o `TRIVIA_TEST_CONNECTION` apuntando a LocalDB
- Secretos locales (fuera de Compose): [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) en el proyecto Api; nunca en el repo

### Cobertura

El CI mide cobertura de líneas. Mínimo **85 %** (`COVERAGE_THRESHOLD`). Con `ENFORCE_COVERAGE: "true"`, quedar por debajo falla el job.

### Configuración sensible

Ningún valor sensible se escribe en el repositorio. `appsettings.json` deja vacíos los secretos. El job `secrets` del CI ejecuta [gitleaks](https://github.com/gitleaks/gitleaks) sobre el historial.
