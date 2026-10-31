# Migraciones de la base de datos

La base de datos de Trivia Battle tiene **4 tablas** y **6 migraciones** de Entity Framework Core: una por cada tabla, más la que creó y la que eliminó `FinishNotifications`. Están en `src/BattleHub.Trivia.Data/Migrations/` y el modelo se define en `src/BattleHub.Trivia.Data/Context/TriviaDbContext.cs`.

- Todas las llaves primarias son una columna `Id` de tipo Guid (`uniqueidentifier`).
- Los identificadores de texto que usa el resto del sistema (`QuestionId`, `AnswerId`, `MatchId` y `UserId`) **no son llaves primarias**: son columnas comunes con índices únicos.
- Las llaves foráneas apuntan siempre al `Id` de la tabla padre.

| Orden | Migración | Tabla |
|---|---|---|
| 1 | `20261018211140_CrearTablaQuestions` | `Questions` |
| 2 | `20261018211355_CrearTablaAnswerOptions` | `AnswerOptions` |
| 3 | `20261018211612_CrearTablaMatchResults` | `MatchResults` |
| 4 | `20261018211842_CrearTablaPlayerResults` | `PlayerResults` |
| 5 | `20261018212042_CrearTablaFinishNotifications` | `FinishNotifications` (ya no existe) |
| 6 | `20261031073116_EliminarTablaFinishNotifications` | Elimina `FinishNotifications` |

## Relaciones

```text
Questions    1 ──── N  AnswerOptions        (AnswerOptions.QuestionRefId → Questions.Id)

MatchResults 1 ──── N  PlayerResults        (PlayerResults.MatchRefId → MatchResults.Id)
```

Las dos llaves foráneas tienen borrado en cascada:

- si se borra una pregunta, se borran sus opciones;
- si se borra el resultado de una partida, se borran sus jugadores.

## 1. CrearTablaQuestions

Crea la tabla **`Questions`**, el banco de preguntas.

| Columna | Tipo SQL | Admite nulos |
|---|---|---|
| `Id` | `uniqueidentifier` | No |
| `QuestionId` | `nvarchar(64)` | No |
| `Text` | `nvarchar(1000)` | No |
| `Category` | `nvarchar(100)` | Sí |
| `Difficulty` | `nvarchar(32)` | Sí |
| `CorrectAnswerId` | `nvarchar(64)` | No |

- **Llave primaria:** `PK_Questions` sobre `Id`.
- **Llaves foráneas:** ninguna, porque es una tabla padre.
- **Índices:**
  - `IX_Questions_QuestionId` sobre `QuestionId`, **único**;
  - `IX_Questions_Category` sobre `Category`, no único;
  - `IX_Questions_Difficulty` sobre `Difficulty`, no único.
- **Restricciones:** ninguna adicional.

## 2. CrearTablaAnswerOptions

Crea la tabla **`AnswerOptions`**, con las opciones de respuesta de cada pregunta.

| Columna | Tipo SQL | Admite nulos |
|---|---|---|
| `Id` | `uniqueidentifier` | No |
| `QuestionRefId` | `uniqueidentifier` | No |
| `QuestionId` | `nvarchar(64)` | No |
| `AnswerId` | `nvarchar(64)` | No |
| `Text` | `nvarchar(500)` | No |

- **Llave primaria:** `PK_AnswerOptions` sobre `Id`.
- **Llaves foráneas:** `FK_AnswerOptions_Questions_QuestionRefId`, de `QuestionRefId` hacia `Questions(Id)`, con `ON DELETE CASCADE`.
- **Índices:**
  - `IX_AnswerOptions_QuestionId_AnswerId` sobre `(QuestionId, AnswerId)`, **único**;
  - `IX_AnswerOptions_QuestionRefId` sobre `QuestionRefId`, no único. Lo crea EF por ser llave foránea.
- **Restricciones:** ninguna adicional.

## 3. CrearTablaMatchResults

Crea la tabla **`MatchResults`**, con el resultado de cada partida terminada.

| Columna | Tipo SQL | Admite nulos |
|---|---|---|
| `Id` | `uniqueidentifier` | No |
| `MatchId` | `nvarchar(64)` | No |
| `GameType` | `nvarchar(32)` | No |
| `StartedAt` | `datetimeoffset` | No |
| `FinishedAt` | `datetimeoffset` | No |
| `WinnerUserId` | `nvarchar(64)` | Sí |
| `IsTie` | `bit` | No |
| `DecidedByTiebreak` | `bit` | No |
| `Metadata` | `nvarchar(max)` | No |

