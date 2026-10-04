# Contrato de eventos SignalR - Trivia Battle

## Hub

El Hub de Trivia Battle estará disponible en:

`/hubs/trivia`

Este documento define la comunicación en tiempo real entre el cliente y el servidor de Trivia Battle. Las reglas del juego están en `reglas-trivia.md`; este contrato las implementa.

## Convenciones

- Todas las fechas y horas se expresan en UTC, en formato ISO-8601 con sufijo `Z`.
- Los temporizadores se comunican mediante una hora absoluta de vencimiento (`expiresAtUtc`) y no mediante segundos restantes.
- Los eventos que involucran tiempo incluyen `serverTimeUtc`, la hora del servidor al enviar el evento. El cliente calcula la diferencia entre `serverTimeUtc` y su propio reloj y la aplica a su temporizador, para que un reloj local desfasado no altere el tiempo mostrado.
- El servidor es la autoridad sobre el estado de la partida, el tiempo, las respuestas correctas, los puntajes y el resultado final. El cliente nunca envía tiempos ni puntajes.
- La respuesta correcta nunca se incluye en un evento mientras la pregunta está activa.
- Los métodos del Hub reciben objetos como parámetros para mantener un contrato extensible y consistente.
- Los nombres de métodos y eventos usan PascalCase; los nombres de campos usan camelCase.
- Cada evento indica su **destinatario**: _grupo_ (todos los jugadores de la partida) o _individual_ (solo un jugador).

## Identidad del jugador

La implementación de integración local obtiene `userId` del claim `sub` del JWT autenticado, con la audiencia `https://api.battlehub.local/trivia`. El nombre visible proviene del participante devuelto por Matchmaking. No acepta `devUserId` ni `devDisplayName` por URL, tampoco en desarrollo.

Antes de `JoinMatch` se valida el detalle de la sala en Matchmaking. La extensión local propuesta es `JoinMatch({ matchId, matchmakingAccessToken })`: el segundo valor es un token de usuario para la audiencia Profile/Matchmaking, obtenido del Shell con `getMatchmakingAccessToken()`. El token que autentica el hub sigue siendo el de Trivia. Ambos proveedores de contexto son extensiones propuestas, no cambios aprobados del contrato central.

Los callbacks a Matchmaking usan un tercer flujo: token M2M del backend, con `matches.finish`. El navegador no recibe el Client Secret. Ver [guía de integración](integracion-shell-auth0.md).

---

# Estructuras comunes

## Jugador (`Player`)

Se usa en `CurrentState` y `ScoreboardUpdated`.

- `userId`: identificador del jugador.
- `displayName`: nombre mostrado.
- `score`: puntaje acumulado.
- `isConnected`: `true` si el jugador está conectado al hub en este momento.

La lista de jugadores incluye a **todos los jugadores de la partida**, incluso a los que todavía no se han conectado por primera vez (con `isConnected: false`). Esto permite mostrar, por ejemplo, "2 de 4 jugadores conectados".

## Pregunta (`Question`)

Se usa en `QuestionStarted` y en `CurrentState`.

- `questionId`: identificador de la pregunta.
- `questionNumber`: número de la pregunta en la partida (de 1 a `totalQuestions`).
- `totalQuestions`: total de preguntas de la partida.
- `text`: texto de la pregunta.
- `category`: categoría de la pregunta. Puede ser `null` si la pregunta no tiene categoría.
- `options`: lista de 4 opciones; cada una contiene `answerId` y `text`.
- `expiresAtUtc`: hora en que se cierra la pregunta.

La respuesta correcta no forma parte de esta estructura.

## Fases de la partida (`phase`)

- `waiting`: la partida aún no comienza; se espera a los jugadores.
- `playing`: hay una pregunta activa.
- `reviewing`: la pregunta anterior se cerró y se está mostrando su resultado durante 5 segundos antes de la siguiente.
- `finished`: la partida terminó.

---

# Cliente → Servidor

## JoinMatch

Solicita unirse o reincorporarse a una partida.

### Datos

- `matchId`: identificador de la partida.

