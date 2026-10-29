using System.Net;
using System.Net.Http.Headers;
using BattleHub.Trivia.Api.Hubs;
using BattleHub.Trivia.Data.Context;
using BattleHub.Trivia.Data.Repositories;
using BattleHub.Trivia.Domain.Entities;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BattleHub.Trivia.IntegrationTests;

[Collection(TriviaDatabaseCollection.Name)]
public sealed class AuthenticationTests(TriviaApiFactory factory)
{
    [Theory]
    [InlineData("anonymous")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("signature")]
    public async Task HubRejectsInvalidCredentials(string scenario)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = scenario == "anonymous" ? null : new AuthenticationHeaderValue("Bearer", factory.Token(
            "player-one", issuer: scenario == "issuer" ? "https://wrong.example/" : TriviaApiFactory.Issuer,
            // La audiencia propia de Trivia ya no se acepta: el Shell entrega la audiencia de la plataforma.
            audience: scenario == "audience" ? "https://api.battlehub.local/trivia" : TriviaApiFactory.Audience,
            expires: scenario == "expired" ? DateTime.UtcNow.AddMinutes(-2) : null));
        if (scenario == "signature")
        {
            var parts = client.DefaultRequestHeaders.Authorization!.Parameter!.Split('.');
            parts[2] = (parts[2][0] == 'A' ? "B" : "A") + parts[2][1..];
            client.DefaultRequestHeaders.Authorization = new("Bearer", string.Join('.', parts));
        }
        var response = await client.PostAsync("/hubs/trivia/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HubAcceptsPlayerButRejectsMachine()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/hubs/trivia/negotiate?negotiateVersion=1", null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token("player-one", writer: false));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/hubs/trivia/negotiate?negotiateVersion=1", null)).StatusCode);
    }

    [Fact]
    public async Task PlayerWithoutTriviaPermissionCannotJoinMatch()
    {
        await using var connection = new HubConnectionBuilder().WithUrl("http://localhost/hubs/trivia", options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            options.AccessTokenProvider = () => Task.FromResult<string?>(factory.Token(TriviaApiFactory.NoPermissionUser, writer: false));
        }).Build();
        await connection.StartAsync();
        var error = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("JoinMatch", new JoinRequest("room", "room-token")));
        Assert.Contains("PERMISSION_DENIED", error.Message);
    }

    [Fact]
    public async Task ServiceTokenWithoutWriteScopeCannotWriteResultsOrPlay()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token("trivia-service@clients", writer: false));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/games/trivia/results", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/hubs/trivia/negotiate?negotiateVersion=1", null)).StatusCode);
    }

    [Fact]
    public async Task PlayerCannotWriteResultsOrReadOthersHistory()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token("player-one", writer: false));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/games/trivia/results", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/games/trivia/players/player-other/history")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/games/trivia/players/player-one/history")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/games/trivia/players/player-one/history?access_token=" + factory.Token("player-one"))).StatusCode);
    }

    [Fact]
    public async Task SavedResultCreatesDurableNotificationInSameDatabase()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TriviaDbContext>();
        var id = "outbox-" + Guid.NewGuid().ToString("N");
        await new ResultRepository(db).SaveAsync(new MatchResult
        {
            MatchId = id,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            FinishedAt = DateTimeOffset.UtcNow,
            Players = [new PlayerResult { UserId = "player-one", DisplayName = "Jugador", Position = 1 }],
            WinnerUserId = "player-one"
        });
        var pending = await db.FinishNotifications.AsNoTracking().SingleAsync(item => item.MatchId == id);
        Assert.False(pending.Delivered);
        Assert.Equal(0, pending.Attempts);
    }
}