- **Llave primaria:** `PK_MatchResults` sobre `Id`.
- **Llaves foráneas:** ninguna, porque es una tabla padre.
- **Índices:** `IX_MatchResults_MatchId` sobre `MatchId`, **único**.
- **Restricciones:** `CK_MatchResults_Metadata_IsJson`, con la condición `ISJSON([Metadata]) = 1`. La columna `Metadata` guarda JSON, como establece el ADR-002.

## 4. CrearTablaPlayerResults

Crea la tabla **`PlayerResults`**, con el resultado de cada jugador en una partida.

| Columna | Tipo SQL | Admite nulos |
|---|---|---|
| `Id` | `uniqueidentifier` | No |
| `MatchRefId` | `uniqueidentifier` | No |
| `MatchId` | `nvarchar(64)` | No |
| `UserId` | `nvarchar(64)` | No |
| `DisplayName` | `nvarchar(100)` | No |
| `Score` | `int` | No |
| `CorrectAnswers` | `int` | No |
| `CorrectAnswersTimeMs` | `bigint` | No |
| `Position` | `int` | No |

- **Llave primaria:** `PK_PlayerResults` sobre `Id`.
- **Llaves foráneas:** `FK_PlayerResults_MatchResults_MatchRefId`, de `MatchRefId` hacia `MatchResults(Id)`, con `ON DELETE CASCADE`.
- **Índices:**
  - `IX_PlayerResults_MatchId_UserId` sobre `(MatchId, UserId)`, **único**;
  - `IX_PlayerResults_UserId` sobre `UserId`, no único. Lo usan las consultas de historial y estadísticas;
  - `IX_PlayerResults_MatchRefId` sobre `MatchRefId`, no único. Lo crea EF por ser llave foránea.
- **Restricciones:** ninguna adicional.

## 5. CrearTablaFinishNotifications

Creó la tabla **`FinishNotifications`**, la cola de avisos de fin de partida que la API enviaba a Matchmaking. La migración 6 la elimina.

| Columna | Tipo SQL | Admite nulos |
|---|---|---|
| `Id` | `uniqueidentifier` | No |
| `MatchRefId` | `uniqueidentifier` | No |
| `MatchId` | `nvarchar(64)` | No |
| `NextAttemptAt` | `datetimeoffset` | No |
| `Attempts` | `int` | No |
| `Delivered` | `bit` | No |
| `Blocked` | `bit` | No |

- **Llave primaria:** `PK_FinishNotifications` sobre `Id`.
- **Llaves foráneas:** `FK_FinishNotifications_MatchResults_MatchRefId`, de `MatchRefId` hacia `MatchResults(Id)`, con `ON DELETE CASCADE`.
- **Índices:**
  - `IX_FinishNotifications_MatchRefId` sobre `MatchRefId`, **único**. Hace que la relación sea 1 a 1: una partida tiene como máximo una notificación;
  - `IX_FinishNotifications_MatchId` sobre `MatchId`, **único**;
  - `IX_FinishNotifications_Delivered_NextAttemptAt` sobre `(Delivered, NextAttemptAt)`, no único. Lo usa el proceso que busca avisos pendientes.
- **Restricciones:** ninguna adicional.

## 6. EliminarTablaFinishNotifications

Elimina la tabla **`FinishNotifications`** con sus índices y su llave foránea. Con el requisito R-46, el juego ya no llama a Matchmaking: avisa el fin de partida al Shell, y es el Shell quien notifica a Matchmaking. La cola ya no tiene uso. Los resultados no se tocan.

## Por qué algunos índices únicos son compuestos

`AnswerId` y `UserId` no son únicos por sí solos:

- **`AnswerId`:** varias preguntas usan los mismos identificadores de opción (`a`, `b`, `c`, `d`). Un índice único solo sobre `AnswerId` impediría guardar la opción `a` de una segunda pregunta. Lo que no puede repetirse es la **combinación** `(QuestionId, AnswerId)`: una pregunta no puede tener dos opciones con el mismo `AnswerId`.
- **`UserId`:** un jugador participa en muchas partidas. Un índice único solo sobre `UserId` impediría guardar su segunda partida. Lo que no puede repetirse es `(MatchId, UserId)`: un jugador aparece una sola vez en cada partida.

