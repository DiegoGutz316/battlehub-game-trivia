# Reglas de Trivia Battle

## Formato de la partida

- Cada partida consta de 10 preguntas.
- Cada pregunta presenta 4 opciones de respuesta.
- Solo una opción es correcta.
- Cada jugador dispone de 15 segundos para responder cada pregunta.
- La primera respuesta válida enviada por un jugador es definitiva y no puede cambiarse.
- Si todos los jugadores activos responden antes de que finalicen los 15 segundos, la pregunta se cierra inmediatamente.
- Si todavía existen jugadores activos sin responder, la pregunta permanece abierta hasta alcanzar su tiempo límite.
- Al cerrar una pregunta comienza una fase de revelación de 5 segundos antes de continuar con la siguiente pregunta.

## Inicio de la partida

Trivia Battle no decide por sí mismo cuándo una sala está lista para comenzar.

La partida inicia cuando BattleHub o el servicio responsable del matchmaking indique que la partida está lista.

Por esta razón, Trivia Battle no define un número mínimo independiente de jugadores para iniciar la partida.

## Sistema de puntuación

Una respuesta correcta otorga 100 puntos base más un bonus determinado por la velocidad de respuesta.

El bonus se calcula utilizando los segundos completos restantes al momento en que el servidor recibe la respuesta:

`Puntaje = 100 + (segundos completos restantes × 10)`

Los segundos restantes se calculan en el servidor y se redondean hacia abajo.

Por ejemplo, si quedan 7,9 segundos al recibirse una respuesta correcta, se utilizan 7 segundos para calcular el bonus:

`100 + (7 × 10) = 170 puntos`

El puntaje máximo posible por pregunta es de 240 puntos.

Las respuestas incorrectas y las preguntas que finalicen sin una respuesta válida otorgan 0 puntos.

El cliente nunca determina ni envía los segundos restantes utilizados para calcular el puntaje.

## Cierre y revelación de una pregunta

Cuando finaliza una pregunta, ya sea porque terminó el tiempo o porque todos los jugadores activos respondieron, comienza una fase de revelación de 5 segundos.

Durante esta fase se muestra:

- la respuesta correcta;
- si la respuesta del jugador fue correcta o incorrecta;
- los puntos obtenidos en la pregunta;
- el marcador actualizado.

Después de los 5 segundos comienza automáticamente la siguiente pregunta, salvo que se haya completado la última pregunta de la partida.

## Marcador

El marcador permanece visible durante la partida.

Después del cierre de cada pregunta, el servidor actualiza el puntaje acumulado de los jugadores.

Los jugadores desconectados permanecen en el marcador y deben mostrarse como desconectados mientras la partida continúe.

## Ganador de la partida

Después de completar las 10 preguntas, gana el jugador que haya acumulado la mayor cantidad de puntos.

## Desempate

Si dos o más jugadores terminan la partida con el mismo puntaje, gana el jugador que haya utilizado menos tiempo total en sus respuestas correctas.

El tiempo utilizado para resolver el desempate es calculado por el servidor.

Si los jugadores continúan empatados después de aplicar este criterio, la partida finaliza en empate.

## Desconexiones y reconexiones

La desconexión de un jugador no pausa la partida.

El servidor conserva el estado y el puntaje acumulado del jugador desconectado.

Mientras permanezca desconectado, la partida continúa normalmente para los demás jugadores. Las preguntas que finalicen mientras el jugador se encuentre desconectado y no hayan sido respondidas otorgan 0 puntos.

El jugador puede reconectarse mientras la partida continúe activa.

Cuando se reconecta, recibe el estado actual de la partida.

Si existe una pregunta activa, todavía queda tiempo disponible y el jugador no había respondido esa pregunta antes de desconectarse, puede responder utilizando el tiempo restante.

Si el jugador ya había respondido la pregunta antes de desconectarse, no puede enviar una segunda respuesta.
