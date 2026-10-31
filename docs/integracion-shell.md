# Integración de Trivia con el Shell

Trivia es una aplicación propia (API, hub de SignalR y microfrontend). Según el requisito **R-46**, el juego no llama a Matchmaking: avisa el fin de partida al Shell y el Shell avisa a Matchmaking.

El Shell abre la sala y le dice al juego quiénes participan. La API de Trivia todavía valida el token de la sesión del Shell, como se explica en [Autenticación de Trivia](#autenticación-de-trivia).

> **Contrato propuesto por el Equipo 5, pendiente de confirmar con el Equipo 3.** El documento de requisitos pide comunicación por eventos, pero no define los nombres ni los datos. Lo de esta guía es lo que implementa Trivia hoy.

## Qué debe hacer el Shell

El Shell carga el módulo `./GameModule` del remote `triviaGame` (`http://localhost:4002/remoteEntry.js`), igual que hasta ahora. Cambia lo siguiente.

### 1. Entregar los jugadores de la sala

`GameContext` agrega `participants`, con los jugadores de la sala incluido el usuario actual. Es obligatorio en una partida: sin él, `initialize()` falla con *"El Shell debe entregar los jugadores de la sala."*

```ts
const context: GameContext = {
  matchId: room.id,
  gameType: 'trivia',
  currentUser: { id: user.id, displayName: user.displayName },
  participants: room.participants, // [{ userId, displayName }]
  getAccessToken: () => auth.getAccessToken(),
};
```

`getMatchmakingAccessToken` ya no se usa; si el Shell lo sigue enviando, el juego lo ignora.

### 2. Escuchar el fin de partida y avisar a Matchmaking

Al terminar una partida, el juego emite un evento en `window`:

```ts
window.addEventListener('battlehub:game-finished', event => {
  const { matchId, gameType } = (event as CustomEvent).detail;
  // POST /api/matches/{matchId}/finish en Matchmaking, con el bearer del usuario
});
```

El evento va en `window` porque el Shell crea el módulo con `container.invoke` y lo muestra con `au-compose`, así que el juego no conoce su elemento contenedor.

Cada jugador conectado emite su propio evento, y puede llegar otra vez si el jugador se reconecta. No es un problema: `POST /finish` acepta a cualquier participante y no falla si la sala ya está finalizada.

### 3. Ofrecer el modo práctica

Para abrir el juego sin sala, el Shell usa `mode: 'practice'`. No necesita `matchId` ni `participants`, y el juego arranca solo al llamar a `start()`. Solo debe ofrecerlo a usuarios con acceso al juego.

```ts
const context: GameContext = {
  matchId: '',
  gameType: 'trivia',
  currentUser: { id: user.id, displayName: user.displayName },
  mode: 'practice',
};
```

La práctica no se guarda en el historial y no emite `battlehub:game-finished`, porque no hay sala que cerrar.

## Modos de juego

| Modo | Quién lo abre | Sala | Se guarda | Avisa al Shell |
| --- | --- | --- | --- | --- |
| `match` (por defecto) | Shell, desde una sala | Sí | Sí | Sí |
| `practice` | Shell, sin sala | No | No | No |
| `playground` | `npm start` o `http://localhost:4002`, sin Shell | No | No | No |

`practice` y `playground` usan el hub `/hubs/trivia/practice`, de un solo jugador, y las mismas pantallas que una partida.

## Cómo funciona una partida

1. Cada jugador se conecta a `/hubs/trivia` y llama a `JoinMatch` con `matchId` y `participants`.
2. El primero que entra crea la partida con esa lista de jugadores. Las llamadas siguientes no la cambian, y quien no esté en la lista recibe `PLAYER_NOT_IN_MATCH`.
3. La partida empieza cuando todos los jugadores de la lista están conectados.
4. Al terminar, el servidor guarda el resultado y envía `GameFinished` a los jugadores. El microfrontend muestra el resultado y emite `battlehub:game-finished`.

Si todos los jugadores se desconectan antes del final, nadie avisa al Shell. La sala queda para el servicio de limpieza de Matchmaking (R-50).

El contrato completo del hub está en [contrato-signalr-trivia.md](contrato-signalr-trivia.md).

## Autenticación de Trivia

Trivia acepta el access token de la sesión del Shell. Los valores están en `appsettings.json`:

| Configuración   | Valor                                        | Descripción                                  |
| --------------- | -------------------------------------------- | -------------------------------------------- |
| `Auth:Issuer`   | `https://dev-jaii1peslxnejq0y.us.auth0.com/` | Emisor de los tokens de la sesión del Shell  |
| `Auth:Audience` | `https://api.battlehub.local/profile`        | Audiencia de la plataforma que entrega Shell |

La API valida la firma, el emisor, la audiencia y el vencimiento. El jugador es el claim `sub`. Al ejecutar `JoinMatch`, además consulta en Profile que el jugador tenga el permiso `games.trivia.play`; si no lo tiene, el hub responde `PERMISSION_DENIED`, y si Profile no responde, `PROFILE_UNAVAILABLE`.

## Pruebas

Las pruebas automáticas cubren la validación de la sala, una partida completa de dos jugadores con el resultado guardado, el evento `battlehub:game-finished` y los modos práctica y playground.

Falta comprobarlo con el Shell real, cuando implemente los tres puntos de arriba:

1. Abrir Trivia desde una sala con dos usuarios y jugar una partida completa.
2. Verificar que Matchmaking marque la sala como finalizada.
3. Abrir el modo práctica desde el Shell.
4. Probar la reconexión durante una partida.

Las partidas activas se mantienen en memoria. Los resultados se guardan en SQL Server.
