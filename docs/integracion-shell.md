# Integración de Trivia con el Shell

Trivia es una aplicación propia (API, hub de SignalR y microfrontend) y solo se comunica con el Shell. Según los requisitos de la Etapa 2:

- **R-30:** el juego no tiene contacto con Auth0.
- **R-46:** el juego no llama a Matchmaking. Avisa el fin de partida al Shell y el Shell avisa a Matchmaking.
- El Shell decide qué juegos ve cada usuario según Profile, así que el juego tampoco consulta permisos.

El Shell inicia la sesión, abre la sala y le dice al juego quién juega. La API de Trivia no valida tokens: confía en lo que recibe del Shell.

> **Contrato pendiente de cerrar con el Equipo 3.** El documento de requisitos pide comunicación por eventos, pero no define los nombres ni los datos. `context.events`, `battlehub:game-finished` y `mode: 'practice'` coinciden con el PR #6 del Shell. `participants` todavía no está en ese PR.

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
  events: new EventTarget(),
};
```

`getAccessToken` y `getMatchmakingAccessToken` ya no se usan; si el Shell los sigue enviando, el juego los ignora.

### 2. Escuchar el fin de partida y avisar a Matchmaking

Al terminar una partida, el juego emite un evento en `context.events`, el canal que el Shell crea para esa partida. Si el Shell no entrega `events`, el juego termina igual, pero nadie se entera.

```ts
context.events.addEventListener('battlehub:game-finished', event => {
  const { matchId, gameType } = (event as CustomEvent).detail;
  // POST /api/matches/{matchId}/finish en Matchmaking, con el bearer del usuario
});
```

Cada jugador conectado emite su propio evento, y puede llegar otra vez si el jugador se reconecta. No es un problema: `POST /finish` acepta a cualquier participante y no falla si la sala ya está finalizada.

### 3. Ofrecer el modo práctica

Para abrir el juego sin sala, el Shell usa `mode: 'practice'`. No necesita `matchId` ni `participants`, y el juego arranca solo al llamar a `start()`. Solo debe ofrecerlo a usuarios con acceso al juego.

```ts
const context = {
  mode: 'practice',
  gameType: 'trivia',
  currentUser: { id: user.id, displayName: user.displayName },
  events: new EventTarget(),
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

1. Cada jugador se conecta a `/hubs/trivia` y llama a `JoinMatch` con `matchId`, su `userId` y `participants`.
2. El primero que entra crea la partida con esa lista de jugadores. Las llamadas siguientes no la cambian, y quien no esté en la lista recibe `PLAYER_NOT_IN_MATCH`.
3. La partida empieza cuando todos los jugadores de la lista están conectados.
4. Al terminar, el servidor guarda el resultado y envía `GameFinished` a los jugadores. El microfrontend muestra el resultado y emite `battlehub:game-finished`.

Si todos los jugadores se desconectan antes del final, nadie avisa al Shell. La sala queda para el servicio de limpieza de Matchmaking (R-50).

El contrato completo del hub está en [contrato-signalr-trivia.md](contrato-signalr-trivia.md).

## Qué protege y qué no la API

Sin tokens, la API no puede comprobar quién llama:

- El hub confía en el `userId` que recibe. Una vez que una conexión se une, ya no puede cambiar de jugador ni de partida.
- `POST /api/games/trivia/results` y las consultas de resultados, historial y estadísticas no piden credenciales.

Las respuestas, el tiempo y los puntajes siguen calculándose en el servidor. El cliente no los envía.

## Pruebas

Las pruebas automáticas cubren la validación de la sala, la identidad de cada conexión, una partida completa de dos jugadores con el resultado guardado, el evento `battlehub:game-finished` y los modos práctica y playground.

Falta comprobarlo con el Shell real, cuando implemente los tres puntos de arriba:

1. Abrir Trivia desde una sala con dos usuarios y jugar una partida completa.
2. Verificar que Matchmaking marque la sala como finalizada.
3. Abrir el modo práctica desde el Shell.
4. Probar la reconexión durante una partida.

Las partidas activas se mantienen en memoria. Los resultados se guardan en SQL Server.