```json
{
  "matchId": "match-001"
}
```

La operación es idempotente para un mismo jugador y partida. Si un jugador pierde la conexión y vuelve a ejecutar `JoinMatch`, recupera su participación existente, con su puntaje, y no se agrega como jugador duplicado.

Después de unirse correctamente, el servidor envía `CurrentState` al jugador y `PlayerJoined` o `PlayerReconnected` al grupo.

### Errores posibles

- `MATCH_NOT_FOUND`: la partida no existe.
- `PLAYER_NOT_IN_MATCH`: el jugador no pertenece a la partida.
- `MATCH_FINISHED`: la partida ya terminó.
- `UNAUTHORIZED`: no se pudo identificar al jugador (identidad simulada ausente antes de E4, o JWT inválido a partir de E4).

## SubmitAnswer

Envía la respuesta seleccionada para la pregunta actual.

### Datos

- `matchId`: identificador de la partida.
- `questionId`: identificador de la pregunta.
- `answerId`: identificador de la opción seleccionada.

```json
{
  "matchId": "match-001",
  "questionId": "question-05",
  "answerId": "answer-b"
}
```

El cliente no envía `userId`, tiempo, puntaje ni indica si la respuesta es correcta.

El servidor identifica al jugador, registra la hora de recepción, valida la respuesta y controla el tiempo.

Cada jugador puede responder **una sola vez** por pregunta. La primera respuesta recibida es la definitiva.

Un jugador que se reconecta mientras una pregunta sigue activa puede responderla con el tiempo que le quede, si aún no la había respondido.

### Errores posibles

- `MATCH_NOT_FOUND`: la partida no existe.
- `PLAYER_NOT_IN_MATCH`: el jugador no pertenece a la partida.
- `QUESTION_NOT_ACTIVE`: el `questionId` no corresponde a la pregunta activa.
- `QUESTION_CLOSED`: la respuesta llegó después de `expiresAtUtc`. Otorga 0 puntos.
- `ALREADY_ANSWERED`: el jugador ya había respondido esta pregunta.
- `INVALID_ANSWER`: el `answerId` no es una de las opciones de la pregunta.

---

# Servidor → Cliente

## CurrentState

**Destinatario:** individual.

Envía al jugador el estado completo de la partida después de unirse o reconectarse. Con este evento el cliente puede dibujar la pantalla correcta sin haber recibido los eventos anteriores.

### Datos

- `matchId`
- `phase`
- `serverTimeUtc`
- `totalQuestions`
- `players`: lista de `Player`.
- `currentQuestion`: solo cuando `phase` es `playing`; si no, `null`.
- `lastQuestionResult`: solo cuando `phase` es `reviewing`; si no, `null`.

Cuando existe, `currentQuestion` contiene la estructura `Question` más:

- `hasAnswered`: `true` si el jugador ya respondió esta pregunta.
- `selectedAnswerId`: la opción que eligió, o `null` si no ha respondido.

Cuando existe, `lastQuestionResult` contiene los mismos datos que `QuestionClosed`.

```json
{
  "matchId": "match-001",
  "phase": "playing",
  "serverTimeUtc": "2026-09-27T05:00:07Z",
  "totalQuestions": 10,
  "players": [
    {
      "userId": "user-123",
      "displayName": "Ana",
      "score": 540,
      "isConnected": true
    },
    {
      "userId": "user-456",
      "displayName": "Luis",
      "score": 420,
      "isConnected": false
    }
  ],
  "currentQuestion": {
    "questionId": "question-05",
    "questionNumber": 5,
    "totalQuestions": 10,
    "text": "¿Cuál es la capital de Costa Rica?",
    "category": "Geografía",
    "options": [
      {
        "answerId": "answer-a",
        "text": "Liberia"
      },
      {
        "answerId": "answer-b",
        "text": "San José"
      },
      {
        "answerId": "answer-c",
        "text": "Cartago"
      },
      {
        "answerId": "answer-d",
        "text": "Alajuela"
      }
    ],
    "expiresAtUtc": "2026-09-27T05:00:15Z",
    "hasAnswered": true,
    "selectedAnswerId": "answer-b"
  },
  "lastQuestionResult": null
}
```

