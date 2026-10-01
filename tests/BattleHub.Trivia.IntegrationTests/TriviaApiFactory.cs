using BattleHub.Trivia.Data.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace BattleHub.Trivia.IntegrationTests;

public sealed class TriviaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04")
        .WithDatabase("BattleHubTrivia")
        .Build();

    private string? _connectionString;

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        _connectionString = new SqlConnectionStringBuilder(_sql.GetConnectionString())
        {
            InitialCatalog = "BattleHubTrivia"
        }.ConnectionString;

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TriviaDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _sql.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException("El contenedor de SQL Server todavía no está listo.");

        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            var stale = services
                .Where(service => service.ServiceType == typeof(DbContextOptions<TriviaDbContext>)
                    || service.ServiceType == typeof(TriviaDbContext))
                .ToList();

            foreach (var descriptor in stale)
                services.Remove(descriptor);

            services.AddDbContext<TriviaDbContext>(options =>
                TriviaDbContext.ConfigureSqlServer(options, _connectionString));
        });
    }
}

[CollectionDefinition(Name)]
public sealed class TriviaDatabaseCollection : ICollectionFixture<TriviaApiFactory>
{
    public const string Name = "TriviaDatabase";
}
