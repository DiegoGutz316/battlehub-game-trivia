# Integración de Trivia con Shell y Matchmaking

## Descripción general

BattleHub Trivia es un juego de preguntas que utiliza .NET 10, SQL Server y SignalR. El backend administra las partidas, preguntas, respuestas, puntajes y resultados.

La autenticación propia de Trivia ya no utiliza Auth0. Actualmente valida tokens JWT firmados con RS256 mediante una clave pública configurada localmente.

La integración con Shell y Matchmaking se mantiene como una funcionalidad del proyecto, pero su compatibilidad con la nueva autenticación todavía debe coordinarse con los equipos correspondientes.

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

El backend valida JWT utilizando los siguientes valores de configuración:

| Configuración    | Descripción                                  |
| ---------------- | -------------------------------------------- |
| `Auth:Issuer`    | Emisor autorizado de los tokens de Trivia    |
| `Auth:Audience`  | Audiencia correspondiente a la API de Trivia |
| `Auth:PublicKey` | Clave pública RSA para verificar las firmas  |

Los valores actuales de emisor y audiencia son:

- Emisor: `battlehub-trivia`
- Audiencia: `battlehub-trivia-api`
- Algoritmo: `RS256`

La clave pública debe configurarse antes de iniciar la API. Para hacerlo localmente, desde la raíz del repositorio se puede utilizar User Secrets:

```powershell
dotnet user-secrets set "Auth:PublicKey" "<CLAVE_PUBLICA_RSA>" --project src/BattleHub.Trivia.Api
```

Se debe reemplazar `<CLAVE_PUBLICA_RSA>` por la clave pública RSA correspondiente a los tokens JWT que utilizará Trivia. La clave debe estar en el formato esperado por el backend.

No se deben almacenar claves privadas ni credenciales en el repositorio. La clave pública se utiliza únicamente para verificar las firmas de los tokens, no para generarlos.

## Integración con Matchmaking

Para ingresar a una partida integrada, Trivia consulta a Matchmaking y comprueba que:

- La partida exista.
- El tipo de juego sea `trivia`.
- Su estado sea `Started`.
- El usuario autenticado figure entre sus participantes.

El método `JoinMatch` recibe el identificador de la partida y un token de acceso para consultar Matchmaking.

Cuando termina una partida, Trivia guarda el resultado y utiliza una cola de notificaciones para solicitar su finalización en Matchmaking.

El componente `MatchmakingGateway` obtiene el token de servicio mediante `IMatchmakingTokenProvider`, sin implementar directamente la autenticación externa.

### Configuración del proveedor de tokens

La implementación actual, `ConfiguredMatchmakingTokenProvider`, obtiene el token desde:

`Matchmaking:ServiceToken`

Para pruebas locales puede configurarse mediante User Secrets:

```powershell
dotnet user-secrets set "Matchmaking:ServiceToken" "TOKEN_VALIDO" --project src/BattleHub.Trivia.Api
```

El token debe ser válido para Matchmaking y contar con el permiso `matches.finish`.

Este mecanismo es temporal: no renueva automáticamente los tokens vencidos. Antes de considerar completa la integración, los equipos deben acordar un mecanismo seguro de autenticación y renovación.

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

La integración debe proporcionar una identidad compatible con la autenticación de Trivia y los tokens necesarios para consultar Matchmaking.

Los JWT locales de Trivia no son aceptados automáticamente por Matchmaking. Esta compatibilidad requiere coordinación entre equipos.

## Ejecución del backend

El proyecto utiliza .NET 10 y SQL Server.

Antes de la primera ejecución debe configurarse `Auth:PublicKey`, como se explica en [Autenticación de Trivia](#autenticación-de-trivia). Es una clave pública, no un secreto; sin ella la API no arranca.

Desde la raíz del repositorio:

```powershell
dotnet run --project src/BattleHub.Trivia.Api
```

Este comando usa el perfil `http` de `launchSettings.json`, con el entorno Development. En ese perfil, `Database:MigrateOnStartup` y `Database:SeedQuestions` están activados: la API aplica las migraciones y carga las 30 preguntas iniciales mediante `QuestionSeeder`, sin scripts. Si la base de datos no existe, la crea; si ya tiene preguntas, no las vuelve a cargar.

Por defecto se usa la base LocalDB `BattleHubTrivia`. Para usar otra base se configura `ConnectionStrings:Trivia`.

Fuera de Development, o al iniciar con `--no-launch-profile`, ambos valores están en `false` (`appsettings.json`) y deben activarse por configuración o variable de entorno.

Para ejecutar la API en un contenedor, ver la sección "Docker" del backend en el [README](../README.md).

## Pruebas de integración

Para comprobar una partida integrada se debe:

1. Iniciar los servicios necesarios y configurar la autenticación compatible.
2. Crear una sala de tipo `trivia` en Matchmaking.
3. Ingresar con los jugadores autorizados.
4. Responder las diez preguntas.
5. Verificar los puntajes, tiempos y resultados.
6. Comprobar la reconexión de jugadores.
7. Consultar el historial correspondiente.
8. Verificar que Matchmaking reciba la notificación de finalización.

También deben probarse los rechazos a usuarios ajenos a la partida, tokens inválidos y solicitudes sin autenticación.

## Estado actual y pendientes

La autenticación JWT propia de Trivia ya fue modificada para eliminar su dependencia directa de Auth0.

También se desacopló la obtención del token de servicio de `MatchmakingGateway`, y sus nueve pruebas unitarias finalizaron correctamente.

Todavía están pendientes:

- Acordar con los otros equipos la compatibilidad de autenticación.
- Resolver la renovación segura de los tokens de servicio.
- Verificar una partida completa con los servicios reales.
- Implementar el modo independiente o de práctica en su tarea correspondiente.
- Revisar las advertencias de seguridad de las dependencias.

Las partidas activas se mantienen en memoria. Los resultados y las notificaciones persistidas se conservan en SQL Server.