## PlayerJoined

**Destinatario:** grupo.

Notifica que un jugador se conectó a la partida por primera vez.

### Datos

- `userId`
- `displayName`

## PlayerDisconnected

**Destinatario:** grupo.

Notifica que un jugador perdió la conexión.

### Datos

- `userId`
- `displayName`

La desconexión no elimina al jugador ni pausa la partida. El jugador conserva su puntaje y las preguntas que no responda le otorgan 0 puntos.

## PlayerReconnected

**Destinatario:** grupo.

Notifica que un jugador recuperó su conexión.

### Datos

- `userId`
- `displayName`

## GameStarted

**Destinatario:** grupo.

Notifica que la partida comenzó.

### Datos

- `matchId`
- `totalQuestions`
- `startedAtUtc`
- `serverTimeUtc`

La partida comienza cuando BattleHub o el servicio responsable del matchmaking indique que está lista. Trivia Battle no define de forma independiente un número mínimo de jugadores para iniciar.

## QuestionStarted

**Destinatario:** grupo.

Envía una nueva pregunta.

### Datos

La estructura `Question` más `serverTimeUtc`.

```json
{
  "questionId": "question-05",
  "questionNumber": 5,
  "totalQuestions": 10,
  "text": "¿Cuál es la capital de Costa Rica?",
  "category": "Geografía",
  "options": [
    {
      "answerId": "answer-a",
      "text": "Liberia"
    },
    {
      "answerId": "answer-b",
      "text": "San José"
    },
    {
      "answerId": "answer-c",
      "text": "Cartago"
    },
    {
      "answerId": "answer-d",
      "text": "Alajuela"
    }
  ],
  "expiresAtUtc": "2026-09-27T05:00:15Z",
  "serverTimeUtc": "2026-09-27T05:00:00Z"
}
```

La respuesta correcta no se envía en este evento.

## AnswerReceived

**Destinatario:** individual (el jugador que respondió).

Confirma que la respuesta fue registrada por el servidor.

### Datos

- `questionId`
- `answerId`: la opción registrada.
- `receivedAtUtc`

Con este evento la interfaz bloquea las demás opciones y muestra que debe esperar el cierre de la pregunta.

## QuestionClosed

**Destinatario:** individual (cada jugador recibe su propia versión).

Notifica el cierre de una pregunta y revela su resultado. Se envía a todos los jugadores de la partida, pero los campos personales son distintos para cada uno.

Una pregunta se cierra cuando ocurre una de estas condiciones:

- se alcanza `expiresAtUtc`; o
- todos los jugadores activos han respondido antes del vencimiento.

Después del cierre comienza una fase de revisión de 5 segundos.

### Datos

- `questionId`
- `correctAnswerId`
- `selectedAnswerId`: la opción que eligió el jugador, o `null` si no respondió.
- `isCorrect`
- `pointsEarned`
- `totalScore`
- `nextQuestionAtUtc`: hora en que se enviará la siguiente pregunta; `null` si era la última.
- `serverTimeUtc`

Con `selectedAnswerId` la interfaz distingue una respuesta incorrecta (`selectedAnswerId` con valor e `isCorrect: false`) de una pregunta no respondida (`selectedAnswerId: null`).

```json
{
  "questionId": "question-05",
  "correctAnswerId": "answer-b",
  "selectedAnswerId": "answer-b",
  "isCorrect": true,
  "pointsEarned": 180,
  "totalScore": 720,
  "nextQuestionAtUtc": "2026-09-27T05:00:20Z",
  "serverTimeUtc": "2026-09-27T05:00:15Z"
}
```

## ScoreboardUpdated

**Destinatario:** grupo.

Envía el marcador actualizado después de cada pregunta y cuando cambia la conexión de un jugador.

### Datos

- `players`: lista de `Player`, ordenada de mayor a menor puntaje.

Los jugadores desconectados permanecen en el marcador con `isConnected: false`.

## GameFinished

**Destinatario:** grupo.

Notifica el final de la partida.

### Datos

