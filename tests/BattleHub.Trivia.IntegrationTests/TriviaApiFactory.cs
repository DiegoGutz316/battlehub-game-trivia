
using BattleHub.Trivia.Api.Matchmaking;
using BattleHub.Trivia.Data.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Testcontainers.MsSql;

namespace BattleHub.Trivia.IntegrationTests;

public sealed class TriviaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public sealed class TestClock : TimeProvider
    {
        private long ticks = DateTimeOffset.UtcNow.UtcTicks;

        public override DateTimeOffset GetUtcNow()
            => new(Interlocked.Read(ref ticks), TimeSpan.Zero);

        public void Advance(int seconds)
            => Interlocked.Add(
                ref ticks,
                TimeSpan.FromSeconds(seconds).Ticks);
    }

    private static IMatchmakingGateway CreateRooms()
    {
        var rooms = Substitute.For<IMatchmakingGateway>();

        rooms
            .ValidateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var id = call.ArgAt<string>(0);
                var user = call.ArgAt<string>(1);

                if (user is not ("auth0|one" or "auth0|two"))
                {
                    throw new HubException("PLAYER_NOT_IN_MATCH");
                }

                return Task.FromResult(new MatchRoom(
                    id,
                    "trivia",
                    "Started",
                    [
                        new("auth0|one", "Uno"),
                        new("auth0|two", "Dos")
                    ]
                ));
            });

        rooms
            .FinishAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(FinishOutcome.Confirmed));

        return rooms;
    }

    public TestClock Clock { get; } = new();

    private MsSqlContainer? sql;
    private string? connectionString;
    private bool local;

    public async Task InitializeAsync()
    {
        var supplied =
            Environment.GetEnvironmentVariable(
                "TRIVIA_TEST_CONNECTION");

        local = !string.IsNullOrWhiteSpace(supplied);

        if (!local)
        {
            sql = new MsSqlBuilder(
                "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04")
                .Build();

            await sql.StartAsync();

            supplied = sql.GetConnectionString();
        }

        connectionString = new SqlConnectionStringBuilder(supplied!)
        {
            InitialCatalog =
                "BattleHubTriviaTests_" +
                Guid.NewGuid().ToString("N")
        }.ConnectionString;

        using var scope = Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<TriviaDbContext>();

        await db.Database.MigrateAsync();

        using var stream = typeof(Program)
            .Assembly
            .GetManifestResourceStream("QuestionBank.sql")!;

        using var reader = new StreamReader(stream);

        await db.Database.ExecuteSqlRawAsync(
            await reader.ReadToEndAsync());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (local && connectionString is not null)
        {
            var name = new SqlConnectionStringBuilder(
                connectionString).InitialCatalog;

            if (!name.StartsWith(
                "BattleHubTriviaTests_",
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Base de prueba inválida.");
            }

            using var scope = Services.CreateScope();

            await scope.ServiceProvider
                .GetRequiredService<TriviaDbContext>()
                .Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();

        if (sql is not null)
        {
            await sql.DisposeAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            services.RemoveAll<IMatchmakingGateway>();
            services.AddSingleton<IMatchmakingGateway>(
                CreateRooms());

            services.RemoveAll<DbContextOptions<TriviaDbContext>>();
            services.RemoveAll<TriviaDbContext>();

            services.AddDbContext<TriviaDbContext>(
                options => TriviaDbContext.ConfigureSqlServer(
                    options,
                    connectionString!));
        });
    }
}

[CollectionDefinition(Name)]
public sealed class TriviaDatabaseCollection
    : ICollectionFixture<TriviaApiFactory>
{
    public const string Name = "TriviaDatabase";
}
