# Integración local: Shell, Auth0 y Matchmaking

Actualizada contra `DiegoGutz316/battlehub-game-trivia`, `main`, commit `58b2732`, el 2026-10-04. El código de integración ya está publicado en esa base. Esta entrega añade la configuración pública del cliente M2M confirmado y estas instrucciones; esos ajustes siguen locales. Las extensiones de contexto y participantes son acuerdos de integración propuestos, no cambios aprobados del contrato del profesor.

## Qué se implementó

- JWT RS256 para API y `/hubs/trivia`, audiencia exclusiva de jugadores `https://api.battlehub.local/trivia`, identidad desde `sub`.
- Cliente SignalR real y pantalla de preguntas, opciones, temporizador, marcador, revisión, ganador, historial y estadísticas.
- Diez preguntas del banco SQL existente; cuatro opciones; quince segundos por pregunta; cinco segundos de revisión. El servidor calcula puntos y desempates. El máximo de 240 puntos también se respeta en respuestas instantáneas.
- Validación de sala `Started`, `gameType: trivia` y participantes contra Matchmaking antes de entrar al grupo. No se acepta identidad por parámetros `devUserId`/`devDisplayName`.
- Reconexión durante una partida, respuestas únicas, cierre por tiempo y conservación del marcador de desconectados. Se espera a todos los participantes para la primera pregunta, con diez segundos de gracia como máximo después del primer ingreso validado.
- Resultado y aviso pendiente guardados en la misma transacción de EF. Una cola SQL avisa al backend de Matchmaking mediante M2M, sin bloquear los temporizadores.
- Reintentos transitorios: hasta tres solicitudes por despacho ante 408, 429, 5xx o fallos de red; próximo despacho treinta segundos después. Otros rechazos quedan `Blocked` para corregir configuración y revisar manualmente. Los avisos pendientes sobreviven al reinicio del backend.
- CORS para Shell 4000 y arnés 4002; resultados legibles solo para participantes; historial/estadísticas propios.

## Configuración pública

| Uso | Valor |
| --- | --- |
| Tenant / Domain | `dev-jaii1peslxnejq0y.us.auth0.com` |
| Issuer | `https://dev-jaii1peslxnejq0y.us.auth0.com/` |
| Audiencia de jugadores Trivia | `https://api.battlehub.local/trivia` |
| Audiencia M2M de Matchmaking | `https://api.battlehub.local/profile` |
| API / Hub local | `http://localhost:5185` / `/hubs/trivia` |
| Remote local | `http://localhost:4002/remoteEntry.js` |
| Scope / módulo | `triviaGame` / `./GameModule` |
| Tipo de juego | `trivia` |

La audiencia compartida de Profile/Matchmaking sigue tratándose como la propuesta ADR-007. Esta integración usa su configuración local actual; no afirma que el profesor la haya aprobado.

## Auth0: lo que falta confirmar

La API de jugadores BattleHub Trivia API debe tener el Identifier de Trivia, RS256 y acceso delegado autorizado para BattleHub Shell. El usuario confirmó ese registro mediante captura; no se verificó el tenant mediante API administrativa.

Para el aviso de finalización crear o confirmar una **aplicación Machine to Machine BattleHub Trivia Service**, distinta del recurso API de jugadores. Se reutilizó la aplicación M2M de prueba de Trivia; cambiar su nombre no cambia su Client ID. Autorizarla en **BattleHub Profile Service**, Identifier `https://api.battlehub.local/profile`, solamente con `matches.finish`.

Compartir el Client ID con Equipo 2 para el mapa de clientes autorizados. Entregar el Client Secret solamente al responsable del backend de Trivia por canal privado. No se usa el Client ID de BattleHub Shell. El cliente M2M confirmado es `xYhNYG9lOGLA6KvEM0Y4cccH2f7JDD5x`: la captura muestra el acceso Client Access sobre Profile con `matches.finish` (1/1). El nombre de la aplicación puede seguir siendo BattleHub Trivia API; es recomendable identificarla como BattleHub Trivia Service.