- `matchId`
- `players`
- `winnerUserId`
- `isTie`
- `decidedByTiebreak`
- `finishedAtUtc`

Cada jugador contiene:

- `userId`
- `displayName`
- `score`
- `correctAnswers`: cantidad de respuestas correctas.
- `correctAnswersTimeMs`: tiempo total, en milisegundos, usado en sus respuestas correctas. Es el dato utilizado para el criterio de desempate.
- `position`

### Reglas del resultado

- Si un jugador tiene más puntos que todos los demás, es el ganador: `winnerUserId` contiene su identificador, `isTie: false` y `decidedByTiebreak: false`.
- Si varios jugadores empatan en puntos y el desempate por tiempo determina un ganador: `winnerUserId` contiene su identificador, `isTie: false` y `decidedByTiebreak: true`.
- Si el empate persiste después del criterio de desempate: `winnerUserId: null`, `isTie: true` y `decidedByTiebreak: false`.
- Los jugadores que quedan empatados de forma definitiva comparten la misma `position`.

```json
{
  "matchId": "match-001",
  "players": [
    {
      "userId": "user-123",
      "displayName": "Ana",
      "score": 1520,
      "correctAnswers": 9,
      "correctAnswersTimeMs": 41200,
      "position": 1
    },
    {
      "userId": "user-456",
      "displayName": "Luis",
      "score": 1520,
      "correctAnswers": 9,
      "correctAnswersTimeMs": 45800,
      "position": 2
    }
  ],
  "winnerUserId": "user-123",
  "isTie": false,
  "decidedByTiebreak": true,
  "finishedAtUtc": "2026-09-27T05:03:10Z"
}
```

## GameError

**Destinatario:** individual (el jugador cuya operación falló).

Comunica un error relacionado con una operación del juego.

### Datos

- `code`: uno de los códigos listados abajo.
- `message`: descripción legible del error.

```json
{
  "code": "QUESTION_CLOSED",
  "message": "El tiempo para responder esta pregunta terminó."
}
```

### Códigos de error

| Código                | Operación               | Significado                           |
| --------------------- | ----------------------- | ------------------------------------- |
| `MATCH_NOT_FOUND`     | JoinMatch, SubmitAnswer | La partida no existe                  |
| `PLAYER_NOT_IN_MATCH` | JoinMatch, SubmitAnswer | El jugador no pertenece a la partida  |
| `MATCH_FINISHED`      | JoinMatch               | La partida ya terminó                 |
| `UNAUTHORIZED`        | JoinMatch               | No se pudo identificar al jugador     |
| `QUESTION_NOT_ACTIVE` | SubmitAnswer            | La pregunta no es la activa           |
| `QUESTION_CLOSED`     | SubmitAnswer            | La respuesta llegó fuera de tiempo    |
| `ALREADY_ANSWERED`    | SubmitAnswer            | El jugador ya respondió esta pregunta |
| `INVALID_ANSWER`      | SubmitAnswer            | La opción no pertenece a la pregunta  |

---

# Reglas de tiempo y puntuación

Cada pregunta permanece activa durante un máximo de 15 segundos.

Si todos los jugadores activos responden antes de que transcurran los 15 segundos, la pregunta se cierra inmediatamente.

La primera respuesta válida enviada por un jugador es definitiva.

El puntaje de una respuesta correcta se calcula mediante:

`Puntaje = 100 + (segundos completos restantes × 10)`

Los segundos completos restantes:

- son calculados exclusivamente por el servidor;
- se obtienen utilizando la hora de recepción de la respuesta;
- se redondean hacia abajo;
- nunca son enviados por el cliente.

El puntaje máximo posible por pregunta es de 240 puntos.

Una respuesta incorrecta o una pregunta finalizada sin respuesta válida otorga 0 puntos.

Después del cierre de una pregunta se inicia una fase `reviewing` de 5 segundos.

---

# Secuencia de una partida

