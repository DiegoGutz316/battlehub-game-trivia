
using BattleHub.Trivia.Data.Context;
using BattleHub.Trivia.Data.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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

        await scope.ServiceProvider
            .GetRequiredService<QuestionSeeder>()
            .SeedAsync();
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
        builder.UseSetting("ConnectionStrings:Trivia", connectionString);
        builder.UseSetting("Cors:Origins:0", "http://localhost:4000");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

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