No se necesita un scope nuevo para que el jugador juegue: la membresía y el permiso `games.trivia.play` se validan al entrar/iniciar la sala mediante Profile y Matchmaking. Si acuerdan exigir ese permiso también en el JWT de Trivia, primero deben acordar RBAC y modificar la política; crear una API con cero permisos no añade automáticamente ese claim.

El POST externo de resultados queda reservado a servicios confiables con `games.trivia.results.write` sobre la audiencia Trivia. No hace falta crear ni conceder ese permiso para el flujo normal: el hub guarda directamente el resultado y el frontend no lo publica. El cliente M2M que avisa a Matchmaking solo necesita `matches.finish`.

## Levantar el backend sin Docker

Requisitos: .NET 10 y SQL Server LocalDB o SQL Server. **Este proyecto usa SQL Server, no MySQL.**

Desde la raíz del repositorio:

```powershell
.\scripts\Start-Integration.ps1
```

El script aplica migraciones y carga el banco únicamente si la tabla de preguntas está vacía. No borra datos. Por defecto usa `(localdb)\MSSQLLocalDB` y `BattleHubTrivia`. Si ya tienen SQL Server propio:

```powershell
.\scripts\Start-Integration.ps1 -ConnectionString 'Server=localhost;Database=BattleHubTrivia;Integrated Security=true;TrustServerCertificate=true'
```

Configurar las credenciales privadas antes de iniciar, o cuando estén disponibles:

```powershell
dotnet user-secrets set 'Matchmaking:Auth0:ClientId' 'xYhNYG9lOGLA6KvEM0Y4cccH2f7JDD5x' --project src/BattleHub.Trivia.Api
```

Para el secreto, usar el script `scripts/Set-MatchmakingCredentials.ps1`, que solicita el valor de forma oculta y lo guarda en los user-secrets locales. No copiarlo en documentos, código, ZIP ni capturas.

Si no se han configurado las credenciales, el juego y el guardado pueden funcionar, pero Matchmaking seguirá en Started y la cola permanecerá pendiente. Completar el M2M y el mapa del Equipo 2 antes de considerar terminada la integración.

## Frontend

En otra terminal, desde la raíz:

```powershell
cd src/trivia-frontend
npm.cmd ci
npm.cmd start
```

Para otra URL de API, definir `TRIVIA_API_URL` en la terminal **antes** de iniciar o compilar Webpack:

```powershell
$env:TRIVIA_API_URL='http://localhost:5185'
npm.cmd start
```

Abrir el juego desde una sala real del Shell. Abrir 4002 directamente muestra un arnés de vista previa y no simula una partida autenticada.

## Extensión local del contexto

```typescript
interface GameContext {
  matchId: string;
  gameType: 'trivia';
  currentUser: { id: string; displayName: string };
  getAccessToken?: () => Promise<string>;
  getMatchmakingAccessToken?: () => Promise<string>;
}
```

`getAccessToken()` entrega un token de usuario con audiencia Trivia; se usa en REST y `accessTokenFactory` de SignalR. `getMatchmakingAccessToken()` entrega un token de usuario con audiencia Profile/Matchmaking; se envía en `JoinMatch({ matchId, matchmakingAccessToken })`. El backend lo reenvía al GET del detalle y comprueba que `sub` del token de Trivia figura como participante. No guarda ese token ni lo cambia por el M2M. El backend nunca reenvía tokens de usuario a la URL de un cliente: usa únicamente `Matchmaking:BaseUrl` configurada.

El Shell incluye estas funciones y las rechaza al cerrar el juego o cambiar sesión. Son extensiones propuestas del contexto central. El frontend de Trivia rechaza contextos parcialmente configurados. `pause()` pausa los controles de esta vista; no detiene la partida compartida. `dispose()` elimina suscripciones, temporizador y conexión.

## Equipo 2: configuración necesaria