1. Cada jugador llama a `JoinMatch` y recibe `CurrentState` (`phase: waiting`). El grupo recibe `PlayerJoined`.
2. Cuando BattleHub o el servicio responsable del matchmaking indique que la partida está lista, el servidor envía `GameStarted` y el primer `QuestionStarted` (`phase: playing`).
3. Cada jugador puede llamar una sola vez a `SubmitAnswer` para la pregunta activa y recibe `AnswerReceived`.
4. La pregunta se cierra cuando todos los jugadores activos hayan respondido o cuando se alcance `expiresAtUtc`. Cada jugador recibe su `QuestionClosed` y el grupo recibe `ScoreboardUpdated` (`phase: reviewing`).
5. Durante 5 segundos se muestra el resultado de la pregunta. Al llegar `nextQuestionAtUtc`, se inicia la siguiente pregunta y se repiten los pasos 3 a 5 hasta completar la pregunta 10.
6. Después de cerrar y revisar la última pregunta, el grupo recibe `GameFinished` (`phase: finished`).

En cualquier momento, una desconexión genera `PlayerDisconnected` y una reconexión genera `CurrentState` para el jugador y `PlayerReconnected` para el grupo.

Si un jugador se reconecta durante una pregunta activa, todavía queda tiempo disponible y no había respondido previamente, puede responder utilizando el tiempo restante.

---

# Responsabilidades

## Servidor

El servidor es responsable de:

- identificar al jugador;
- mantener el estado de la partida;
- controlar el temporizador y la pausa entre preguntas;
- cerrar anticipadamente una pregunta cuando todos los jugadores activos hayan respondido;
- validar las respuestas;
- garantizar que cada jugador responda una sola vez por pregunta;
- determinar si una respuesta llegó dentro del tiempo permitido;
- determinar la respuesta correcta;
- calcular los segundos completos restantes utilizando su propia hora;
- calcular los puntos;
- mantener el marcador;
- resolver empates;
- determinar el ganador;
- gestionar el estado de jugadores desconectados y reconectados.

## Cliente

El cliente es responsable de:

- solicitar la unión a una partida;
- representar el estado recibido mediante `CurrentState`;
- mostrar jugadores conectados y desconectados;
- mostrar preguntas y opciones;
- enviar la respuesta seleccionada;
- representar el tiempo restante con `expiresAtUtc`, corregido con `serverTimeUtc`;
- mostrar la confirmación de una respuesta enviada y bloquear las demás opciones;
- mostrar la respuesta correcta, la elegida y los puntos ganados cuando la pregunta haya cerrado;
- mostrar la fase de revisión de 5 segundos;
- actualizar el marcador;
- mostrar el resultado final, incluido el desempate o el empate;
- mostrar los errores recibidos desde el servidor.

---

# Cambios respecto a la versión anterior

- Se agregó `serverTimeUtc` a los eventos relacionados con tiempo para corregir relojes locales desfasados.
- Se definió cómo se utiliza una identidad simulada antes de implementar JWT en E4.
- Se agregaron las estructuras comunes `Player` y `Question`.
- La lista de jugadores incluye a los jugadores que aún no se han conectado.
- Se agregó la fase `reviewing`, con una duración de 5 segundos.
- Se agregó `lastQuestionResult` en `CurrentState`.
- `currentQuestion` incluye `hasAnswered` y `selectedAnswerId` para permitir la recuperación correcta después de una reconexión.
- `QuestionClosed` incluye `selectedAnswerId` y `nextQuestionAtUtc`.
- `AnswerReceived` incluye `answerId`.
- `GameFinished` incluye `correctAnswers`, `correctAnswersTimeMs` y `decidedByTiebreak`.
- Se agregaron errores para `SubmitAnswer` y el error `UNAUTHORIZED`.
- Se indicó el destinatario de cada evento.
- Se agregó la secuencia completa de una partida.
- Se estableció que cada jugador puede responder una sola vez por pregunta.
- Se estableció que una pregunta se cierra anticipadamente cuando todos los jugadores activos han respondido.
- Se estableció el redondeo hacia abajo para el cálculo del bonus.
- Se estableció un máximo de 240 puntos por pregunta.
- Se estableció que el inicio de la partida depende de BattleHub o del servicio responsable del matchmaking.
