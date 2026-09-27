# Reglas de Trivia Battle

## Formato de la partida

- Cada partida consta de 10 preguntas.
- Cada pregunta presenta 4 opciones de respuesta.
- Solo una opción es correcta.
- Cada jugador dispone de 15 segundos para responder cada pregunta.
- La partida continúa automáticamente cuando finaliza el tiempo establecido para cada pregunta.

## Sistema de puntuación

Una respuesta correcta otorga 100 puntos base más un bonus determinado por la velocidad de respuesta.

El bonus se calcula utilizando los segundos restantes al momento de responder:

`Puntaje = 100 + (segundos restantes × 10)`

Ejemplo: si un jugador responde correctamente cuando quedan 8 segundos, obtiene:

`100 + (8 × 10) = 180 puntos`

Las respuestas incorrectas y las respuestas realizadas fuera del tiempo establecido otorgan 0 puntos.

## Ganador de la partida

Después de completar las 10 preguntas, gana el jugador que haya acumulado la mayor cantidad de puntos.

## Desempate

Si dos o más jugadores terminan la partida con el mismo puntaje, gana el jugador que haya utilizado menos tiempo total en sus respuestas correctas.

Si los jugadores continúan empatados después de aplicar este criterio, la partida finaliza en empate.

## Desconexiones y reconexiones

La desconexión de un jugador no pausa la partida.

El servidor conserva el estado y el puntaje acumulado del jugador desconectado.

Mientras permanezca desconectado, la partida continúa normalmente para los demás jugadores y las preguntas que el jugador no pueda responder otorgan 0 puntos.

El jugador puede reconectarse mientras la partida continúe activa.

Cuando se reconecta, recibe el estado actual de la partida y continúa jugando desde ese momento.