Su GET `/api/matches/{id}` debe devolver `id`, `gameType`, `status` y `participants: [{ userId, displayName }]`, como en la copia local preparada previamente. Debe aceptar el token de usuario de la audiencia Profile/Matchmaking.

Agregar **sin borrar los clientes existentes de Typing y Memory** en `src/BattleHub.Matchmaking.Api/appsettings.json`:

```json
"GameServices": {
  "Clients": {
    "8BWcE4T8HxhJrxU1CtgmNkjDOpkrN4Su": "typing",
    "xYhNYG9lOGLA6KvEM0Y4cccH2f7JDD5x": "trivia"
  }
}
```

Alternativa privada/local, sin editar el JSON: establecer `GameServices__Clients__xYhNYG9lOGLA6KvEM0Y4cccH2f7JDD5x=trivia` como variable de entorno del proceso de Matchmaking, Esta variable usa el ID confirmado.

Debe existir POST `/api/matches/{id}/finish` con Bearer M2M y `matches.finish`, respuesta 204 y finalización idempotente, como la entrega del Equipo 2. No admite el token de jugadores Trivia para este callback.

## Equipo 1 / Profile

En la copia local, las migraciones ya incluyen `trivia` habilitado y `games.trivia.play`, y el servicio asigna permisos iniciales a perfiles nuevos. No asegura que una base instalada o una versión publicada tenga esos mismos datos. Verificar en el Profile que realmente usarán que ambos jugadores tengan `games.trivia.play` y el anfitrión `matches.create`. No son roles que haya que inventar en Auth0.

## Prueba entre equipos

1. Levantar Profile, Matchmaking, API Trivia, frontend Trivia y Shell.
2. Iniciar sesión con dos cuentas autorizadas, en navegadores/perfiles separados.
3. Crear sala `trivia`, entrar ambos e iniciar desde Matchmaking.
4. Abrir el juego desde Salas y autorizar la audiencia Trivia con la misma cuenta si el Shell lo solicita.
5. Responder las diez preguntas. Comprobar marcador, bloqueo de segunda respuesta, vencimiento, revelación y resultado final.
6. Desconectar y reconectar un jugador durante una pregunta. Su respuesta previa debe seguir bloqueada.
7. Consultar el historial propio. Verificar que Matchmaking pasa a Finished y que `FinishNotifications.Delivered=1`.
8. Probar usuario ajeno, token de otra audiencia y llamada anónima: deben rechazarse.

Si la cola queda bloqueada, revisar los logs de rechazo, permisos, audiencia y mapa M2M antes de reactivarla. En la base **de Trivia**, después de corregir la causa y confirmar el match concreto:

```sql
SELECT MatchId, Delivered, Blocked, Attempts, NextAttemptAt FROM FinishNotifications;
UPDATE FinishNotifications SET Blocked = 0, NextAttemptAt = SYSDATETIMEOFFSET()
WHERE MatchId = 'ID_PARTIDA_REVISADA' AND Delivered = 0;
```

## Verificación y límites

Se comprobó una partida completa con dos clientes SignalR reales contra el host de pruebas, JWT RS256 firmados con una clave temporal y SQL Server LocalDB. Matchmaking se sustituye por un servicio controlado en esa prueba; sus llamadas HTTP, membresía, token M2M y reintentos se prueban por separado. No equivale a una prueba manual con las cuentas reales de Auth0 ni a un despliegue conjunto.

Las partidas activas se mantienen en memoria: reiniciar Trivia interrumpe las partidas que no terminaron. Los resultados y avisos guardados sobreviven. Ejecutar una sola instancia del backend para esta entrega; escalar requiere coordinar estado compartido. La URL de producción, HTTPS, CORS y remote real deben configurarse cuando el equipo despliegue.

El lockfile conservó las dependencias previas del equipo y añadió SignalR. La instalación reportó 40 hallazgos de npm audit; no se aplicaron actualizaciones masivas ni cambios de versión de Aurelia. Revisar dependencias aparte antes de publicar.
