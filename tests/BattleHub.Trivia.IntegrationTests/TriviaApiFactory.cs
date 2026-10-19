
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using BattleHub.Trivia.Api.Matchmaking;
using BattleHub.Trivia.Data.Context;
using BattleHub.Trivia.Data.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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

    private static IMatchmakingGateway CreateRooms()
    {
        var rooms = Substitute.For<IMatchmakingGateway>();

        rooms
            .ValidateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var id = call.ArgAt<string>(0);
                var user = call.ArgAt<string>(1);
                var token = call.ArgAt<string>(2);

                if (token != "room-token" ||
                    user is not ("player-one" or "player-two"))
                {
                    throw new HubException("PLAYER_NOT_IN_MATCH");
                }

                return Task.FromResult(new MatchRoom(
                    id,
                    "trivia",
                    "Started",
                    [
                        new("player-one", "Uno"),
                        new("player-two", "Dos")
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

    public const string Issuer = "battlehub-trivia";
    public const string Audience = "battlehub-trivia-api";

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
                    "Base de prueba invÃ¡lida.");
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

        builder.UseSetting("Auth:Issuer", Issuer);

        builder.UseSetting("Auth:Audience", Audience);

        builder.UseSetting(
            "Auth:PublicKey",
            Convert.ToBase64String(
                rsa.ExportSubjectPublicKeyInfo()));

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

            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                jwt =>
                {
                    jwt.Authority = null;
                    jwt.ConfigurationManager = null;

                    jwt.TokenValidationParameters.ValidIssuer = Issuer;
                    jwt.TokenValidationParameters.ValidAudience = Audience;

                    jwt.TokenValidationParameters.IssuerSigningKey =
                        new RsaSecurityKey(rsa.ExportParameters(false))
                        {
                            KeyId = "test-rsa"
                        };
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
