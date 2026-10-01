using System.Net;
using System.Net.Http.Json;
using BattleHub.Trivia.Api.Dtos;

namespace BattleHub.Trivia.IntegrationTests;

[Collection(TriviaDatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class PlayerHistoryTests(TriviaApiFactory factory)
{
    [Fact]
    public async Task History_ConMasDeUnResultado_DevuelveLasPartidasDeLaMasRecienteALaMasAntigua()
    {
        var client = factory.CreateClient();

        var older = await client.PostAsJsonAsync("/api/games/trivia/results", new
        {
            matchId = "match-history-old",
            gameType = "trivia",
            players = new[]
            {
                new { userId = "user-history", displayName = "Francisco", score = 100 },
                new { userId = "user-rival", displayName = "Ana", score = 400 }
            },
            startedAt = "2026-09-01T18:00:00Z",
            finishedAt = "2026-09-01T18:10:00Z",
            winnerUserId = "user-rival",
            metadata = new { }
        });

        var newer = await client.PostAsJsonAsync("/api/games/trivia/results", new
        {
            matchId = "match-history-new",
            gameType = "trivia",
            players = new[]
            {
                new { userId = "user-history", displayName = "Francisco", score = 300 },
                new { userId = "user-rival", displayName = "Ana", score = 150 }
            },
            startedAt = "2026-09-03T18:00:00Z",
            finishedAt = "2026-09-03T18:10:00Z",
            winnerUserId = "user-history",
            metadata = new { }
        });

        Assert.Equal(HttpStatusCode.Created, older.StatusCode);
        Assert.Equal(HttpStatusCode.Created, newer.StatusCode);

        var historyResponse = await client.GetAsync("/api/games/trivia/players/user-history/history");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<MatchResultResponse>>();

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        Assert.NotNull(history);
        Assert.True(history.Count > 1);
        Assert.Equal(["match-history-new", "match-history-old"], history.Select(match => match.MatchId).ToArray());

        var statsResponse = await client.GetAsync("/api/games/trivia/players/user-history/stats");
        var stats = await statsResponse.Content.ReadFromJsonAsync<PlayerStatsResponse>();

        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);
        Assert.NotNull(stats);
        Assert.Equal("user-history", stats.UserId);
        Assert.Equal(2, stats.MatchesPlayed);
        Assert.Equal(1, stats.Wins);
        Assert.Equal(200, stats.AverageScore);
        Assert.Equal(300, stats.BestScore);
    }
}
