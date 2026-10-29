using BattleHub.Trivia.Data;
using BattleHub.Trivia.Data.Context;
using BattleHub.Trivia.Data.Seed;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

if (args.Length == 0 || args is ["--help"] or ["-h"] or ["help"])
{
    PrintUsage();
    return args.Length == 0 ? 1 : 0;
}

var command = args[0].Trim().ToLowerInvariant();
if (command is not ("migrate" or "seed"))
{
    Console.Error.WriteLine($"Comando desconocido: {args[0]}");
    PrintUsage();
    return 1;
}

using var host = Host.CreateDefaultBuilder(args[1..])
    .ConfigureAppConfiguration((_, config) =>
    {
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddTriviaData(context.Configuration);
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
        });
    })
    .Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DbInit");

try
{
    await WaitForDatabaseAsync(host.Services, logger);

    using var scope = host.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TriviaDbContext>();

    if (command == "migrate")
    {
        logger.LogInformation("Aplicando migraciones de Entity Framework Core…");
        await db.Database.MigrateAsync();
        logger.LogInformation("Migraciones aplicadas correctamente.");
        return 0;
    }

    logger.LogInformation("Generando datos iniciales del banco de preguntas…");
    var inserted = await scope.ServiceProvider
        .GetRequiredService<InitialDataGenerator>()
        .GenerateAsync();
    logger.LogInformation(
        inserted == 0
            ? "Generación de datos omitida (ya existían preguntas)."
            : "Generación de datos completada ({Inserted} preguntas).",
        inserted);
    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "DbInit falló al ejecutar '{Command}'.", command);
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine(
        """
        BattleHub.Trivia.DbInit — proceso independiente de esquema y datos.

        Uso:
          dotnet run --project src/BattleHub.Trivia.DbInit -- migrate
          dotnet run --project src/BattleHub.Trivia.DbInit -- seed

        En Docker Compose:
          db-migrate  → migrate
          db-seed     → seed

        Requiere ConnectionStrings__Trivia (o ConnectionStrings:Trivia).
        """);
}

static async Task WaitForDatabaseAsync(IServiceProvider services, ILogger logger)
{
    // Esperar al motor (master), no a la base de la app: BattleHubTrivia aún no existe
    // antes de `migrate`, y CanConnectAsync sobre esa base devolvería false para siempre.
    const int maxAttempts = 90;
    var delay = TimeSpan.FromSeconds(2);

    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TriviaDbContext>();
    var configured = db.Database.GetConnectionString()
        ?? throw new InvalidOperationException(
            "Falta ConnectionStrings__Trivia (o ConnectionStrings:Trivia).");

    var builder = new SqlConnectionStringBuilder(configured)
    {
        InitialCatalog = "master"
    };

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            _ = await command.ExecuteScalarAsync();

            logger.LogInformation(
                "SQL Server disponible (intento {Attempt}/{MaxAttempts}).",
                attempt,
                maxAttempts);
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(
                "Esperando SQL Server (intento {Attempt}/{MaxAttempts}): {Message}",
                attempt,
                maxAttempts,
                ex.Message);
        }

        await Task.Delay(delay);
    }

    throw new InvalidOperationException(
        $"SQL Server no respondió después de {maxAttempts} intentos.");
}
