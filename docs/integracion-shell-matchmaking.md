# Integración de Trivia con Shell y Matchmaking

## Descripción general

BattleHub Trivia es un juego de preguntas que utiliza .NET 10, SQL Server y SignalR. El backend administra las partidas, preguntas, respuestas, puntajes y resultados.

Trivia no tiene login propio: el jugador inicia sesión una sola vez en Shell y Shell entrega el token de esa sesión al juego. El backend de Trivia valida ese token (firma, emisor, audiencia y vencimiento) y aplica sus propias reglas: permiso de Profile para jugar, pertenencia a la partida en Matchmaking y escritura de resultados reservada a servicios.

## Funcionalidades implementadas

- Partidas de diez preguntas con cuatro opciones.
- Quince segundos para responder cada pregunta.
- Cinco segundos para revisar las respuestas.
- Cálculo de puntajes y desempates desde el servidor.
- Validación de participantes antes de ingresar a una partida integrada.
- Reconexión de jugadores durante las partidas.
- Almacenamiento de resultados e historial.
- Comunicación en tiempo real mediante SignalR.
- Notificaciones de finalización hacia Matchmaking con reintentos.

## Autenticación de Trivia

Trivia acepta el access token de la sesión del Shell, el mismo que usan Profile y Matchmaking (login único, acordado con el equipo de Shell; la audiencia compartida está pendiente del ADR correspondiente). Los valores están en `appsettings.json`:

| Configuración   | Valor                                        | Descripción                                  |
| --------------- | -------------------------------------------- | -------------------------------------------- |
| `Auth:Issuer`   | `https://dev-jaii1peslxnejq0y.us.auth0.com/` | Emisor de los tokens de la sesión del Shell  |
| `Auth:Audience` | `https://api.battlehub.local/profile`        | Audiencia de la plataforma que entrega Shell |

Validación de cada token:

- **Firma:** solo `RS256`. Las claves públicas no se configuran a mano: la API las descarga del emisor (`<Auth:Issuer>.well-known/openid-configuration`) y las vuelve a descargar cuando el emisor las rota.
- **Emisor, audiencia y vencimiento:** se rechazan los tokens de otro emisor, de otra audiencia (por ejemplo, la antigua `https://api.battlehub.local/trivia`) y los vencidos. Margen de reloj: 30 segundos.
- **Identidad:** el jugador es el claim `sub`. No se usa el ID token ni identificadores enviados por URL.

Políticas:

| Política | Quién                                                                | Dónde                                |
| -------- | -------------------------------------------------------------------- | ------------------------------------ |
| `Play`   | Usuarios (no tokens de servicio)                                     | Hub `/hubs/trivia`                   |
| `Read`   | El propio jugador, o un servicio con `games.trivia.results.write`    | Resultados, historial y estadísticas |
| `Write`  | Solo servicios (client credentials) con `games.trivia.results.write` | `POST /api/games/trivia/results`     |

Un token de servicio no puede jugar ni hacerse pasar por un jugador, y un jugador no puede escribir resultados.

### Permiso de Profile para jugar

Los permisos de Profile no vienen en el token. Al ejecutar `JoinMatch`, Trivia consulta `GET {Profile:BaseUrl}/api/profiles/me/permissions` con el token del jugador y exige `games.trivia.play`. Profile asigna ese permiso a las cuentas nuevas al sincronizar su perfil, así que una cuenta nueva puede jugar sin configurar nada. Si el jugador no lo tiene, el hub responde `PERMISSION_DENIED`; si Profile no responde, `PROFILE_UNAVAILABLE`.

## Integración con Matchmaking

Para ingresar a una partida integrada, Trivia consulta a Matchmaking y comprueba que:

- La partida exista.
- El tipo de juego sea `trivia`.
- Su estado sea `Started`.
- El usuario autenticado figure entre sus participantes.

El método `JoinMatch` recibe el identificador de la partida y el token que entrega `getMatchmakingAccessToken()` del Shell, con el que Trivia consulta la sala en Matchmaking.

Cuando termina una partida, Trivia guarda el resultado y utiliza una cola de notificaciones para solicitar su finalización en Matchmaking (`POST /api/matches/{matchId}/finish`, ADR-004).

### Token de servicio para la finalización

La finalización usa un token de servicio, no el del jugador. `ClientCredentialsTokenProvider` lo pide al emisor con el flujo *client credentials* del cliente de servicio de Trivia (permiso `matches.finish`, registrado en `GameServices:Clients` de Matchmaking), lo guarda en memoria y lo renueva un minuto antes de que venza. La renovación es del servidor: nunca se pide otro login al jugador.

| Configuración               | Valor                                                      | Dónde                                  |
| --------------------------- | ---------------------------------------------------------- | -------------------------------------- |
| `Matchmaking:TokenEndpoint` | `https://dev-jaii1peslxnejq0y.us.auth0.com/oauth/token`    | `appsettings.json`                     |
| `Matchmaking:Audience`      | `https://api.battlehub.local/profile`                      | `appsettings.json`                     |
| `Matchmaking:ClientId`      | `xYhNYG9lOGLA6KvEM0Y4cccH2f7JDD5x` (identificador público) | `appsettings.json`                     |
| `Matchmaking:ClientSecret`  | Secreto del cliente de servicio                            | **user-secrets** o variable de entorno |

