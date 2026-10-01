using System.Net;
using System.Net.Http.Json;
using BattleHub.Trivia.Api.Dtos;

namespace BattleHub.Trivia.IntegrationTests;

[Collection(TriviaDatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class PostThenGetResultTests(TriviaApiFactory factory)
{
    [Fact]
    public async Task PostResult_LuegoGetPorMatchId_DevuelveLaMismaPartida()
    {
        var client = factory.CreateClient();
        var payload = new
        {
            matchId = "match-post-get",
            gameType = "trivia",
            players = new[]
            {
                new { userId = "user-001", displayName = "Francisco", score = 850 },
                new { userId = "user-002", displayName = "Ana", score = 620 }
            },
            startedAt = "2026-09-02T20:00:00Z",
            finishedAt = "2026-09-02T20:07:32Z",
            winnerUserId = "user-001",
            metadata = new { }
        };

        var post = await client.PostAsJsonAsync("/api/games/trivia/results", payload);
        var created = await post.Content.ReadFromJsonAsync<MatchResultResponse>();

        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("match-post-get", created.MatchId);
        Assert.Equal("trivia", created.GameType);
        Assert.Equal("user-001", created.WinnerUserId);

        var get = await client.GetAsync("/api/games/trivia/results/match-post-get");
        var fetched = await get.Content.ReadFromJsonAsync<MatchResultResponse>();

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.NotNull(fetched);
        Assert.Equal(created.MatchId, fetched.MatchId);
        Assert.Equal(created.WinnerUserId, fetched.WinnerUserId);
        Assert.Equal(created.StartedAt, fetched.StartedAt);
        Assert.Equal(created.FinishedAt, fetched.FinishedAt);
        Assert.Equal(2, fetched.Players.Count);
        Assert.Equal(850, fetched.Players.Single(player => player.UserId == "user-001").Score);
        Assert.Equal("Francisco", fetched.Players.Single(player => player.UserId == "user-001").DisplayName);
        Assert.Equal(620, fetched.Players.Single(player => player.UserId == "user-002").Score);
    }
}
