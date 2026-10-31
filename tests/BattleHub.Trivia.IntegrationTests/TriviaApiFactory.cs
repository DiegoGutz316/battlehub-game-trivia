
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using BattleHub.Trivia.Api.Profiles;
using BattleHub.Trivia.Data.Context;
using BattleHub.Trivia.Data.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
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

    // Profile: todos tienen games.trivia.play salvo NoPermissionUser.
    private static IProfileGateway CreateProfiles()
    {
        var profiles = Substitute.For<IProfileGateway>();

        profiles
            .CanPlayAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(
                new JwtSecurityTokenHandler()
                    .ReadJwtToken(call.ArgAt<string>(0))
                    .Subject != NoPermissionUser));

        return profiles;
    }

    public TestClock Clock { get; } = new();

    public const string NoPermissionUser = "player-without-permission";

    // Emisor y audiencia acordados con Shell (los mismos de appsettings.json).
    public const string Issuer = "https://dev-jaii1peslxnejq0y.us.auth0.com/";
    public const string Audience = "https://api.battlehub.local/profile";

    private readonly RSA rsa = RSA.Create(2048);

    private MsSqlContainer? sql;
    private string? connectionString;
    private bool local;

    public string Token(
        string subject = "test-service@clients",
        string audience = Audience,
        string issuer = Issuer,
        bool writer = true,
        DateTime? expires = null)
    {
        var claims = new List<Claim>
        {
            new("sub", subject)
        };

        if (subject.EndsWith("@clients"))
        {
            claims.Add(new("gty", "client-credentials"));
        }

        if (writer)
        {
            claims.Add(new("scope", "games.trivia.results.write"));
        }

        return new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                issuer,
                audience,
                claims,
                DateTime.UtcNow.AddHours(-2),
                expires ?? DateTime.UtcNow.AddHours(1),
                new SigningCredentials(
                    new RsaSecurityKey(rsa)
                    {
                        KeyId = "test-rsa"
                    },
                    SecurityAlgorithms.RsaSha256)
            )
        );
    }

    public new HttpClient CreateClient()
    {
        var client = base.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                Token());

        return client;
    }

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

        rsa.Dispose();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            services.RemoveAll<IProfileGateway>();
            services.AddSingleton<IProfileGateway>(
                CreateProfiles());

            services.RemoveAll<DbContextOptions<TriviaDbContext>>();
            services.RemoveAll<TriviaDbContext>();

            services.AddDbContext<TriviaDbContext>(
                options => TriviaDbContext.ConfigureSqlServer(
                    options,
                    connectionString!));

            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                jwt =>
                {
                    // Reemplaza la descarga de claves del emisor por la clave de prueba, sin red.
                    var configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = Issuer
                    };

                    configuration.SigningKeys.Add(
                        new RsaSecurityKey(rsa.ExportParameters(false))
                        {
                            KeyId = "test-rsa"
                        });

                    jwt.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(
                            configuration);
                });
        });
    }
}

[CollectionDefinition(Name)]
public sealed class TriviaDatabaseCollection
    : ICollectionFixture<TriviaApiFactory>
{
    public const string Name = "TriviaDatabase";
}