`AnswerOptions.QuestionId` y `PlayerResults.MatchId` repiten el identificador de texto de la tabla padre, además del Guid de la llave foránea. Al insertar datos, los dos valores deben corresponder a la misma fila padre.

## Cómo se genera el Guid

Las columnas `Id` no tienen un valor por defecto en la base de datos. El Guid lo asigna Entity Framework Core en la aplicación, al agregar la entidad (`Add`) y antes de `SaveChanges`. Con SQL Server, EF genera Guids secuenciales, que fragmentan menos el índice de la llave primaria que los Guids aleatorios.

La carga inicial de preguntas la hace el proceso independiente `BattleHub.Trivia.DbInit` con `InitialDataGenerator` (`src/BattleHub.Trivia.Data/Seed/`), no las migraciones ni el arranque de la API. Agrega cada pregunta con sus opciones mediante EF, que genera los `Id` y llena `QuestionRefId`. Si alguna vez se insertan filas directamente con SQL, hay que incluir el valor de `Id` (y, en las tablas hijas, el de la llave foránea `QuestionRefId` o `MatchRefId`).

## Comandos

Se ejecutan desde la raíz del repositorio. La herramienta `dotnet-ef` está fijada en el manifiesto `dotnet-tools.json`; en un clon nuevo se instala con:

```powershell
dotnet tool restore
```

**Aplicar las migraciones** a la base de `ConnectionStrings__Trivia`, que debe estar definida:

```powershell
dotnet ef database update --project src/BattleHub.Trivia.Data
```

Para aplicarlas a otra base sin cambiar la variable, por ejemplo una de prueba:

```powershell
dotnet ef database update --project src/BattleHub.Trivia.Data --connection "Server=(localdb)\mssqllocaldb;Database=BattleHubTrivia_prueba;Trusted_Connection=True;TrustServerCertificate=True"
```

**Crear una migración nueva** después de cambiar el modelo en `TriviaDbContext.cs` o en `Persistence/`:

```powershell
dotnet ef migrations add NombreDeLaMigracion --project src/BattleHub.Trivia.Data
```

**Comprobar que el modelo y las migraciones coinciden.** Debe responder *"No changes have been made to the model since the last migration."*:

```powershell
dotnet ef migrations has-pending-model-changes --project src/BattleHub.Trivia.Data
```

**Listar las migraciones** sin conectarse a ninguna base:

```powershell
dotnet ef migrations list --project src/BattleHub.Trivia.Data --no-connect
```

## Cómo se rehicieron en el issue #28

Antes había dos migraciones: `20260930163936_CrearResultadosYPreguntas` y `20261004062521_AgregarColaFinalizacion`. Usaban los identificadores de texto como llaves primarias. Como el proyecto todavía no estaba desplegado, se eliminaron junto con el `ModelSnapshot`, y se generaron las 5 migraciones de creación, ya con las llaves Guid del issue #27.

Para obtener una migración por tabla, el modelo se construyó por etapas con una herramienta temporal que agregaba una tabla a la vez. Esa herramienta no quedó en el repositorio, y la última migración se generó con el modelo completo.

## Notas

- **`__EFMigrationsHistory`.** Es la tabla interna en la que EF registra las migraciones aplicadas. Su llave primaria, `MigrationId`, es de texto (`nvarchar`), pero la crea EF y no forma parte del modelo del juego. Ninguna de las tablas del modelo tiene una llave primaria de texto.
- **Borrar una base de prueba.** En `dotnet-ef` 10.0.12, `dotnet ef database drop` **no acepta** `--connection`. Sin esa opción usaría la cadena de `TriviaDbContextFactory` y borraría `BattleHubTrivia`. Para borrar una base de prueba se usa `sqlcmd` conectado a `master`, nombrando **solo** la base de prueba:

  ```powershell
  sqlcmd -S "(localdb)\mssqllocaldb" -d master -E -C -Q "ALTER DATABASE [BattleHubTrivia_prueba] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [BattleHubTrivia_prueba];"
  ```

  La primera instrucción cierra las conexiones abiertas para que el `DROP` no falle.
