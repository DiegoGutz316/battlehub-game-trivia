using System.Net;
using System.Net.Http.Json;
using BattleHub.Trivia.Api.Matchmaking;
using Microsoft.Extensions.Configuration;

namespace BattleHub.Trivia.UnitTests;

public sealed class ClientCredentialsTokenProviderTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Handler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(respond(request, ++Calls));
    }

    private static IConfiguration Config(string? secret = "test-only-not-a-real-secret", string endpoint = "https://issuer.test/oauth/token") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Matchmaking:TokenEndpoint"] = endpoint,
            ["Matchmaking:ClientId"] = "trivia-client",
            ["Matchmaking:ClientSecret"] = secret,
            ["Matchmaking:Audience"] = "https://api.battlehub.local/profile"
        }).Build();

    private static HttpResponseMessage Token(string value, int expiresIn = 3600) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new { access_token = value, expires_in = expiresIn, token_type = "Bearer" }) };

    [Fact]
    public async Task RequestsClientCredentialsTokenForMatchmakingAudience()
    {
        var handler = new Handler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://issuer.test/oauth/token", request.RequestUri!.AbsoluteUri);
            var form = request.Content!.ReadAsStringAsync().Result.Split('&')
                .Select(pair => pair.Split('=')).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
            Assert.Equal("client_credentials", form["grant_type"]);
            Assert.Equal("trivia-client", form["client_id"]);
            Assert.Equal("test-only-not-a-real-secret", form["client_secret"]);
            Assert.Equal("https://api.battlehub.local/profile", form["audience"]);
            return Token("service-token");
        });
        using var http = new HttpClient(handler);

        Assert.Equal("service-token", await new ClientCredentialsTokenProvider(http, Config(), new Clock()).GetTokenAsync(default));
    }

    [Fact]
    public async Task ReusesTokenUntilShortlyBeforeItExpiresAndThenRenewsIt()
    {
        var clock = new Clock();
        var handler = new Handler((_, call) => Token("service-token-" + call));
        using var http = new HttpClient(handler);
        var provider = new ClientCredentialsTokenProvider(http, Config(), clock);

        Assert.Equal("service-token-1", await provider.GetTokenAsync(default));
        clock.Now = clock.Now.AddMinutes(58);
        Assert.Equal("service-token-1", await provider.GetTokenAsync(default));
        Assert.Equal(1, handler.Calls);

        // A un minuto de vencer (3600 s) se pide uno nuevo, sin intervención del jugador.
        clock.Now = clock.Now.AddMinutes(1);
        Assert.Equal("service-token-2", await provider.GetTokenAsync(default));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task RenewsShortLivedTokensHalfwayThroughTheirLifetime()
    {
        var clock = new Clock();
        var handler = new Handler((_, call) => Token("service-token-" + call, expiresIn: 60));
        using var http = new HttpClient(handler);
        var provider = new ClientCredentialsTokenProvider(http, Config(), clock);

        await provider.GetTokenAsync(default);
        clock.Now = clock.Now.AddSeconds(29);
        Assert.Equal("service-token-1", await provider.GetTokenAsync(default));
        clock.Now = clock.Now.AddSeconds(1);
        Assert.Equal("service-token-2", await provider.GetTokenAsync(default));
    }

    [Theory]
    [InlineData(null, "https://issuer.test/oauth/token")]
    [InlineData("", "https://issuer.test/oauth/token")]
    [InlineData("test-only-not-a-real-secret", "http://issuer.test/oauth/token")]
    [InlineData("test-only-not-a-real-secret", "")]
    public async Task RequiresHttpsEndpointAndConfiguredSecretBeforeCallingIssuer(string? secret, string endpoint)
    {
        var handler = new Handler((_, _) => Token("service-token"));
        using var http = new HttpClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ClientCredentialsTokenProvider(http, Config(secret, endpoint), new Clock()).GetTokenAsync(default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task FailsWhenIssuerRejectsCredentialsOrReturnsInvalidToken()
    {
        using var rejected = new HttpClient(new Handler((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        using var empty = new HttpClient(new Handler((_, _) => Token("", expiresIn: 3600)));
        using var expired = new HttpClient(new Handler((_, _) => Token("service-token", expiresIn: 0)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ClientCredentialsTokenProvider(rejected, Config(), new Clock()).GetTokenAsync(default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ClientCredentialsTokenProvider(empty, Config(), new Clock()).GetTokenAsync(default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ClientCredentialsTokenProvider(expired, Config(), new Clock()).GetTokenAsync(default));
    }
}