```powershell
dotnet user-secrets set "Matchmaking:ClientSecret" "<secreto del cliente de servicio>" --project src/BattleHub.Trivia.Api
```

Si el secreto falta o el emisor no entrega el token, la notificación queda pendiente en la cola y se reintenta; el resultado ya quedó guardado.

## Integración con Shell

El frontend de Trivia puede cargarse desde Shell mediante el módulo remoto correspondiente.

Configuración local de referencia:

- API: `http://localhost:5185`
- Hub SignalR: `/hubs/trivia`
- Frontend remoto: `http://localhost:4002/remoteEntry.js`
- Módulo: `triviaGame` / `./GameModule`

Para iniciar el frontend:

```powershell
cd src/trivia-frontend
npm.cmd ci
npm.cmd start
```

Shell (rama `fix/login-unico-juegos`, PR #5) entrega en `GameContext`:

- `getAccessToken()`: el token de la sesión del Shell, que el frontend de Trivia usa para la API y el hub (`access_token` en SignalR).
- `getMatchmakingAccessToken()`: el token para que Trivia consulte la sala en Matchmaking.

Hoy ambos son el mismo token de la sesión. No se generan tokens en el navegador, no se usa el ID token y el navegador nunca recibe tokens de servicio. El frontend de Trivia (`game-module.ts` y `signalr-trivia-client.ts`) no necesitó cambios: pide un token nuevo al Shell en cada conexión y reconexión.

## Modo práctica

El Shell abre el juego sin sala con `mode: 'practice'` en `GameContext`. No necesita `matchId` ni tokens, y el juego arranca solo al llamar a `start()`. Solo debe ofrecerlo a usuarios con acceso al juego.

```ts
const context: GameContext = {
  matchId: '',
  gameType: 'trivia',
  currentUser: { id: user.id, displayName: user.displayName },
  mode: 'practice',
};
```

Usa el hub de un solo jugador `/hubs/trivia/practice` y las mismas pantallas que una partida. No se guarda en el historial y no notifica a Matchmaking, porque no hay sala.

> **Contrato propuesto por el Equipo 5, pendiente de confirmar con el Equipo 3.** El documento de requisitos no define cómo el Shell abre este modo.

## Ejecución del backend

El proyecto utiliza .NET 10 y SQL Server.

Para avisar el fin de partida a Matchmaking debe configurarse `Matchmaking:ClientSecret` en user-secrets, como se explica en [Token de servicio para la finalización](#token-de-servicio-para-la-finalización). Sin él la API arranca, pero las finalizaciones quedan pendientes.

Desde la raíz del repositorio:

```powershell
dotnet run --project src/BattleHub.Trivia.Api
```

Antes de `dotnet run` de la API, hay que preparar el esquema y el banco de preguntas con el proceso independiente `BattleHub.Trivia.DbInit` (no se hace en el arranque de la API):

```powershell
dotnet run --project src/BattleHub.Trivia.DbInit -- migrate
dotnet run --project src/BattleHub.Trivia.DbInit -- seed
```

Por defecto se usa la base LocalDB `BattleHubTrivia`. Para usar otra base se configura `ConnectionStrings:Trivia`.

Para levantar SQL Server, migraciones, datos, API y frontend con un solo comando, ver la sección **Docker Compose** del [README](../README.md).

## Pruebas de integración

Las pruebas automáticas (`AuthenticationTests`, `LiveHubTests`, `ProfileGatewayTests`, `ClientCredentialsTokenProviderTests`) cubren: tokens ausentes, vencidos, de otro emisor, de otra audiencia o con firma incorrecta; jugador sin `games.trivia.play`; jugador ajeno a la partida; jugador que intenta escribir resultados; token de servicio que intenta jugar o escribir sin permiso; renovación del token de servicio; y una partida completa de dos jugadores con resultado guardado y finalización enviada.

Con los servicios reales (Profile, Matchmaking, Shell y Trivia) falta comprobar manualmente, con cuentas de la clase:

1. Iniciar sesión una vez en Shell y entrar a Trivia sin otra ventana de Authorize.
2. Completar una partida con dos usuarios distintos; verificar puntajes, resultado e historial.
3. Verificar que Matchmaking reciba la finalización (requiere `Matchmaking:ClientSecret`).
4. Probar con una cuenta nueva, sin configurar tokens a mano.
5. Probar reconexión, cierre de sesión, cambio de cuenta y sesión vencida.
6. Comprobar que un usuario sin permiso o ajeno a la partida no pueda jugar.

## Estado actual y pendientes

Trivia acepta el token de la sesión del Shell (login único) y renueva por su cuenta el token de servicio para Matchmaking.

Todavía están pendientes:

- El ADR de la audiencia compartida (`https://api.battlehub.local/profile`) con Shell y Profile.
- La comprobación manual con los servicios reales y cuentas de la clase descrita arriba.
- Revisar las advertencias de seguridad de las dependencias.

Las partidas activas se mantienen en memoria. Los resultados y las notificaciones persistidas se conservan en SQL Server.
