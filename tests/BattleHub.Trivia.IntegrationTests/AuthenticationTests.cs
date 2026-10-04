using System.Net;
using System.Net.Http.Headers;
using BattleHub.Trivia.Data.Context;
using BattleHub.Trivia.Data.Repositories;
using BattleHub.Trivia.Domain.Entities;
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
            "auth0|player", issuer: scenario == "issuer" ? "https://wrong.example/" : TriviaApiFactory.Issuer,
            audience: scenario == "audience" ? "https://api.battlehub.local/profile" : TriviaApiFactory.Audience,
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
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token("auth0|player", writer: false));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/hubs/trivia/negotiate?negotiateVersion=1", null)).StatusCode);
    }

    [Fact]
    public async Task PlayerCannotWriteResultsOrReadOthersHistory()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token("auth0|player", writer: false));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/games/trivia/results", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/games/trivia/players/auth0%7Cother/history")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/games/trivia/players/auth0%7Cplayer/history")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/games/trivia/players/auth0%7Cplayer/history?access_token=" + factory.Token("auth0|player"))).StatusCode);
    }

    [Fact]
    public async Task SavedResultCreatesDurableNotificationInSameDatabase()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TriviaDbContext>();
        var id = "outbox-" + Guid.NewGuid().ToString("N");
        await new ResultRepository(db).SaveAsync(new MatchResult {
            MatchId = id, StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1), FinishedAt = DateTimeOffset.UtcNow,
            Players = [new PlayerResult { UserId = "auth0|player", DisplayName = "Jugador", Position = 1 }], WinnerUserId = "auth0|player"
        });
        var pending = await db.FinishNotifications.AsNoTracking().SingleAsync(item => item.MatchId == id);
        Assert.False(pending.Delivered);
        Assert.Equal(0, pending.Attempts);
    }
}