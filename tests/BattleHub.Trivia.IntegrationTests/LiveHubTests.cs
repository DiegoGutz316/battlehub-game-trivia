using System.Text.Json;
using System.Threading.Channels;
using BattleHub.Trivia.Api.Hubs;
using BattleHub.Trivia.Domain.Interfaces;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace BattleHub.Trivia.IntegrationTests;

[Collection(TriviaDatabaseCollection.Name)]
public sealed class LiveHubTests(TriviaApiFactory factory)
{
    private static readonly RoomPlayer[] Room = [new("player-one", "Uno"), new("player-two", "Dos")];

    [Fact]
    public async Task TwoPlayersFromTheSameRoomPlayTenRoundsAndPersistTheResult()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = timeout.Token;
        var id = "live-" + Guid.NewGuid().ToString("N");
        await using var one = Connection("player-one");
        await using var two = Connection("player-two");
        var questions = Channel.CreateUnbounded<JsonElement>();
        var finished = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishedForTwo = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        one.On<JsonElement>("QuestionStarted", data => { questions.Writer.TryWrite(data); });
        one.On<JsonElement>("GameFinished", data => { finished.TrySetResult(data); });
        two.On<JsonElement>("GameFinished", data => { finishedForTwo.TrySetResult(data); });
        await one.StartAsync(ct); await two.StartAsync(ct);
        await one.InvokeAsync("JoinMatch", new JoinRequest(id, Room), ct);
        await two.InvokeAsync("JoinMatch", new JoinRequest(id, Room), ct);
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IQuestionRepository>();
        var sessions = factory.Services.GetRequiredService<TriviaSessions>();
        for (var i = 0; i < 10; i++)
        {
            var question = await questions.Reader.ReadAsync(ct);
            Assert.False(question.TryGetProperty("correctAnswerId", out _));
            var questionId = question.GetProperty("questionId").GetString()!;
            var stored = (await repository.GetByIdAsync(questionId, ct))!;
            factory.Clock.Advance(3);
            await one.InvokeAsync("SubmitAnswer", new AnswerRequest(id, questionId, stored.CorrectAnswerId), ct);
            factory.Clock.Advance(1);
            await two.InvokeAsync("SubmitAnswer", new AnswerRequest(id, questionId, stored.Options.First(o => o.AnswerId != stored.CorrectAnswerId).AnswerId), ct);
            factory.Clock.Advance(5);
            await sessions.TickAsync(ct);
        }
        var result = await finished.Task.WaitAsync(ct);
        Assert.Equal("player-one", result.GetProperty("winnerUserId").GetString());
        // Los dos jugadores reciben GameFinished: con ese evento el juego avisa al Shell y el Shell a Matchmaking.
        Assert.Equal(id, (await finishedForTwo.Task.WaitAsync(ct)).GetProperty("matchId").GetString());
        var saved = await scope.ServiceProvider.GetRequiredService<IResultRepository>().GetByMatchIdAsync(id, ct);
        Assert.NotNull(saved);
        Assert.Equal(2200, saved.Players.Single(p => p.UserId == "player-one").Score);
        Assert.Equal(10, saved.Players.Single(p => p.UserId == "player-one").CorrectAnswers);
    }

    [Fact]
    public async Task PlayerOutsideTheRoomIsRejected()
    {
        await using var outsider = Connection("ajeno");
        await outsider.StartAsync();

        var error = await Assert.ThrowsAsync<HubException>(() => outsider.InvokeAsync(
            "JoinMatch", new JoinRequest("room-" + Guid.NewGuid().ToString("N"), Room)));

        Assert.Contains("PLAYER_NOT_IN_MATCH", error.Message);
    }

    private HubConnection Connection(string user) => new HubConnectionBuilder().WithUrl("http://localhost/hubs/trivia", options =>
    {
        options.Transports = HttpTransportType.LongPolling;
        options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
        options.AccessTokenProvider = () => Task.FromResult<string?>(factory.Token(user, writer: false));
    }).Build();
}
